using System;
using System.Collections.Generic;
using System.Linq;

namespace Quantum.Track
{
    /// <summary>
    /// Generates support anchor candidate distances from canonical station distance only.
    /// </summary>
    public static class SupportAnchorSpacingGenerator
    {
        private const double Tolerance = 1e-9;

        public static SupportAnchorSpacingResult Generate(
            double startDistance,
            double endDistance,
            double targetSpacing,
            double startOffset = 0.0,
            IReadOnlyList<SupportAnchorExcludedRange>? excludedRanges = null)
        {
            return Generate(new SupportAnchorSpacingRequest(
                startDistance,
                endDistance,
                targetSpacing,
                startOffset,
                excludedRanges));
        }

        public static SupportAnchorSpacingResult Generate(SupportAnchorSpacingRequest request)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var warnings = new List<SupportAnchorSpacingWarning>();
            ValidateGlobalInputs(request, warnings);

            if (warnings.Any(IsBlockingWarning))
            {
                return CreateEmptyResult(request, warnings);
            }

            List<IndexedExcludedRange> validExcludedRanges = GetValidExcludedRanges(request, warnings);
            var candidates = new List<SupportAnchorCandidate>();
            double firstRawDistance = request.StartDistance + request.StartOffset;
            int generatedIndex = 0;

            while (true)
            {
                double distance = firstRawDistance + (generatedIndex * request.TargetSpacing);
                if (distance > request.EndDistance + Tolerance)
                {
                    break;
                }

                if (IsNear(distance, request.EndDistance))
                {
                    distance = request.EndDistance;
                }

                IndexedExcludedRange? excludedRange = FindContainingRange(validExcludedRanges, distance);
                if (excludedRange is null)
                {
                    candidates.Add(new SupportAnchorCandidate(candidates.Count, distance));
                }
                else
                {
                    warnings.Add(new SupportAnchorSpacingWarning(
                        SupportAnchorSpacingWarningCode.ExcludedAnchorCandidate,
                        $"Anchor candidate at distance {distance} was skipped because it is inside excluded range {excludedRange.Value.OriginalIndex}.",
                        distance: distance,
                        startDistance: excludedRange.Value.StartDistance,
                        endDistance: excludedRange.Value.EndDistance,
                        excludedRangeIndex: excludedRange.Value.OriginalIndex));
                }

                generatedIndex++;
            }

            if (candidates.Count == 0)
            {
                warnings.Add(new SupportAnchorSpacingWarning(
                    SupportAnchorSpacingWarningCode.NoAnchorCandidates,
                    "No support anchor candidates were generated for the supplied spacing inputs.",
                    startDistance: request.StartDistance,
                    endDistance: request.EndDistance));

                return CreateEmptyResult(request, warnings);
            }

            List<SupportAnchorSpacingInterval> intervals = BuildIntervals(candidates, validExcludedRanges, warnings);
            SupportAnchorSpacingRemainder remainder = BuildRemainder(request, candidates);

            if (remainder.EndRemainder > Tolerance)
            {
                warnings.Add(new SupportAnchorSpacingWarning(
                    SupportAnchorSpacingWarningCode.UnevenEndRemainder,
                    $"End gap is {remainder.EndRemainder}, so the spacing range does not end on an anchor candidate.",
                    startDistance: candidates[candidates.Count - 1].Distance,
                    endDistance: request.EndDistance));
            }

            return new SupportAnchorSpacingResult(candidates, intervals, remainder, warnings);
        }

