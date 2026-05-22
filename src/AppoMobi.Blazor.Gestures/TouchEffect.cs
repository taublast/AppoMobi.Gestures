using System.Runtime.CompilerServices;

namespace AppoMobi.Gestures;

/// <summary>
/// Attaches gesture recognition to a Blazor element via JS pointer/wheel events.
/// Mirrors the AppoMobi.Maui.Gestures TouchEffect logic: tap, long press, panning, pinch.
/// One instance per element. Dispose when the element unmounts.
/// </summary>
public class TouchEffect : IAsyncDisposable
{
    private const int PolicyPreventDefault = 1;
    private const int PolicyCapturePointer = 1 << 1;
    private const int PolicyReleasePointer = 1 << 2;

    public static int LongPressTimeMsDefault = 1500;
    public static float TappedCancelMoveThresholdPoints = 16f;

    public int LongPressTimeMs { get; set; } = LongPressTimeMsDefault;

    /// <summary>
    /// Current browser device pixel ratio used by the Blazor gestures implementation.
    /// This is updated from JS when the browser pixel scale changes.
    /// </summary>
    public static float Density { get; set; } = 1f;

    /// <summary>
    /// When true, Moved events outside the element boundary do not become Exited.
    /// </summary>
    public bool Draggable { get; set; }

    /// <summary>
    /// Controls how this element cooperates with parent scrolling/input surfaces.
    /// Mirrors AppoMobi.Maui.Gestures TouchMode behavior.
    /// </summary>
    public TouchHandlingStyle TouchMode { get; set; } = TouchHandlingStyle.Default;

    /// <summary>
    /// Dynamic cooperation state for <see cref="TouchHandlingStyle.Manual"/>.
    /// The consumer updates this while handling gestures to either keep control
    /// or release the gesture to the parent surface.
    /// </summary>
    public ShareLockState WIllLock { get; set; } = ShareLockState.Initial;

    private readonly IJSRuntime _js;
    private DotNetObjectReference<TouchEffect>? _dotNetRef;
    private IJSObjectReference? _module;
    private ElementReference _element;
    private IGestureListener? _listener;

    private readonly HashSet<long> _activePointers = new();
    private readonly MultitouchTracker _manipulationTracker = new();

    private TouchActionEventArgs? _lastArgs;
    private TouchActionEventArgs? _lastDown;
    private volatile bool _maybeTapped;
    private bool _isPanning;
    private volatile bool _isLongPressing;
    private volatile bool _lockLongPress;
    private float _thresholdCancelTapPixels;
    private TouchActionResult _lastActionResult;
    private CancellationTokenSource? _longPressCts;

