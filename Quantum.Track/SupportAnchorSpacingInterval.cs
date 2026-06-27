namespace Quantum.Track
{
    /// <summary>
    /// Actual spacing between two generated support anchor candidates.
    /// </summary>
    public sealed class SupportAnchorSpacingInterval
    {
        internal SupportAnchorSpacingInterval(
            int startCandidateIndex,
            int endCandidateIndex,
            double startDistance,
            double endDistance,
            bool crossesExcludedRange)
        {
            StartCandidateIndex = startCandidateIndex;
            EndCandidateIndex = endCandidateIndex;
            StartDistance = startDistance;
            EndDistance = endDistance;
            Length = endDistance - startDistance;
            CrossesExcludedRange = crossesExcludedRange;
        }

        public int StartCandidateIndex { get; }

        public int EndCandidateIndex { get; }

        public double StartDistance { get; }

        public double EndDistance { get; }

        public double Length { get; }

        public bool CrossesExcludedRange { get; }
    }
}
