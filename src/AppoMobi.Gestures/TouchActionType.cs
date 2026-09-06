namespace AppoMobi.Gestures
{
    public enum TouchActionType
    {
        Entered,
        Pressed,
        Pressing,
        Moved,
        Rotated,
        Released,
        Exited,
        Cancelled,
        PanStarted,
        PanChanged,
        PanEnded,
        Wheel,
        Pointer,  // Mouse/pen pointer movement without press (desktop platforms)

        /// <summary>
        /// A context-menu request: right click, long press on touch (browsers raise it), or the keyboard Menu key.
        /// Raised by the web implementations as a standalone event, not as part of the press/release sequence.
        /// Set <see cref="TouchActionEventArgs.Handled"/> to true to suppress the platform's own menu.
        /// </summary>
        ContextMenu
    }
}
