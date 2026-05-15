using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;


namespace AppoMobi.Gestures
{
    public partial class TouchEffect : RoutingEffect, IDisposable
    {
        public TouchEffect()
        {

        }

        /// <summary>
        /// Gets the TouchEffect instance from a view's Effects collection
        /// </summary>
        /// <param name="view">The view to get the effect from</param>
        /// <returns>The TouchEffect instance, or null if not found</returns>
        public static TouchEffect GetFrom(View view)
        {
            if (view == null)
                return null;

            return view.Effects.FirstOrDefault(e => e is TouchEffect) as TouchEffect;
        }

        public static bool LogEnabled { get; set; }

        /// <summary>
        /// How much finger can move for the gestured to be still considered as TAPPED. In points, not pixels, default is 16.
        /// </summary>
        public static float TappedCancelMoveThresholdPoints = 16f;

        protected MultitouchTracker _manipulationTracker = new();

        public static ConcurrentDictionary<string, DateTime> TapLocks = new();

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


        /// <summary>
        /// Relative to the view coordinates, not the entire screen
        /// </summary>
        /// <param name="hitPixelsX"></param>
        /// <param name="hitPixelsY"></param>
        /// <param name="viewWidthPixels"></param>
        /// <param name="viewHeightPixels"></param>
        /// <returns></returns>
        public static bool IsInsideView(float hitPixelsX, float hitPixelsY, float viewWidthPixels, float viewHeightPixels)
        {


            bool isInsideView = hitPixelsX >= 0 && hitPixelsY >= 0
                                                && hitPixelsX <= viewWidthPixels
                                                && hitPixelsY <= viewHeightPixels;
            return isInsideView;
        }



        public static void CloseKeyboard()
        {

#if ANDROID
            ClosePlatformKeyboard();
#endif
        }

        private float _thresholdCancelTapIfMovePixels;

        static float _density = 0f;
        public static float Density
        {
            get
            {
                if (_density == 0)
                {
                    try
                    {
                        _density = (float)Microsoft.Maui.Devices.DeviceDisplay.Current.MainDisplayInfo.Density;
                        if (_density == 0)
                            throw new Exception("Density is 0");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        return 1f;
                    }
                }
                return _density;
            }
            set
            {
                _density = value;
            }
        }

        #region ATTACHABLE PROPS


        public static readonly BindableProperty ForceAttachProperty =
            BindableProperty.CreateAttached("ForceAttach",
                typeof(bool), typeof(TouchEffect),
                false, propertyChanged: OnPropsChanged);
        public static bool GetForceAttach(BindableObject target) => (bool)target.GetValue(ForceAttachProperty);
        public static void SetForceAttach(BindableObject view, bool value)
        {
            view.SetValue(ForceAttachProperty, value);
        }

        public static readonly BindableProperty ShareTouchProperty =
            BindableProperty.CreateAttached("ShareTouch",
                typeof(TouchHandlingStyle), typeof(TouchEffect),
                TouchHandlingStyle.Default, propertyChanged: OnPropsChanged);
        public static TouchHandlingStyle GetShareTouch(BindableObject target) => (TouchHandlingStyle)target.GetValue(ShareTouchProperty);
        public static void SetShareTouch(BindableObject view, TouchHandlingStyle value)
        {
            view.SetValue(ShareTouchProperty, value);
        }

        /*

        public static readonly BindableProperty AnimationTappedProperty =
            BindableProperty.CreateAttached("AnimationTapped",
                typeof(SkiaTouchAnimation), typeof(TouchEffect),
                SkiaTouchAnimation.None, propertyChanged: OnPropsChanged);
        public static SkiaTouchAnimation GetAnimationTapped(BindableObject target) => (SkiaTouchAnimation)target.GetValue(AnimationTappedProperty);
        public static void SetAnimationTapped(BindableObject view, SkiaTouchAnimation value)
        {
            view.SetValue(AnimationTappedProperty, value);
        }

        public static readonly BindableProperty AnimationViewProperty =
            BindableProperty.CreateAttached("AnimationView",
                typeof(object), typeof(TouchEffect),
                null, propertyChanged: OnPropsChanged);
        public static object GetAnimationView(BindableObject target) => (object)target.GetValue(AnimationViewProperty);
        public static void SetAnimationView(BindableObject view, object value)
        {
            view.SetValue(AnimationViewProperty, value);
        }

        */

