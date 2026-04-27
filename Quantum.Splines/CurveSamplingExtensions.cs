using Quantum.Math;

namespace Quantum.Splines
{
    public static class CurveSamplingExtensions
    {
        public static CurveSample Sample(this IParamCurve curve, double t)
        {
            Vector3d position = curve.Evaluate(t);
            Vector3d tangent = curve.Tangent(t);

            return new CurveSample(t, position, tangent);
        }
    }
}
