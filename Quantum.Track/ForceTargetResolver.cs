using System;
using System.Collections.Generic;
using Quantum.Math;

namespace Quantum.Track
{
    public static class ForceTargetResolver
    {
        public static IReadOnlyList<ResolvedSectionInterval<ForceSection>> Resolve(
            IEnumerable<(ForceSection Section, double Length)> sections)
        {
            if (sections is null)
            {
                throw new ArgumentNullException(nameof(sections));
            }

            var definitions = new List<(ForceSection Section, double Length)>();

            foreach ((ForceSection section, double length) in sections)
            {
                if (section is null)
                {
                    throw new ArgumentException("Section entries cannot be null.", nameof(sections));
                }

                definitions.Add((section, length));
            }

            return SectionResolver.Resolve(definitions);
        }

        public static ForceTargetSnapshot Lookup(
            IEnumerable<(ForceSection Section, double Length)> sections,
            double distance)
        {
            IReadOnlyList<ResolvedSectionInterval<ForceSection>> resolved = Resolve(sections);
            return Lookup(resolved, distance);
        }

        public static ForceTargetSnapshot Lookup(
            IReadOnlyList<ResolvedSectionInterval<ForceSection>> resolvedIntervals,
            double distance)
        {
            ResolvedSectionInterval<ForceSection> interval = SectionResolver.Lookup(resolvedIntervals, distance);
            ForceSection resolvedSection = interval.Section
                ?? throw new InvalidOperationException("Resolved interval section entries cannot be null.");

            double localDistance = distance - interval.StartDistance;
            double normalizedT = interval.IncludeEndDistance && distance == interval.EndDistance
                ? 1.0
                : MathUtil.Clamp(localDistance / interval.Length, 0.0, 1.0);

            return new ForceTargetSnapshot(
                resolvedSection,
                interval.StartDistance,
                interval.EndDistance,
                localDistance,
                normalizedT);
        }
    }
}