        public static readonly BindableProperty PassViewProperty =
            BindableProperty.CreateAttached("PassView",
                typeof(object), typeof(TouchEffect),
                null, propertyChanged: OnPropsChanged);
        public static object GetPassView(BindableObject target) => (object)target.GetValue(PassViewProperty);
        public static void SetPassView(BindableObject view, object value)
        {
            view.SetValue(PassViewProperty, value);
        }


        public static void SetCommandTappedParameter(BindableObject view, object value)
        {
            view.SetValue(CommandTappedParameterProperty, value);
        }
        protected static void SetProperties(TouchEffect effect, BindableObject bindable)
        {
            effect.CommandLongPressing = GetCommandLongPressing(bindable);
            effect.CommandLongPressingParameter = GetCommandLongPressingParameter(bindable);

            effect.TappedCommand = GetCommandTapped(bindable);
            effect.TappedCommandParameter = GetCommandTappedParameter(bindable);

            var handlerDown = GetHandlerDown(bindable);
            if (handlerDown != null)
            {
                effect.Down -= handlerDown;
                effect.Down += handlerDown;
            }
            var handlerTapped = GetHandlerTapped(bindable);
            if (handlerTapped != null)
            {
                effect.Tapped -= handlerTapped;
                effect.Tapped += handlerTapped;
            }
            var handlerLongPressing = GetHandlerLongPressing(bindable);
            if (handlerLongPressing != null)
            {
                effect.LongPressing -= handlerLongPressing;
                effect.LongPressing += handlerLongPressing;
            }

            effect.TouchMode = GetShareTouch(bindable);
        }

        protected static void OnPropsChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (!(bindable is View view))
                return;

            var eff = view.Effects.FirstOrDefault(e => e is TouchEffect) as TouchEffect;

            var hasOurEffect =
                GetCommandTapped(bindable) != null
                || GetForceAttach(bindable)
                || GetCommandLongPressing(bindable) != null;

            if (hasOurEffect)
            {
                if (eff == null) //need attach effect
                {
                    var add = new TouchEffect();
                    SetProperties(add, bindable);
                    view.Effects.Add(add);
                    //todo cleanup..
                    //if (EffectsConfig.AutoChildrenInputTransparent && bindable is Layout &&
                    //    !EffectsConfig.GetChildrenInputTransparent(view))
                    //{
                    //    EffectsConfig.SetChildrenInputTransparent(view, true);
                    //}
                }
                else
                {
                    //modify existing
                    SetProperties(eff, bindable);
                }
            }
            else
            {
                if (eff != null)
                {
                    view.Effects.Remove(eff);
                }
            }
        }

        public void Dispose()
        {
            if (Disposed)
                return;

            Disposed = true;

            if (_longPressTimer != null)
            {
                _longPressTimer.Tick -= OnLongPressTimerTick;
                _longPressTimer.Stop();
                _longPressTimer = null;
            }

            //not sending any references by purpose
            Disposing?.Invoke(null, null);
        }

        public static TouchActionEventHandler GetHandlerDown(BindableObject target) => (TouchActionEventHandler)target.GetValue(HandlerDownProperty);
        public static void SetHandlerDown(BindableObject view, TouchActionEventHandler value)
        {
            view.SetValue(HandlerDownProperty, value);
        }
        public static readonly BindableProperty HandlerDownProperty =
            BindableProperty.CreateAttached("HandlerDown",
                typeof(TouchActionEventHandler), typeof(TouchEffect),
                null,
                propertyChanging: OnDownHandlerChanging,
                propertyChanged: OnPropsChanged);
        private static void OnDownHandlerChanging(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (!(bindable is View view))
                return;

            if (oldvalue is TouchActionEventHandler oldHandler)
            {
                var effect = view.Effects.FirstOrDefault(e => e is TouchEffect) as TouchEffect;

                effect.Down -= oldHandler;
            }
        }


        public static TouchActionEventHandler GetHandlerTapped(BindableObject target) => (TouchActionEventHandler)target.GetValue(HandlerTappedProperty);
        public static void SetHandlerTapped(BindableObject view, TouchActionEventHandler value)
        {
            view.SetValue(HandlerTappedProperty, value);
        }
        public static readonly BindableProperty HandlerTappedProperty =
            BindableProperty.CreateAttached("HandlerTapped",
                typeof(TouchActionEventHandler), typeof(TouchEffect),
                null,
                propertyChanging: OnTappedHandlerChanging,
                propertyChanged: OnPropsChanged);
        private static void OnTappedHandlerChanging(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (!(bindable is View view))
                return;

            if (oldvalue is TouchActionEventHandler oldHandler)
            {
                var effect = view.Effects.FirstOrDefault(e => e is TouchEffect) as TouchEffect;

                effect.Tapped -= oldHandler;
            }
        }

