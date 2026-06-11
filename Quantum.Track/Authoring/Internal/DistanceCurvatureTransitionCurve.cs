using System;
using Quantum.Math;
using Quantum.Splines;

namespace Quantum.Track.Authoring.Internal
{
    internal sealed class DistanceCurvatureTransitionCurve : IArcLengthCurve, IParamCurveCurvature
    {
        private readonly double _startCurvature;
        private readonly double _curvatureDelta;

        public DistanceCurvatureTransitionCurve(
            double length,
            double startCurvature,
            double endCurvature)
            : this(
                length,
                startCurvature,
                endCurvature,
                CurvatureTransitionInterpolationMode.Linear)
        {
        }

        public DistanceCurvatureTransitionCurve(
            double length,
            double startCurvature,
            double endCurvature,
            CurvatureTransitionInterpolationMode interpolationMode)
        {
            if (!IsFinite(length) || length <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(length),
                    length,
                    "Transition curve length must be finite and greater than zero.");
            }

            if (!IsFinite(startCurvature))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startCurvature),
                    startCurvature,
                    "Start curvature must be finite.");
            }

            if (!IsFinite(endCurvature))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(endCurvature),
                    endCurvature,
                    "End curvature must be finite.");
            }

            if (interpolationMode != CurvatureTransitionInterpolationMode.Linear)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(interpolationMode),
                    interpolationMode,
                    "Only linear distance-domain curvature interpolation is supported.");
            }

            double curvatureDelta = endCurvature - startCurvature;
            if (!IsFinite(curvatureDelta))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(endCurvature),
                    endCurvature,
                    "Curvature range must be finite.");
            }

            double headingSweep = length * (startCurvature + (0.5 * curvatureDelta));
            if (!IsFinite(headingSweep))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(endCurvature),
                    endCurvature,
                    "Transition curvature must produce a finite heading sweep.");
            }

            Length = length;
            _startCurvature = startCurvature;
            _curvatureDelta = curvatureDelta;
            InterpolationMode = interpolationMode;
        }

        public double Length { get; }

        public CurvatureTransitionInterpolationMode InterpolationMode { get; }

        public Vector3d Evaluate(double t)
        {
            double clampedT = MathUtil.Clamp(t, 0.0, 1.0);
            return EvaluateByLength(Length * clampedT);
        }

        public Vector3d Tangent(double t)
        {
            double clampedT = MathUtil.Clamp(t, 0.0, 1.0);
            return TangentByLength(Length * clampedT);
        }

        public Vector3d EvaluateByLength(double s)
        {
            double clampedDistance = MathUtil.Clamp(s, 0.0, Length);

            if (_startCurvature == 0.0 && _curvatureDelta == 0.0)
            {
                return new Vector3d(clampedDistance, 0.0, 0.0);
            }

            if (_curvatureDelta == 0.0)
            {
                return EvaluateConstantCurvature(clampedDistance, _startCurvature);
            }

            double x = FixedGaussLegendreQuadrature.Integrate(
                distance => System.Math.Cos(HeadingAtDistance(distance)),
                0.0,
                clampedDistance);
            double y = FixedGaussLegendreQuadrature.Integrate(
                distance => System.Math.Sin(HeadingAtDistance(distance)),
                0.0,
                clampedDistance);

            return new Vector3d(x, y, 0.0);
        }

        public Vector3d TangentByLength(double s)
        {
            double clampedDistance = MathUtil.Clamp(s, 0.0, Length);
            double heading = HeadingAtDistance(clampedDistance);

            return new Vector3d(
                System.Math.Cos(heading),
                System.Math.Sin(heading),
                0.0);
        }

        public bool TryGetCurvature(double t, out double curvature)
        {
            double clampedT = MathUtil.Clamp(t, 0.0, 1.0);
            curvature = _startCurvature + (_curvatureDelta * clampedT);
            return true;
        }

        private Vector3d EvaluateConstantCurvature(double distance, double curvature)
        {
            if (curvature == 0.0)
            {
                return new Vector3d(distance, 0.0, 0.0);
            }

            double heading = curvature * distance;
            double halfHeadingSin = System.Math.Sin(0.5 * heading);
            return new Vector3d(
                System.Math.Sin(heading) / curvature,
                (2.0 * halfHeadingSin * halfHeadingSin) / curvature,
                0.0);
        }

        private double HeadingAtDistance(double distance)
        {
            double t = distance / Length;
            return distance * (_startCurvature + (0.5 * _curvatureDelta * t));
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
