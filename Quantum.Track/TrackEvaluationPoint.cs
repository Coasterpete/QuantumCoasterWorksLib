namespace Quantum.Track
{
    /// <summary>
    /// Resolved centerline sample target made of a segment reference and local parameter.
    /// </summary>
    /// <remarks>
    /// This is the result of resolving a station distance against a
    /// <see cref="TrackDocument"/>. It keeps the segment reference that will be
    /// sampled by <see cref="TrackEvaluator"/>.
    /// </remarks>
    public readonly struct TrackEvaluationPoint
    {
        /// <summary>
        /// Creates a resolved evaluation point.
        /// </summary>
        /// <param name="segment">Segment to sample.</param>
        /// <param name="localT">Normalized local parameter within the segment.</param>
        public TrackEvaluationPoint(TrackSegment segment, double localT)
        {
            if (segment is null)
            {
                throw new System.ArgumentNullException(nameof(segment));
            }

            Segment = segment;
            LocalT = localT;
        }

        /// <summary>
        /// Segment selected for centerline evaluation.
        /// </summary>
        public TrackSegment Segment { get; }

        /// <summary>
        /// Normalized local parameter within <see cref="Segment"/>.
        /// </summary>
        public double LocalT { get; }
    }
}
