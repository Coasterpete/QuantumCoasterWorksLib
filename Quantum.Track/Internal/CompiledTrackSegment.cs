using System;
using Quantum.Splines;

namespace Quantum.Track.Internal
{
    internal sealed class CompiledTrackSegment
    {
        private readonly ArcLengthLUT? _arcLengthLookup;
        private readonly double _lookupLength;

        public CompiledTrackSegment(
            TrackSegment segment,
            double stationStartDistance,
            double geometricLength,
            ArcLengthLUT? arcLengthLookup,
            double lookupLength)
        {
            Segment = segment ?? throw new ArgumentNullException(nameof(segment));
            StationStartDistance = stationStartDistance;
            GeometricLength = geometricLength;
            _arcLengthLookup = arcLengthLookup;
            _lookupLength = lookupLength;
        }

        public TrackSegment Segment { get; }

        public double StationStartDistance { get; }

        public double GeometricLength { get; }

        public double MapLocalDistanceToParameter(double localDistance)
        {
            if (_arcLengthLookup != null)
            {
                double lookupDistance = localDistance * _lookupLength / GeometricLength;
                return _arcLengthLookup.MapS2T(lookupDistance);
            }

            return System.Math.Max(
                0.0,
                System.Math.Min(localDistance / GeometricLength, 1.0));
        }
    }
}
