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
    Pointer,

    /// <summary>
    /// Context-menu request (right click, long press on touch, keyboard Menu key). The listener sets
    /// <see cref="TouchActionEventArgs.Handled"/> to suppress the platform's own menu; otherwise it shows.
    /// </summary>
    ContextMenu
}