        private static void ValidateGlobalInputs(
            SupportAnchorSpacingRequest request,
            List<SupportAnchorSpacingWarning> warnings)
        {
            if (!IsFinite(request.StartDistance))
            {
                warnings.Add(new SupportAnchorSpacingWarning(
                    SupportAnchorSpacingWarningCode.InvalidStartDistance,
                    "Start distance must be finite.",
                    distance: request.StartDistance));
            }

            if (!IsFinite(request.EndDistance))
            {
                warnings.Add(new SupportAnchorSpacingWarning(
                    SupportAnchorSpacingWarningCode.InvalidEndDistance,
                    "End distance must be finite.",
                    distance: request.EndDistance));
            }

            if (!IsFinite(request.TargetSpacing) || request.TargetSpacing <= 0.0)
            {
                warnings.Add(new SupportAnchorSpacingWarning(
                    SupportAnchorSpacingWarningCode.InvalidTargetSpacing,
                    "Target spacing must be finite and greater than zero.",
                    distance: request.TargetSpacing));
            }

            if (!IsFinite(request.StartOffset) || request.StartOffset < 0.0)
            {
                warnings.Add(new SupportAnchorSpacingWarning(
                    SupportAnchorSpacingWarningCode.InvalidStartOffset,
                    "Start offset must be finite and greater than or equal to zero.",
                    distance: request.StartOffset));
            }

            if (IsFinite(request.StartDistance) &&
                IsFinite(request.EndDistance) &&
                request.StartDistance >= request.EndDistance)
            {
                warnings.Add(new SupportAnchorSpacingWarning(
                    SupportAnchorSpacingWarningCode.InvalidSpacingRange,
                    "Start distance must be less than end distance.",
                    startDistance: request.StartDistance,
                    endDistance: request.EndDistance));
            }
        }

        private static bool IsBlockingWarning(SupportAnchorSpacingWarning warning)
        {
            return warning.Code == SupportAnchorSpacingWarningCode.InvalidStartDistance ||
                warning.Code == SupportAnchorSpacingWarningCode.InvalidEndDistance ||
                warning.Code == SupportAnchorSpacingWarningCode.InvalidTargetSpacing ||
                warning.Code == SupportAnchorSpacingWarningCode.InvalidStartOffset ||
                warning.Code == SupportAnchorSpacingWarningCode.InvalidSpacingRange;
        }

        private static List<IndexedExcludedRange> GetValidExcludedRanges(
            SupportAnchorSpacingRequest request,
            List<SupportAnchorSpacingWarning> warnings)
        {
            var ranges = new List<IndexedExcludedRange>();

            for (int i = 0; i < request.ExcludedRanges.Count; i++)
            {
                SupportAnchorExcludedRange range = request.ExcludedRanges[i];
                if (!IsFinite(range.StartDistance) ||
                    !IsFinite(range.EndDistance) ||
                    range.StartDistance >= range.EndDistance)
                {
                    warnings.Add(new SupportAnchorSpacingWarning(
                        SupportAnchorSpacingWarningCode.InvalidExcludedRange,
                        $"Excluded range {i} must use finite distances with start less than end.",
                        startDistance: range.StartDistance,
                        endDistance: range.EndDistance,
                        excludedRangeIndex: i));
                    continue;
                }

                if (range.EndDistance < request.StartDistance || range.StartDistance > request.EndDistance)
                {
                    continue;
                }

                ranges.Add(new IndexedExcludedRange(
                    originalIndex: i,
                    startDistance: System.Math.Max(request.StartDistance, range.StartDistance),
                    endDistance: System.Math.Min(request.EndDistance, range.EndDistance)));
            }

            ranges.Sort((left, right) =>
            {
                int startComparison = left.StartDistance.CompareTo(right.StartDistance);
                if (startComparison != 0)
                {
                    return startComparison;
                }

                int endComparison = left.EndDistance.CompareTo(right.EndDistance);
                if (endComparison != 0)
                {
                    return endComparison;
                }

                return left.OriginalIndex.CompareTo(right.OriginalIndex);
            });

            return ranges;
        }

