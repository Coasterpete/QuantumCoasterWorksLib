using Quantum.Math;

namespace Quantum.Splines
{
    public class LineCurve : IArcLengthCurve, IParamCurveCurvature
    {
        private readonly Vector3d _start;
        private readonly Vector3d _end;
        private readonly Vector3d _direction;

        public LineCurve(Vector3d start, Vector3d end)
        {
            _start = start;
            _end = end;
            _direction = end - start;
        }

        public double Length => _direction.Length;

        public Vector3d Evaluate(double t)
        {
            return _start + _direction * t;
        }

        public Vector3d Tangent(double t)
        {
            if (_direction.Length <= MathUtil.Epsilon)
                throw new System.InvalidOperationException(
                    $"Unable to compute line tangent at t={t:0.######}: line direction magnitude is near zero.");

            return _direction.Normalized();
        }

        public Vector3d EvaluateByLength(double s)
        {
            if (Length < MathUtil.Epsilon)
                return _start;

            double clampedS = MathUtil.Clamp(s, 0.0, Length);
            double t = clampedS / Length;
            return Evaluate(t);
        }

        public Vector3d TangentByLength(double s)
        {
            return Tangent(0.0);
        }

        public bool TryGetCurvature(double t, out double curvature)
        {
            curvature = 0.0;
            return _direction.Length > MathUtil.Epsilon;
        }
    }
}
