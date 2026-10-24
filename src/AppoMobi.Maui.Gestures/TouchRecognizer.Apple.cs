using System.Diagnostics;
using CoreGraphics;
using Foundation;
using Microsoft.Maui.Controls;
using UIKit;

namespace AppoMobi.Gestures
{
    class TouchRecognizer : UIGestureRecognizer, IUIGestureRecognizerDelegate
    {
        volatile PlatformTouchEffect _parent;
        UIView _view;

        private bool _disposed;

        // Store button info per touch ID (iOS 13.4+)
        private Dictionary<long, (MouseButton button, int buttonNumber)> _touchButtonMap =
            new Dictionary<long, (MouseButton, int)>();

        public TouchRecognizer(UIView view, PlatformTouchEffect parent)
        {
            _view = view;
            _parent = parent;
            // UIKit holds touchesEnded of the views below until a recognizer ends or fails; this one stays Possible,
            // so native views hosted inside the canvas would never see their touch end
            DelaysTouchesEnded = false;
        }

        #region Lock Pan

        void CheckLockPan()
        {
            if (_disposed || _parent == null || _parent.FormsEffect == null)
                return;

            this.CancelsTouchesInView = false;

            if (_parent.FormsEffect.TouchMode == TouchHandlingStyle.Lock ||
                _parent.FormsEffect.TouchMode == TouchHandlingStyle.Manual)
            {
                AttachPanning();
            }
            else if (_childPanGestureRecognizer != null)
            {
                DetachPanning();
            }

            if (_parent.FormsEffect.TouchMode == TouchHandlingStyle.Lock)
            {
                LockTouch();
            }
            else if (_parent.FormsEffect.TouchMode == TouchHandlingStyle.Manual)
            {
                SoftLockTouch();
            }
            else
            {
                UnlockTouch();
            }
        }

        private UIPanGestureRecognizer _childPanGestureRecognizer; // Reference to child UIPanGestureRecognizer
        private UIGestureRecognizer _parentScrollViewRecognizer; // Reference to parent ScrollView's recognizer

        void AttachPanning()
        {
            if (_childPanGestureRecognizer != null)
                return;

            _childPanGestureRecognizer = new UIPanGestureRecognizer(HandlePan)
            {
                CancelsTouchesInView = false, DelaysTouchesEnded = false, Delegate = this, AllowedScrollTypesMask = UIScrollTypeMask.All
            };
            _view.AddGestureRecognizer(_childPanGestureRecognizer);
        }


        void DetachPanning()
        {
            if (_childPanGestureRecognizer != null)
            {
                _view?.RemoveGestureRecognizer(_childPanGestureRecognizer);
                _childPanGestureRecognizer.Delegate = null;
                _childPanGestureRecognizer = null;
            }
        }

        /// <summary>
        /// Handles the pan gesture action.
        /// For Lock mode: just blocks parent scrollviews by existing.
        /// For SoftLock mode: controlled dynamically via State in TouchesMoved.
        /// Also handles scroll events (two-finger scroll on trackpad).
        /// </summary>
        /// <param name="gesture">The UIPanGestureRecognizer triggering the action.</param>
        private void HandlePan(UIPanGestureRecognizer gesture)
        {
            // Handle two-finger trackpad scroll  
            if (gesture.State == UIGestureRecognizerState.Changed && _parent != null)
            {
                // Get the translation - this is the scroll delta for two-finger scroll
                var translation = gesture.TranslationInView(_view);
                var location = gesture.LocationInView(_view);

                // Convert to pixel coordinates
                var point = new PointF(
                    (float)(location.X * TouchEffect.Density),
                    (float)(location.Y * TouchEffect.Density)
                );

                var delta = new PointF(
                    (float)(translation.X * TouchEffect.Density),
                    (float)(translation.Y * TouchEffect.Density)
                );

                // Only fire if there's actual movement
                if (delta.X != 0 || delta.Y != 0)
                {
                    Debug.WriteLine(
                        $"[Pan/Scroll] Delta: ({delta.X:F2}, {delta.Y:F2}) at ({point.X:F0}, {point.Y:F0})");

                    // Fire wheel event for scroll
                    _parent.FireEventScroll(0, point, delta);

                    // Reset translation for next event
                    gesture.SetTranslation(CGPoint.Empty, _view);
                }
            }
        }

