using System;
using System.Collections.Generic;
using Quantum.Math;
using Quantum.Splines;
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

            List<TrackSegment> segments = ContainsTransition(definitions)
                ? CreateTransitionAwareSegments(definitions, geometricSections)
                : CreateM140Segments(definitions, resolvedSections, geometricSections);

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

            if (definition is CurvatureTransitionSectionDefinition)
            {
                return new GeometricSection(
                    definition.Length,
                    curvature: null,
                    roll: definition.RollRadians);
            }

            throw new NotSupportedException(
                $"Unsupported geometric section definition type '{definition.GetType().FullName}'.");
        }

        private static bool ContainsTransition(
            IReadOnlyList<GeometricSectionDefinition> definitions)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] is CurvatureTransitionSectionDefinition)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<TrackSegment> CreateM140Segments(
            IReadOnlyList<GeometricSectionDefinition> definitions,
            IReadOnlyList<ResolvedSectionInterval<GeometricSectionDefinition>> resolvedSections,
            IReadOnlyList<GeometricSection> geometricSections)
        {
            CompositeSectionCurve assembledCurve = SectionCurveAssembler.Assemble(geometricSections);
            var segments = new List<TrackSegment>(definitions.Count);

            for (int i = 0; i < definitions.Count; i++)
            {
                ResolvedSectionInterval<GeometricSectionDefinition> resolvedSection = resolvedSections[i];
                var curve = new AuthoringSectionCurve(
                    assembledCurve,
                    resolvedSection.StartDistance,
                    resolvedSection.Length);

                segments.Add(CreateSegment(definitions[i], curve));
            }

            return segments;
        }

        private static List<TrackSegment> CreateTransitionAwareSegments(
            IReadOnlyList<GeometricSectionDefinition> definitions,
            IReadOnlyList<GeometricSection> geometricSections)
        {
            var segments = new List<TrackSegment>(definitions.Count);
            Vector3d currentPosition = Vector3d.Zero;
            double currentHeadingRadians = 0.0;

            for (int i = 0; i < definitions.Count; i++)
            {
                IArcLengthCurve localCurve = CreateLocalCurve(definitions[i], geometricSections[i]);
                var placedCurve = new PlacedAuthoringSectionCurve(
                    localCurve,
                    currentPosition,
                    currentHeadingRadians);

                segments.Add(CreateSegment(definitions[i], placedCurve));

                currentPosition = placedCurve.Evaluate(1.0);
                Vector3d endTangent = placedCurve.Tangent(1.0);
                currentHeadingRadians = System.Math.Atan2(endTangent.Y, endTangent.X);
            }

            return segments;
        }

        private static IArcLengthCurve CreateLocalCurve(
            GeometricSectionDefinition definition,
            GeometricSection geometricSection)
        {
            if (definition is CurvatureTransitionSectionDefinition transition)
            {
                return new DistanceCurvatureTransitionCurve(
                    transition.Length,
                    transition.StartCurvature,
                    transition.EndCurvature,
                    transition.InterpolationMode);
            }

            IParamCurve generatedCurve = geometricSection.GenerateCurve();
            if (generatedCurve is IArcLengthCurve arcLengthCurve)
            {
                return arcLengthCurve;
            }

            throw new InvalidOperationException(
                $"Generated curve for section '{definition.Id}' does not support distance evaluation.");
        }

        private static TrackSegment CreateSegment(
            GeometricSectionDefinition definition,
            IParamCurve curve)
        {
            if (definition is StraightSectionDefinition)
            {
                return new StraightSegment(
                    definition.Length,
                    definition.Id,
                    spline: curve,
                    rollRadians: definition.RollRadians);
            }

            if (definition is ConstantCurvatureSectionDefinition ||
                definition is CurvatureTransitionSectionDefinition)
            {
                return new CurvedSegment(
                    definition.Length,
                    definition.Id,
                    spline: curve,
                    rollRadians: definition.RollRadians);
            }

            throw new NotSupportedException(
                $"Unsupported geometric section definition type '{definition.GetType().FullName}'.");
        }
    }
}
