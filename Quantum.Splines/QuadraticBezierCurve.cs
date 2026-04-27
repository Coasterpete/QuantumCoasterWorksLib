using Quantum.Math;

namespace Quantum.Splines
{
    /// <summary>
    /// Quadratic Bezier curve: P0 - P1 - P2
    /// </summary>
    public sealed class QuadraticBezierCurve : IParamCurve
    {
        public Vector3d P0;
        public Vector3d P1;
        public Vector3d P2;

        public QuadraticBezierCurve(Vector3d p0, Vector3d p1, Vector3d p2)
        {
            P0 = p0;
            P1 = p1;
            P2 = p2;
        }

        public Vector3d Evaluate(double t)
        {
            double u = 1.0 - t;

            return
                (u * u) * P0 +
                (2.0 * u * t) * P1 +
                (t * t) * P2;
        }

        public Vector3d Tangent(double t)
        {
            Vector3d derivative =
                (2.0 * (1.0 - t)) * (P1 - P0) +
                (2.0 * t) * (P2 - P1);

            if (derivative.Length <= MathUtil.Epsilon)
                throw new System.InvalidOperationException(
                    $"Unable to compute quadratic Bezier tangent at t={t:0.######}: derivative magnitude is near zero.");

            return derivative.Normalized();
        }
    }
}
