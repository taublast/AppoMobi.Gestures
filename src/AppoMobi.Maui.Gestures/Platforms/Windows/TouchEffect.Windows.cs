using System.ComponentModel;
using Microsoft.Maui.Controls.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Input;
using PointerDeviceType = AppoMobi.Gestures.PointerDeviceType;

namespace AppoMobi.Gestures
{
    public partial class PlatformTouchEffect : PlatformEffect
    {
        FrameworkElement frameworkElement;


        protected override void OnElementPropertyChanged(PropertyChangedEventArgs args)
        {
            base.OnElementPropertyChanged(args);

            if (args.PropertyName == "Window" && Element is View view)
            {
                //if (view.Window == null)
                //    OnDetached();
            }
        }
        protected override void OnAttached()
        {
            // Get the Windows FrameworkElement corresponding to the Element that the effect is attached to
            frameworkElement = Control == null ? Container : Control;

            // Get access to the TouchEffect class in the .NET Standard library
            _touchEffect = Element.Effects.OfType<TouchEffect>().FirstOrDefault();

            if (_touchEffect != null && frameworkElement != null)
            {
                // Save the method to call on touch events

                // Set event handlers on FrameworkElement
                frameworkElement.PointerPressed += OnPointerPressed;
                frameworkElement.PointerMoved += OnPointerMoved;
                frameworkElement.PointerReleased += OnPointerReleased;
                frameworkElement.PointerExited += OnPointerExited;
                frameworkElement.PointerWheelChanged += OnWheelChanged;
                frameworkElement.PointerEntered += OnPointerEntered;
            }
        }

        protected override void OnDetached()
        {
            if (frameworkElement != null)
            {
                frameworkElement.PointerPressed -= OnPointerPressed;
                frameworkElement.PointerMoved -= OnPointerMoved;
                frameworkElement.PointerReleased -= OnPointerReleased;
                frameworkElement.PointerExited -= OnPointerExited;
                frameworkElement.PointerWheelChanged -= OnWheelChanged;
                frameworkElement.PointerEntered -= OnPointerEntered;
            }
        }

        private bool _pressed = false;

        private volatile TouchEffect _touchEffect;

        private readonly HashSet<uint> activePointerIds = new HashSet<uint>();

        private readonly HashSet<uint> capturedPointerIds = new HashSet<uint>();

        private MouseButtons _currentPressedButtons = MouseButtons.None;

        public float ScaleLimitMin { get; set; } = 0.1f;

        public float ScaleLimitMax { get; set; } = 1000.0f;

        public float ScaleFactor { get; set; } = 1.0f;

        public float WheelDelta { get; set; } = 40 / 0.1f;

        private void OnWheelChanged(object sender, PointerRoutedEventArgs args)
        {
            //_pressed = true;
            var id = args.Pointer.PointerId;

            var pointerPoint = args.GetCurrentPoint(frameworkElement);
            var windowsPoint = pointerPoint.Position;
            //var mouse = GetMouseButton(pointerPoint);
            //var device = GetTouchDevice(evt);
            var wheelDelta = pointerPoint?.Properties?.MouseWheelDelta ?? 0;

            float scaleFactorAdjustment = wheelDelta > 0 ? 1.05f : 0.95f;
            ScaleFactor = Math.Max(ScaleLimitMin, Math.Min(ScaleFactor * scaleFactorAdjustment, ScaleLimitMax));

            activePointerIds.Add(args.Pointer.PointerId);
            Wheel = new WheelEventArgs()
            {
                Delta = wheelDelta / WheelDelta,
                Scale = (float)ScaleFactor,
                Center = new PointF((float)windowsPoint.X * TouchEffect.Density, (float)windowsPoint.Y * TouchEffect.Density)
            };

            // Always fire the event to YOUR TouchEffect first
            FireEvent(sender, TouchActionType.Wheel, args);

            // Then decide whether to block it from parent ScrollView
            // For Manual mode: block wheel events when WIllLock is Locked (consumer has control)
            if (_touchEffect.TouchMode == TouchHandlingStyle.Manual)
            {
                if (_touchEffect.WIllLock == ShareLockState.Locked)
                {
                    // Consumer has control - block wheel from reaching parent ScrollView
                    args.Handled = true;
                    System.Diagnostics.Debug.WriteLine("Windows: Wheel event delivered to effect but BLOCKED from parent - Manual mode locked");
                }
            }
            // For Lock mode: always block wheel events from parent
            else if (_touchEffect.TouchMode == TouchHandlingStyle.Lock)
            {
                args.Handled = true;
                System.Diagnostics.Debug.WriteLine("Windows: Wheel event delivered to effect but BLOCKED from parent - Lock mode");
            }
        }