        public static TouchActionEventHandler GetHandlerLongPressing(BindableObject target) => (TouchActionEventHandler)target.GetValue(HandlerLongPressingProperty);
        public static void SetHandlerLongPressing(BindableObject view, TouchActionEventHandler value)
        {
            view.SetValue(HandlerLongPressingProperty, value);
        }
        public static readonly BindableProperty HandlerLongPressingProperty =
            BindableProperty.CreateAttached("HandlerLongPressing",
                typeof(TouchActionEventHandler), typeof(TouchEffect),
                null,
                propertyChanging: OnLongPressingHandlerChanging,
                propertyChanged: OnPropsChanged);
        private static void OnLongPressingHandlerChanging(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (!(bindable is View view))
                return;

            if (oldvalue is TouchActionEventHandler oldHandler)
            {
                var effect = view.Effects.FirstOrDefault(e => e is TouchEffect) as TouchEffect;

                effect.LongPressing -= oldHandler;
            }
        }




        public static readonly BindableProperty CommandTappedProperty =
            BindableProperty.CreateAttached("CommandTapped",
                typeof(ICommand), typeof(TouchEffect),
                null, propertyChanged: OnPropsChanged);
        public static ICommand GetCommandTapped(BindableObject target) => (ICommand)target.GetValue(CommandTappedProperty);
        public static void SetCommandTapped(BindableObject view, ICommand value)
        {
            view.SetValue(CommandTappedProperty, value);
        }

        public static readonly BindableProperty CommandTappedParameterProperty =
            BindableProperty.CreateAttached("CommandTappedParameter",
                typeof(object), typeof(TouchEffect),
                null, propertyChanged: OnPropsChanged);
        public static object GetCommandTappedParameter(BindableObject target) => target.GetValue(CommandTappedParameterProperty);


        public static readonly BindableProperty CommandLongPressingProperty =
            BindableProperty.CreateAttached("CommandLongPressing",
                typeof(ICommand), typeof(TouchEffect),
                null, propertyChanged: OnPropsChanged);
        public static ICommand GetCommandLongPressing(BindableObject target) => (ICommand)target.GetValue(CommandLongPressingProperty);

        public static readonly BindableProperty CommandLongPressingParameterProperty =
            BindableProperty.CreateAttached("CommandLongPressingParameter",
                typeof(object), typeof(TouchEffect),
                null, propertyChanged: OnPropsChanged);
        public static object GetCommandLongPressingParameter(BindableObject target) => target.GetValue(CommandLongPressingParameterProperty);


        #endregion

        #region DEFAULT PARAMETERS

        public static int LockTimeTimeMsDefault = 0;
        public static int LongPressTimeMsDefault = 1500;

        #endregion

        #region INSTANCE PARAMETERS

        private int _lockTimeTimeMs = -1;
        public int LockTimeTimeMs
        {
            get => _lockTimeTimeMs == -1 ? LockTimeTimeMsDefault : _lockTimeTimeMs;
            set => _lockTimeTimeMs = value;
        }

        private int _longPressTimeMs = -1;
        public int LongPressTimeMs
        {
            get => _longPressTimeMs == -1 ? LongPressTimeMsDefault : _longPressTimeMs;
            set => _longPressTimeMs = value;
        }

        private TouchHandlingStyle _TouchMode;
        public TouchHandlingStyle TouchMode
        {
            get
            {
                return _TouchMode;
            }
            set
            {
                if (_TouchMode != value)
                {
                    _TouchMode = value;
                }
            }
        }

        public int LoсkDownTimeMs { get; set; } = -1;


        #endregion

        public event TouchActionEventHandler TouchAction;
        //public event TouchActionEventHandler LongPressed;  whats the point?
        public event TouchActionEventHandler LongPressing;
        public event TouchActionEventHandler Pinched;
        public event TouchActionEventHandler Panning;
        public event TouchActionEventHandler Panned;
        public event TouchActionEventHandler Tapped;
        public event TouchActionEventHandler Down;
        public event TouchActionEventHandler Up;
        public event TouchActionEventHandler Swiped; //todo

