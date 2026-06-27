using System;
using System.Collections.Generic;

namespace Quantum.Track
{
    /// <summary>
    /// Input settings for Support Anchor Spacing V1.
    /// </summary>
    public sealed class SupportAnchorSpacingRequest
    {
        private readonly IReadOnlyList<SupportAnchorExcludedRange> _excludedRanges;

        public SupportAnchorSpacingRequest(
            double startDistance,
            double endDistance,
            double targetSpacing,
            double startOffset = 0.0,
            IReadOnlyList<SupportAnchorExcludedRange>? excludedRanges = null)
        {
            StartDistance = startDistance;
            EndDistance = endDistance;
            TargetSpacing = targetSpacing;
            StartOffset = startOffset;

            if (excludedRanges is null)
            {
                _excludedRanges = Array.AsReadOnly(Array.Empty<SupportAnchorExcludedRange>());
                return;
            }

            var ranges = new SupportAnchorExcludedRange[excludedRanges.Count];
            for (int i = 0; i < excludedRanges.Count; i++)
            {
                ranges[i] = excludedRanges[i] ?? throw new ArgumentException(
                    $"Excluded range at index {i} cannot be null.",
                    nameof(excludedRanges));
            }

            _excludedRanges = Array.AsReadOnly(ranges);
        }

        public double StartDistance { get; }

        public double EndDistance { get; }

        public double TargetSpacing { get; }

        public double StartOffset { get; }

        public IReadOnlyList<SupportAnchorExcludedRange> ExcludedRanges => _excludedRanges;
    }
}
