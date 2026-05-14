namespace AppoMobi.Gestures;

public enum TouchActionResult
{
    Touch,
    Down,
    Up,
    Tapped,
    LongPressing,
    Panning,
    Wheel,

    /// <summary>
    /// Mouse/pen pointer movement without press
    /// </summary>
    Pointer
}
