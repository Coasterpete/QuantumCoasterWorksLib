using System;
using System.Collections.Generic;
using Quantum.Math;
using Quantum.Splines;

namespace Quantum.Track
{
    /// <summary>
    /// Composite curve built from resolved geometric section intervals.
    /// Distance is evaluated in the resolved interval domain.
    /// </summary>
    public sealed class CompositeSectionCurve : IArcLengthCurve
    {
        private readonly IReadOnlyList<ResolvedSectionInterval<GeometricSection>> _resolvedIntervals;
        private readonly Dictionary<ResolvedSectionInterval<GeometricSection>, IntervalState> _stateByInterval;

        public CompositeSectionCurve(IReadOnlyList<ResolvedSectionInterval<GeometricSection>> resolvedIntervals)
        {
            _resolvedIntervals = NormalizeResolvedIntervals(resolvedIntervals);
            _stateByInterval = BuildIntervalStates(_resolvedIntervals);
            TotalLength = _resolvedIntervals[_resolvedIntervals.Count - 1].EndDistance;
        }

        public double TotalLength { get; }

        double IArcLengthCurve.Length => TotalLength;

        public Vector3d Evaluate(double distance)
        {
            IntervalState state = ResolveState(distance, out ResolvedSectionInterval<GeometricSection> interval);
            double localT = MapDistanceToLocalT(interval, distance);

            Vector3d localPosition = state.Curve.Evaluate(localT);
            Vector3d worldPosition = state.StartPosition + RotateAroundZ(localPosition, state.StartHeadingRadians);

            EnsureFiniteVector(worldPosition, nameof(distance), distance);
            return worldPosition;
        }

        public Vector3d Tangent(double distance)
        {
            IntervalState state = ResolveState(distance, out ResolvedSectionInterval<GeometricSection> interval);
            double localT = MapDistanceToLocalT(interval, distance);

            Vector3d localTangent = state.Curve.Tangent(localT);
            Vector3d worldTangent = RotateAroundZ(localTangent, state.StartHeadingRadians);

            EnsureFiniteVector(worldTangent, nameof(distance), distance);
            return worldTangent;
        }

        public Vector3d EvaluateByLength(double s)
        {
            return Evaluate(s);
        }

        public Vector3d TangentByLength(double s)
        {
            return Tangent(s);
        }

        Vector3d IParamCurve.Evaluate(double t)
        {
            ValidateParamT(t);
            return Evaluate(t * TotalLength);
        }

        Vector3d IParamCurve.Tangent(double t)
        {
            ValidateParamT(t);
            return Tangent(t * TotalLength);
        }

        private IntervalState ResolveState(
            double distance,
            out ResolvedSectionInterval<GeometricSection> interval)
        {
            interval = SectionResolver.Lookup(_resolvedIntervals, distance);
            if (_stateByInterval.TryGetValue(interval, out IntervalState state))
            {
                return state;
            }

            throw new InvalidOperationException("Resolved interval state lookup failed.");
        }

        private static IReadOnlyList<ResolvedSectionInterval<GeometricSection>> NormalizeResolvedIntervals(
            IReadOnlyList<ResolvedSectionInterval<GeometricSection>> resolvedIntervals)
        {
            if (resolvedIntervals is null)
            {
                throw new ArgumentNullException(nameof(resolvedIntervals));
            }

            if (resolvedIntervals.Count == 0)
            {
                throw new ArgumentException("At least one resolved interval is required.", nameof(resolvedIntervals));
            }

            var normalized = new List<ResolvedSectionInterval<GeometricSection>>(resolvedIntervals.Count);
            double expectedStart = 0.0;

            for (int i = 0; i < resolvedIntervals.Count; i++)
            {
                ResolvedSectionInterval<GeometricSection> interval = resolvedIntervals[i];
                if (interval is null)
                {
                    throw new ArgumentException("Resolved interval entries cannot be null.", nameof(resolvedIntervals));
                }

                if (interval.Section is null)
                {
                    throw new ArgumentException("Resolved interval section entries cannot be null.", nameof(resolvedIntervals));
                }

                if (!IsFinite(interval.StartDistance))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(resolvedIntervals),
                        interval.StartDistance,
                        "Interval start distance must be finite.");
                }

