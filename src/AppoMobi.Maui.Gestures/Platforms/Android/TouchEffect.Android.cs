using Android.Content;
using Android.Views.InputMethods;

namespace AppoMobi.Gestures
{
    public partial class TouchEffect
    {
        bool _isPanning;
        bool _blockedPanning;
        bool _hadTap;
        bool _hadLong;

        /// <summary>
        /// To filter micro-gestures on super sensitive screens, start passing panning only when threshold is once overpassed
        /// </summary>
        public static float FirstPanThreshold = 5;

        // Track which pointers are currently long pressed
        private readonly HashSet<long> _longPressedPointers = new();

        void SendActionPlatform(IGestureListener listener, TouchActionType action, TouchActionEventArgs args, TouchActionResult touchAction)
        {
            // on some devices like galaxy the screen is too sensitive for panning
            // so it send micro-panning gestures when the finger just went down to screen
            // like not moving yet so we filter micro-pan
            // at the same time those screens detect pan instead of tap inside getsure lib
            // so this is a specific android workaround, to be moved to gestures lib
            if (touchAction == TouchActionResult.Tapped)
            {
                _hadTap = true;
            }
            else if (touchAction == TouchActionResult.LongPressing)
            {
                _hadLong = true;
                _longPressedPointers.Add(args.Id); // mark this pointer as long-pressed
            }
            else if (touchAction == TouchActionResult.Down)
            {
                _isPanning = false;
                _hadLong = false;
                _hadTap = false;
                _blockedPanning = false;
            }
            else if (touchAction == TouchActionResult.Up)
            {
                _longPressedPointers.Remove(args.Id); // clear long-press state
            }
            else if (touchAction == TouchActionResult.Panning)
            {
                //filter micro-gestures
                if ((Math.Abs(args.Distance.Delta.X) < 1 && Math.Abs(args.Distance.Delta.Y) < 1)
                    || (Math.Abs(args.Distance.Velocity.X / Density) < 1 &&
                        Math.Abs(args.Distance.Velocity.Y / Density) < 1))
                {
                    _blockedPanning = true;
                    return;
                }

                var threshold = FirstPanThreshold * Density;

                if (!_isPanning)
                {
                    //filter first panning movement on super sensitive screens
                    if (Math.Abs(args.Distance.Total.X) < threshold && Math.Abs(args.Distance.Total.Y) < threshold)
                    {
                        return;
                    }

                    _isPanning = true;
                }
            }

            //filter micro-pan
            bool fixMicroPan = _blockedPanning && !_hadTap && !_isPanning && !_hadLong;

            // allow taps from other fingers if some pointer is in long-press state
            bool hadPalm = touchAction == TouchActionResult.Up && _longPressedPointers.Any(id => id != args.Id);

            listener.OnGestureEvent(action, args, touchAction);

            //add programmatic tap for micro-pan if we filtered it but the finger is actually up,
            //or if we had a long-press and now we have an up from another finger,
            //to allow taps from other fingers when some pointer is in long-press state
            if (fixMicroPan || hadPalm)
            {
                listener.OnGestureEvent(action, args, TouchActionResult.Tapped);
            }

            //System.Diagnostics.Debug.WriteLine($"[TOUCH] Sent {action} {result} y {args.Location.Y:0}"); //x,y {args.Location.X:0}, {args.Location.Y:0} inside: {isInsideView}

            if (touchAction == TouchActionResult.Up || touchAction == TouchActionResult.Down)
            {
                WIllLock = ShareLockState.Initial;
            }
        }

        static bool _closingKeyboard;
        public static void ClosePlatformKeyboard()
        {
            if (!_closingKeyboard)
            {
                _closingKeyboard = true;

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Task.Delay(250); // For some reason, a short delay is required here.
                    try
                    {
                        var imm = (InputMethodManager)Platform.AppContext.GetSystemService(Context.InputMethodService);
                        var token = Platform.CurrentActivity?.Window?.DecorView?.WindowToken;
                        imm.HideSoftInputFromWindow(token, 0);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                    finally
                    {
                        _closingKeyboard = false;
                    }

                });
            }
        }

    }
}
