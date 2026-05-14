using AppoMobi.Gestures;
using AppoMobi.Maui.Gestures;

namespace MauiSample;

// Gesture-aware view: implement IGestureListener directly on a MAUI view.
public class GesturePad : Border, IGestureListener
{
    public event Action<TouchActionResult, TouchActionEventArgs>? GestureOccurred;

    public void OnGestureEvent(TouchActionType type, TouchActionEventArgs args, TouchActionResult action)
    {
        GestureOccurred?.Invoke(action, args);
    }

    public new bool InputTransparent => false;
}

public partial class MainPage : ContentPage
{
    private int _tapCount;
    private const int MaxLogLines = 30;

    public MainPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        Pad.GestureOccurred += OnGesture;

        // Also subscribe to raw events for position tracking
        var effect = TouchEffect.GetFrom(Pad);
        if (effect != null)
            effect.TouchAction += OnTouchAction;
    }

    private void OnTouchAction(object? sender, TouchActionEventArgs args)
    {
        TouchCountLabel.Text = args.NumberOfTouches.ToString();

        bool show = args.Type is TouchActionType.Pressed or TouchActionType.Moved;
        if (show)
        {
            var density = TouchEffect.Density;
            TouchDot.IsVisible = true;
            TouchDot.TranslationX = args.Location.X / density - 20;
            TouchDot.TranslationY = args.Location.Y / density - 20;
        }
        else if (args.Type is TouchActionType.Released or TouchActionType.Cancelled or TouchActionType.Exited)
        {
            TouchDot.IsVisible = false;
            TouchCountLabel.Text = "0";
        }
    }

    private void OnGesture(TouchActionResult action, TouchActionEventArgs args)
    {
        string msg = action switch
        {
            TouchActionResult.Tapped                                         => $"Tap #{++_tapCount}",
            TouchActionResult.LongPressing                                   => "Long press",
            TouchActionResult.Panning when args.Manipulation != null         => $"Pinch  scale={args.Manipulation.Scale:F2}  rot={args.Manipulation.Rotation:F1}°",
            TouchActionResult.Panning                                        => $"Pan  Δ({args.Distance?.Delta.X:F0}, {args.Distance?.Delta.Y:F0})",
            TouchActionResult.Wheel                                          => $"Wheel  Δ={args.Wheel?.Delta:F1}",
            _                                                                => $"{action}"
        };

        LastGestureLabel.Text = msg;
        if (action == TouchActionResult.Tapped)
            TapCountLabel.Text = _tapCount.ToString();

        AppendLog(msg);
    }

    private void AppendLog(string msg)
    {
        var label = new Label
        {
            Text = $"{DateTime.Now:HH:mm:ss.fff}  {msg}",
            FontSize = 11,
            FontFamily = "OpenSansRegular",
            TextColor = Colors.DimGray
        };

        LogStack.Children.Insert(0, label);

        while (LogStack.Children.Count > MaxLogLines)
            LogStack.Children.RemoveAt(LogStack.Children.Count - 1);
    }
}
