using System;
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

        public static double EaseInQuart(double t, double tension = 1.0)
        {
            double x = ApplyTension(Clamp01(t), tension);
            return Clamp01(System.Math.Pow(x, 4.0));
        }

        public static double EaseOutQuart(double t, double tension = 1.0)
        {
            double x = ApplyTension(Clamp01(t), tension);
            return Clamp01(1.0 - System.Math.Pow(1.0 - x, 4.0));
        }

        public static double EaseInOutQuart(double t, double center = 0.5, double tension = 1.0)
        {
            double x = ApplyCenterAndTension(t, center, tension);
            if (x < 0.5)
                return Clamp01(8.0 * System.Math.Pow(x, 4.0));

            return Clamp01(1.0 - (8.0 * System.Math.Pow(1.0 - x, 4.0)));
        }

        public static double EaseInQuint(double t, double tension = 1.0)
        {
            double x = ApplyTension(Clamp01(t), tension);
            return Clamp01(System.Math.Pow(x, 5.0));
        }

        public static double EaseOutQuint(double t, double tension = 1.0)
        {
            double x = ApplyTension(Clamp01(t), tension);
            return Clamp01(1.0 - System.Math.Pow(1.0 - x, 5.0));
        }

        public static double EaseInOutQuint(double t, double center = 0.5, double tension = 1.0)
        {
            double x = ApplyCenterAndTension(t, center, tension);
            if (x < 0.5)
                return Clamp01(16.0 * System.Math.Pow(x, 5.0));

            return Clamp01(1.0 - (16.0 * System.Math.Pow(1.0 - x, 5.0)));
        }

        public static double EaseInSine(double t, double tension = 1.0)
        {
            double x = ApplyTension(Clamp01(t), tension);
            return Clamp01(1.0 - System.Math.Cos((System.Math.PI * 0.5) * x));
        }

        public static double EaseOutSine(double t, double tension = 1.0)
        {
            double x = ApplyTension(Clamp01(t), tension);
            return Clamp01(System.Math.Sin((System.Math.PI * 0.5) * x));
        }

        public static double EaseInOutSine(double t, double center = 0.5, double tension = 1.0)
        {
            double x = ApplyCenterAndTension(t, center, tension);
            return Clamp01(0.5 - (0.5 * System.Math.Cos(System.Math.PI * x)));
        }

        public static double Plateau(
            double t,
            double plateauAmount = 0.25,
            double center = 0.5,
            double tension = 1.0)
        {
            double x = Clamp01(t);
            if (x <= 0.0)
                return 0.0;

            if (x >= 1.0)
                return 1.0;

            double c = ClampCenter(center);
            double amount = Clamp01(plateauAmount);

            double maxHalfWidth = System.Math.Max(0.0, System.Math.Min(c, 1.0 - c) - MathUtil.Epsilon);
            double halfWidth = System.Math.Min(amount * 0.5, maxHalfWidth);

            double start = c - halfWidth;
            double end = c + halfWidth;

            if (x < start)
            {
                double u = start <= MathUtil.Epsilon ? 0.0 : x / start;
                return Clamp01(0.5 * ApplyTension(u, tension));
            }

            if (x > end)
            {
                double denom = 1.0 - end;
                double u = denom <= MathUtil.Epsilon ? 1.0 : (x - end) / denom;
                return Clamp01(0.5 + (0.5 * ApplyTension(u, tension)));
            }

            return 0.5;
        }

        /// <summary>
        /// Clamp a parameter into [0, 1].
        /// </summary>
        public static double Clamp01(double t)
        {
            return MathUtil.Clamp(t, 0.0, 1.0);
        }

        private static double ApplyCenterAndTension(double t, double center, double tension)
        {
            double x = Clamp01(t);
            double c = ClampCenter(center);

            double pivoted;
            if (x <= c)
            {
                pivoted = 0.5 * (x / c);
            }
            else
            {
                pivoted = 0.5 + (0.5 * ((x - c) / (1.0 - c)));
            }

            return ApplyTension(pivoted, tension);
        }

        private static double ApplyTension(double value, double tension)
        {
            double x = Clamp01(value);
            double k = ClampPositive(tension);

            if (System.Math.Abs(k - 1.0) <= MathUtil.Epsilon)
                return x;

            double a = System.Math.Pow(x, k);
            double b = System.Math.Pow(1.0 - x, k);
            double denom = a + b;

            if (double.IsNaN(denom) || double.IsInfinity(denom) || denom <= MathUtil.Epsilon)
                return x;

            return Clamp01(a / denom);
        }

        private static double ClampCenter(double center)
        {
            if (double.IsNaN(center) || double.IsInfinity(center))
                return 0.5;

            return MathUtil.Clamp(center, MathUtil.Epsilon, 1.0 - MathUtil.Epsilon);
        }

        private static double ClampPositive(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 1.0;

            return System.Math.Max(MathUtil.Epsilon, value);
        }
    }
}

