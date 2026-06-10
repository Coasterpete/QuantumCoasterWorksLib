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
            return BuildDocument(definition);
        }

        public static TrackDocument BuildDocument(TrackAuthoringDefinition definition)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            IReadOnlyList<GeometricSectionDefinition> definitions = definition.Sections;
            var geometricSections = new List<GeometricSection>(definitions.Count);

            for (int i = 0; i < definitions.Count; i++)
            {
                geometricSections.Add(CreateGeometricSection(definitions[i]));
            }

            CompositeSectionCurve assembledCurve = SectionCurveAssembler.Assemble(geometricSections);
            var segments = new List<TrackSegment>(definitions.Count);
            double startDistance = 0.0;

            for (int i = 0; i < definitions.Count; i++)
            {
                GeometricSectionDefinition section = definitions[i];
                var curve = new AuthoringSectionCurve(
                    assembledCurve,
                    startDistance,
                    section.Length);

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
                startDistance += section.Length;
            }

            return new TrackDocument(segments, geometricSections);
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