        /// <summary>
        /// Set this to true not to cancel Moving action when moved outside while pressing
        /// </summary>
        public bool Draggable { get; set; }

        /// <summary>
        /// Useful for removing tapped visual  effects from button etc
        /// </summary>
        public event TouchActionEventHandler TapUnlocked;

        //todo parameter
        public ICommand TappedCommand;
        public object TappedCommandParameter;

        public ICommand CommandLongPressing;
        public object CommandLongPressingParameter;

        public View PassView;

        //public ICommand LongPressedCommand;
        //public object LongPressedCommandParameter;

        public ICommand PinchedCommand;
        public object PinchedCommandParameter;



        protected DateTime TimerStarted { get; set; }

        public bool Disposed { get; private set; }

        protected override void OnDetached()
        {
            base.OnDetached();

            Dispose();
        }


        /// <summary>
        /// Used by the platform code to dispose itself if Xamarin bugs and fails to detach it properly
        /// </summary>
        public EventHandler Disposing;

        public bool Capture { set; get; }

        public bool IsPressed { get; protected set; }

        private bool _maybeTapped;

        public bool IsLongPressing
        {
            get => _longPressState == LongPressState.Active;
            set
            {
                _onTimerBusy = false;
                bool currentValue = _longPressState == LongPressState.Active;
                if (currentValue != value)
                {
                    _longPressState = value ? LongPressState.Active : LongPressState.Idle;

                    if (value)
                    {
                        _wasLongPressing = true;
                    }
                }
            }
        }

        bool _wasLongPressing { get; set; }

        /// <summary>
        /// Handler for the timer tick event
        /// </summary>
        private void OnLongPressTimerTick(object sender, EventArgs e)
        {
            // Stop timer after first tick - we only need one notification
            _longPressTimer.Stop();

            // Process the long press if not locked
            if (!lockLongPress && lastDown != null)
            {
                _longPressState = LongPressState.Active;
                IsLongPressing = true;

                var listener = Element as IGestureListener;
                if (listener is { InputTransparent: true })
                {
                    listener = null;
                }

                if (!lastDown.PreventDefault)
                {
                    if (listener != null)
                    {
                        var receiver = listener;
                        SendAction(receiver, TouchActionType.Pressing, lastDown, TouchActionResult.LongPressing);
                    }

                    if (CommandLongPressing != null)
                    {
                        lastDown.Context = CommandLongPressingParameter;
                        if (CommandLongPressingParameter != null)
                        {
                            CommandLongPressing.Execute(CommandLongPressingParameter);
                        }
                        else
                        {
                            CommandLongPressing.Execute(lastDown);
                        }
                    }

                    LongPressing?.Invoke(Element, lastDown);
                }
            }
        }

        private volatile bool _onTimerBusy;
        private volatile bool lockLongPress;

        // Single timer instance to reuse
        private IDispatcherTimer _longPressTimer;

        private enum LongPressState
        {
            Idle,
            Pending,
            Active
        }

        private LongPressState _longPressState = LongPressState.Idle;

        /// <summary>
        /// Initializes the long press timer if not already created
        /// </summary>
        private void EnsureLongPressTimer()
        {
            if (_longPressTimer == null)
            {
                _longPressTimer = Application.Current.Dispatcher.CreateTimer();
                _longPressTimer.Tick += OnLongPressTimerTick;
            }
        }

        /// <summary>
        /// Starts the long press detection timer
        /// </summary>
        private void EnableLongPressingTimer()
        {
            Debug.WriteLine("EnableLongPressingTimer");

            EnsureLongPressTimer();

            // Stop any existing timer
            _longPressTimer.Stop();

            // Set timer interval to the long press time
            _longPressTimer.Interval = TimeSpan.FromMilliseconds(LongPressTimeMs);

            // Update state
            _longPressState = LongPressState.Pending;

            // Start the timer
            _longPressTimer.Start();
        }

        /// <summary>
        /// Disables the long press timer
        /// </summary>
        private void DisableLongPressingTimer()
        {
            Debug.WriteLine("DisableLongPressingTimer");

            if (_longPressTimer != null && _longPressTimer.IsRunning)
            {
                _longPressTimer.Stop();
            }

            if (_longPressState == LongPressState.Pending)
            {
                _longPressState = LongPressState.Idle;
            }
        }

