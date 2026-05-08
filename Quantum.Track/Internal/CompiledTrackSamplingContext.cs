using System;

namespace Quantum.Track.Internal
{
    internal sealed class CompiledTrackSamplingContext
    {
        private readonly TrackSegment[] _segments;
        private readonly double[] _segmentStartDistances;

        private CompiledTrackSamplingContext(
            TrackSegment[] segments,
            double[] segmentStartDistances,
            double totalLength)
        {
            _segments = segments;
            _segmentStartDistances = segmentStartDistances;
            TotalLength = totalLength;
        }

        public double TotalLength { get; }

        public static CompiledTrackSamplingContext Compile(TrackDocument document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            int segmentCount = document.Segments.Count;
            var segments = new TrackSegment[segmentCount];
            var segmentStartDistances = new double[segmentCount];
            double totalLength = 0.0;

            for (int i = 0; i < segmentCount; i++)
            {
                TrackSegment segment = document.Segments[i];

                if (segment is null)
                {
                    throw new InvalidOperationException("TrackDocument contains a null segment entry.");
                }

                segments[i] = segment;
                segmentStartDistances[i] = totalLength;
                totalLength += segment.Length;
            }

            return new CompiledTrackSamplingContext(segments, segmentStartDistances, totalLength);
        }

        public ResolvedTrackDistance Resolve(double distance)
        {
            double clampedDistance = System.Math.Max(0.0, System.Math.Min(distance, TotalLength));

            for (int i = 0; i < _segments.Length; i++)
            {
                TrackSegment segment = _segments[i];
                double segmentStartDistance = _segmentStartDistances[i];
                double segmentEndDistance = segmentStartDistance + segment.Length;
                bool isLastSegment = i == _segments.Length - 1;

                if (clampedDistance < segmentEndDistance || isLastSegment)
                {
                    if (segment.Length <= 0.0)
                    {
                        return new ResolvedTrackDistance(
                            i,
                            segment,
                            localT: 0.0,
                            clampedDistance,
                            segmentStartDistance);
                    }

                    double localT = (clampedDistance - segmentStartDistance) / segment.Length;
                    localT = System.Math.Max(0.0, System.Math.Min(localT, 1.0));

                    return new ResolvedTrackDistance(
                        i,
                        segment,
                        localT,
                        clampedDistance,
                        segmentStartDistance);
                }
            }

            throw new InvalidOperationException("TrackDocument could not be evaluated at the specified distance.");
        }
    }
}
