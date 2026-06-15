using System;
using System.Collections.Generic;
using Quantum.Math;
using Quantum.Splines;
using SystemMath = System.Math;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Supported compiled-geometry boundary discontinuity kinds.
    /// </summary>
    public enum TrackAuthoringGeometryContinuityDiagnosticKind
    {
        PositionDiscontinuity = 0,
        TangentDiscontinuity = 1,
        CurvatureVectorDiscontinuity = 2,
        RollDiscontinuity = 3
    }

    /// <summary>
    /// Thresholds used when comparing adjacent compiled authoring curves.
    /// </summary>
    public readonly struct TrackAuthoringGeometryContinuityTolerances
    {
        public TrackAuthoringGeometryContinuityTolerances(
            double positionTolerance,
            double tangentAngleToleranceRadians,
            double curvatureVectorTolerance,
            double rollToleranceRadians)
        {
            PositionTolerance = ValidateTolerance(positionTolerance, nameof(positionTolerance));
            TangentAngleToleranceRadians = ValidateTolerance(
                tangentAngleToleranceRadians,
                nameof(tangentAngleToleranceRadians));
            CurvatureVectorTolerance = ValidateTolerance(
                curvatureVectorTolerance,
                nameof(curvatureVectorTolerance));
            RollToleranceRadians = ValidateTolerance(
                rollToleranceRadians,
                nameof(rollToleranceRadians));
        }

        /// <summary>
        /// Accepted endpoint position-gap magnitude in station-distance units.
        /// </summary>
        public double PositionTolerance { get; }

        /// <summary>
        /// Accepted endpoint tangent angle in radians.
        /// </summary>
        public double TangentAngleToleranceRadians { get; }

        /// <summary>
        /// Accepted endpoint curvature-vector delta magnitude in inverse distance units.
        /// </summary>
        public double CurvatureVectorTolerance { get; }

        /// <summary>
        /// Accepted absolute wrapped roll delta in radians.
        /// </summary>
        public double RollToleranceRadians { get; }

        public static TrackAuthoringGeometryContinuityTolerances Default =>
            new TrackAuthoringGeometryContinuityTolerances(
                positionTolerance: 1e-7,
                tangentAngleToleranceRadians: 1e-7,
                curvatureVectorTolerance: 1e-4,
                rollToleranceRadians: 1e-9);

        private static double ValidateTolerance(double value, string paramName)
        {
            if (!AuthoringValidation.IsFinite(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    value,
                    "Tolerance must be finite and non-negative.");
            }

            return value;
        }
    }

    /// <summary>
    /// Deterministic compiled-geometry comparison for one adjacent authored section pair.
    /// </summary>
    public readonly struct TrackAuthoringGeometryContinuityBoundary
    {
        internal TrackAuthoringGeometryContinuityBoundary(
            int boundaryIndex,
            double station,
            string previousSectionId,
            string nextSectionId,
            Vector3d previousEndPosition,
            Vector3d nextStartPosition,
            Vector3d positionGap,
            Vector3d previousEndTangent,
            Vector3d nextStartTangent,
            double tangentAngleRadians,
            Vector3d previousEndCurvatureVector,
            Vector3d nextStartCurvatureVector,
            Vector3d curvatureVectorDelta,
            double previousRollRadians,
            double nextRollRadians,
            double rollDeltaRadians)
        {
            BoundaryIndex = boundaryIndex;
            Station = station;
            PreviousSectionId = previousSectionId;
            NextSectionId = nextSectionId;
            PreviousEndPosition = previousEndPosition;
            NextStartPosition = nextStartPosition;
            PositionGap = positionGap;
            PreviousEndTangent = previousEndTangent;
            NextStartTangent = nextStartTangent;
            TangentAngleRadians = tangentAngleRadians;
            PreviousEndCurvatureVector = previousEndCurvatureVector;
            NextStartCurvatureVector = nextStartCurvatureVector;
            CurvatureVectorDelta = curvatureVectorDelta;
            PreviousRollRadians = previousRollRadians;
            NextRollRadians = nextRollRadians;
            RollDeltaRadians = rollDeltaRadians;
        }

        public int BoundaryIndex { get; }

        public int PreviousSectionIndex => BoundaryIndex;

        public int NextSectionIndex => BoundaryIndex + 1;

        /// <summary>
        /// Cumulative station distance at the previous section endpoint.
        /// </summary>
        public double Station { get; }

        public double StationDistance => Station;

        public string PreviousSectionId { get; }

        public string NextSectionId { get; }

        public Vector3d PreviousEndPosition { get; }

        public Vector3d NextStartPosition { get; }

        /// <summary>
        /// Next start position minus previous end position.
        /// </summary>
        public Vector3d PositionGap { get; }

        public double PositionGapMagnitude => PositionGap.Length;

        public Vector3d PreviousEndTangent { get; }

        public Vector3d NextStartTangent { get; }

        public double TangentAngleRadians { get; }

        public Vector3d PreviousEndCurvatureVector { get; }

        public Vector3d NextStartCurvatureVector { get; }

        /// <summary>
        /// Next start curvature vector minus previous end curvature vector.
        /// </summary>
        public Vector3d CurvatureVectorDelta { get; }

        public double CurvatureVectorDeltaMagnitude => CurvatureVectorDelta.Length;

        public double PreviousRollRadians { get; }

        public double NextRollRadians { get; }

        /// <summary>
        /// Shortest full-turn-wrapped next-minus-previous roll difference.
        /// </summary>
        public double RollDeltaRadians { get; }

        public double AbsoluteRollDeltaRadians => SystemMath.Abs(RollDeltaRadians);
    }

    /// <summary>
    /// One tolerance-exceeding compiled-geometry boundary value.
    /// </summary>
    public readonly struct TrackAuthoringGeometryContinuityDiagnostic
    {
        internal TrackAuthoringGeometryContinuityDiagnostic(
            TrackAuthoringGeometryContinuityDiagnosticKind kind,
            TrackAuthoringGeometryContinuityBoundary boundary,
            double measuredValue,
            double tolerance)
        {
            Kind = kind;
            Boundary = boundary;
            MeasuredValue = measuredValue;
            Tolerance = tolerance;
        }

        public TrackAuthoringGeometryContinuityDiagnosticKind Kind { get; }

        public TrackAuthoringGeometryContinuityBoundary Boundary { get; }

        /// <summary>
        /// Non-negative magnitude or angle compared with <see cref="Tolerance"/>.
        /// </summary>
        public double MeasuredValue { get; }

        public double Tolerance { get; }

        public int BoundaryIndex => Boundary.BoundaryIndex;

        public double Station => Boundary.Station;

        public string PreviousSectionId => Boundary.PreviousSectionId;

        public string NextSectionId => Boundary.NextSectionId;
    }

    /// <summary>
    /// Read-only compiled-geometry boundary comparisons and emitted diagnostics.
    /// </summary>
    public sealed class TrackAuthoringGeometryContinuityReport
    {
        private readonly IReadOnlyList<TrackAuthoringGeometryContinuityBoundary> _boundaries;
        private readonly IReadOnlyList<TrackAuthoringGeometryContinuityDiagnostic> _diagnostics;

        internal TrackAuthoringGeometryContinuityReport(
            IEnumerable<TrackAuthoringGeometryContinuityBoundary> boundaries,
            IEnumerable<TrackAuthoringGeometryContinuityDiagnostic> diagnostics,
            TrackAuthoringGeometryContinuityTolerances tolerances)
        {
            if (boundaries is null)
            {
                throw new ArgumentNullException(nameof(boundaries));
            }

            if (diagnostics is null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            _boundaries = new List<TrackAuthoringGeometryContinuityBoundary>(
                boundaries).AsReadOnly();
            _diagnostics = new List<TrackAuthoringGeometryContinuityDiagnostic>(
                diagnostics).AsReadOnly();
            Tolerances = tolerances;
        }

        public IReadOnlyList<TrackAuthoringGeometryContinuityBoundary> Boundaries => _boundaries;

        public IReadOnlyList<TrackAuthoringGeometryContinuityDiagnostic> Diagnostics => _diagnostics;

        public TrackAuthoringGeometryContinuityTolerances Tolerances { get; }

        public int BoundaryCount => Boundaries.Count;

        public int DiagnosticCount => Diagnostics.Count;

        public bool HasDiagnostics => Diagnostics.Count > 0;
    }

    /// <summary>
    /// Non-fatal diagnostics measured from compiled authoring centerline curves.
    /// </summary>
    public static class TrackAuthoringGeometryContinuityDiagnostics
    {
        private const double TwoPi = 2.0 * SystemMath.PI;
        private const double MaximumCurvatureDerivativeStep = 1e-3;
        private const double CurvatureDerivativeLengthDivisor = 1024.0;

        public static TrackAuthoringGeometryContinuityReport Analyze(
            TrackAuthoringDefinition definition)
        {
            return Analyze(definition, TrackAuthoringGeometryContinuityTolerances.Default);
        }

        public static TrackAuthoringGeometryContinuityReport Analyze(
            TrackAuthoringDefinition definition,
            TrackAuthoringGeometryContinuityTolerances tolerances)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(definition);
            return AnalyzeCompilation(compilation, tolerances);
        }

        internal static TrackAuthoringGeometryContinuityReport AnalyzeCompilation(
            TrackAuthoringCompilation compilation,
            TrackAuthoringGeometryContinuityTolerances tolerances)
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            IReadOnlyList<GeometricSectionDefinition> sections = compilation.Definition.Sections;
            if (compilation.Document.Segments.Count != sections.Count ||
                compilation.ResolvedSections.Count != sections.Count)
            {
                throw new InvalidOperationException(
                    "Compiled authoring geometry is not aligned with its source sections.");
            }

            int boundaryCount = sections.Count - 1;
            var boundaries = new TrackAuthoringGeometryContinuityBoundary[boundaryCount];
            var diagnostics = new List<TrackAuthoringGeometryContinuityDiagnostic>();

            for (int boundaryIndex = 0; boundaryIndex < boundaryCount; boundaryIndex++)
            {
                GeometricSectionDefinition previousDefinition = sections[boundaryIndex];
                GeometricSectionDefinition nextDefinition = sections[boundaryIndex + 1];
                IArcLengthCurve previousCurve = GetGeneratedCurve(compilation, boundaryIndex);
                IArcLengthCurve nextCurve = GetGeneratedCurve(compilation, boundaryIndex + 1);

                Vector3d previousEndPosition = previousCurve.EvaluateByLength(previousCurve.Length);
                Vector3d nextStartPosition = nextCurve.EvaluateByLength(0.0);
                Vector3d positionGap = nextStartPosition - previousEndPosition;
                Vector3d previousEndTangent = NormalizeTangent(
                    previousCurve.TangentByLength(previousCurve.Length),
                    previousDefinition.Id,
                    "end");
                Vector3d nextStartTangent = NormalizeTangent(
                    nextCurve.TangentByLength(0.0),
                    nextDefinition.Id,
                    "start");
                double tangentAngleRadians = GetAngleRadians(
                    previousEndTangent,
                    nextStartTangent);
                Vector3d previousEndCurvatureVector = EstimateEndCurvatureVector(
                    previousCurve,
                    previousEndTangent,
                    previousDefinition.Id);
                Vector3d nextStartCurvatureVector = EstimateStartCurvatureVector(
                    nextCurve,
                    nextStartTangent,
                    nextDefinition.Id);
                Vector3d curvatureVectorDelta =
                    nextStartCurvatureVector - previousEndCurvatureVector;
                double rollDeltaRadians = GetShortestFullTurnDelta(
                    nextDefinition.RollRadians - previousDefinition.RollRadians);

                var boundary = new TrackAuthoringGeometryContinuityBoundary(
                    boundaryIndex,
                    compilation.ResolvedSections[boundaryIndex].EndDistance,
                    previousDefinition.Id,
                    nextDefinition.Id,
                    previousEndPosition,
                    nextStartPosition,
                    positionGap,
                    previousEndTangent,
                    nextStartTangent,
                    tangentAngleRadians,
                    previousEndCurvatureVector,
                    nextStartCurvatureVector,
                    curvatureVectorDelta,
                    previousDefinition.RollRadians,
                    nextDefinition.RollRadians,
                    rollDeltaRadians);

                boundaries[boundaryIndex] = boundary;

                AddDiagnosticIfExceeded(
                    diagnostics,
                    TrackAuthoringGeometryContinuityDiagnosticKind.PositionDiscontinuity,
                    boundary,
                    boundary.PositionGapMagnitude,
                    tolerances.PositionTolerance);
                AddDiagnosticIfExceeded(
                    diagnostics,
                    TrackAuthoringGeometryContinuityDiagnosticKind.TangentDiscontinuity,
                    boundary,
                    boundary.TangentAngleRadians,
                    tolerances.TangentAngleToleranceRadians);
                AddDiagnosticIfExceeded(
                    diagnostics,
                    TrackAuthoringGeometryContinuityDiagnosticKind.CurvatureVectorDiscontinuity,
                    boundary,
                    boundary.CurvatureVectorDeltaMagnitude,
                    tolerances.CurvatureVectorTolerance);
                AddDiagnosticIfExceeded(
                    diagnostics,
                    TrackAuthoringGeometryContinuityDiagnosticKind.RollDiscontinuity,
                    boundary,
                    boundary.AbsoluteRollDeltaRadians,
                    tolerances.RollToleranceRadians);
            }

            return new TrackAuthoringGeometryContinuityReport(
                boundaries,
                diagnostics,
                tolerances);
        }

        private static IArcLengthCurve GetGeneratedCurve(
            TrackAuthoringCompilation compilation,
            int sectionIndex)
        {
            TrackSegment segment = compilation.Document.Segments[sectionIndex];
            if (segment?.Spline is IArcLengthCurve curve)
            {
                return curve;
            }

            string sectionId = compilation.Definition.Sections[sectionIndex].Id;
            throw new InvalidOperationException(
                $"Compiled authoring section '{sectionId}' does not have an arc-length curve.");
        }

        private static Vector3d EstimateStartCurvatureVector(
            IArcLengthCurve curve,
            Vector3d endpointTangent,
            string sectionId)
        {
            double step = GetCurvatureDerivativeStep(curve, sectionId);
            Vector3d tangent0 = endpointTangent;
            Vector3d tangent1 = NormalizeTangent(
                curve.TangentByLength(step),
                sectionId,
                "near-start");
            Vector3d tangent2 = NormalizeTangent(
                curve.TangentByLength(2.0 * step),
                sectionId,
                "near-start");
            Vector3d derivative = ((tangent0 * -3.0) + (tangent1 * 4.0) - tangent2) /
                                  (2.0 * step);
            return ProjectPerpendicular(derivative, endpointTangent);
        }

        private static Vector3d EstimateEndCurvatureVector(
            IArcLengthCurve curve,
            Vector3d endpointTangent,
            string sectionId)
        {
            double step = GetCurvatureDerivativeStep(curve, sectionId);
            double length = curve.Length;
            Vector3d tangent0 = endpointTangent;
            Vector3d tangent1 = NormalizeTangent(
                curve.TangentByLength(length - step),
                sectionId,
                "near-end");
            Vector3d tangent2 = NormalizeTangent(
                curve.TangentByLength(length - (2.0 * step)),
                sectionId,
                "near-end");
            Vector3d derivative = ((tangent0 * 3.0) - (tangent1 * 4.0) + tangent2) /
                                  (2.0 * step);
            return ProjectPerpendicular(derivative, endpointTangent);
        }

        private static double GetCurvatureDerivativeStep(IArcLengthCurve curve, string sectionId)
        {
            double length = curve.Length;
            if (!AuthoringValidation.IsFinite(length) || length <= 0.0)
            {
                throw new InvalidOperationException(
                    $"Compiled authoring section '{sectionId}' has an invalid curve length.");
            }

            return SystemMath.Min(
                MaximumCurvatureDerivativeStep,
                length / CurvatureDerivativeLengthDivisor);
        }

        private static Vector3d ProjectPerpendicular(Vector3d vector, Vector3d tangent)
        {
            return vector - (tangent * Vector3d.Dot(vector, tangent));
        }

        private static Vector3d NormalizeTangent(
            Vector3d tangent,
            string sectionId,
            string endpointLabel)
        {
            double length = tangent.Length;
            if (!AuthoringValidation.IsFinite(length) || length <= 0.0)
            {
                throw new InvalidOperationException(
                    $"Compiled authoring section '{sectionId}' has an invalid {endpointLabel} tangent.");
            }

            return tangent / length;
        }

        private static double GetAngleRadians(Vector3d previousTangent, Vector3d nextTangent)
        {
            double crossLength = Vector3d.Cross(previousTangent, nextTangent).Length;
            double dot = Vector3d.Dot(previousTangent, nextTangent);
            return SystemMath.Atan2(crossLength, dot);
        }

        private static double GetShortestFullTurnDelta(double deltaRadians)
        {
            double wrapped = deltaRadians % TwoPi;

            if (wrapped <= -SystemMath.PI)
            {
                wrapped += TwoPi;
            }
            else if (wrapped > SystemMath.PI)
            {
                wrapped -= TwoPi;
            }

            return wrapped;
        }

        private static void AddDiagnosticIfExceeded(
            ICollection<TrackAuthoringGeometryContinuityDiagnostic> diagnostics,
            TrackAuthoringGeometryContinuityDiagnosticKind kind,
            TrackAuthoringGeometryContinuityBoundary boundary,
            double measuredValue,
            double tolerance)
        {
            if (measuredValue > tolerance)
            {
                diagnostics.Add(new TrackAuthoringGeometryContinuityDiagnostic(
                    kind,
                    boundary,
                    measuredValue,
                    tolerance));
            }
        }
    }
}
