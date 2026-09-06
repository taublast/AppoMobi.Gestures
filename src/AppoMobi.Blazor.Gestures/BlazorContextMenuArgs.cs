namespace AppoMobi.Gestures;

/// <summary>
/// DTO matching the object sent by canvasGestures.js OnCanvasContextMenu call (a browser contextmenu event).
/// </summary>
public class BlazorContextMenuArgs
{
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    /// <summary>"mouse" (right click), "touch" / "pen" (long press) or "" (keyboard Menu key).</summary>
    public string PointerType { get; set; } = "mouse";
}
