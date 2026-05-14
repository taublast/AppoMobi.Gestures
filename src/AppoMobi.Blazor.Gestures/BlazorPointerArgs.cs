namespace AppoMobi.Gestures;

/// <summary>
/// DTO matching the object sent by canvasGestures.js OnCanvasPointer call.
/// </summary>
public class BlazorPointerArgs
{
    public string Type { get; set; } = "";
    public long PointerId { get; set; }
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    /// <summary>Which button triggered the event (0=Left, 1=Middle, 2=Right, 3=Back, 4=Forward).</summary>
    public int Button { get; set; }
    /// <summary>Bitmask of all currently pressed buttons (1=Left, 2=Right, 4=Middle, 8=Back, 16=Forward).</summary>
    public int Buttons { get; set; }
    /// <summary>"mouse", "touch" or "pen"</summary>
    public string PointerType { get; set; } = "mouse";
    public float Pressure { get; set; }
    public bool IsInsideView { get; set; }
}
