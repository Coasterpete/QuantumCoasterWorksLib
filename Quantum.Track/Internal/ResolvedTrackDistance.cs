using System;

namespace Quantum.Track.Internal
{
    internal readonly struct ResolvedTrackDistance
    {
        public ResolvedTrackDistance(
            TrackSegment segment,
            double localT,
            double localDistance,
            double clampedDistance)
        {
            if (segment is null)
            {
                throw new ArgumentNullException(nameof(segment));
            }

            Segment = segment;
            LocalT = localT;
            LocalDistance = localDistance;
            ClampedDistance = clampedDistance;
        }

        public TrackSegment Segment { get; }

        public double LocalT { get; }

        public double LocalDistance { get; }

        public double ClampedDistance { get; }
    }
}
