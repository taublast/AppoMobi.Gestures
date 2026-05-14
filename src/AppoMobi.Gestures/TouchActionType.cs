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
        Pointer  // Mouse/pen pointer movement without press (desktop platforms)
    }
}
