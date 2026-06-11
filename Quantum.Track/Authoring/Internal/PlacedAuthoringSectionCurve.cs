using Quantum.Math;
using Quantum.Splines;

namespace Quantum.Track.Authoring.Internal
{
    internal sealed class PlacedAuthoringSectionCurve : IArcLengthCurve
    {
        private readonly IArcLengthCurve _localCurve;
        private readonly Vector3d _startPosition;
        private readonly double _startHeadingRadians;

        public PlacedAuthoringSectionCurve(
            IArcLengthCurve localCurve,
            Vector3d startPosition,
            double startHeadingRadians)
        {
            _localCurve = localCurve;
            _startPosition = startPosition;
            _startHeadingRadians = startHeadingRadians;
        }

        public double Length => _localCurve.Length;

        public Vector3d Evaluate(double t)
        {
            double clampedT = MathUtil.Clamp(t, 0.0, 1.0);
            return PlacePosition(_localCurve.Evaluate(clampedT));
        }

        public Vector3d Tangent(double t)
        {
            double clampedT = MathUtil.Clamp(t, 0.0, 1.0);
            return RotateAroundZ(_localCurve.Tangent(clampedT), _startHeadingRadians);
        }

        public Vector3d EvaluateByLength(double s)
        {
            double clampedDistance = MathUtil.Clamp(s, 0.0, Length);
            return PlacePosition(_localCurve.EvaluateByLength(clampedDistance));
        }

        public Vector3d TangentByLength(double s)
        {
            double clampedDistance = MathUtil.Clamp(s, 0.0, Length);
            return RotateAroundZ(
                _localCurve.TangentByLength(clampedDistance),
                _startHeadingRadians);
        }

        private Vector3d PlacePosition(Vector3d localPosition)
        {
            return _startPosition + RotateAroundZ(localPosition, _startHeadingRadians);
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
    }
}
