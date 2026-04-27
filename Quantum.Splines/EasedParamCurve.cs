using System;
using Quantum.Math;

namespace Quantum.Splines
{
    /// <summary>
    /// Wraps a parametric curve and remaps t through an easing function.
    /// </summary>
    public sealed class EasedParamCurve : IParamCurve
    {
        private readonly IParamCurve _innerCurve;
        private readonly Func<double, double> _easing;

        public EasedParamCurve(IParamCurve innerCurve, Func<double, double> easing)
        {
            _innerCurve = innerCurve ?? throw new ArgumentNullException(nameof(innerCurve));
            _easing = easing ?? throw new ArgumentNullException(nameof(easing));
        }

        public Vector3d Evaluate(double t)
        {
            double mappedT = MapT(t);
            return _innerCurve.Evaluate(mappedT);
        }

        public Vector3d Tangent(double t)
        {
            double mappedT = MapT(t);
            Vector3d tangent = _innerCurve.Tangent(mappedT);

            double length = tangent.Length;
            if (double.IsNaN(length) || double.IsInfinity(length) || length <= MathUtil.Epsilon)
            {
                throw new InvalidOperationException(
                    $"Unable to compute eased tangent at t={t:0.######}: mapped tangent magnitude is invalid.");
            }

            return tangent.Normalized();
        }

        private double MapT(double t)
        {
            double clampedT = CurveEasing.Clamp01(t);

            // Preserve exact endpoint behavior.
            if (clampedT <= 0.0)
                return 0.0;

            if (clampedT >= 1.0)
                return 1.0;

            double easedT = _easing(clampedT);
            if (double.IsNaN(easedT) || double.IsInfinity(easedT))
            {
                throw new InvalidOperationException(
                    $"Unable to map eased parameter at t={t:0.######}: easing function returned a non-finite value.");
            }

            return CurveEasing.Clamp01(easedT);
        }
    }
}