                if (!IsFinite(interval.EndDistance))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(resolvedIntervals),
                        interval.EndDistance,
                        "Interval end distance must be finite.");
                }

                if (i == 0 && interval.StartDistance != 0.0)
                {
                    throw new ArgumentException(
                        "First interval must start at distance 0.0.",
                        nameof(resolvedIntervals));
                }

                if (i > 0 && System.Math.Abs(interval.StartDistance - expectedStart) > MathUtil.Epsilon)
                {
                    throw new ArgumentException(
                        "Intervals must be contiguous and ordered.",
                        nameof(resolvedIntervals));
                }

                double length = interval.EndDistance - interval.StartDistance;
                if (!IsFinite(length) || length <= 0.0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(resolvedIntervals),
                        length,
                        "Interval length must be finite and greater than zero.");
                }

                bool includeEndDistance = i == resolvedIntervals.Count - 1;
                normalized.Add(new ResolvedSectionInterval<GeometricSection>(
                    interval.Section,
                    interval.StartDistance,
                    interval.EndDistance,
                    includeEndDistance));

                expectedStart = interval.EndDistance;
            }

            return normalized;
        }

        private static Dictionary<ResolvedSectionInterval<GeometricSection>, IntervalState> BuildIntervalStates(
            IReadOnlyList<ResolvedSectionInterval<GeometricSection>> intervals)
        {
            var states = new Dictionary<ResolvedSectionInterval<GeometricSection>, IntervalState>(intervals.Count);

            Vector3d currentPosition = Vector3d.Zero;
            double currentHeadingRadians = 0.0;

            for (int i = 0; i < intervals.Count; i++)
            {
                ResolvedSectionInterval<GeometricSection> interval = intervals[i];
                IParamCurve localCurve = CreateCurveForInterval(interval);

                var state = new IntervalState(localCurve, currentPosition, currentHeadingRadians);
                states.Add(interval, state);

                Vector3d localEndPosition = localCurve.Evaluate(1.0);
                Vector3d worldEndPosition = currentPosition + RotateAroundZ(localEndPosition, currentHeadingRadians);
                EnsureFiniteVector(worldEndPosition, nameof(intervals), interval.EndDistance);

                Vector3d localEndTangent = localCurve.Tangent(1.0);
                Vector3d worldEndTangent = RotateAroundZ(localEndTangent, currentHeadingRadians);
                EnsureFiniteVector(worldEndTangent, nameof(intervals), interval.EndDistance);

                double planarMagnitude = System.Math.Sqrt(
                    (worldEndTangent.X * worldEndTangent.X) +
                    (worldEndTangent.Y * worldEndTangent.Y));

                if (planarMagnitude <= MathUtil.Epsilon)
                {
                    throw new InvalidOperationException("Unable to derive section heading from tangent.");
                }

                currentPosition = worldEndPosition;
                currentHeadingRadians = System.Math.Atan2(worldEndTangent.Y, worldEndTangent.X);
            }

            return states;
        }

        private static IParamCurve CreateCurveForInterval(ResolvedSectionInterval<GeometricSection> interval)
        {
            GeometricSection sourceSection = interval.Section;
            double length = interval.Length;

            GeometricSection sectionForLength =
                System.Math.Abs(sourceSection.Length - length) <= MathUtil.Epsilon
                    ? sourceSection
                    : new GeometricSection(length, sourceSection.Curvature, sourceSection.Roll);

            IParamCurve curve = sectionForLength.GenerateCurve();
            if (curve is null)
            {
                throw new InvalidOperationException("GeometricSection.GenerateCurve returned null.");
            }

            return curve;
        }

        private static double MapDistanceToLocalT(
            ResolvedSectionInterval<GeometricSection> interval,
            double distance)
        {
            double localDistance = distance - interval.StartDistance;
            double t = localDistance / interval.Length;
            return MathUtil.Clamp(t, 0.0, 1.0);
        }

        private static Vector3d RotateAroundZ(Vector3d vector, double radians)
        {
            double cos = System.Math.Cos(radians);
            double sin = System.Math.Sin(radians);

            return new Vector3d(
                (vector.X * cos) - (vector.Y * sin),
                (vector.X * sin) + (vector.Y * cos),
                vector.Z);
        }

        private static void ValidateParamT(double t)
        {
            if (!IsFinite(t) || t < 0.0 || t > 1.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(t),
                    t,
                    "Parameter t must be finite and within [0.0, 1.0].");
            }
        }

        private static void EnsureFiniteVector(Vector3d value, string paramName, double paramValue)
        {
            if (!IsFinite(value.X) || !IsFinite(value.Y) || !IsFinite(value.Z))
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    paramValue,
                    "Computed curve value must be finite.");
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private readonly struct IntervalState
        {
            public IntervalState(IParamCurve curve, Vector3d startPosition, double startHeadingRadians)
            {
                Curve = curve;
                StartPosition = startPosition;
                StartHeadingRadians = startHeadingRadians;
            }

            public IParamCurve Curve { get; }

            public Vector3d StartPosition { get; }

            public double StartHeadingRadians { get; }
        }
    }
}
