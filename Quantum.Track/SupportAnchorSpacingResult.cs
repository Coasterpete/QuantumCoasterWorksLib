using System;
using System.Collections.Generic;
using System.Linq;

namespace Quantum.Track
{
    /// <summary>
    /// Immutable result from Support Anchor Spacing V1.
    /// </summary>
    public sealed class SupportAnchorSpacingResult
    {
        private readonly IReadOnlyList<SupportAnchorCandidate> _candidates;
        private readonly IReadOnlyList<double> _candidateDistances;
        private readonly IReadOnlyList<SupportAnchorSpacingInterval> _intervals;
        private readonly IReadOnlyList<SupportAnchorSpacingWarning> _warnings;

        internal SupportAnchorSpacingResult(
            IEnumerable<SupportAnchorCandidate> candidates,
            IEnumerable<SupportAnchorSpacingInterval> intervals,
            SupportAnchorSpacingRemainder remainder,
            IEnumerable<SupportAnchorSpacingWarning> warnings)
        {
            if (candidates is null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (intervals is null)
            {
                throw new ArgumentNullException(nameof(intervals));
            }

            if (remainder is null)
            {
                throw new ArgumentNullException(nameof(remainder));
            }

            if (warnings is null)
            {
                throw new ArgumentNullException(nameof(warnings));
            }

            SupportAnchorCandidate[] candidateArray = candidates.ToArray();
            SupportAnchorSpacingInterval[] intervalArray = intervals.ToArray();
            SupportAnchorSpacingWarning[] warningArray = warnings.ToArray();

            for (int i = 0; i < candidateArray.Length; i++)
            {
                if (candidateArray[i] is null)
                {
                    throw new ArgumentException(
                        "Candidate collection cannot contain null entries.",
                        nameof(candidates));
                }
            }

            for (int i = 0; i < intervalArray.Length; i++)
            {
                if (intervalArray[i] is null)
                {
                    throw new ArgumentException(
                        "Interval collection cannot contain null entries.",
                        nameof(intervals));
                }
            }

            for (int i = 0; i < warningArray.Length; i++)
            {
                if (warningArray[i] is null)
                {
                    throw new ArgumentException(
                        "Warning collection cannot contain null entries.",
                        nameof(warnings));
                }
            }

            _candidates = Array.AsReadOnly(candidateArray);
            _candidateDistances = Array.AsReadOnly(candidateArray.Select(candidate => candidate.Distance).ToArray());
            _intervals = Array.AsReadOnly(intervalArray);
            Remainder = remainder;
            _warnings = Array.AsReadOnly(warningArray);
        }

        public IReadOnlyList<SupportAnchorCandidate> Candidates => _candidates;

        public IReadOnlyList<double> CandidateDistances => _candidateDistances;

        public IReadOnlyList<SupportAnchorSpacingInterval> Intervals => _intervals;

        public SupportAnchorSpacingRemainder Remainder { get; }

        public IReadOnlyList<SupportAnchorSpacingWarning> Warnings => _warnings;

        public bool HasWarnings => _warnings.Count > 0;
    }
}
