namespace AppoMobi.Gestures
{
    /// <summary>
    /// Everything is in pixels!!! Convert to points if needed
    /// </summary>
    public class TouchActionEventArgs : EventArgs
    {
        /// <summary>
        /// Source scale of the coordinate values carried by this event.
        /// On Blazor this is typically the effect density used when the event was created.
        /// Use <see cref="Rescale(float)"/> when your rendering surface uses a different scale.
        /// </summary>
        public float Scale { get; set; }
        public float DeltaTimeMs { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public static void FillDistanceInfo(TouchActionEventArgs current, TouchActionEventArgs? previous)
        {
            if (previous == null)
            {
                current.Distance = new DistanceInfo();
                return;
            }

            current.StartingLocation = previous.StartingLocation;
            current.IsInContact = previous.IsInContact;

            current.DeltaTimeMs = (float)(current.Timestamp - previous.Timestamp).TotalMilliseconds;

            var distance = new TouchActionEventArgs.DistanceInfo
            {
                Start = previous.Location,
                End = current.Location,
                Delta = current.Location.Subtract(previous.Location),
            };

            if (current.Type == TouchActionType.Released || current.Type == TouchActionType.Cancelled ||
                current.Type == TouchActionType.Exited)
            {
                //we don't care about delta if it's the last event because it could be anywhere
                //but velocity would be recalculated based on this event time
                distance = new TouchActionEventArgs.DistanceInfo
                {
                    Start = previous.Location,
                    End = previous.Location,
                    Delta = new(0, 0),
                };
            }

            distance.Total = previous.Distance.Total.Add(distance.Delta);
            current.Distance = distance;

            current.Distance.Velocity = GetVelocity(current, previous);
            distance.TotalVelocity = previous.Distance.TotalVelocity.Add(current.Distance.Velocity);
        }

        public static PointF GetVelocity(TouchActionEventArgs current, TouchActionEventArgs? previous)
        {
            const float VelocityEpsilon = 0.001f;
            var velocity = new PointF(0, 0);

            if (previous != null)
            {
                PointF deltaDistance;
                float deltaSeconds;

                if (current.Distance.Delta.X == 0 && current.Distance.Delta.Y == 0 && (current.Type == TouchActionType.Released
                        || current.Type == TouchActionType.Cancelled || current.Type == TouchActionType.Exited))
                {
                    var prevDeltaSecondsX = Math.Abs(previous.Distance.Velocity.X) > VelocityEpsilon
                        ? previous.Distance.Delta.X / previous.Distance.Velocity.X : 0;
                    var prevDeltaSecondsY = Math.Abs(previous.Distance.Velocity.Y) > VelocityEpsilon
                        ? previous.Distance.Delta.Y / previous.Distance.Velocity.Y : 0;

                    var prevDeltaSeconds = !float.IsNaN(prevDeltaSecondsX) && !float.IsInfinity(prevDeltaSecondsX)
                        ? prevDeltaSecondsX
                        : prevDeltaSecondsY;

                    deltaDistance = new(previous.Distance.Delta.X, previous.Distance.Delta.Y);
                    deltaSeconds = (float)((current.Timestamp - previous.Timestamp).TotalSeconds + prevDeltaSeconds);
                    if (deltaSeconds > 0)
                    {
                        velocity = new PointF(deltaDistance.X / deltaSeconds, deltaDistance.Y / deltaSeconds);
                    }
                }
                else
                {
                    deltaDistance = new(current.Distance.Delta.X, current.Distance.Delta.Y);
                    deltaSeconds = (float)((current.Timestamp - previous.Timestamp).TotalSeconds);
                    if (deltaSeconds > 0)
                    {
                        velocity = new PointF(deltaDistance.X / deltaSeconds, deltaDistance.Y / deltaSeconds);
                    }
                }

                if (float.IsNaN(velocity.X) || float.IsInfinity(velocity.X))
                    velocity.X = 0;
                if (float.IsNaN(velocity.Y) || float.IsInfinity(velocity.Y))
                    velocity.Y = 0;
            }

            return velocity;
        }

        /// <summary>
        /// Using Distance.Delta and Time of previous args
        /// </summary>
        public void CalculateVelocity(TouchActionEventArgs? previous)
        {
            var velocity = GetVelocity(this, previous);
            this.Distance.Velocity = velocity;
        }

        /// <summary>
        /// Creates a copy of this event args instance with all coordinate-based values rescaled
        /// from <see cref="Scale"/> into the consumer's rendering scale.
        /// </summary>
        /// <param name="scale">The consumer rendering scale. Invalid values are treated as 1.</param>
        /// <returns>
        /// The current instance when <paramref name="scale"/> matches <see cref="Scale"/>;
        /// otherwise a scaled copy with updated location, start position, distance, wheel center,
        /// and manipulation centers.
        /// </returns>
        public TouchActionEventArgs Rescale(float scale)
        {
            float density = this.Scale;

            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0)
                scale = 1.0f;

            if (float.IsNaN(density) || float.IsInfinity(density) || density <= 0)
                density = 1.0f;

            var dispatchScale = scale / density;

            if (dispatchScale == 1.0f)
                return this;

            var scaled = new TouchActionEventArgs(Id, Type, PointFExtensions.Multiply(dispatchScale, Location), Context, scale)
            {
                DeltaTimeMs = DeltaTimeMs,
                Timestamp = Timestamp,
                PreventDefault = PreventDefault,
                StartingLocation = PointFExtensions.Multiply(dispatchScale, StartingLocation),
                IsInContact = IsInContact,
                IsInsideView = IsInsideView,
                Handled = Handled,
                NumberOfTouches = NumberOfTouches,
                Pointer = Pointer
            };

            scaled.Distance = new DistanceInfo
            {
                Delta = PointFExtensions.Multiply(dispatchScale, Distance.Delta),
                Total = PointFExtensions.Multiply(dispatchScale, Distance.Total),
                TotalVelocity = PointFExtensions.Multiply(dispatchScale, Distance.TotalVelocity),
                Velocity = PointFExtensions.Multiply(dispatchScale, Distance.Velocity),
                Start = PointFExtensions.Multiply(dispatchScale, Distance.Start),
                End = PointFExtensions.Multiply(dispatchScale, Distance.End)
            };

            if (Wheel != null)
            {
                scaled.Wheel = new WheelEventArgs
                {
                    Delta = Wheel.Delta,
                    Scale = Wheel.Scale,
                    Center = PointFExtensions.Multiply(dispatchScale, Wheel.Center)
                };
            }

            if (Manipulation != null)
            {
                scaled.Manipulation = new ManipulationInfo(
                    PointFExtensions.Multiply(dispatchScale, Manipulation.Center),
                    PointFExtensions.Multiply(dispatchScale, Manipulation.PreviousCenter),
                    Manipulation.Scale,
                    Manipulation.Rotation,
                    Manipulation.ScaleTotal,
                    Manipulation.RotationTotal,
                    Manipulation.TouchesCount);
            }

            return scaled;
        }