        void OnPointerPressed(object sender, PointerRoutedEventArgs args)
        {
            _pressed = true;
            activePointerIds.Add(args.Pointer.PointerId);

            // Handle pointer capture based on touch mode
            if (_touchEffect.TouchMode == TouchHandlingStyle.Lock)
            {
                // Lock mode: always capture pointer to block parent
                if (frameworkElement.CapturePointer(args.Pointer))
                {
                    capturedPointerIds.Add(args.Pointer.PointerId);
                }
                frameworkElement.ManipulationMode = ManipulationModes.None;
            }
            else if (_touchEffect.TouchMode == TouchHandlingStyle.Manual)
            {
                // Manual mode: start without capture, will be controlled dynamically in Move
                frameworkElement.ManipulationMode = ManipulationModes.System;
            }
            else
            {
                // Default mode: capture pointer with system manipulation
                if (frameworkElement.CapturePointer(args.Pointer))
                {
                    capturedPointerIds.Add(args.Pointer.PointerId);
                }
                frameworkElement.ManipulationMode = ManipulationModes.System;
            }

            // Check if this is a mouse/pen event and handle button tracking
            var deviceType = GetPointerDeviceType(args.Pointer);
            if (deviceType == PointerDeviceType.Mouse || deviceType == PointerDeviceType.Pen)
            {
                var buttonInfo = GetPressedButtonInfo(args);
                if (buttonInfo.HasValue)
                {
                    _currentPressedButtons |= GetMouseButtonFlag(buttonInfo.Value.button);

                    // IMPORTANT: Only use Pressed for primary button (Left/Button 1)
                    // All other buttons use Pointer to avoid breaking existing touch logic
                    if (buttonInfo.Value.button == MouseButton.Left)
                    {
                        // Primary button - use normal touch flow with mouse data
                        FireEventWithMouse(sender, TouchActionType.Pressed, args, buttonInfo.Value.button,
                            buttonInfo.Value.buttonNumber, MouseButtonState.Pressed, deviceType);
                    }
                    else
                    {
                        // Secondary buttons (Right, Middle, XButton1, etc.) - use Pointer
                        //System.Diagnostics.Debug.WriteLine($"[Windows] Right/Secondary button detected: {buttonInfo.Value.button} (#{buttonInfo.Value.buttonNumber})");
                        FireEventWithMouse(sender, TouchActionType.Pointer, args, buttonInfo.Value.button,
                            buttonInfo.Value.buttonNumber, MouseButtonState.Pressed, deviceType);
                    }
                    return;
                }
            }

            // Regular touch event
            FireEvent(sender, TouchActionType.Pressed, args);
        }

