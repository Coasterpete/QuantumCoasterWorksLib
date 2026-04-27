using Quantum.Math;

namespace Quantum.Splines
{
    /// <summary>
    /// Minimal easing helpers for remapping normalized curve parameters.
    /// </summary>
    public static class CurveEasing
    {
        /// <summary>
        /// Identity easing.
        /// </summary>
        public static double Linear(double t)
        {
            return Clamp01(t);
        }

        /// <summary>
        /// Simple smooth in/out easing in [0, 1].
        /// </summary>
        public static double SmoothStep(double t)
        {
            double x = Clamp01(t);
            return x * x * (3.0 - (2.0 * x));
        }

        /// <summary>
        /// Clamp a parameter into [0, 1].
        /// </summary>
        public static double Clamp01(double t)
        {
            return MathUtil.Clamp(t, 0.0, 1.0);
        }
    }
}
