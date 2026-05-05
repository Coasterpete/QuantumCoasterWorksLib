using System;
using System.Collections.Generic;

namespace Quantum.Track
{
    public static class SectionResolver
    {
        public static IReadOnlyList<ResolvedSectionInterval<TSection>> Resolve<TSection>(
            IEnumerable<(TSection Section, double Length)> sections)
        {
            if (sections is null)
            {
                throw new ArgumentNullException(nameof(sections));
            }

            var resolved = new List<ResolvedSectionInterval<TSection>>();
            double startDistance = 0.0;

            foreach ((TSection section, double length) in sections)
            {
                ValidateLength(length);

                double endDistance = startDistance + length;
                resolved.Add(new ResolvedSectionInterval<TSection>(section, startDistance, endDistance));
                startDistance = endDistance;
            }

            if (resolved.Count > 0)
            {
                int lastIndex = resolved.Count - 1;
                ResolvedSectionInterval<TSection> last = resolved[lastIndex];
                resolved[lastIndex] = new ResolvedSectionInterval<TSection>(
                    last.Section,
                    last.StartDistance,
                    last.EndDistance,
                    includeEndDistance: true);
            }

            return resolved;
        }

        public static ResolvedSectionInterval<TSection> Lookup<TSection>(
            IReadOnlyList<ResolvedSectionInterval<TSection>> resolvedIntervals,
            double distance)
        {
            if (resolvedIntervals is null)
            {
                throw new ArgumentNullException(nameof(resolvedIntervals));
            }

            if (double.IsNaN(distance) || double.IsInfinity(distance))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(distance),
                    distance,
                    "Distance must be finite.");
            }

            if (resolvedIntervals.Count == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(distance),
                    distance,
                    "Distance cannot be resolved for an empty section set.");
            }

            double minDistance = resolvedIntervals[0].StartDistance;
            double maxDistance = resolvedIntervals[resolvedIntervals.Count - 1].EndDistance;

            if (distance < minDistance || distance > maxDistance)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(distance),
                    distance,
                    $"Distance must be within [{minDistance}, {maxDistance}].");
            }

            for (int i = 0; i < resolvedIntervals.Count; i++)
            {
                ResolvedSectionInterval<TSection> interval = resolvedIntervals[i];
                if (interval.Contains(distance))
                {
                    return interval;
                }
            }

            throw new InvalidOperationException("Distance could not be resolved to a section interval.");
        }

        private static void ValidateLength(double length)
        {
            if (double.IsNaN(length) || double.IsInfinity(length) || length < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(length),
                    length,
                    "Section length must be non-negative and finite.");
            }

            // Zero-length intervals are intentionally rejected to keep lookup deterministic.
            if (length == 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(length),
                    length,
                    "Section length must be greater than zero.");
            }
        }
    }
}