        #endregion

        public void Detach()
        {
            try
            {
                if (_twoFingerTapRecognizer != null)
                {
                    _view?.RemoveGestureRecognizer(_twoFingerTapRecognizer);
                }

                if (_hoverRecognizer != null)
                {
                    _view?.RemoveGestureRecognizer(_hoverRecognizer);
                    _hoverRecognizer = null;
                }

                _view?.RemoveGestureRecognizer(this);

                DetachPanning();
            }
            catch (Exception e)
            {
                //might still crash when detaching from a destroyed view
                Console.WriteLine(e);
            }
        }

        public void Attach()
        {
            _view?.AddGestureRecognizer(this);

            // Enable pointer events (iOS 13.4+)
            if (_view != null && UIDevice.CurrentDevice.CheckSystemVersion(13, 4))
            {
                _view.UserInteractionEnabled = true;
                _view.MultipleTouchEnabled = true;

                // Hover tracking: a mouse / trackpad pointer or a hovering Apple Pencil (iPad, Mac Catalyst). An iPhone has
                // neither, and there the recognizer reported a hover where a finger had just lifted.
                if (UIDevice.CurrentDevice.UserInterfaceIdiom != UIUserInterfaceIdiom.Phone)
                {
                    _hoverRecognizer = new UIHoverGestureRecognizer(HandleHover);
                    _view.AddGestureRecognizer(_hoverRecognizer);

                    // Secondary click (right button, two-finger trackpad click) of a mouse or trackpad. Not on an
                    // iPhone, and it receives pointer touches only (ShouldReceiveTouch): on iOS 26 a plain finger tap
                    // satisfied ButtonMaskRequired and every tap also sent a mouse right click.
                    _twoFingerTapRecognizer = new UITapGestureRecognizer(HandleTwoFingerTap)
                    {
                        ButtonMaskRequired = UIEventButtonMask.Secondary, Delegate = this
                    };
                    _view.AddGestureRecognizer(_twoFingerTapRecognizer);
                }
            }

            CheckLockPan();
        }

        PointF _lastPoint;
        private UITapGestureRecognizer _twoFingerTapRecognizer;
        private UIHoverGestureRecognizer _hoverRecognizer;

        public override bool ShouldRequireFailureOfGestureRecognizer(UIGestureRecognizer otherGestureRecognizer)
        {
            return false;
        }

        private bool ShouldRecLocked(UIGestureRecognizer gesturerecognizer, UIGestureRecognizer othergesturerecognizer)
        {
            if (othergesturerecognizer == _childPanGestureRecognizer
                || othergesturerecognizer == _twoFingerTapRecognizer ||
                othergesturerecognizer == this)
            {
                return true; // Allow simultaneous recognition with child UIPanGestureRecognizer
            }

            // Capture reference to parent ScrollView's pan recognizer for SoftLock mode
            if (_parent.FormsEffect.TouchMode == TouchHandlingStyle.Manual)
            {
                if (othergesturerecognizer is UIPanGestureRecognizer &&
                    othergesturerecognizer != _childPanGestureRecognizer &&
                    _parentScrollViewRecognizer == null)
                {
                    _parentScrollViewRecognizer = othergesturerecognizer;
                    Debug.WriteLine($"Captured parent recognizer: {othergesturerecognizer.GetType().Name}");
                }
            }

            return false;
        }

