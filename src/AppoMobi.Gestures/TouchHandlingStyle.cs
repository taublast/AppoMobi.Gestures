namespace AppoMobi.Gestures;

public enum TouchHandlingStyle
{
    Default,

    /// <summary>
    /// Locks totally input for self, useful inside scroll view, panning controls like slider etc
    /// </summary>
    Lock,

    /// <summary>
    /// You control how it works with parent controls by setting the effect WillLock property to Locked/Unlocked at runtime.
    /// This allows working simultaneously inside a ScrollView; set WillLock to locked when consuming panning, unlock when panning is wrong direction.
    /// </summary>
    Manual,

    /// <summary>
    /// Smart lock for working inside native controls like scroll view to share gestures with it if panning not consumed by your controls.
    /// </summary>
    SoftLock,

    /// <summary>
    /// Same as InputTransparent=true
    /// </summary>
    Disabled
}