        void OnPointerMoved(object sender, PointerRoutedEventArgs args)
        {
            if (_pressed)
            {
                // Check if this is a mouse/pen event for drag operations
                var deviceType = GetPointerDeviceType(args.Pointer);
                if (deviceType == PointerDeviceType.Mouse || deviceType == PointerDeviceType.Pen)
                {
                    FireEventWithMouseMove(sender, TouchActionType.Moved, args, deviceType);
                }
                else
                {
                    // Regular touch event
                    FireEvent(sender, TouchActionType.Moved, args);
                }

                // For Manual mode: dynamically control pointer capture and manipulation based on WIllLock state
                if (_touchEffect.TouchMode == TouchHandlingStyle.Manual)
                {
                    if (_touchEffect.WIllLock == ShareLockState.Locked)
                    {
                        // Consumer wants control - capture pointer and disable manipulation to block parent
                        if (!capturedPointerIds.Contains(args.Pointer.PointerId))
                        {
                            if (frameworkElement.CapturePointer(args.Pointer))
                            {
                                capturedPointerIds.Add(args.Pointer.PointerId);
                                frameworkElement.ManipulationMode = ManipulationModes.None;
                                System.Diagnostics.Debug.WriteLine("Windows: Pointer CAPTURED + ManipulationMode.None - taking control");
                            }
                        }
                    }
                    else if (_touchEffect.WIllLock == ShareLockState.Unlocked)
                    {
                        // Consumer doesn't want control - release pointer and enable system manipulation to allow parent
                        if (capturedPointerIds.Contains(args.Pointer.PointerId))
                        {
                            frameworkElement.ReleasePointerCapture(args.Pointer);
                            capturedPointerIds.Remove(args.Pointer.PointerId);
                            frameworkElement.ManipulationMode = ManipulationModes.System;
                            System.Diagnostics.Debug.WriteLine("Windows: Pointer RELEASED + ManipulationMode.System - releasing to parent");
                        }
                    }
                    // For Initial state, do nothing (keep previous state)
                }
            }
            else
            {
                // Pointer moved without being pressed - desktop pointer/hover tracking
                var deviceType = GetPointerDeviceType(args.Pointer);
                if (deviceType == PointerDeviceType.Mouse || deviceType == PointerDeviceType.Pen)
                {
                    FireEventPointerWithMouse(sender, args, deviceType);
                }
                else
                {
                    FireEventPointer(sender, args);
                }
            }
        }

