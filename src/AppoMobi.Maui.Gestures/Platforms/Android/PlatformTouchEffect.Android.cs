using Microsoft.Maui.Controls.Platform;

namespace AppoMobi.Gestures
{

    public partial class PlatformTouchEffect : PlatformEffect
    {
        Android.Views.View _androidView;

        protected override void OnAttached()
        {
            // Get the Android View corresponding to the Element that the effect is attached to
            _androidView = Control == null ? Container : Control;

            // Get access to the TouchEffect class in the .NET Standard library
            FormsEffect = Element.Effects.FirstOrDefault(e => e is TouchEffect) as TouchEffect;

            if (FormsEffect != null && _androidView != null)
            {
                // Set event handlers on View
                var touchListener = new TouchListener(this, _androidView.Context);
                _androidView.SetOnTouchListener(touchListener);

                // Register mouse event listeners
                _androidView.SetOnHoverListener(touchListener);
                _androidView.SetOnGenericMotionListener(touchListener);

                FormsEffect.Disposing += OnFormsDisposing;

                Element.HandlerChanged += OnHandlerChanged;
            }

        }

        private void OnHandlerChanged(object sender, EventArgs e)
        {
            if (sender is Element element)
            {
                if (element.Handler == null)
                {
                    element.HandlerChanged -= OnHandlerChanged;
                    OnDetached();
                }
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
                FormsEffect.Disposing -= OnFormsDisposing;

                FormsEffect.Dispose();
                FormsEffect = null;
            }

            if (_androidView != null)
            {
                _androidView.SetOnTouchListener(null);
                _androidView.SetOnHoverListener(null);
                _androidView.SetOnGenericMotionListener(null);
                _androidView = null;
            }

        }

        void FireEvent(int id, TouchActionType actionType,
            PointF pointerLocation)
        {
            try
            {
                var args = new TouchActionEventArgs(id, actionType, pointerLocation, null, TouchEffect.Density);

                args.Wheel = Wheel;
                args.NumberOfTouches = CountFingers;
                args.IsInsideView = isInsideView;

                FormsEffect?.OnTouchAction(args);

            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        void FireEventWithMouse(int id, TouchActionType actionType, PointF pointerLocation,
            PointerDeviceType deviceType, float pressure, MouseButton button, int buttonNumber,
            MouseButtonState buttonState, MouseButtons pressedButtons)
        {
            try
            {
                var args = new TouchActionEventArgs(id, actionType, pointerLocation, null, TouchEffect.Density);
                args.Wheel = Wheel;
                args.NumberOfTouches = CountFingers;
                args.IsInsideView = isInsideView;

                // Set mouse-specific data
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
            catch (System.Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        void FireEventPointerWithMouse(int id, PointF pointerLocation, PointerDeviceType deviceType,
            float pressure, MouseButton button, int buttonNumber, MouseButtonState buttonState,
            MouseButtons pressedButtons)
        {
            try
            {
                var args = new TouchActionEventArgs(id, TouchActionType.Pointer, pointerLocation, null, TouchEffect.Density);
                args.Wheel = Wheel;
                args.NumberOfTouches = CountFingers;
                args.IsInsideView = isInsideView;

                // Set mouse-specific data
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
            catch (System.Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        void FireEventScroll(int id, PointF pointerLocation, PointF scrollDelta)
        {
            try
            {
                var args = new TouchActionEventArgs(id, TouchActionType.Pointer, pointerLocation, null, TouchEffect.Density);
                args.NumberOfTouches = CountFingers;
                args.IsInsideView = true;
                args.Distance.Delta = scrollDelta;
                args.Distance.Total = scrollDelta;
                args.Distance.Start = pointerLocation;
                args.Distance.End = pointerLocation.Add(scrollDelta);

                // Set wheel-specific pointer data
                args.Pointer = new PointerData
                {
                    DeviceType = PointerDeviceType.Mouse,
                    IsScrolling = true,
                };

                FormsEffect?.OnTouchAction(args);
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        ///// <summary>
        ///// must be called on UI thread only as long as we access View
        ///// </summary>
        ///// <param name="view"></param>
        ///// <returns></returns>
        //public bool CheckIsInsideView(View view)
        //{
        //	bool isInsideView = Location.X >= 0 && Location.Y >= 0
        //	                                    && Location.X <= view.Width * TouchEffect.Density
        //	                                    && Location.Y <= view.Height * TouchEffect.Density;
        //	return isInsideView;
        //}



    }

    //#elif WINDOWS

    ////public class PlatformTouchEffect : Microsoft.Maui.Controls.Compatibility.Platform.UWP.PlatformEffect
    ////    {
    ////        public FocusPlatformEffect() : base()
    ////        {
    ////        }

    ////        protected override void OnAttached()
    ////        {
    ////        }

    ////        protected override void OnDetached()
    ////        {
    ////        }
    ////    }

    //#elif __IOS__
    //    //public class PlatformTouchEffect : Microsoft.Maui.Controls.Compatibility.Platform.iOS.PlatformEffect
    //    //{
    //    //    // ...
    //    //}
    //#endif
}