    public TouchEffect(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>
    /// Attaches gesture recognition to <paramref name="element"/>.
    /// Safe to call again to re-attach or swap listener.
    /// </summary>
    public async Task AttachAsync(ElementReference element, IGestureListener listener)
    {
        await DetachCoreAsync();
        _element = element;
        _listener = listener;
        _dotNetRef = DotNetObjectReference.Create(this);
        _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/AppoMobi.Blazor.Gestures/canvasGestures.js");
        await _module.InvokeVoidAsync("attachCanvasGestures", element, _dotNetRef, true);
    }

    public async Task DetachAsync()
    {
        await DetachCoreAsync();
        _listener = null;
    }

    private async Task DetachCoreAsync()
    {
        CancelLongPress();
        if (_module != null)
        {
            try { await _module.InvokeVoidAsync("detachCanvasGestures", _element); }
            catch { }
        }
        _dotNetRef?.Dispose();
        _dotNetRef = null;
        _activePointers.Clear();
        _lastArgs = null;
        _lastDown = null;
        _isPanning = false;
        _isLongPressing = false;
    }

    [JSInvokable]
    public void OnDensityChanged(double density)
    {
        if (!double.IsFinite(density) || density <= 0)
        {
            density = 1;
        }

        Density = (float)density;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float GetEventScale()
    {
        var density = Density;
        return float.IsFinite(density) && density > 0 ? density : 1f;
    }

    [JSInvokable]
    public int OnCanvasPointer(BlazorPointerArgs p)
    {
        var type = p.Type switch
        {
            "pointerdown"   => TouchActionType.Pressed,
            "pointermove"   => p.Buttons == 0 && p.PointerType == "mouse"
                                   ? TouchActionType.Pointer
                                   : TouchActionType.Moved,
            "pointerup"     => TouchActionType.Released,
            "pointercancel" => TouchActionType.Cancelled,
            "pointerleave"  => TouchActionType.Exited,
            _               => TouchActionType.Moved
        };

        if (type == TouchActionType.Pressed)
            _activePointers.Add(p.PointerId);

        var scale = GetEventScale();
        var location = new PointF(p.OffsetX * scale, p.OffsetY * scale);

        var args = new TouchActionEventArgs(p.PointerId, type, location, null, scale)
        {
            IsInsideView = p.IsInsideView,
            NumberOfTouches = _activePointers.Count,
            Pointer = new PointerData
            {
                Button = MapButton(p.Button),
                ButtonNumber = p.Button + 1,
                State = type == TouchActionType.Pressed ? MouseButtonState.Pressed : MouseButtonState.Released,
                PressedButtons = (MouseButtons)p.Buttons,
                DeviceType = p.PointerType switch
                {
                    "touch" => PointerDeviceType.Touch,
                    "pen"   => PointerDeviceType.Pen,
                    _       => PointerDeviceType.Mouse
                },
                Pressure = p.Pressure,
                IsScrolling = false
            }
        };

        if (type == TouchActionType.Released || type == TouchActionType.Cancelled || type == TouchActionType.Exited)
            _activePointers.Remove(p.PointerId);

        OnTouchAction(args);

        return GetInteropPolicy(type);
    }

    [JSInvokable]
    public int OnCanvasWheel(BlazorWheelArgs w)
    {
        var scale = GetEventScale();
        var location = new PointF(w.OffsetX * scale, w.OffsetY * scale);
        var args = new TouchActionEventArgs(0, TouchActionType.Wheel, location, null, scale)
        {
            IsInsideView = true,
            Wheel = new WheelEventArgs { Delta = -w.DeltaY, Center = location }
        };
        OnTouchAction(args);

        return GetInteropPolicy(TouchActionType.Wheel);
    }

    private void OnTouchAction(TouchActionEventArgs args)
    {
        var listener = _listener;
        if (listener is { InputTransparent: true })
            listener = null;

        var action = args.Type;

        if (action == TouchActionType.Pressed)
        {
            _lastArgs = null;
            _thresholdCancelTapPixels = TappedCancelMoveThresholdPoints * Density;
            _lockLongPress = false;
            args.StartingLocation = args.Location;

            CancelLongPress();
            _maybeTapped = false;
            _isLongPressing = false;

            _maybeTapped = true;
            _manipulationTracker.Restart(args.Id, args.Location);
            ScheduleLongPress(args);

            args.IsInContact = true;
            _lastDown = args;
    
            listener?.OnGestureEvent(action, args, TouchActionResult.Down);
            _lastActionResult = TouchActionResult.Down;
            WIllLock = ShareLockState.Initial;
        }

        TouchActionEventArgs.FillDistanceInfo(args, _lastArgs);

        if (action == TouchActionType.Wheel)
        {
            _lockLongPress = true;
            listener?.OnGestureEvent(TouchActionType.Wheel, args, TouchActionResult.Wheel);
            _lastActionResult = TouchActionResult.Wheel;
        }
        else if (action == TouchActionType.Pointer)
        {
            listener?.OnGestureEvent(TouchActionType.Pointer, args, TouchActionResult.Pointer);
            _lastActionResult = TouchActionResult.Pointer;
        }
        else if (action == TouchActionType.Moved)
        {
            var manipulation = _manipulationTracker.AddMovement(args.Id, args.Location);
            if (manipulation != null)
                args.Manipulation = manipulation;

            if (!args.IsInsideView && !Draggable)
            {
                action = TouchActionType.Exited;
            }
            else
            {
                _isPanning = true;
                _lastActionResult = TouchActionResult.Panning;
            }
        }

        // No else — action may have been changed to Exited above
        if (action == TouchActionType.Released || action == TouchActionType.Cancelled || action == TouchActionType.Exited)
        {
            args.IsInContact = !(args.NumberOfTouches < 2);

            if (args.IsInContact)
                _maybeTapped = false;

            if (!args.IsInContact)
            {
                _manipulationTracker.Reset();
                CancelLongPress();

                if (!_isLongPressing
                    && args.NumberOfTouches == 1
                    && _maybeTapped
                    && _lastDown != null
                    && action == TouchActionType.Released
                    && !_lastDown.PreventDefault
                    && Math.Abs(args.Distance.Total.X) < _thresholdCancelTapPixels
                    && Math.Abs(args.Distance.Total.Y) < _thresholdCancelTapPixels)
                {
                    listener?.OnGestureEvent(action, args, TouchActionResult.Tapped);
                    _lastActionResult = TouchActionResult.Tapped;
                }

                if (_isPanning)
                    _isPanning = false;
            }
            else
            {
                _manipulationTracker.RemoveTouch(args.Id);
            }

            listener?.OnGestureEvent(action, args, TouchActionResult.Up);
            _lastActionResult = TouchActionResult.Up;
            WIllLock = ShareLockState.Initial;
        }

        if ((args.Distance.Delta.X != 0 || args.Distance.Delta.Y != 0)
            && _lastActionResult == TouchActionResult.Panning)
        {
            listener?.OnGestureEvent(action, args, TouchActionResult.Panning);
        }

        _lastArgs = args;
    }

    private int GetInteropPolicy(TouchActionType action)
    {
        return action switch
        {
            TouchActionType.Wheel => GetWheelPolicy(),
            TouchActionType.Pressed => GetPressedPolicy(),
            TouchActionType.Moved => GetMovePolicy(),
            TouchActionType.Released or TouchActionType.Cancelled or TouchActionType.Exited => GetReleasePolicy(),
            _ => 0
        };
    }

    private int GetWheelPolicy()
    {
        if (TouchMode == TouchHandlingStyle.Lock)
            return PolicyPreventDefault;

        if (TouchMode == TouchHandlingStyle.Manual && WIllLock == ShareLockState.Locked)
            return PolicyPreventDefault;

        return 0;
    }

    private int GetPressedPolicy()
    {
        if (TouchMode == TouchHandlingStyle.Lock)
            return PolicyPreventDefault | PolicyCapturePointer;

        return 0;
    }

    private int GetMovePolicy()
    {
        if (TouchMode == TouchHandlingStyle.Lock)
            return PolicyPreventDefault | PolicyCapturePointer;

        if (TouchMode == TouchHandlingStyle.Manual)
        {
            if (WIllLock == ShareLockState.Locked)
                return PolicyPreventDefault | PolicyCapturePointer;

            if (WIllLock == ShareLockState.Unlocked)
                return PolicyReleasePointer;
        }

        return 0;
    }

    private int GetReleasePolicy()
    {
        if (TouchMode == TouchHandlingStyle.Lock)
            return PolicyPreventDefault | PolicyReleasePointer;

        if (TouchMode == TouchHandlingStyle.Manual)
            return PolicyReleasePointer;

        return 0;
    }

    private void ScheduleLongPress(TouchActionEventArgs downArgs)
    {
        var cts = new CancellationTokenSource();
        _longPressCts = cts;
        _ = Task.Delay(LongPressTimeMs, cts.Token).ContinueWith(t =>
        {
            if (t.IsCanceled || _lockLongPress || _lastDown == null || downArgs.PreventDefault)
                return;
            _isLongPressing = true;
            _listener?.OnGestureEvent(TouchActionType.Pressing, downArgs, TouchActionResult.LongPressing);
        }, TaskContinuationOptions.NotOnCanceled);
    }

    private void CancelLongPress()
    {
        _longPressCts?.Cancel();
        _longPressCts?.Dispose();
        _longPressCts = null;
    }

    private static MouseButton MapButton(int button) => button switch
    {
        0 => MouseButton.Left,
        1 => MouseButton.Middle,
        2 => MouseButton.Right,
        3 => MouseButton.XButton1,
        4 => MouseButton.XButton2,
        _ => MouseButton.Extended
    };

    public async ValueTask DisposeAsync()
    {
        await DetachCoreAsync();
        if (_module != null)
        {
            try { await _module.DisposeAsync(); }
            catch { }
            _module = null;
        }
        _listener = null;
    }

    public static bool LogEnabled;

    public static Dictionary<string, DateTime> TapLocks = new();

    static object lockTapLocks = new();

    public static bool CheckLocked(string uid)
    {
        lock (lockTapLocks)
        {
            if (TapLocks.TryGetValue(uid, out DateTime lockTime))
            {
                // If the lock is about to be removed, treat it as unlocked
                if (DateTime.UtcNow >= lockTime)
                {
                    TapLocks.Remove(uid, out _);
                    return false;
                }
                return true;
            }
            return false;
        }
    }

    public static void CloseKeyboard()
    {

    }

    /// <summary>
    /// Returns TRUE if still locked
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="ms"></param>
    /// <returns></returns>
    public static bool CheckLockAndSet([CallerMemberName] string uid = null, int ms = 500)
    {
        if (CheckLocked(uid))
            return true;

        var unlockTime = DateTime.UtcNow.AddMilliseconds(ms);
        TapLocks[uid] = unlockTime;

        _ = Task.Delay(ms).ContinueWith(t =>
        {
            lock (lockTapLocks)
            {
                TapLocks.Remove(uid, out _);
            }
        });

        return false;
    }

}