        private bool SetTrueCapture(UIGestureRecognizer gesturerecognizer, UIGestureRecognizer othergesturerecognizer)
        {
            // Capture reference to parent ScrollView's pan recognizer for SoftLock mode
            if (_parent.FormsEffect.TouchMode == TouchHandlingStyle.Manual)
            {
                if (othergesturerecognizer is UIPanGestureRecognizer &&
                    othergesturerecognizer != _childPanGestureRecognizer &&
                    _parentScrollViewRecognizer == null)
                {
                    _parentScrollViewRecognizer = othergesturerecognizer;
                    Debug.WriteLine($"Captured parent recognizer: {othergesturerecognizer.GetType().Name}");
                }
            }

            return true;
        }

        private bool SetTrue(UIGestureRecognizer gesturerecognizer, UIGestureRecognizer othergesturerecognizer)
        {
            return true;
        }

        private bool SetFalse(UIGestureRecognizer gesturerecognizer, UIGestureRecognizer othergesturerecognizer)
        {
            return false;
        }

        void SoftLockTouch()
        {
            // For SoftLock: recognize simultaneously and require us to fail to pass gesture to parent
            ShouldBeRequiredToFailBy = SetFalse;
            ShouldRecognizeSimultaneously = SetTrueCapture;
        }

        void LockTouch()
        {
            ShouldBeRequiredToFailBy = SetFalse;
            ShouldRecognizeSimultaneously = ShouldRecLocked;
        }

        private bool ShouldEvent(UIGestureRecognizer gesturerecognizer, UIEvent @event)
        {
            return true;
        }

        // Implement ShouldReceiveTouch from IUIGestureRecognizerDelegate
        [Export("gestureRecognizer:shouldReceiveTouch:")]
        public bool ShouldReceiveTouch(UIGestureRecognizer recognizer, UITouch touch)
        {
            // For the child pan recognizer (scroll handler), reject direct touches
            // Only accept trackpad/mouse scroll events (which don't come through as touches)
            if (recognizer == _childPanGestureRecognizer)
            {
                Debug.WriteLine($"[ShouldReceiveTouch] Pan recognizer rejecting touch type: {touch.Type}");
                return false; // Disallow touches, only allow trackpad/mouse scroll
            }

            // the secondary click recognizer: a mouse or trackpad click only, never a finger
            if (recognizer == _twoFingerTapRecognizer)
            {
                return touch.Type == UITouchType.IndirectPointer;
            }

            return true; // Allow touches for other recognizers
        }

        void UnlockTouch()
        {
            if (UIDevice.CurrentDevice.CheckSystemVersion(13, 4))
            {
                ShouldReceiveEvent = ShouldEvent;
            }

            ShouldBeRequiredToFailBy = null;
            ShouldRecognizeSimultaneously = null;
        }

        private bool IsViewOrAncestorHidden(UIView view)
        {
            if (view == null)
            {
                return false;
            }

            return view.Hidden || view.Alpha == 0 || IsViewOrAncestorHidden(view.Superview);
        }

        #region Pointer Event Handling (iOS 13.4+)

        private (MouseButton button, int buttonNumber)? GetButtonFromEvent(UIEvent evt)
        {
            // Use UIEvent.ButtonMask (iOS 13.4+) for reliable button detection
            // Reference: https://developer.apple.com/videos/play/wwdc2020/10094/
            // IMPORTANT: ButtonMask shows CURRENTLY pressed buttons, so check this during TouchesBegan
            // and store the result per touch ID for use in TouchesEnded

            if (evt == null)
                return null;

            // Check ButtonMask property - requires iOS 13.4+
            var buttonMask = evt.ButtonMask;

            // Primary button (left click)
            if ((buttonMask & UIEventButtonMask.Primary) != 0)
                return (MouseButton.Left, 1);

            // Secondary button (right click)
            if ((buttonMask & UIEventButtonMask.Secondary) != 0)
                return (MouseButton.Right, 2);

            // No buttons pressed (happens during release or hover)
            return null;
        }

