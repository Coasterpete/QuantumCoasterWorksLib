using Quantum.Math;
using Quantum.Splines;

namespace Quantum.Track.Authoring.Internal
{
    internal sealed class AuthoringSectionCurve : IArcLengthCurve
    {
        private readonly CompositeSectionCurve _source;
        private readonly double _startDistance;

        public AuthoringSectionCurve(
            CompositeSectionCurve source,
            double startDistance,
            double length)
        {
            _source = source;
            _startDistance = startDistance;
            Length = length;
        }

        public double Length { get; }

        public Vector3d Evaluate(double t)
        {
            double clampedT = MathUtil.Clamp(t, 0.0, 1.0);
            return _source.Evaluate(_startDistance + (Length * clampedT));
        }

        public Vector3d Tangent(double t)
        {
            double clampedT = MathUtil.Clamp(t, 0.0, 1.0);
            return _source.Tangent(_startDistance + (Length * clampedT));
        }

        public Vector3d EvaluateByLength(double s)
        {
            double clampedDistance = MathUtil.Clamp(s, 0.0, Length);
            return _source.Evaluate(_startDistance + clampedDistance);
        }

        public Vector3d TangentByLength(double s)
        {
            double clampedDistance = MathUtil.Clamp(s, 0.0, Length);
            return _source.Tangent(_startDistance + clampedDistance);
        }
    }
}
