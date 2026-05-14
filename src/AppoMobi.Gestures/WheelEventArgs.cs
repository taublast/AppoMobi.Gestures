namespace AppoMobi.Gestures;

public class WheelEventArgs : EventArgs
{
    public float Delta { get; set; }

    public float Scale { get; set; }

    /// <summary>
    /// Pixels inside parent view
    /// </summary>
    public PointF Center { get; set; }
}