        private PointerDeviceType GetPointerDeviceType(UITouch touch)
        {
            // Check for Apple Pencil (stylus)
            if (touch.Type == UITouchType.Stylus)
                return PointerDeviceType.Pen;

            // Mouse or trackpad click (iOS 13.4+). Indirect is the Apple TV remote, not a pointer. UIKit marks pointer
            // clicks only when the app declares UIApplicationSupportsIndirectInputEvents = true in Info.plist; without
            // it they arrive as plain finger touches.
            if (touch.Type == UITouchType.IndirectPointer)
                return PointerDeviceType.Mouse;

            // Direct touch
            return PointerDeviceType.Touch;
        }

        private PointF GetPointFromTouch(UITouch touch)
        {
            var locationInView = touch.LocationInView(_view);
            return new PointF((float)(locationInView.X * TouchEffect.Density),
                (float)(locationInView.Y * TouchEffect.Density));
        }

        private bool IsPointerEvent(UITouch touch)
        {
            // iOS 13.4+ provides TouchType.IndirectPointer for mouse/trackpad input (see GetPointerDeviceType)
            return touch.Type == UITouchType.IndirectPointer || touch.Type == UITouchType.Stylus;
        }

        private MouseButtons GetCurrentPressedButtons(UIEvent evt)
        {
            var buttonMask = evt.ButtonMask;
            MouseButtons result = MouseButtons.None;

            if ((buttonMask & UIEventButtonMask.Primary) != 0)
                result |= MouseButtons.Left;
            if ((buttonMask & UIEventButtonMask.Secondary) != 0)
                result |= MouseButtons.Right;

            return result;
        }

        private void HandleHover(UIHoverGestureRecognizer recognizer)
        {
            if (_parent == null || IsViewOrAncestorHidden(this.View))
                return;

            // Began / Changed are hover moves; Ended / Cancelled mean the pointer is gone, not that it moved there
            if (recognizer.State != UIGestureRecognizerState.Began && recognizer.State != UIGestureRecognizerState.Changed)
                return;

            var location = recognizer.LocationInView(_view);
            var point = new PointF((float)(location.X * TouchEffect.Density),
                (float)(location.Y * TouchEffect.Density));

            // Fire hover event (no button pressed during hover)
            _parent.FireEventPointerWithMouse(0, point, GetHoverDeviceType(recognizer), 1.0f);
        }

        /// <summary>
        /// A hovering Apple Pencil reports its height above the screen (zOffset, iOS / Mac Catalyst 16.1+); a mouse or
        /// trackpad pointer always reports 0.
        /// </summary>
        private static PointerDeviceType GetHoverDeviceType(UIHoverGestureRecognizer recognizer)
        {
            if ((OperatingSystem.IsIOSVersionAtLeast(16, 1) || OperatingSystem.IsMacCatalystVersionAtLeast(16, 1))
                && recognizer.ZOffset > 0)
                return PointerDeviceType.Pen;

            return PointerDeviceType.Mouse;
        }

        private void HandleTwoFingerTap(UITapGestureRecognizer recognizer)
        {
            if (_parent == null || IsViewOrAncestorHidden(this.View))
                return;

            if (recognizer.State == UIGestureRecognizerState.Ended)
            {
                var location = recognizer.LocationInView(_view);
                var point = new PointF((float)(location.X * TouchEffect.Density),
                    (float)(location.Y * TouchEffect.Density));

                Debug.WriteLine($"[TwoFingerTap] Detected at ({point.X:F0}, {point.Y:F0})");

                // Fire right-click event (two-finger tap = secondary button according to WWDC 2020)
                // Fire both pressed and released for a complete click
                _parent.FireEventWithMouse(0, TouchActionType.Pointer, point,
                    MouseButton.Right, 2, MouseButtonState.Pressed, MouseButtons.Right,
                    PointerDeviceType.Mouse, 1.0f);

                _parent.FireEventWithMouse(0, TouchActionType.Pointer, point,
                    MouseButton.Right, 2, MouseButtonState.Released, MouseButtons.None,
                    PointerDeviceType.Mouse, 1.0f);
            }
        }

        #endregion

