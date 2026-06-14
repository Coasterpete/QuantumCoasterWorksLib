using Quantum.Math;
using Quantum.Splines;

namespace Quantum.Track.Authoring.Internal
{
    internal sealed class PlacedAuthoringSectionCurve : IArcLengthCurve
    {
        private readonly IArcLengthCurve _localCurve;
        private readonly Vector3d _startPosition;
        private readonly Vector3d _startTangent;
        private readonly Vector3d _startNormal;
        private readonly Vector3d _startBinormal;

        public PlacedAuthoringSectionCurve(
            IArcLengthCurve localCurve,
            Vector3d startPosition,
            Vector3d startTangent,
            Vector3d startNormal,
            Vector3d startBinormal)
        {
            _localCurve = localCurve;
            _startPosition = startPosition;
            _startTangent = startTangent;
            _startNormal = startNormal;
            _startBinormal = startBinormal;
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
            return PlaceDirection(_localCurve.Tangent(clampedT));
        }

        public Vector3d EvaluateByLength(double s)
        {
            double clampedDistance = MathUtil.Clamp(s, 0.0, Length);
            return PlacePosition(_localCurve.EvaluateByLength(clampedDistance));
        }

        public Vector3d TangentByLength(double s)
        {
            double clampedDistance = MathUtil.Clamp(s, 0.0, Length);
            return PlaceDirection(_localCurve.TangentByLength(clampedDistance));
        }

        private Vector3d PlacePosition(Vector3d localPosition)
        {
            return _startPosition + PlaceDirection(localPosition);
        }

        private Vector3d PlaceDirection(Vector3d localDirection)
        {
            return (_startTangent * localDirection.X) +
                   (_startNormal * localDirection.Y) +
                   (_startBinormal * localDirection.Z);
        }
    }
}