        private static List<SupportAnchorSpacingInterval> BuildIntervals(
            IReadOnlyList<SupportAnchorCandidate> candidates,
            IReadOnlyList<IndexedExcludedRange> excludedRanges,
            List<SupportAnchorSpacingWarning> warnings)
        {
            var intervals = new List<SupportAnchorSpacingInterval>();

            for (int i = 1; i < candidates.Count; i++)
            {
                SupportAnchorCandidate start = candidates[i - 1];
                SupportAnchorCandidate end = candidates[i];
                IndexedExcludedRange? crossedRange = FindOverlappingRange(
                    excludedRanges,
                    start.Distance,
                    end.Distance);
                bool crossesExcludedRange = crossedRange != null;

                intervals.Add(new SupportAnchorSpacingInterval(
                    start.Index,
                    end.Index,
                    start.Distance,
                    end.Distance,
                    crossesExcludedRange));

                if (crossedRange != null)
                {
                    warnings.Add(new SupportAnchorSpacingWarning(
                        SupportAnchorSpacingWarningCode.ExcludedGap,
                        $"Spacing interval from {start.Distance} to {end.Distance} crosses excluded range {crossedRange.Value.OriginalIndex}.",
                        startDistance: start.Distance,
                        endDistance: end.Distance,
                        excludedRangeIndex: crossedRange.Value.OriginalIndex));
                }
            }

            return intervals;
        }

        private static SupportAnchorSpacingRemainder BuildRemainder(
            SupportAnchorSpacingRequest request,
            IReadOnlyList<SupportAnchorCandidate> candidates)
        {
            SupportAnchorCandidate first = candidates[0];
            SupportAnchorCandidate last = candidates[candidates.Count - 1];
            return new SupportAnchorSpacingRemainder(
                trackSpan: request.EndDistance - request.StartDistance,
                firstCandidateDistance: first.Distance,
                lastCandidateDistance: last.Distance,
                startGap: first.Distance - request.StartDistance,
                endGap: request.EndDistance - last.Distance);
        }

        private static SupportAnchorSpacingResult CreateEmptyResult(
            SupportAnchorSpacingRequest request,
            List<SupportAnchorSpacingWarning> warnings)
        {
            double trackSpan = IsFinite(request.StartDistance) &&
                IsFinite(request.EndDistance) &&
                request.EndDistance > request.StartDistance
                    ? request.EndDistance - request.StartDistance
                    : 0.0;
            var remainder = new SupportAnchorSpacingRemainder(
                trackSpan,
                firstCandidateDistance: null,
                lastCandidateDistance: null,
                startGap: trackSpan,
                endGap: trackSpan);

            return new SupportAnchorSpacingResult(
                Array.Empty<SupportAnchorCandidate>(),
                Array.Empty<SupportAnchorSpacingInterval>(),
                remainder,
                warnings);
        }

        private static IndexedExcludedRange? FindContainingRange(
            IReadOnlyList<IndexedExcludedRange> ranges,
            double distance)
        {
            for (int i = 0; i < ranges.Count; i++)
            {
                IndexedExcludedRange range = ranges[i];
                if (distance >= range.StartDistance - Tolerance &&
                    distance <= range.EndDistance + Tolerance)
                {
                    return range;
                }
            }

            return null;
        }

        private static IndexedExcludedRange? FindOverlappingRange(
            IReadOnlyList<IndexedExcludedRange> ranges,
            double startDistance,
            double endDistance)
        {
            for (int i = 0; i < ranges.Count; i++)
            {
                IndexedExcludedRange range = ranges[i];
                if (range.StartDistance < endDistance - Tolerance &&
                    range.EndDistance > startDistance + Tolerance)
                {
                    return range;
                }
            }

            return null;
        }

        private static bool IsFinite(double value)
        {
            return !(double.IsNaN(value) || double.IsInfinity(value));
        }

        private static bool IsNear(double left, double right)
        {
            return System.Math.Abs(left - right) <= Tolerance;
        }

        private struct IndexedExcludedRange
        {
            public IndexedExcludedRange(int originalIndex, double startDistance, double endDistance)
            {
                OriginalIndex = originalIndex;
                StartDistance = startDistance;
                EndDistance = endDistance;
            }

            public int OriginalIndex { get; }

            public double StartDistance { get; }

            public double EndDistance { get; }
        }
    }
}