        // touches = touches of interest; evt = all touches of type UITouch
        public override void TouchesBegan(NSSet touches, UIEvent evt)
        {
            base.TouchesBegan(touches, evt);

            if (IsViewOrAncestorHidden(this.View))
            {
                this.State = UIGestureRecognizerState.Failed;
                return;
            }

            _parent.CountFingers = (int)NumberOfTouches;

            // Debug logging to see what events we're receiving
            Debug.WriteLine(
                $"[TouchesBegan] Type:{evt.Type} Touches:{touches.Count} NumberOfTouches:{NumberOfTouches} ButtonMask:{evt.ButtonMask}");

            foreach (UITouch touch in touches.Cast<UITouch>())
            {
                long id = ((IntPtr)touch.Handle).ToInt64();

                // Check if this is a pointer event (mouse/pen) on iOS 13.4+
                if (IsPointerEvent(touch))
                {
                    var deviceType = GetPointerDeviceType(touch);
                    var point = GetPointFromTouch(touch);
                    var buttonInfo = GetButtonFromEvent(evt);
                    var pressedButtons = GetCurrentPressedButtons(evt);

                    if (buttonInfo.HasValue)
                    {
                        // Store button info for this touch ID to use later in TouchesEnded
                        _touchButtonMap[id] = buttonInfo.Value;

                        // Get pressure for Apple Pencil
                        var pressure = touch.Type == UITouchType.Stylus ? (float)touch.Force : 1.0f;

                        // Primary button (Left) uses Pressed event type for backward compatibility
                        // Secondary buttons use Pointer event type to avoid interfering with touch logic
                        var eventType = buttonInfo.Value.button == MouseButton.Left
                            ? TouchActionType.Pressed
                            : TouchActionType.Pointer;

                        var buttonState = MouseButtonState.Pressed;

                        _parent.FireEventWithMouse(id, eventType, point, buttonInfo.Value.button,
                            buttonInfo.Value.buttonNumber, buttonState, pressedButtons, deviceType, pressure);
                    }
                }
                else
                {
                    // Regular touch event
                    _parent.FireEvent(id, TouchActionType.Pressed, touch);
                }
            }

            _parent.isInsideView = true;

            CheckLockPan();
        }

        public override void TouchesMoved(NSSet touches, UIEvent evt)
        {
            base.TouchesMoved(touches, evt);

            _parent.CountFingers = (int)NumberOfTouches;

            foreach (UITouch touch in touches.Cast<UITouch>())
            {
                long id = ((IntPtr)touch.Handle).ToInt64();

                // Check if this is a pointer event (mouse/pen) on iOS 13.4+
                if (IsPointerEvent(touch))
                {
                    var deviceType = GetPointerDeviceType(touch);
                    var point = GetPointFromTouch(touch);
                    var pressedButtons = GetCurrentPressedButtons(evt);

                    // Get pressure for Apple Pencil
                    var pressure = touch.Type == UITouchType.Stylus ? (float)touch.Force : 1.0f;

                    // Fire move event with current button state
                    _parent.FireEventWithMouseMove(id, TouchActionType.Moved, point, pressedButtons, deviceType,
                        pressure);
                }
                else
                {
                    // Regular touch event
                    _parent.FireEvent(id, TouchActionType.Moved, touch);
                }
            }

            // For Manual mode: dynamically control gesture blocking based on WIllLock state
            if (_parent.FormsEffect.TouchMode == TouchHandlingStyle.Manual)
            {
                if (_parent.FormsEffect.WIllLock == ShareLockState.Unlocked)
                {
                    // Consumer didn't handle - fail child pan to release parent ScrollView
                    if (_childPanGestureRecognizer != null)
                    {
                        _childPanGestureRecognizer.State = UIGestureRecognizerState.Failed;
                        Debug.WriteLine("Child pan FAILED - releasing to parent");
                    }
                }
                else if (_parent.FormsEffect.WIllLock == ShareLockState.Locked)
                {
                    // Consumer wants control - fail parent ScrollView if it already started
                    if (_parentScrollViewRecognizer != null &&
                        _parentScrollViewRecognizer.State == UIGestureRecognizerState.Changed)
                    {
                        _parentScrollViewRecognizer.State = UIGestureRecognizerState.Cancelled;
                        Debug.WriteLine("Parent ScrollView CANCELLED - taking control");
                    }
                }
            }
        }

