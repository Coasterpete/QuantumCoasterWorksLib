using System;
using System.Collections.Generic;
using Quantum.Track.Authoring.Internal;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Builds existing evaluator-ready track documents from validated authoring definitions.
    /// </summary>
    public static class TrackAuthoringDocumentBuilder
    {
        public static TrackDocument Build(TrackAuthoringDefinition definition)
        {
            return Compile(definition).Document;
        }

        public static TrackDocument BuildDocument(TrackAuthoringDefinition definition)
        {
            return Compile(definition).Document;
        }

        public static TrackAuthoringCompilation Compile(TrackAuthoringDefinition definition)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            IReadOnlyList<GeometricSectionDefinition> definitions = definition.Sections;
            var sectionLengths = new List<(GeometricSectionDefinition Section, double Length)>(
                definitions.Count);

            for (int i = 0; i < definitions.Count; i++)
            {
                GeometricSectionDefinition section = definitions[i];
                sectionLengths.Add((section, section.Length));
            }

            IReadOnlyList<ResolvedSectionInterval<GeometricSectionDefinition>> resolvedSections =
                SectionResolver.Resolve(sectionLengths);
            var geometricSections = new List<GeometricSection>(definitions.Count);

            for (int i = 0; i < definitions.Count; i++)
            {
                geometricSections.Add(CreateGeometricSection(definitions[i]));
            }

            CompositeSectionCurve assembledCurve = SectionCurveAssembler.Assemble(geometricSections);
            var segments = new List<TrackSegment>(definitions.Count);

            for (int i = 0; i < definitions.Count; i++)
            {
                GeometricSectionDefinition section = definitions[i];
                ResolvedSectionInterval<GeometricSectionDefinition> resolvedSection = resolvedSections[i];
                var curve = new AuthoringSectionCurve(
                    assembledCurve,
                    resolvedSection.StartDistance,
                    resolvedSection.Length);

                TrackSegment segment;
                if (section is StraightSectionDefinition)
                {
                    segment = new StraightSegment(
                        section.Length,
                        section.Id,
                        spline: curve,
                        rollRadians: section.RollRadians);
                }
                else if (section is ConstantCurvatureSectionDefinition)
                {
                    segment = new CurvedSegment(
                        section.Length,
                        section.Id,
                        spline: curve,
                        rollRadians: section.RollRadians);
                }
                else
                {
                    throw new NotSupportedException(
                        $"Unsupported geometric section definition type '{section.GetType().FullName}'.");
                }

                segments.Add(segment);
            }

            var document = new TrackDocument(segments, geometricSections);
            double totalLength = resolvedSections[resolvedSections.Count - 1].EndDistance;

            return new TrackAuthoringCompilation(
                definition,
                document,
                resolvedSections,
                totalLength);
        }

        private static GeometricSection CreateGeometricSection(
            GeometricSectionDefinition definition)
        {
            if (definition is StraightSectionDefinition)
            {
                return new GeometricSection(
                    definition.Length,
                    curvature: null,
                    roll: definition.RollRadians);
            }

            if (definition is ConstantCurvatureSectionDefinition arc)
            {
                return new GeometricSection(
                    definition.Length,
                    curvature: 1.0 / arc.Radius,
                    roll: definition.RollRadians);
            }

            throw new NotSupportedException(
                $"Unsupported geometric section definition type '{definition.GetType().FullName}'.");
        }
    }
}
