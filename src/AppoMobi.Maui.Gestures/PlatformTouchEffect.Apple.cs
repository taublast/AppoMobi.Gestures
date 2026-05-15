using CoreGraphics;
using Microsoft.Maui.Controls.Platform;
using System.ComponentModel;
using UIKit;

namespace AppoMobi.Gestures
{
    public partial class PlatformTouchEffect : PlatformEffect
    {

        UIView _appleView;
        TouchRecognizer _touchRecognizer;

        public void FireEvent(long id, TouchActionType actionType, PointF point)
        {
            try
            {
                var args = new TouchActionEventArgs(id, actionType, point, null, TouchEffect.Density);
                args.Wheel = Wheel;
                args.NumberOfTouches = CountFingers;
                args.IsInsideView = isInsideView;

                FormsEffect?.OnTouchAction(args);

                // Track if consumer handled the event (for Cooperative mode)
                //WasHandled = args.Handled;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void FireEvent(long id, TouchActionType actionType, UITouch touch, double pinch = 0.0)
        {

            try
            {
                // ios gives us points but the funny part we want pixels
                CGPoint cgPoint = touch.LocationInView(_appleView);
                var point = new PointF((float)(cgPoint.X * TouchEffect.Density), (float)(cgPoint.Y * TouchEffect.Density));

                FireEvent(id, actionType, point);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

        }

        public void FireEventWithMouse(long id, TouchActionType actionType, PointF point,
            MouseButton button, int buttonNumber, MouseButtonState buttonState, MouseButtons pressedButtons,
            PointerDeviceType deviceType, float pressure)
        {
            try
            {
                var args = new TouchActionEventArgs(id, actionType, point, null, TouchEffect.Density);
                args.Wheel = Wheel;
                args.NumberOfTouches = CountFingers;
                args.IsInsideView = isInsideView;

                // Set pointer data with all button information
                args.Pointer = new PointerData
                {
                    Button = button,
                    ButtonNumber = buttonNumber,
                    State = buttonState,
                    PressedButtons = pressedButtons,
                    DeviceType = deviceType,
                    Pressure = pressure
                };

                FormsEffect?.OnTouchAction(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void FireEventWithMouseMove(long id, TouchActionType actionType, PointF point,
            MouseButtons pressedButtons, PointerDeviceType deviceType, float pressure)
        {
            try
            {
                var args = new TouchActionEventArgs(id, actionType, point, null, TouchEffect.Density);
                args.Wheel = Wheel;
                args.NumberOfTouches = CountFingers;
                args.IsInsideView = isInsideView;

                // Set pointer data for move events with current button state
                args.Pointer = new PointerData
                {
                    Button = MouseButton.Left, // Not used for move events
                    ButtonNumber = 0, // Not used for move events
                    State = MouseButtonState.Pressed, // Not used for move events
                    PressedButtons = pressedButtons,
                    DeviceType = deviceType,
                    Pressure = pressure
                };

                FormsEffect?.OnTouchAction(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void FireEventPointerWithMouse(long id, PointF point, PointerDeviceType deviceType)
        {
            FireEventPointerWithMouse(id, point, deviceType, 1.0f);
        }

        public void FireEventPointerWithMouse(long id, PointF point, PointerDeviceType deviceType, float pressure)
        {
            try
            {
                var args = new TouchActionEventArgs(id, TouchActionType.Pointer, point, null, TouchEffect.Density);
                args.Wheel = Wheel;
                args.NumberOfTouches = 0; // No touches, just pointer
                args.IsInsideView = true; // Always inside when hovering

                // Set mouse-specific data for pointer events (hover)
                args.Pointer = new PointerData
                {
                    Button = MouseButton.Left, // Not relevant for pointer events
                    ButtonNumber = 0, // Not relevant for pointer events
                    State = MouseButtonState.Released, // Not relevant for pointer events
                    PressedButtons = MouseButtons.None, // No buttons pressed during hover
                    DeviceType = deviceType,
                    Pressure = pressure
                };

                FormsEffect?.OnTouchAction(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void FireEventScroll(long id, PointF point, PointF scrollDelta)
        {
            try
            {
                var args = new TouchActionEventArgs(id, TouchActionType.Pointer, point, null, TouchEffect.Density);
                args.IsInsideView = true;
                args.NumberOfTouches = CountFingers;
                args.Distance.Delta = scrollDelta;
                args.Distance.Total = scrollDelta;
                args.Distance.Start = point;
                args.Distance.End = point.Add(scrollDelta);

                args.Pointer = new PointerData
                {
                    DeviceType = PointerDeviceType.Mouse,
                    IsScrolling = true
                };

                FormsEffect?.OnTouchAction(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        protected override void OnElementPropertyChanged(PropertyChangedEventArgs args)
        {
            base.OnElementPropertyChanged(args);

            if (args.PropertyName == "Window" && Element is View view)
            {
                //if (view.Window == null)
                //    OnDetached();
            }
        }

        private void OnParentVIewChanged(object sender, EventArgs e)
        {
            if (sender is Element element)
            {
                if (element.Parent == null)
                {
                    OnDetached();
                }
            }
        }

        protected override void OnAttached()
        {


            // Get the iOS UIView corresponding to the Element that the effect is attached to
            _appleView = Control == null ? Container : Control;

            // Get access to the TouchEffect class in the .NET Standard library
            FormsEffect = Element.Effects.FirstOrDefault(e => e is TouchEffect) as TouchEffect;


            if (FormsEffect != null && _appleView != null)
            {

                if (this.Element.Parent != null)
                {
                    //gonna watch parent changing, if parent becomes null we gonna dispose this effect thank you
                    this.Element.ParentChanged += OnParentVIewChanged;

                }


                // Create a TouchRecognizer for this UIView
                _touchRecognizer = new TouchRecognizer(_appleView, this);

                _touchRecognizer.Attach();

                _appleView.MultipleTouchEnabled = true;

                //panRecognizer = new PanGestureRecognizer(Element, view, touchEffect);
                //view.AddGestureRecognizer(panRecognizer);

                FormsEffect.Disposing += OnFormsDisposing;
            }
        }



        private void OnFormsDisposing(object sender, EventArgs e)
        {
            OnDetached();
        }

        protected override void OnDetached()
        {
            if (FormsEffect != null)
            {

                if (this.Element != null)
                {
                    this.Element.ParentChanged -= OnParentVIewChanged;
                }

                FormsEffect.Disposing -= OnFormsDisposing;
                FormsEffect?.Dispose();
                FormsEffect = null;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _touchRecognizer?.Detach();
                _touchRecognizer?.Dispose();
                _touchRecognizer = null;
            });
        }
    }
}