        public override void TouchesEnded(NSSet touches, UIEvent evt)
        {
            base.TouchesEnded(touches, evt);

            _parent.CountFingers = (int)NumberOfTouches;

            if (evt.ButtonMask == UIEventButtonMask.Secondary)
            {
                Debug.WriteLine("We GOT RIGHT CLICK !!!");
            }

            var uiTouches = touches.Cast<UITouch>();
            if (uiTouches.Count() > 0)
            {
                foreach (UITouch touch in uiTouches)
                {
                    CGPoint cgPoint = touch.LocationInView(this.View);
                    var xfPoint = new PointF((float)cgPoint.X, (float)cgPoint.Y);
                    bool isInside = CheckPointIsInsideRecognizer(xfPoint, this);
                    long id = ((IntPtr)touch.Handle).ToInt64();

                    // Check if this is a pointer event (mouse/pen) on iOS 13.4+
                    if (IsPointerEvent(touch))
                    {
                        var deviceType = GetPointerDeviceType(touch);
                        var point = GetPointFromTouch(touch);
                        var pressedButtons = GetCurrentPressedButtons(evt);

                        // Retrieve button info that was stored during TouchesBegan
                        // (ButtonMask is empty during release, so we need the stored value)
                        if (_touchButtonMap.TryGetValue(id, out var buttonInfo))
                        {
                            // Get pressure for Apple Pencil
                            var pressure = touch.Type == UITouchType.Stylus ? (float)touch.Force : 1.0f;

                            // Primary button (Left) uses Released event type for backward compatibility
                            // Secondary buttons use Pointer event type
                            var eventType = buttonInfo.button == MouseButton.Left
                                ? (isInside ? TouchActionType.Released : TouchActionType.Exited)
                                : TouchActionType.Pointer;

                            var buttonState = MouseButtonState.Released;

                            if (eventType != TouchActionType.Exited)
                            {
                                _parent.FireEventWithMouse(id, eventType, point, buttonInfo.button,
                                    buttonInfo.buttonNumber, buttonState, pressedButtons, deviceType, pressure);
                            }
                            else
                            {
                                _parent.FireEvent(id, TouchActionType.Exited, touch);
                            }

                            // Clean up stored button info for this touch
                            _touchButtonMap.Remove(id);
                        }
                    }
                    else
                    {
                        // Regular touch event
                        if (isInside)
                            _parent.FireEvent(id, TouchActionType.Released, touch);
                        else
                            _parent.FireEvent(id, TouchActionType.Exited, touch);
                    }
                }
            }
            else
            {
                _parent.FireEvent(0, TouchActionType.Released, PointF.Empty);
            }

            UnlockTouch();

            // Reset parent recognizer reference for next gesture
            _parentScrollViewRecognizer = null;
        }

        public override void TouchesCancelled(NSSet touches, UIEvent evt)
        {
            base.TouchesCancelled(touches, evt);

            foreach (UITouch touch in touches.Cast<UITouch>())
            {
                long id = ((IntPtr)touch.Handle).ToInt64();
                _parent.FireEvent(id, TouchActionType.Cancelled, touch);

                // Clean up stored button info for this touch
                _touchButtonMap.Remove(id);
            }

            UnlockTouch();

            // Reset parent recognizer reference for next gesture
            _parentScrollViewRecognizer = null;
        }

        bool CheckPointIsInsideRecognizer(PointF xfPoint, TouchRecognizer recognizer)
        {
            if (xfPoint.Y < 0 || xfPoint.Y > recognizer.View.Bounds.Height)
            {
                return false;
            }

            if (xfPoint.X < 0 || xfPoint.X > recognizer.View.Bounds.Width)
            {
                return false;
            }

            return true;
        }


        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _disposed = true;
        }
    }
}
