using AppoMobi.Gestures;

namespace AppleTouchTests;

/// Stands in for the way DrawnUi's canvas is used: a view that carries the touch effect and has
/// native views living inside its platform view. It records what the library reports, so a fix for
/// the hosted views can be shown not to cost us our own events.
public class TouchHost : ContentView, IGestureListener
{
    public string Name { get; }
    public TouchHandlingStyle Mode { get; }

    public int Down, Up, Tapped, Panning, Other;

    public TouchHost(string name, TouchHandlingStyle mode)
    {
        Name = name;
        Mode = mode;
        TouchEffect.SetShareTouch(this, mode);
        TouchEffect.SetForceAttach(this, true);
    }

    public void OnGestureEvent(TouchActionType type, TouchActionEventArgs args, TouchActionResult action)
    {
        switch (action)
        {
            case TouchActionResult.Down: Down++; break;
            case TouchActionResult.Up: Up++; break;
            case TouchActionResult.Tapped: Tapped++; break;
            case TouchActionResult.Panning: Panning++; break;
            default: Other++; break;
        }
    }

    public new bool InputTransparent => false;

    public string Counters => $"down={Down} up={Up} tapped={Tapped} panning={Panning}";
}