        void OnPointerReleased(object sender, PointerRoutedEventArgs args)
        {
            try
            {
                activePointerIds.Remove(args.Pointer.PointerId);

                // Check if this is a mouse/pen event and handle button tracking
                var deviceType = GetPointerDeviceType(args.Pointer);
                if (deviceType == PointerDeviceType.Mouse || deviceType == PointerDeviceType.Pen)
                {
                    var buttonInfo = GetReleasedButtonInfo(args);
                    if (buttonInfo.HasValue)
                    {
                        _currentPressedButtons &= ~GetMouseButtonFlag(buttonInfo.Value.button);

                        // IMPORTANT: Only use Released for primary button (Left/Button 1)
                        // All other buttons use Pointer to avoid breaking existing touch logic
                        if (buttonInfo.Value.button == MouseButton.Left)
                        {
                            // Primary button - use normal touch flow with mouse data
                            FireEventWithMouse(sender, TouchActionType.Released, args, buttonInfo.Value.button,
                                buttonInfo.Value.buttonNumber, MouseButtonState.Released, deviceType);
                        }
                        else
                        {
                            // Secondary buttons (Right, Middle, XButton1, etc.) - use Pointer
                            //System.Diagnostics.Debug.WriteLine($"[Windows] Right/Secondary button RELEASED: {buttonInfo.Value.button} (#{buttonInfo.Value.buttonNumber})");
                            FireEventWithMouse(sender, TouchActionType.Pointer, args, buttonInfo.Value.button,
                                buttonInfo.Value.buttonNumber, MouseButtonState.Released, deviceType);
                        }
                    }
                    else
                    {
                        // Fallback for mouse events without specific button info
                        FireEvent(sender, TouchActionType.Released, args);
                    }
                }
                else
                {
                    // Regular touch event
                    FireEvent(sender, TouchActionType.Released, args);
                }

                _pressed = activePointerIds.Count > 0;

                // Release pointer capture for Manual/Lock modes when all fingers are released
                if (!_pressed)
                {
                    if (capturedPointerIds.Contains(args.Pointer.PointerId))
                    {
                        frameworkElement.ReleasePointerCapture(args.Pointer);
                        capturedPointerIds.Remove(args.Pointer.PointerId);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        void OnPointerExited(object sender, PointerRoutedEventArgs args)
        {
            try
            {
                if (_pressed)
                {
                    activePointerIds.Remove(args.Pointer.PointerId);
                    _pressed = activePointerIds.Count > 0;
                    FireEvent(sender, TouchActionType.Exited, args);

                    // Release pointer capture when all fingers are gone
                    if (!_pressed)
                    {
                        if (capturedPointerIds.Contains(args.Pointer.PointerId))
                        {
                            frameworkElement.ReleasePointerCapture(args.Pointer);
                            capturedPointerIds.Remove(args.Pointer.PointerId);
                        }
                        _currentPressedButtons = 0;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        void OnPointerEntered(object sender, PointerRoutedEventArgs args)
        {
            try
            {
                // Only fire pointer events when not pressed (hover/pointer tracking)
                // This provides mouse/pen pointer movement without press for desktop platforms
                if (!_pressed)
                {
                    FireEventPointer(sender, args);
                }
                else
                {
                    // If already pressed, treat as normal touch event
                    FireEvent(sender, TouchActionType.Entered, args);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }


        void FireEvent(object sender, TouchActionType touchActionType, PointerRoutedEventArgs pointer)
        {
            try
            {

                var pointerPoint = pointer.GetCurrentPoint(sender as UIElement);

                var windowsPoint = pointerPoint.Position;

                var args = new TouchActionEventArgs(
                    pointer.Pointer.PointerId,
                    touchActionType,
                    new PointF((float)(windowsPoint.X * TouchEffect.Density), (float)(windowsPoint.Y * TouchEffect.Density)), null, TouchEffect.Density);

                args.IsInsideView = _pressed;

                args.Wheel = Wheel;

                if (pointer.Pointer.IsInContact)
                {
                    args.NumberOfTouches = activePointerIds.Count;
                }
                else
                {
                    args.NumberOfTouches = 1; //last finger released, and it was 1
                }

                //Trace.WriteLine($"TouchEffect: {touchActionType} {args.Location.X}x{args.Location.Y} {args.NumberOfTouches}");

                _touchEffect?.OnTouchAction(args);
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        void FireEventPointer(object sender, PointerRoutedEventArgs pointer)
        {
            try
            {
                var pointerPoint = pointer.GetCurrentPoint(sender as UIElement);
                var windowsPoint = pointerPoint.Position;

                var args = new TouchActionEventArgs(
                    pointer.Pointer.PointerId,
                    TouchActionType.Pointer,
                    new PointF((float)(windowsPoint.X * TouchEffect.Density), (float)(windowsPoint.Y * TouchEffect.Density)), null, TouchEffect.Density);

                args.IsInsideView = true; // Always inside when hovering
                args.NumberOfTouches = 0; // No touches, just pointer

                // Send pointer event to TouchEffect for processing
                // TouchEffect will handle it and set LastActionResult = TouchActionResult.Pointer
                _touchEffect?.OnTouchAction(args);

                // Note: Pointer events (hover/mouse movement without press) don't need to respect
                // touch handling modes like Lock/Manual since they don't interfere with parent scrolling
                // They are purely informational for hover effects, cursor tracking, etc.
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        #region Mouse Button Detection and Event Firing

        private PointerDeviceType GetPointerDeviceType(Pointer pointer)
        {
            return pointer.PointerDeviceType switch
            {
                Microsoft.UI.Input.PointerDeviceType.Mouse => PointerDeviceType.Mouse,
                Microsoft.UI.Input.PointerDeviceType.Pen => PointerDeviceType.Pen,
                Microsoft.UI.Input.PointerDeviceType.Touch => PointerDeviceType.Touch,
                _ => PointerDeviceType.Touch
            };
        }

        private (MouseButton button, int buttonNumber)? GetPressedButtonInfo(PointerRoutedEventArgs args)
        {
            var pointerPoint = args.GetCurrentPoint(frameworkElement);
            var properties = pointerPoint.Properties;

            // Check which button was just pressed by comparing with current state
            if (properties.IsLeftButtonPressed && !_currentPressedButtons.HasFlag(MouseButtons.Left))
                return (MouseButton.Left, 1);
            if (properties.IsRightButtonPressed && !_currentPressedButtons.HasFlag(MouseButtons.Right))
                return (MouseButton.Right, 2);
            if (properties.IsMiddleButtonPressed && !_currentPressedButtons.HasFlag(MouseButtons.Middle))
                return (MouseButton.Middle, 3);
            if (properties.IsXButton1Pressed && !_currentPressedButtons.HasFlag(MouseButtons.XButton1))
                return (MouseButton.XButton1, 4);
            if (properties.IsXButton2Pressed && !_currentPressedButtons.HasFlag(MouseButtons.XButton2))
                return (MouseButton.XButton2, 5);

            // For buttons 6+, we need to detect them differently
            // Windows PointerRoutedEventArgs doesn't expose buttons beyond 5
            // We could use raw input or window messages for this, but for now
            // we'll create a generic "Extended" button entry

            // Check if this is a mouse device and we have a press but no known button
            if (args.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse)
            {
                // This is a mouse press but we couldn't identify the specific button
                // Likely button 6+ - create an Extended button entry
                return (MouseButton.Extended, 6); // Assume button 6 for now
            }

            return null;
        }

        private (MouseButton button, int buttonNumber)? GetReleasedButtonInfo(PointerRoutedEventArgs args)
        {
            var pointerPoint = args.GetCurrentPoint(frameworkElement);
            var properties = pointerPoint.Properties;

            // Check which button was just released by comparing with current state
            if (!properties.IsLeftButtonPressed && _currentPressedButtons.HasFlag(MouseButtons.Left))
                return (MouseButton.Left, 1);
            if (!properties.IsRightButtonPressed && _currentPressedButtons.HasFlag(MouseButtons.Right))
                return (MouseButton.Right, 2);
            if (!properties.IsMiddleButtonPressed && _currentPressedButtons.HasFlag(MouseButtons.Middle))
                return (MouseButton.Middle, 3);
            if (!properties.IsXButton1Pressed && _currentPressedButtons.HasFlag(MouseButtons.XButton1))
                return (MouseButton.XButton1, 4);
            if (!properties.IsXButton2Pressed && _currentPressedButtons.HasFlag(MouseButtons.XButton2))
                return (MouseButton.XButton2, 5);

            return null;
        }

        private MouseButtons GetMouseButtonFlag(MouseButton button)
        {
            return button switch
            {
                MouseButton.Left => MouseButtons.Left,
                MouseButton.Right => MouseButtons.Right,
                MouseButton.Middle => MouseButtons.Middle,
                MouseButton.XButton1 => MouseButtons.XButton1,
                MouseButton.XButton2 => MouseButtons.XButton2,
                MouseButton.XButton3 => MouseButtons.XButton3,
                MouseButton.XButton4 => MouseButtons.XButton4,
                MouseButton.XButton5 => MouseButtons.XButton5,
                MouseButton.XButton6 => MouseButtons.XButton6,
                MouseButton.XButton7 => MouseButtons.XButton7,
                MouseButton.XButton8 => MouseButtons.XButton8,
                MouseButton.XButton9 => MouseButtons.XButton9,
                _ => MouseButtons.None
            };
        }

        private MouseButtons GetCurrentPressedButtons(PointerRoutedEventArgs args)
        {
            var pointerPoint = args.GetCurrentPoint(frameworkElement);
            var properties = pointerPoint.Properties;
            var buttons = MouseButtons.None;

            if (properties.IsLeftButtonPressed) buttons |= MouseButtons.Left;
            if (properties.IsRightButtonPressed) buttons |= MouseButtons.Right;
            if (properties.IsMiddleButtonPressed) buttons |= MouseButtons.Middle;
            if (properties.IsXButton1Pressed) buttons |= MouseButtons.XButton1;
            if (properties.IsXButton2Pressed) buttons |= MouseButtons.XButton2;

            return buttons;
        }

        private float GetPressure(PointerRoutedEventArgs args, PointerDeviceType deviceType)
        {
            if (deviceType == PointerDeviceType.Pen)
            {
                var pointerPoint = args.GetCurrentPoint(frameworkElement);
                return pointerPoint.Properties.Pressure;
            }
            return 1.0f; // Mouse always has full pressure
        }

        private void FireEventWithMouse(object sender, TouchActionType touchActionType, PointerRoutedEventArgs pointer,
            MouseButton button, int buttonNumber, MouseButtonState buttonState, PointerDeviceType deviceType)
        {
            try
            {
                var pointerPoint = pointer.GetCurrentPoint(sender as UIElement);
                var windowsPoint = pointerPoint.Position;

                var args = new TouchActionEventArgs(
                    pointer.Pointer.PointerId,
                    touchActionType,
                    new PointF((float)(windowsPoint.X * TouchEffect.Density), (float)(windowsPoint.Y * TouchEffect.Density)), null, TouchEffect.Density);

                args.IsInsideView = _pressed;
                args.Wheel = Wheel;

                if (pointer.Pointer.IsInContact)
                {
                    args.NumberOfTouches = activePointerIds.Count;
                }
                else
                {
                    args.NumberOfTouches = 1; // Last finger released, and it was 1
                }

                // Set mouse-specific data
                args.Pointer = new PointerData
                {
                    Button = button,
                    ButtonNumber = buttonNumber,
                    State = buttonState,
                    PressedButtons = _currentPressedButtons,
                    DeviceType = deviceType,
                    Pressure = GetPressure(pointer, deviceType)
                };

                _touchEffect?.OnTouchAction(args);
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private void FireEventWithMouseMove(object sender, TouchActionType touchActionType, PointerRoutedEventArgs pointer,
            PointerDeviceType deviceType)
        {
            try
            {
                var pointerPoint = pointer.GetCurrentPoint(sender as UIElement);
                var windowsPoint = pointerPoint.Position;

                var args = new TouchActionEventArgs(
                    pointer.Pointer.PointerId,
                    touchActionType,
                    new PointF((float)(windowsPoint.X * TouchEffect.Density), (float)(windowsPoint.Y * TouchEffect.Density)), null, TouchEffect.Density);

                args.IsInsideView = _pressed;
                args.Wheel = Wheel;
                args.NumberOfTouches = activePointerIds.Count;

                // Update current pressed buttons state
                _currentPressedButtons = GetCurrentPressedButtons(pointer);

                // Set mouse-specific data for move events
                args.Pointer = new PointerData
                {
                    Button = MouseButton.Left, // Not relevant for move events
                    ButtonNumber = 0, // Not relevant for move events
                    State = MouseButtonState.Pressed, // Not relevant for move events
                    PressedButtons = _currentPressedButtons,
                    DeviceType = deviceType,
                    Pressure = GetPressure(pointer, deviceType)
                };

                _touchEffect?.OnTouchAction(args);
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private void FireEventPointerWithMouse(object sender, PointerRoutedEventArgs pointer, PointerDeviceType deviceType)
        {
            try
            {
                var pointerPoint = pointer.GetCurrentPoint(sender as UIElement);
                var windowsPoint = pointerPoint.Position;

                var args = new TouchActionEventArgs(
                    pointer.Pointer.PointerId,
                    TouchActionType.Pointer,
                    new PointF((float)(windowsPoint.X * TouchEffect.Density), (float)(windowsPoint.Y * TouchEffect.Density)), null, TouchEffect.Density);

                args.IsInsideView = true; // Always inside when hovering
                args.NumberOfTouches = 0; // No touches, just pointer

                // Set mouse-specific data for pointer events (hover)
                args.Pointer = new PointerData
                {
                    Button = MouseButton.Left, // Not relevant for pointer events
                    ButtonNumber = 0, // Not relevant for pointer events
                    State = MouseButtonState.Released, // Not relevant for pointer events
                    PressedButtons = MouseButtons.None, // No buttons pressed during hover
                    DeviceType = deviceType,
                    Pressure = GetPressure(pointer, deviceType)
                };

                _touchEffect?.OnTouchAction(args);
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        #endregion
    }
}
