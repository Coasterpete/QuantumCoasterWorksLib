namespace Quantum.Track
{
    /// <summary>
    /// Segment-local coordinate for evaluating a <see cref="TrackDocument"/>.
    /// </summary>
    /// <remarks>
    /// Use station-distance methods on <see cref="TrackEvaluator"/> for the public
    /// coaster-facing sampling path. This value is the lower-level segment index and
    /// normalized local parameter used after a distance has been resolved.
    /// </remarks>
    public readonly struct TrackPosition
    {
        /// <summary>
        /// Creates a segment-local track position.
        /// </summary>
        /// <param name="segmentIndex">Zero-based index into <see cref="TrackDocument.Segments"/>.</param>
        /// <param name="localT">Normalized local parameter within the segment, normally in [0, 1].</param>
        public TrackPosition(int segmentIndex, double localT)
        {
            SegmentIndex = segmentIndex;
            LocalT = localT;
        }

        /// <summary>
        /// Zero-based index into <see cref="TrackDocument.Segments"/>.
        /// </summary>
        public int SegmentIndex { get; }

        /// <summary>
        /// Normalized local parameter within the selected segment.
        /// </summary>
        public double LocalT { get; }
    }
}
