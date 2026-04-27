using System;

namespace Quantum.Math
{
    /// <summary>
    /// Small numeric utility helpers.
    /// Keep this minimal and engine-agnostic.
    /// </summary>
    public static class MathUtil
    {
        /// <summary>
        /// Default numeric tolerance for double comparisons.
        /// </summary>
        public const double Epsilon = 1e-9;

        /// <summary>
        /// Clamp a value between min and max.
        /// </summary>
        public static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>
        /// Linearly interpolate between a and b by t (0..1).
        /// </summary>
        public static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }
    }
}
