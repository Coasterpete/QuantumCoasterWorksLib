using System;

namespace Quantum.Track.Internal
{
    internal readonly struct ResolvedTrackDistance
    {
        public ResolvedTrackDistance(
            int segmentIndex,
            TrackSegment segment,
            double localT,
            double clampedDistance,
            double segmentStartDistance)
        {
            if (segment is null)
            {
                throw new ArgumentNullException(nameof(segment));
            }

            SegmentIndex = segmentIndex;
            Segment = segment;
            LocalT = localT;
            ClampedDistance = clampedDistance;
            SegmentStartDistance = segmentStartDistance;
        }

        public int SegmentIndex { get; }

        public TrackSegment Segment { get; }

        public double LocalT { get; }

        public double ClampedDistance { get; }

        public double SegmentStartDistance { get; }
    }
}
