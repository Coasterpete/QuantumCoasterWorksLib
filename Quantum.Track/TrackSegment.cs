using Quantum.Splines;

namespace Quantum.Track
{
    /// <summary>
    /// Base coaster centerline segment used by <see cref="TrackDocument"/> station sampling.
    /// </summary>
    /// <remarks>
    /// Segment identity, length, and roll are part of the coaster-domain document
    /// boundary. The current spline reference is a support-layer centerline carrier
    /// and should be reached through <see cref="TrackEvaluator"/> by consumers.
    /// </remarks>
    public abstract class TrackSegment
    {
        /// <summary>
        /// Creates a track segment with an optional support-layer centerline curve.
        /// </summary>
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

        /// <summary>
        /// Declared segment length in station-distance units. Spline-backed segments
        /// must match their measured geometric length when sampled.
        /// </summary>
        public double Length { get; }

        /// <summary>
        /// Optional stable segment identifier for authoring/debug/export adapters.
        /// </summary>
        public string? Id { get; }

        /// <summary>
        /// Optional reference to force-section data associated with this segment.
        /// </summary>
        public string? ForceSegmentReference { get; }

        /// <summary>
        /// Optional support-layer curve used by the evaluator for centerline point and tangent sampling.
        /// </summary>
        public IParamCurve? Spline { get; }

        /// <summary>
        /// Segment roll angle in radians applied around the sampled tangent.
        /// </summary>
        public double RollRadians { get; }
    }
}
