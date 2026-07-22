using System;
using System.Collections.Generic;
using SystemMath = System.Math;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Supported authored boundary discontinuity kinds.
    /// </summary>
    public enum TrackAuthoringBoundaryContinuityDiagnosticKind
    {
        CurvatureDiscontinuity = 0,
        RollDiscontinuity = 1
    }

    /// <summary>
    /// Thresholds used when comparing adjacent authored section endpoints.
    /// </summary>
    public readonly struct TrackAuthoringBoundaryContinuityTolerances
    {
        public TrackAuthoringBoundaryContinuityTolerances(
            double curvatureTolerance,
            double rollToleranceRadians)
        {
            CurvatureTolerance = ValidateTolerance(
                curvatureTolerance,
                nameof(curvatureTolerance));
            RollToleranceRadians = ValidateTolerance(
                rollToleranceRadians,
                nameof(rollToleranceRadians));
        }

        /// <summary>
        /// Accepted absolute curvature delta in inverse station-distance units.
        /// </summary>
        public double CurvatureTolerance { get; }

        /// <summary>
        /// Accepted absolute wrapped roll delta in radians.
        /// </summary>
        public double RollToleranceRadians { get; }

        public static TrackAuthoringBoundaryContinuityTolerances Default =>
            new TrackAuthoringBoundaryContinuityTolerances(
                curvatureTolerance: 1e-9,
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
    /// Deterministic endpoint comparison for one adjacent authored section pair.
    /// </summary>
    public readonly struct TrackAuthoringBoundaryContinuityBoundary
    {
        internal TrackAuthoringBoundaryContinuityBoundary(
            int boundaryIndex,
            double station,
            string previousSectionId,
            string nextSectionId,
            double previousEndCurvature,
            double nextStartCurvature,
            double curvatureDelta,
            double previousRollRadians,
            double nextRollRadians,
            double rollDeltaRadians)
        {
            BoundaryIndex = boundaryIndex;
            Station = station;
            PreviousSectionId = previousSectionId;
            NextSectionId = nextSectionId;
            PreviousEndCurvature = previousEndCurvature;
            NextStartCurvature = nextStartCurvature;
            CurvatureDelta = curvatureDelta;
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

        public double PreviousEndCurvature { get; }

        public double NextStartCurvature { get; }

        /// <summary>
        /// Next start curvature minus previous end curvature.
        /// </summary>
        public double CurvatureDelta { get; }

        public double PreviousRollRadians { get; }

        public double NextRollRadians { get; }

        /// <summary>
        /// Shortest full-turn-wrapped next-minus-previous roll difference.
        /// </summary>
        public double RollDeltaRadians { get; }
    }

    /// <summary>
    /// One tolerance-exceeding authored boundary value.
    /// </summary>
    public readonly struct TrackAuthoringBoundaryContinuityDiagnostic
    {
        internal TrackAuthoringBoundaryContinuityDiagnostic(
            TrackAuthoringBoundaryContinuityDiagnosticKind kind,
            TrackAuthoringBoundaryContinuityBoundary boundary,
            double delta,
            double tolerance)
        {
            Kind = kind;
            Boundary = boundary;
            Delta = delta;
            Tolerance = tolerance;
        }

        public TrackAuthoringBoundaryContinuityDiagnosticKind Kind { get; }

        public TrackAuthoringBoundaryContinuityBoundary Boundary { get; }

        public double Delta { get; }

        public double AbsoluteDelta => SystemMath.Abs(Delta);

        public double Tolerance { get; }

        public int BoundaryIndex => Boundary.BoundaryIndex;

        public double Station => Boundary.Station;

        public string PreviousSectionId => Boundary.PreviousSectionId;

        public string NextSectionId => Boundary.NextSectionId;
    }

    /// <summary>
    /// Read-only authored boundary comparisons and emitted diagnostics.
    /// </summary>
    public sealed class TrackAuthoringBoundaryContinuityReport
    {
        private readonly IReadOnlyList<TrackAuthoringBoundaryContinuityBoundary> _boundaries;
        private readonly IReadOnlyList<TrackAuthoringBoundaryContinuityDiagnostic> _diagnostics;

        internal TrackAuthoringBoundaryContinuityReport(
            IEnumerable<TrackAuthoringBoundaryContinuityBoundary> boundaries,
            IEnumerable<TrackAuthoringBoundaryContinuityDiagnostic> diagnostics,
            TrackAuthoringBoundaryContinuityTolerances tolerances)
        {
            if (boundaries is null)
            {
                throw new ArgumentNullException(nameof(boundaries));
            }

            if (diagnostics is null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            _boundaries = new List<TrackAuthoringBoundaryContinuityBoundary>(boundaries).AsReadOnly();
            _diagnostics = new List<TrackAuthoringBoundaryContinuityDiagnostic>(diagnostics).AsReadOnly();
            Tolerances = tolerances;
        }

        public IReadOnlyList<TrackAuthoringBoundaryContinuityBoundary> Boundaries => _boundaries;

        public IReadOnlyList<TrackAuthoringBoundaryContinuityDiagnostic> Diagnostics => _diagnostics;

        public TrackAuthoringBoundaryContinuityTolerances Tolerances { get; }

        public int BoundaryCount => Boundaries.Count;

        public int DiagnosticCount => Diagnostics.Count;

        public bool HasDiagnostics => Diagnostics.Count > 0;
    }

    /// <summary>
    /// Non-fatal diagnostics for curvature and roll continuity between authored sections.
    /// </summary>
    public static class TrackAuthoringBoundaryContinuityDiagnostics
    {
        private const double TwoPi = 2.0 * SystemMath.PI;

        public static TrackAuthoringBoundaryContinuityReport Analyze(
            TrackAuthoringDefinition definition)
        {
            return Analyze(definition, TrackAuthoringBoundaryContinuityTolerances.Default);
        }

        public static TrackAuthoringBoundaryContinuityReport Analyze(
            TrackAuthoringDefinition definition,
            TrackAuthoringBoundaryContinuityTolerances tolerances)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            int boundaryCount = definition.Sections.Count - 1;
            var boundaries = new TrackAuthoringBoundaryContinuityBoundary[boundaryCount];
            var diagnostics = new List<TrackAuthoringBoundaryContinuityDiagnostic>();
            double station = 0.0;

            for (int boundaryIndex = 0; boundaryIndex < boundaryCount; boundaryIndex++)
            {
                GeometricSectionDefinition previous = definition.Sections[boundaryIndex];
                GeometricSectionDefinition next = definition.Sections[boundaryIndex + 1];
                station += previous.Length;

                double previousEndCurvature = GetEndCurvature(previous);
                double nextStartCurvature = GetStartCurvature(next);
                double curvatureDelta = nextStartCurvature - previousEndCurvature;
                double rollDelta = GetShortestFullTurnDelta(
                    next.RollRadians - previous.RollRadians);

                var boundary = new TrackAuthoringBoundaryContinuityBoundary(
                    boundaryIndex,
                    station,
                    previous.Id,
                    next.Id,
                    previousEndCurvature,
                    nextStartCurvature,
                    curvatureDelta,
                    previous.RollRadians,
                    next.RollRadians,
                    rollDelta);

                boundaries[boundaryIndex] = boundary;

                AddDiagnosticIfExceeded(
                    diagnostics,
                    TrackAuthoringBoundaryContinuityDiagnosticKind.CurvatureDiscontinuity,
                    boundary,
                    curvatureDelta,
                    tolerances.CurvatureTolerance);
                AddDiagnosticIfExceeded(
                    diagnostics,
                    TrackAuthoringBoundaryContinuityDiagnosticKind.RollDiscontinuity,
                    boundary,
                    rollDelta,
                    tolerances.RollToleranceRadians);
            }

            return new TrackAuthoringBoundaryContinuityReport(
                boundaries,
                diagnostics,
                tolerances);
        }

        private static double GetStartCurvature(GeometricSectionDefinition section)
        {
            if (TrackAuthoringScalarCurvature.TryGetStartCurvature(
                    section,
                    out double curvature))
            {
                return curvature;
            }

            throw new NotSupportedException(
                $"Authoring section type '{section.GetType().FullName}' does not expose supported scalar curvature.");
        }

        private static double GetEndCurvature(GeometricSectionDefinition section)
        {
            if (TrackAuthoringScalarCurvature.TryGetEndCurvature(
                    section,
                    out double curvature))
            {
                return curvature;
            }

            throw new NotSupportedException(
                $"Authoring section type '{section.GetType().FullName}' does not expose supported scalar curvature.");
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
            ICollection<TrackAuthoringBoundaryContinuityDiagnostic> diagnostics,
            TrackAuthoringBoundaryContinuityDiagnosticKind kind,
            TrackAuthoringBoundaryContinuityBoundary boundary,
            double delta,
            double tolerance)
        {
            if (SystemMath.Abs(delta) > tolerance)
            {
                diagnostics.Add(new TrackAuthoringBoundaryContinuityDiagnostic(
                    kind,
                    boundary,
                    delta,
                    tolerance));
            }
        }
    }
}
