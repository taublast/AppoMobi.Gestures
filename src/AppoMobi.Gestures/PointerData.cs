namespace AppoMobi.Gestures;

public class PointerData : EventArgs
{
    /// <summary>
    /// The specific button that triggered this event (for Press/Release events)
    /// </summary>
    public MouseButton Button { get; set; }

    /// <summary>
    /// Button number (1-based) - useful for gaming mice with many buttons
    /// 1=Left, 2=Right, 3=Middle, 4+=Extended buttons
    /// </summary>
    public int ButtonNumber { get; set; }

    /// <summary>
    /// This is a scrolling gesture; Scrolled will not have start or end of scrolling.
    /// </summary>
    public bool IsScrolling { get; set; }

    /// <summary>
    /// State of the button that triggered this event
    /// </summary>
    public MouseButtonState State { get; set; }

    /// <summary>
    /// All currently pressed buttons (flags)
    /// </summary>
    public MouseButtons PressedButtons { get; set; }

    /// <summary>
    /// Type of pointer device (Mouse, Pen, Touch)
    /// </summary>
    public PointerDeviceType DeviceType { get; set; }

    /// <summary>
    /// For pen: pressure (0.0 to 1.0), for mouse: always 1.0
    /// </summary>
    public float Pressure { get; set; } = 1.0f;
}
