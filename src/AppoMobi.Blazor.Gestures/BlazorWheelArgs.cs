namespace AppoMobi.Gestures;

/// <summary>
/// DTO matching the object sent by canvasGestures.js OnCanvasWheel call.
/// </summary>
public class BlazorWheelArgs
{
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public float DeltaY { get; set; }
    public int Buttons { get; set; }
}