        CancellationTokenSource ctsLongPressingTime;

        protected TouchActionEventArgs lastDown { get; set; }

        private TouchActionEventArgs _lastArgs { get; set; }


        public bool IsPanning { get; protected set; }

        //protected TouchActionEventArgs lastPan { get; set; }



        void SendPinchCommand(TouchActionEventArgs args)
        {
            if (PinchedCommand != null)
            {
                if (PinchedCommandParameter != null)
                {
                    PinchedCommand?.Execute(PinchedCommandParameter);
                }
                else
                {
                    PinchedCommand?.Execute(args);
                }
            }
        }

        /// <summary>
        /// We CANNOT invoke commands from background thread with Task.Run,
        /// otherwise it bugs (checked on android) when quickly spammed taps
        /// The UI thread just gets locked processing commands and bindings stop working
        /// </summary>
        /// <param name="args"></param>
        void SendTappedCommand(TouchActionEventArgs args)
        {
            if (TappedCommand != null)
            {
                if (TappedCommandParameter != null)
                {
                    TappedCommand?.Execute(TappedCommandParameter);
                }
                else
                {
                    TappedCommand?.Execute(args);
                }
            }
        }

        public TouchActionResult LastActionResult { get; protected set; }

        void SendAction(IGestureListener listener, TouchActionType action, TouchActionEventArgs args, TouchActionResult result)
        {
#if ANDROID
            SendActionPlatform(listener, action, args, result);
#else

            listener.OnGestureEvent(action, args, result);

            //System.Diagnostics.Debug.WriteLine($"[TOUCH] Sent {action} {result} y {args.Location.Y:0}"); //x,y {args.Location.X:0}, {args.Location.Y:0} inside: {isInsideView}

            if (result == TouchActionResult.Up || result == TouchActionResult.Down)
            {
                WIllLock = ShareLockState.Initial;
            }
#endif
        }

        object lockOnTouch = new();
        private ShareLockState wIllLock;

        public ShareLockState WIllLock
        {
            get => wIllLock;
            set
            {
                if (wIllLock != value)
                {
                    wIllLock = value;
                    //Debug.WriteLine($"WIllLock: {value}");
                }
            }
        }

