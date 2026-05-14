namespace AppoMobi.Gestures;

/// <summary>
/// Flags enumeration for tracking multiple pressed mouse buttons simultaneously
/// </summary>
[Flags]
public enum MouseButtons
{
    None = 0,
    Left = 1,
    Right = 2,
    Middle = 4,
    XButton1 = 8,
    XButton2 = 16,
    XButton3 = 32,
    XButton4 = 64,
    XButton5 = 128,
    XButton6 = 256,
    XButton7 = 512,
    XButton8 = 1024,
    XButton9 = 2048
}
