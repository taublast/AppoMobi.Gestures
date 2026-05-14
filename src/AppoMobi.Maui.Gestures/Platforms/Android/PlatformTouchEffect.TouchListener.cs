using Android.Content;
using Android.Views;
using View = Android.Views.View;


namespace AppoMobi.Gestures;

public partial class PlatformTouchEffect
{
    public class TouchListener : GestureDetector.SimpleOnGestureListener, View.IOnTouchListener, View.IOnHoverListener, View.IOnGenericMotionListener
    {
        public TouchListener(PlatformTouchEffect platformEffect, Context ctx)
        {
            _parent = platformEffect; //todo remove on dispose
        }

        private volatile PlatformTouchEffect _parent;

        // Mouse state tracking
        private MouseButtons _currentPressedButtons = MouseButtons.None;


        void LockInput(View sender)
        {
            sender.Parent?.RequestDisallowInterceptTouchEvent(true);
        }

        void UnlockInput(View sender)
        {
            sender.Parent?.RequestDisallowInterceptTouchEvent(false);
        }

        public bool OnTouch(View sender, MotionEvent motionEvent)
        {
            //System.Diagnostics.Debug.WriteLine($"[TOUCH] Android: {motionEvent.Action} {motionEvent.RawY:0}");

            if (_parent.FormsEffect.TouchMode == TouchHandlingStyle.Disabled)
                return false;

            // Check if this is a mouse event
            if (IsMouseEvent(motionEvent))
            {
                return HandleMouseEvent(sender, motionEvent);
            }

            _parent.CountFingers = motionEvent.PointerCount;

            // Get the pointer index
            int pointerIndex = motionEvent.ActionIndex;

            // Get the id that identifies a finger over the course of its progress
            int id = motionEvent.GetPointerId(pointerIndex);

            //Pixels relative to the view, not the screen
            var coorsInsideView = new PointF(motionEvent.GetX(pointerIndex), motionEvent.GetY(pointerIndex));
            _parent.isInsideView = coorsInsideView.X >= 0 && coorsInsideView.X <= sender.Width && coorsInsideView.Y >= 0 && coorsInsideView.Y <= sender.Height;

            try
            {
                switch (motionEvent.ActionMasked)
                {
                    //detect additional pointers (i.e., fingers) going down on the screen after the initial touch.
                    //typically used in multi-touch scenarios when multiple fingers are involved.
                    case MotionEventActions.PointerDown:
                        _parent.FireEvent(id, TouchActionType.Pressed, coorsInsideView);
                        break;

                    case MotionEventActions.Down:
                        //case MotionEventActions.PointerDown:
                        if (_parent.FormsEffect.TouchMode == TouchHandlingStyle.Lock)
                        {
                            LockInput(sender);
                        }
                        else if (_parent.FormsEffect.TouchMode == TouchHandlingStyle.Manual)
                        {
                            // For Manual mode: start unlocked, will be controlled dynamically in Move
                            UnlockInput(sender);
                        }
                        else
                        {
                            UnlockInput(sender);
                        }
                        _parent.FireEvent(id, TouchActionType.Pressed, coorsInsideView);
                        break;

                    case MotionEventActions.Move:
                        // Multiple Move events are bundled, so handle them in a loop
                        for (pointerIndex = 0; pointerIndex < motionEvent.PointerCount; pointerIndex++)
                        {
                            id = motionEvent.GetPointerId(pointerIndex);
                            coorsInsideView = new PointF(motionEvent.GetX(pointerIndex), motionEvent.GetY(pointerIndex));
                            _parent.FireEvent(id, TouchActionType.Moved, coorsInsideView);
                        }

                        // For Manual mode: dynamically control parent input based on WIllLock state
                        if (_parent.FormsEffect.TouchMode == TouchHandlingStyle.Manual)
                        {
                            if (_parent.FormsEffect.WIllLock == ShareLockState.Locked)
                            {
                                // Consumer wants control - block parent
                                LockInput(sender);
                            }
                            else if (_parent.FormsEffect.WIllLock == ShareLockState.Unlocked)
                            {
                                // Consumer doesn't want control - allow parent
                                UnlockInput(sender);
                            }
                            // For Initial state, do nothing (keep previous state)
                        }
                        break;

                    case MotionEventActions.PointerUp:
                        _parent.FireEvent(id, TouchActionType.Released,
                            coorsInsideView);
                        break;

                    case MotionEventActions.Up:
                        UnlockInput(sender);
                        _parent.FireEvent(id, TouchActionType.Released,
                            coorsInsideView);
                        break;

                    case MotionEventActions.Cancel:
                        UnlockInput(sender);
                        _parent.FireEvent(id, TouchActionType.Cancelled, coorsInsideView);
                        break;

                    default:
                        UnlockInput(sender);
                        break;

                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[TOUCH] Android: {motionEvent} - {e}");
            }

            return true;
        }

        #region Mouse Support

        private bool IsMouseEvent(MotionEvent motionEvent)
        {
            // Check if this is a mouse event based on tool type
            var toolType = motionEvent.GetToolType(0);
            return toolType == MotionEventToolType.Mouse ||
                   toolType == MotionEventToolType.Stylus ||
                   toolType == MotionEventToolType.Eraser;
        }

        private bool HandleMouseEvent(View sender, MotionEvent motionEvent)
        {
            try
            {
                // Get mouse position
                var coorsInsideView = new PointF(motionEvent.GetX(), motionEvent.GetY());
                _parent.isInsideView = coorsInsideView.X >= 0 && coorsInsideView.X <= sender.Width &&
                                     coorsInsideView.Y >= 0 && coorsInsideView.Y <= sender.Height;

                // Detect device type
                var deviceType = GetDeviceTypeFromMotionEvent(motionEvent);
                var pressure = GetPressureFromMotionEvent(motionEvent);

                // Handle different mouse actions
                switch (motionEvent.ActionMasked)
                {
                    case MotionEventActions.Down:
                        return HandleMouseButtonPress(sender, motionEvent, coorsInsideView, deviceType, pressure);

                    case MotionEventActions.Up:
                        return HandleMouseButtonRelease(sender, motionEvent, coorsInsideView, deviceType, pressure);

                    case MotionEventActions.Move:
                        return HandleMouseMove(sender, motionEvent, coorsInsideView, deviceType, pressure);

                    case MotionEventActions.Cancel:
                        return HandleMouseCancel(sender, motionEvent, coorsInsideView, deviceType, pressure);

                    default:
                        // Handle ButtonPress/ButtonRelease for API 23+ only
                        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M)
                        {
                            if (motionEvent.ActionMasked == MotionEventActions.ButtonPress)
                                return HandleMouseButtonPress(sender, motionEvent, coorsInsideView, deviceType, pressure);
                            if (motionEvent.ActionMasked == MotionEventActions.ButtonRelease)
                                return HandleMouseButtonRelease(sender, motionEvent, coorsInsideView, deviceType, pressure);
                        }
                        return false;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[MOUSE] Android: {motionEvent} - {e}");
                return false;
            }
        }

        private PointerDeviceType GetDeviceTypeFromMotionEvent(MotionEvent motionEvent)
        {
            var toolType = motionEvent.GetToolType(0);
            switch (toolType)
            {
                case MotionEventToolType.Stylus:
                case MotionEventToolType.Eraser:
                    return PointerDeviceType.Pen;
                case MotionEventToolType.Mouse:
                default:
                    return PointerDeviceType.Mouse;
            }
        }

        private float GetPressureFromMotionEvent(MotionEvent motionEvent)
        {
            // Android provides pressure information for stylus and some mice
            return motionEvent.GetPressure(0);
        }

        private bool HandleMouseButtonPress(View sender, MotionEvent motionEvent, PointF location, PointerDeviceType deviceType, float pressure)
        {
            var buttonInfo = GetPressedButtonInfo(motionEvent);
            var button = buttonInfo.Button;
            var buttonNumber = buttonInfo.ButtonNumber;

            // Update pressed buttons state
            _currentPressedButtons |= GetMouseButtonsFromButton(button);

            // Smart button handling: Left button uses standard events, others use Pointer events
            if (button == MouseButton.Left)
            {
                // Left button uses standard touch events for backward compatibility
                _parent.FireEventWithMouse(0, TouchActionType.Pressed, location, deviceType, pressure,
                    button, buttonNumber, MouseButtonState.Pressed, _currentPressedButtons);
            }
            else
            {
                // Other buttons use Pointer events to avoid breaking existing controls
                _parent.FireEventPointerWithMouse(0, location, deviceType, pressure,
                    button, buttonNumber, MouseButtonState.Pressed, _currentPressedButtons);
            }

            return true;
        }

        private bool HandleMouseButtonRelease(View sender, MotionEvent motionEvent, PointF location, PointerDeviceType deviceType, float pressure)
        {
            var buttonInfo = GetReleasedButtonInfo(motionEvent);
            var button = buttonInfo.Button;
            var buttonNumber = buttonInfo.ButtonNumber;

            // Update pressed buttons state
            _currentPressedButtons &= ~GetMouseButtonsFromButton(button);

            // Smart button handling: Left button uses standard events, others use Pointer events
            if (button == MouseButton.Left)
            {
                // Left button uses standard touch events for backward compatibility
                _parent.FireEventWithMouse(0, TouchActionType.Released, location, deviceType, pressure,
                    button, buttonNumber, MouseButtonState.Released, _currentPressedButtons);
            }
            else
            {
                // Other buttons use Pointer events to avoid breaking existing controls
                _parent.FireEventPointerWithMouse(0, location, deviceType, pressure,
                    button, buttonNumber, MouseButtonState.Released, _currentPressedButtons);
            }

            return true;
        }

        private bool HandleMouseMove(View sender, MotionEvent motionEvent, PointF location, PointerDeviceType deviceType, float pressure)
        {
            if (_currentPressedButtons != MouseButtons.None)
            {
                // Mouse drag with buttons pressed
                _parent.FireEventWithMouse(0, TouchActionType.Moved, location, deviceType, pressure,
                    MouseButton.Left, 0, MouseButtonState.Pressed, _currentPressedButtons);
            }
            else
            {
                // Mouse hover (no buttons pressed)
                _parent.FireEventPointerWithMouse(0, location, deviceType, pressure,
                    MouseButton.Left, 0, MouseButtonState.Released, _currentPressedButtons);
            }

            return true;
        }

        private bool HandleMouseCancel(View sender, MotionEvent motionEvent, PointF location, PointerDeviceType deviceType, float pressure)
        {
            // Reset button state on cancel
            _currentPressedButtons = MouseButtons.None;

            _parent.FireEventWithMouse(0, TouchActionType.Cancelled, location, deviceType, pressure,
                MouseButton.Left, 0, MouseButtonState.Released, _currentPressedButtons);

            return true;
        }

        private (MouseButton Button, int ButtonNumber) GetPressedButtonInfo(MotionEvent motionEvent)
        {
            // Android provides button state through ButtonState property
            var buttonState = motionEvent.ButtonState;

            // Check for specific buttons
            if (buttonState.HasFlag(MotionEventButtonState.Primary))
                return (MouseButton.Left, 1);
            if (buttonState.HasFlag(MotionEventButtonState.Secondary))
                return (MouseButton.Right, 2);
            if (buttonState.HasFlag(MotionEventButtonState.Tertiary))
                return (MouseButton.Middle, 3);
            if (buttonState.HasFlag(MotionEventButtonState.Back))
                return (MouseButton.XButton1, 4);
            if (buttonState.HasFlag(MotionEventButtonState.Forward))
                return (MouseButton.XButton2, 5);

            // Default to left button
            return (MouseButton.Left, 1);
        }

        private (MouseButton Button, int ButtonNumber) GetReleasedButtonInfo(MotionEvent motionEvent)
        {
            // For release events, we need to determine which button was released
            // This is more complex in Android as we need to compare previous and current state

            // For now, use the same logic as pressed (Android limitation)
            return GetPressedButtonInfo(motionEvent);
        }

        private MouseButtons GetMouseButtonsFromButton(MouseButton button)
        {
            return button switch
            {
                MouseButton.Left => MouseButtons.Left,
                MouseButton.Right => MouseButtons.Right,
                MouseButton.Middle => MouseButtons.Middle,
                MouseButton.XButton1 => MouseButtons.XButton1,
                MouseButton.XButton2 => MouseButtons.XButton2,
                _ => MouseButtons.None
            };
        }

        #endregion

        #region Interface Implementations for Mouse Support

        public bool OnHover(View v, MotionEvent e)
        {
            // Handle mouse hover events (movement without button press)
            if (_parent.FormsEffect.TouchMode == TouchHandlingStyle.Disabled)
                return false;

            try
            {
                var location = new PointF(e.GetX(), e.GetY());
                var deviceType = GetDeviceTypeFromMotionEvent(e);
                var pressure = GetPressureFromMotionEvent(e);

                // Fire hover event
                _parent.FireEventPointerWithMouse(0, location, deviceType, pressure,
                    MouseButton.Left, 0, MouseButtonState.Released, MouseButtons.None);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HOVER] Android: {e} - {ex}");
                return false;
            }
        }

        public bool OnGenericMotion(View v, MotionEvent e)
        {
            // Handle scroll wheel and other generic motion events
            if (_parent.FormsEffect.TouchMode == TouchHandlingStyle.Disabled)
                return false;

            try
            {
                if (e.Action == MotionEventActions.Scroll)
                {
                    var location = new PointF(e.GetX(), e.GetY());
                    var scrollX = e.GetAxisValue(Axis.Hscroll);
                    var scrollY = e.GetAxisValue(Axis.Vscroll);

                    // Fire wheel event
                    _parent.FireEventScroll(0, location, new PointF(scrollX, scrollY));
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GENERIC] Android: {e} - {ex}");
                return false;
            }
        }

        #endregion


    }
}
