// Adapted from https://github.com/Mapsui/Mapsui

namespace AppoMobi.Gestures;

public class MultitouchTracker
{
    private readonly Dictionary<long, PointF> _positions = new();

    private double _totalScaleChange;
    private double _totalRotationChange;
    private TouchState? _touchState;
    private TouchState? _previousTouchState;

    private TouchState? GetTouchState()
    {
        var positions = _positions.Values.ToArray();

        if (positions.Length == 0)
            return null;

        if (positions.Length == 1)
            return new TouchState(positions[0], null, null, positions.Length);

        var (centerX, centerY) = GetCenter(positions);
        var radius = Distance(centerX, centerY, positions[0].X, positions[0].Y);
        var angle = Math.Atan2(positions[1].Y - positions[0].Y, positions[1].X - positions[0].X) * 180.0 / Math.PI;

        return new TouchState(new PointF((float)centerX, (float)centerY), radius, angle, positions.Length);
    }

    private static double Distance(double x1, double y1, double x2, double y2)
        => Math.Sqrt(Math.Pow(x1 - x2, 2.0) + Math.Pow(y1 - y2, 2.0));

    private static (double centerX, double centerY) GetCenter(PointF[] touches)
    {
        double centerX = 0;
        double centerY = 0;

        foreach (var location in touches)
        {
            centerX += location.X;
            centerY += location.Y;
        }

        centerX /= touches.Length;
        centerY /= touches.Length;

        return (centerX, centerY);
    }

    /// <summary>Call on first Down.</summary>
    public void Restart(long id, PointF position)
    {
        Reset();
        _positions[id] = position;
        if (_positions.Count == 1)
        {
            _totalRotationChange = 0;
            _totalScaleChange = 0;
            _touchState = GetTouchState();
            _previousTouchState = null;
        }
    }

    /// <summary>Call on last Up.</summary>
    public void Reset()
    {
        _positions.Clear();
    }

    /// <summary>Call during Panning. Returns manipulation info or null for single-touch.</summary>
    public TouchActionEventArgs.ManipulationInfo? AddMovement(long id, PointF position)
    {
        _positions[id] = position;
        return Calculate();
    }

    public void RemoveTouch(long id)
    {
        _positions.Remove(id);
    }

    public TouchActionEventArgs.ManipulationInfo? Calculate()
    {
        var touchState = GetTouchState();

        _previousTouchState = _touchState;
        _touchState = touchState;

        if (_positions.Count == 1)
            return null;

        if (!(touchState?.LocationsLength == _previousTouchState?.LocationsLength))
        {
            _totalRotationChange = 0;
            _totalScaleChange = 0;
            _previousTouchState = null;
            return null;
        }

        if (touchState is null || _touchState is null || _previousTouchState is null)
            return null;

        var scaleChange = _touchState.GetScaleChange(_previousTouchState);
        var rotationChange = _touchState.GetRotationChange(_previousTouchState);

        if (touchState is not null && _previousTouchState is not null)
        {
            _totalRotationChange += rotationChange;
            _totalScaleChange += scaleChange;
        }

        if (_touchState.Equals(_previousTouchState))
            return null;

        return new TouchActionEventArgs.ManipulationInfo(_touchState!.Center, _previousTouchState!.Center, scaleChange, rotationChange, _totalScaleChange, _totalRotationChange, _positions.Count);
    }

    private record TouchState(PointF Center, double? Radius, double? Angle, int LocationsLength)
    {
        public double GetRotationChange(TouchState previousTouchState)
        {
            if (Angle is null || previousTouchState.Angle is null)
                return 0;
            return Angle.Value - previousTouchState.Angle.Value;
        }

        public double GetScaleChange(TouchState previousTouchState)
        {
            if (Radius is null || previousTouchState.Radius is null)
                return 0;
            var ratio = Radius.Value / previousTouchState.Radius.Value;
            return ratio == 1 ? 0 : ratio - 1;
        }
    }
}
