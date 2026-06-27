namespace Quantum.Track
{
    /// <summary>
    /// Start and end gaps left by support anchor spacing.
    /// </summary>
    public sealed class SupportAnchorSpacingRemainder
    {
        internal SupportAnchorSpacingRemainder(
            double trackSpan,
            double? firstCandidateDistance,
            double? lastCandidateDistance,
            double startGap,
            double endGap)
        {
            TrackSpan = trackSpan;
            FirstCandidateDistance = firstCandidateDistance;
            LastCandidateDistance = lastCandidateDistance;
            StartGap = startGap;
            EndGap = endGap;
        }

        public double TrackSpan { get; }

        public double? FirstCandidateDistance { get; }

        public double? LastCandidateDistance { get; }

        public double StartGap { get; }

        public double EndGap { get; }

        public double EndRemainder => EndGap;
    }
}