        public TouchActionEventArgs(long id, TouchActionType type,
            PointF location,
            object? elementBindingContext, float scale)
        {
            Id = id;
            Type = type;
            Location = location;
            Context = elementBindingContext;
            Scale = scale;
            Distance = new DistanceInfo();
        }

        public TouchActionEventArgs(float scale)
        {
            Distance = new DistanceInfo();
            Scale = scale;
        }

        public long Id { private set; get; }

        /// <summary>
        /// This is used in some cases, ex: can set this to true inside LongPressing handler to avoid calling Tapped
        /// </summary>
        public bool PreventDefault { get; set; }

        public TouchActionType Type { private set; get; }

        /// <summary>
        /// In pixels inside parent view, 0,0 is top-left corner of the view
        /// </summary>
        public PointF Location { set; get; }

        /// <summary>
        /// In pixels inside parent view, 0,0 is top-left corner of the view
        /// </summary>
        public PointF StartingLocation { set; get; }

        /// <summary>
        /// Gesture started inside view
        /// </summary>
        public bool IsInContact { set; get; }

        /// <summary>
        /// Current hit is inside the view
        /// </summary>
        public bool IsInsideView { set; get; }

        /// <summary>
        /// Parameter to pass to commands
        /// </summary>
        public object? Context { get; set; }

        /// <summary>
        /// To do, would be used in synchronous mode, not used yet
        /// </summary>
        public bool Handled { get; set; }

        /// <summary>
        /// How many fingers we have down actually
        /// </summary>
        public int NumberOfTouches { get; set; }

        public WheelEventArgs? Wheel { get; set; }

        /// <summary>
        /// Mouse/Pointer specific data. Only set for mouse/pen events, null for touch events.
        /// Check if not null to determine if this is a mouse/pen event.
        /// </summary>
        public PointerData? Pointer { get; set; }

        /// <summary>
        /// In pixels inside parent view, 0,0 is top-left corner of the view
        /// </summary>
        public DistanceInfo Distance
        {
            get;
            set;
        }

        public ManipulationInfo? Manipulation
        {
            get;
            set;
        }

        public record ManipulationInfo(
            PointF Center,
            PointF PreviousCenter,
            double Scale,
            double Rotation,
            double ScaleTotal,
            double RotationTotal,
            int TouchesCount);

        /// <summary>
        /// In pixels inside parent view, 0,0 is top-left corner of the view
        /// </summary>
        public record DistanceInfo
        {
            public PointF Delta { get; set; }
            public PointF Total { get; set; }
            /// <summary>Pixels per second</summary>
            public PointF TotalVelocity { get; set; }
            /// <summary>Pixels per second</summary>
            public PointF Velocity { get; set; }
            public PointF Start { get; set; }
            public PointF End { get; set; }
        }
    }
}
