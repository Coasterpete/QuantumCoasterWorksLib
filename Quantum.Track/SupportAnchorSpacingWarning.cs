namespace Quantum.Track
{
    public enum SupportAnchorSpacingWarningCode
    {
        InvalidStartDistance = 0,
        InvalidEndDistance = 1,
        InvalidTargetSpacing = 2,
        InvalidStartOffset = 3,
        InvalidSpacingRange = 4,
        InvalidExcludedRange = 5,
        ExcludedAnchorCandidate = 6,
        ExcludedGap = 7,
        UnevenEndRemainder = 8,
        NoAnchorCandidates = 9
    }

    /// <summary>
    /// Non-fatal support anchor spacing diagnostic.
    /// </summary>
    public sealed class SupportAnchorSpacingWarning
    {
        internal SupportAnchorSpacingWarning(
            SupportAnchorSpacingWarningCode code,
            string message,
            double? distance = null,
            double? startDistance = null,
            double? endDistance = null,
            int? excludedRangeIndex = null)
        {
            Code = code;
            Message = message;
            Distance = distance;
            StartDistance = startDistance;
            EndDistance = endDistance;
            ExcludedRangeIndex = excludedRangeIndex;
        }

        public SupportAnchorSpacingWarningCode Code { get; }

        public string Message { get; }

        public double? Distance { get; }

        public double? StartDistance { get; }

        public double? EndDistance { get; }

        public int? ExcludedRangeIndex { get; }
    }
}
