using Quantum.Math;

namespace Quantum.Splines
{
    /// <summary>
    /// Cubic Bezier curve: P0 - P1 - P2 - P3
    /// </summary>
    public sealed class CubicBezierCurve : IParamCurve
    {
        public Vector3d P0;
        public Vector3d P1;
        public Vector3d P2;
        public Vector3d P3;

        public CubicBezierCurve(
            Vector3d p0,
            Vector3d p1,
            Vector3d p2,
            Vector3d p3)
        {
            P0 = p0;
            P1 = p1;
            P2 = p2;
            P3 = p3;
        }

        public Vector3d Evaluate(double t)
        {
            double u = 1.0 - t;
            double u2 = u * u;
            double t2 = t * t;

            return
                (u2 * u) * P0 +
                (3.0 * u2 * t) * P1 +
                (3.0 * u * t2) * P2 +
                (t2 * t) * P3;
        }

        public Vector3d Tangent(double t)
        {
            double u = 1.0 - t;

            Vector3d derivative =
                (3.0 * u * u) * (P1 - P0) +
                (6.0 * u * t) * (P2 - P1) +
                (3.0 * t * t) * (P3 - P2);

            if (derivative.Length <= MathUtil.Epsilon)
                throw new System.InvalidOperationException(
                    $"Unable to compute cubic Bezier tangent at t={t:0.######}: derivative magnitude is near zero.");

            return derivative.Normalized();
        }
    }
}
