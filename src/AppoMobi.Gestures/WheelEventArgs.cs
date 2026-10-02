namespace AppoMobi.Gestures;

public class WheelEventArgs : EventArgs
{
    /// <summary>
    /// How far the wheel or touchpad scrolled, in the platform's units. Positive scrolls toward the start of the
    /// content: up for a vertical wheel turned away from the user, left for a horizontal one.
    /// </summary>
    public float Delta { get; set; }

    /// <summary>
    /// Zoom factor accumulated from vertical wheel turns; horizontal ones leave it unchanged.
    /// </summary>
    public float Scale { get; set; }

    /// <summary>
    /// The scroll is along the X axis: a tilting wheel, or the sideways part of a two-finger touchpad swipe, which
    /// Windows reports as separate horizontal wheel events. A vertical list should ignore it.
    /// </summary>
    public bool IsHorizontal { get; set; }

    /// <summary>
    /// Pixels inside parent view
    /// </summary>
    public PointF Center { get; set; }
}
