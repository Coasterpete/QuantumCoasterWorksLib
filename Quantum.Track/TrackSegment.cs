using Quantum.Splines;

namespace Quantum.Track
{
    public abstract class TrackSegment
    {
        protected TrackSegment(
            double length,
            string? id = null,
            string? forceSegmentReference = null,
            IParamCurve? spline = null,
            double rollRadians = 0.0)
        {
            Length = length;
            Id = id;
            ForceSegmentReference = forceSegmentReference;
            Spline = spline;
            RollRadians = rollRadians;
        }

        public double Length { get; }

        public string? Id { get; }

        public string? ForceSegmentReference { get; }

        public IParamCurve? Spline { get; }

        public double RollRadians { get; }
    }
}
