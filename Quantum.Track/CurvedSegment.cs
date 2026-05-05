using Quantum.Splines;

namespace Quantum.Track
{
    public sealed class CurvedSegment : TrackSegment
    {
        public CurvedSegment(
            double length,
            string? id = null,
            string? forceSegmentReference = null,
            IParamCurve? spline = null,
            double rollRadians = 0.0)
            : base(length, id, forceSegmentReference, spline, rollRadians)
        {
        }
    }
}