        public void OnTouchAction(TouchActionEventArgs args)
        {

            lock (lockOnTouch)
            {
            
                try
                {


                    var element = Element;

                    var listener = element as IGestureListener;
                    if (listener is { InputTransparent: true })
                    {
                        listener = null;
                    }

                    //System.Diagnostics.Debug.WriteLine($"[TOUCH] Got {args.Type} "); //x,y {args.Location.X:0}, {args.Location.Y:0} inside: {isInsideView}

                    var action = args.Type;

                    if (action == TouchActionType.Entered
                        || action == TouchActionType.Pressed)
                    {
                        _lastArgs = null;
                        _thresholdCancelTapIfMovePixels = TappedCancelMoveThresholdPoints * Density;

                        TimerStarted = DateTime.Now;
                        lockLongPress = false;

                        args.StartingLocation = args.Location;

                        DisableLongPressingTimer();

                        _maybeTapped = false;
                        IsLongPressing = false;

                        if (_lastArgs == null || !_lastArgs.IsInContact)
                        {
                            _maybeTapped = true;
                            _manipulationTracker.Restart(args.Id, args.Location);
                            //Debug.WriteLine("[TOUCH] Restarted tracker!");

                            EnableLongPressingTimer();
                        }

                        args.IsInContact = true;

                        lastDown = args;

                        _wasLongPressing = false;

                        if (listener != null)
                        {
                            SendAction(listener, action, args, TouchActionResult.Down);
                        }

                        var passView = element;
                        if (PassView != null)
                        {
                            passView = PassView;
                        }
                        Down?.Invoke(passView, args);

                        LastActionResult = TouchActionResult.Down;

                    }

                    TouchActionEventArgs.FillDistanceInfo(args, _lastArgs);

                    if (action == TouchActionType.Wheel)
                    {
                        lockLongPress = true;
                        if (listener != null)
                        {
                            SendAction(listener, TouchActionType.Wheel, args, TouchActionResult.Wheel);
                        }

                        SendPinchCommand(args);

                        Pinched?.Invoke(Element, args);
                        LastActionResult = TouchActionResult.Wheel;
                    }

                    else if (action == TouchActionType.Pointer)
                    {
                        if (listener != null)
                        {
                            SendAction(listener, TouchActionType.Pointer, args, TouchActionResult.Pointer);
                        }
                        LastActionResult = TouchActionResult.Pointer;
                    }

                    else if (action == TouchActionType.Moved
                             || action == TouchActionType.PanEnded
                             || action == TouchActionType.PanChanged
                             || action == TouchActionType.PanStarted)
                    {

                        var manipulation = _manipulationTracker.AddMovement(args.Id, args.Location);
                        if (manipulation != null)
                        {
                            args.Manipulation = manipulation;
                            //Debug.WriteLine($"[TOUCH] added movement, S: {args.Manipulation.Scale:0.00}/{args.Manipulation.ScaleTotal:0.00} R: {args.Manipulation.Rotation:0.00}/{args.Manipulation.RotationTotal:0.00}, touches {args.Manipulation.TouchesCount}");
                        }

                        if (action != TouchActionType.Moved)
                        {
                            lockLongPress = true;
                            DisableLongPressingTimer();
                        }

                        if (
                            action == TouchActionType.PanEnded || action == TouchActionType.Moved)
                        {

                            if (!args.IsInsideView && !Draggable)
                            {
                                action = TouchActionType.Exited;
                            }
                            else
                            {
                                IsPanning = true;
                                LastActionResult = TouchActionResult.Panning;
                            }
                        }
                        else
                        {
                            LastActionResult = TouchActionResult.Panning;
                        }

                    }

                    //no else because we could have set "action" manually in code above

                    if (action == TouchActionType.Released
                        || action == TouchActionType.Cancelled
                        || action == TouchActionType.Exited
                       )
                    {
                        args.IsInContact = !(args.NumberOfTouches < 2);

                        if (args.IsInContact)
                        {
                            _maybeTapped = false;
                        }
                        //Debug.WriteLine($"[TOUCH] touches: {args.NumberOfTouches} in contact: {args.IsInContact}");

                        if (!args.IsInContact)
                        {
                            _manipulationTracker.Reset();

                            DisableLongPressingTimer();

                            //TAPPED
                            if (//!IsPanning &&
                                !IsLongPressing
                                && args.NumberOfTouches == 1
                                && _maybeTapped
                               && lastDown != null
                              && action == TouchActionType.Released
                              && !lastDown.PreventDefault
                              &&  Math.Abs(args.Distance.Total.X) < _thresholdCancelTapIfMovePixels
                                && Math.Abs(args.Distance.Total.Y)  < _thresholdCancelTapIfMovePixels
                                )
                            {
                                if (listener != null)
                                {
                                    //tapped
                                    SendAction(listener, action, args, TouchActionResult.Tapped);
                                }

                                SendTappedCommand(args);
                                var passView = element;
                                if (PassView != null)
                                {
                                    passView = PassView;
                                }
                                Tapped?.Invoke(passView, args);
                                LastActionResult = TouchActionResult.Tapped;
                            }
                            else
                            {
                                Debug.WriteLine("[Gestures] Tap skipped");
                            }

                            //IsLongPressing = false;

                            //PANNED
                            if (IsPanning)
                            {
                                IsPanning = false;
                            }
                        }
                        else
                        {
                            //Multutouch
                            _manipulationTracker.RemoveTouch(args.Id);
                        }

                        //UP
                        if (listener != null)
                        {
                            SendAction(listener, action, args, TouchActionResult.Up);
                        }

                        Up?.Invoke(element, args);
                        LastActionResult = TouchActionResult.Up;

                    }

                    //Console.WriteLine($"[TOUCH] {LastActionResult} fingers {FingersCount}");
                    //advise client with some logic..


                    if (//args.NumberOfTouches == 1 &&
                        (args.Distance.Delta.X != 0 || args.Distance.Delta.Y != 0)
                        && LastActionResult == TouchActionResult.Panning)
                    {
                        if (listener != null)
                        {
                            SendAction(listener, action, args, TouchActionResult.Panning);
                        }
                        Panning?.Invoke(element, args);
                        IsPanning = true;
                    }

                    #region Send raw touch event
                    // Always fire TouchAction for ALL event types so subscribers can see everything
                    TouchAction?.Invoke(element, args);
                    #endregion

                    _lastArgs = args;

                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

            }



        }


    }
}
