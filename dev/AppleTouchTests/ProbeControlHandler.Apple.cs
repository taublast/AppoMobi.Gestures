#if IOS || MACCATALYST
using System.Globalization;
using CoreGraphics;
using Foundation;
using Microsoft.Maui.Handlers;
using UIKit;

namespace AppleTouchTests;

/// A UIView that records touches and nothing else. It does not consume them and installs no
/// recognizer, so a missing TouchesEnded here is UIKit withholding it, not this view swallowing it.
public class ProbeView : UIView
{
    public ProbeControl? Owner;

    public ProbeView()
    {
        BackgroundColor = UIColor.FromRGB(60, 120, 220);
        UserInteractionEnabled = true;
    }

    public override void TouchesBegan(NSSet touches, UIEvent? evt)
    {
        if (Owner is not null) { Owner.Began++; Log.Line($"EVENT {Owner.Name} began"); }
        base.TouchesBegan(touches, evt);
    }

    public override void TouchesMoved(NSSet touches, UIEvent? evt)
    {
        if (Owner is not null) Owner.Moved++;
        base.TouchesMoved(touches, evt);
    }

    public override void TouchesEnded(NSSet touches, UIEvent? evt)
    {
        if (Owner is not null)
        {
            Owner.Ended++;
            if (Owner.ReportLocation && touches.AnyObject is UITouch touch)
            {
                var p = touch.LocationInView(null);
                Log.Line(string.Create(CultureInfo.InvariantCulture, $"CALIB {p.X:0.##} {p.Y:0.##}"));
            }
            else
            {
                Log.Line($"EVENT {Owner.Name} ended");
            }
        }
        base.TouchesEnded(touches, evt);
    }

    public override void TouchesCancelled(NSSet touches, UIEvent? evt)
    {
        if (Owner is not null) { Owner.Cancelled++; Log.Line($"EVENT {Owner.Name} cancelled"); }
        base.TouchesCancelled(touches, evt);
    }
}

public class ProbeControlHandler : ViewHandler<ProbeControl, ProbeView>
{
    public static IPropertyMapper<ProbeControl, ProbeControlHandler> Mapper =
        new PropertyMapper<ProbeControl, ProbeControlHandler>(ViewMapper);

    public ProbeControlHandler() : base(Mapper) { }

    protected override ProbeView CreatePlatformView() => new ProbeView();

    protected override void ConnectHandler(ProbeView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Owner = VirtualView;
    }

    protected override void DisconnectHandler(ProbeView platformView)
    {
        platformView.Owner = null;
        base.DisconnectHandler(platformView);
    }
}
#endif
