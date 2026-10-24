#if IOS || MACCATALYST
using System.Globalization;
using AppoMobi.Gestures;
using UIKit;

namespace AppleTouchTests;

/// What the recognizer looks like once the effect has attached, read off the live view rather than
/// through the library's internals.
public record RecognizerInfo(
    bool Found, bool DelaysTouchesEnded, bool CancelsTouchesInView,
    bool PanFound, bool PanDelaysTouchesEnded, bool PanCancelsTouchesInView)
{
    public override string ToString() =>
        Found
            ? $"delaysEnded={DelaysTouchesEnded} cancels={CancelsTouchesInView}" +
              (PanFound ? $" pan(delaysEnded={PanDelaysTouchesEnded} cancels={PanCancelsTouchesInView})" : " pan(absent)")
            : "recognizer not found";
}

public static class Platform
{
    static UIView? Native(VisualElement v) => v.Handler?.PlatformView as UIView;

    /// The view's rectangle in device points, which is the space the harness taps in.
    public static string RectOf(VisualElement v)
    {
        var native = Native(v);
        if (native is null) return "0,0,0,0";
        var r = native.ConvertRectToView(native.Bounds, null);
        // Invariant: the harness parses these, and a locale with a decimal comma would make
        // "0,59,320,70" impossible to split.
        return string.Create(CultureInfo.InvariantCulture, $"{r.X:0.##},{r.Y:0.##},{r.Width:0.##},{r.Height:0.##}");
    }

    public static RecognizerInfo Inspect(VisualElement host)
    {
        var native = Native(host);
        var recognizers = native?.GestureRecognizers;
        if (recognizers is null)
            return new RecognizerInfo(false, false, false, false, false, false);

        UIGestureRecognizer? touch = null;
        UIPanGestureRecognizer? pan = null;
        foreach (var g in recognizers)
        {
            var name = g.GetType().Name;
            if (name == "TouchRecognizer") touch = g;
            else if (g is UIPanGestureRecognizer p) pan = p;
        }

        if (touch is null)
            return new RecognizerInfo(false, false, false, false, false, false);

        return new RecognizerInfo(
            true, touch.DelaysTouchesEnded, touch.CancelsTouchesInView,
            pan is not null, pan?.DelaysTouchesEnded ?? false, pan?.CancelsTouchesInView ?? false);
    }

    /// The modes whose recognizer also installs the child pan recognizer.
    public static bool ExpectsPan(TouchHandlingStyle mode)
        => mode is TouchHandlingStyle.Lock or TouchHandlingStyle.Manual;
}
#endif
