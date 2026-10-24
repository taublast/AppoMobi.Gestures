namespace AppleTouchTests;

/// A MAUI view whose platform view is a plain UIView that only records its touches. It consumes
/// nothing, so whatever it sees is what UIKit delivered.
public class ProbeControl : View
{
    public string Name { get; set; } = "probe";

    /// Calibration only: log where the touch landed, in device points.
    public bool ReportLocation { get; set; }

    public int Began, Ended, Cancelled, Moved;

    public void Reset() { Began = Ended = Cancelled = Moved = 0; }

    public string Counters => $"began={Began} ended={Ended} cancelled={Cancelled} moved={Moved}";
}
