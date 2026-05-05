using System;
using System.Collections.Generic;

namespace Quantum.Track
{
    public static class SectionCurveAssembler
    {
        public static CompositeSectionCurve Assemble(IEnumerable<GeometricSection> sections)
        {
            if (sections is null)
            {
                throw new ArgumentNullException(nameof(sections));
            }

            var definitions = new List<(GeometricSection Section, double Length)>();

            foreach (GeometricSection section in sections)
            {
                if (section is null)
                {
                    throw new ArgumentException("Section entries cannot be null.", nameof(sections));
                }

                definitions.Add((section, section.Length));
            }

            return Assemble(definitions);
        }

        public static CompositeSectionCurve Assemble(IEnumerable<(GeometricSection Section, double Length)> sections)
        {
            if (sections is null)
            {
                throw new ArgumentNullException(nameof(sections));
            }

            var definitions = new List<(GeometricSection Section, double Length)>();

            foreach ((GeometricSection section, double length) in sections)
            {
                if (section is null)
                {
                    throw new ArgumentException("Section entries cannot be null.", nameof(sections));
                }

                // Normalize section length to the resolved interval length for deterministic distance mapping.
                definitions.Add((new GeometricSection(length, section.Curvature, section.Roll), length));
            }

            IReadOnlyList<ResolvedSectionInterval<GeometricSection>> resolved = SectionResolver.Resolve(definitions);
            return Assemble(resolved);
        }

        public static CompositeSectionCurve Assemble(
            IReadOnlyList<ResolvedSectionInterval<GeometricSection>> resolvedIntervals)
        {
            return new CompositeSectionCurve(resolvedIntervals);
        }
    }
}
