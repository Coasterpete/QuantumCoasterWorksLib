using System;
using System.Collections.Generic;
using Quantum.Math;
using Quantum.Splines;
using Quantum.Track.Authoring.Internal;
using Quantum.Track.Internal;

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

            List<TrackSegment> segments = CreatePlacedSegments(
                definitions,
                geometricSections,
                definition.StartPose);

            var document = new TrackDocument(
                segments,
                geometricSections,
                definition.StartPose);
            double totalLength = resolvedSections[resolvedSections.Count - 1].EndDistance;
            BankingProfile bankingProfile = CreateBankingProfile(
                definition,
                resolvedSections,
                totalLength);
            TrackRuntimeCompileResult runtimeCompilation = TrackRuntimeCompiler.Compile(
                document,
                TrackSamplingOptions.Default);

            if (!runtimeCompilation.Success || runtimeCompilation.Runtime is null)
            {
                throw CreateRuntimeCompilationException(runtimeCompilation);
            }

            return new TrackAuthoringCompilation(
                definition,
                document,
                runtimeCompilation.Runtime,
                bankingProfile,
                resolvedSections,
                totalLength);
        }

        private static BankingProfile CreateBankingProfile(
            TrackAuthoringDefinition definition,
            IReadOnlyList<ResolvedSectionInterval<GeometricSectionDefinition>> resolvedSections,
            double totalLength)
        {
            if (definition.Banking != null)
            {
                ValidateAuthoredBankingDomain(definition.Banking, totalLength);
                return new BankingProfile(definition.Banking.Keys);
            }

            IReadOnlyList<GeometricSectionDefinition> definitions = definition.Sections;
            var keys = new List<BankingProfileKey>(definitions.Count + 1)
            {
                new BankingProfileKey(
                    0.0,
                    definitions[0].RollRadians,
                    BankingProfileInterpolationMode.Constant)
            };

            for (int i = 1; i < definitions.Count; i++)
            {
                keys.Add(new BankingProfileKey(
                    resolvedSections[i].StartDistance,
                    definitions[i].RollRadians,
                    BankingProfileInterpolationMode.Constant));
            }

            keys.Add(new BankingProfileKey(
                totalLength,
                definitions[definitions.Count - 1].RollRadians,
                BankingProfileInterpolationMode.Constant));

            return new BankingProfile(keys);
        }

        private static void ValidateAuthoredBankingDomain(
            TrackBankingDefinition banking,
            double totalLength)
        {
            IReadOnlyList<BankingProfileKey> keys = banking.Keys;
            if (keys.Count < 2)
            {
                throw new ArgumentException(
                    "Authored banking requires at least two keys.",
                    nameof(banking));
            }

            for (int i = 0; i < keys.Count; i++)
            {
                double distance = keys[i].Distance;
                if (distance < 0.0 || distance > totalLength)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(banking),
                        distance,
                        $"Authored banking key distance at index {i} must be within [0, {totalLength}].");
                }
            }

            if (keys[0].Distance != 0.0)
            {
                throw new ArgumentException(
                    "Authored banking must start exactly at distance 0.",
                    nameof(banking));
            }

            if (keys[keys.Count - 1].Distance != totalLength)
            {
                throw new ArgumentException(
                    $"Authored banking must end exactly at total length {totalLength}.",
                    nameof(banking));
            }
        }

        private static InvalidOperationException CreateRuntimeCompilationException(
            TrackRuntimeCompileResult runtimeCompilation)
        {
            TrackRuntimeDiagnostic? firstError = null;
            for (int i = 0; i < runtimeCompilation.Diagnostics.Count; i++)
            {
                TrackRuntimeDiagnostic diagnostic = runtimeCompilation.Diagnostics[i];
                if (diagnostic.Severity == TrackRuntimeDiagnosticSeverity.Error)
                {
                    firstError = diagnostic;
                    break;
                }
            }

            if (firstError is null)
            {
                return new InvalidOperationException(
                    "Track runtime compilation failed without an error diagnostic.");
            }

            string segmentContext = firstError.SegmentIndex.HasValue
                ? $"segment index {firstError.SegmentIndex.Value}" +
                  (firstError.SegmentId is null ? string.Empty : $" (ID '{firstError.SegmentId}')")
                : "global track context";

            return new InvalidOperationException(
                $"Track runtime compilation failed with {firstError.Code} at " +
                $"{segmentContext}: {firstError.Message}");
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

            if (definition is SpatialSectionDefinition)
            {
                return new GeometricSection(
                    definition.Length,
                    curvature: null,
                    roll: definition.RollRadians);
            }

            throw new NotSupportedException(
                $"Unsupported geometric section definition type '{definition.GetType().FullName}'.");
        }

        private static List<TrackSegment> CreatePlacedSegments(
            IReadOnlyList<GeometricSectionDefinition> definitions,
            IReadOnlyList<GeometricSection> geometricSections,
            TrackStartPose startPose)
        {
            var segments = new List<TrackSegment>(definitions.Count);
            Vector3d currentPosition = startPose.Position;
            Vector3d currentTangent = startPose.Tangent;
            Vector3d currentNormal = startPose.Normal;
            Vector3d currentBinormal = startPose.Binormal;

            for (int i = 0; i < definitions.Count; i++)
            {
                IArcLengthCurve localCurve = CreateLocalCurve(definitions[i], geometricSections[i]);
                var placedCurve = new PlacedAuthoringSectionCurve(
                    localCurve,
                    currentPosition,
                    currentTangent,
                    currentNormal,
                    currentBinormal);

                segments.Add(CreateSegment(definitions[i], placedCurve));

                currentPosition = placedCurve.Evaluate(1.0);
                if (definitions[i] is SpatialSectionDefinition)
                {
                    AdvanceSpatialConstructionBasis(
                        placedCurve,
                        ref currentTangent,
                        ref currentNormal,
                        ref currentBinormal);
                }
                else
                {
                    Vector3d localEndTangent = localCurve.Tangent(1.0);
                    Vector3d previousTangent = currentTangent;
                    Vector3d previousNormal = currentNormal;
                    currentTangent = (previousTangent * localEndTangent.X) +
                                     (previousNormal * localEndTangent.Y);
                    currentNormal = (previousTangent * -localEndTangent.Y) +
                                    (previousNormal * localEndTangent.X);
                }
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

            if (definition is SpatialSectionDefinition spatial)
            {
                var curve = new GSharkNurbsCurveAdapter(
                    new List<Vector3d>(spatial.ControlPoints),
                    new List<double>(spatial.Weights),
                    spatial.Degree);
                return new ArcLengthCurveAdapter(
                    curve,
                    TrackSamplingOptions.DefaultArcLengthSamples,
                    TrackSamplingOptions.DefaultArcLengthTolerance);
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
                definition is CurvatureTransitionSectionDefinition ||
                definition is SpatialSectionDefinition)
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

        private static void AdvanceSpatialConstructionBasis(
            IArcLengthCurve curve,
            ref Vector3d tangent,
            ref Vector3d normal,
            ref Vector3d binormal)
        {
            Vector3d previousTangent = curve.TangentByLength(0.0);
            Vector3d transportedNormal = normal;
            double curveLength = curve.Length;

            for (int i = 1; i <= TrackSamplingOptions.DefaultTransportSamplesPerSegment; i++)
            {
                double fraction =
                    (double)i / TrackSamplingOptions.DefaultTransportSamplesPerSegment;
                Vector3d currentTangent = curve.TangentByLength(curveLength * fraction);
                transportedNormal = RotationMinimizingFrameTransport.TransportNormal(
                    transportedNormal,
                    previousTangent,
                    currentTangent);
                previousTangent = currentTangent;
            }

            tangent = previousTangent.Normalized();
            normal = transportedNormal.Normalized();
            binormal = Vector3d.Cross(tangent, normal).Normalized();
            normal = Vector3d.Cross(binormal, tangent).Normalized();
        }
    }
}
