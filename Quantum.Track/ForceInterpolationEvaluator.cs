using System;

namespace Quantum.Track
{
    public sealed class ForceInterpolationEvaluator
    {
        public double Evaluate(double t, ForceInterpolationMode mode)
        {
            switch (mode)
            {
                case ForceInterpolationMode.Constant:
                    return 0.0;

                case ForceInterpolationMode.Linear:
                    return t;

                case ForceInterpolationMode.SmoothStep:
                    return t * t * (3.0 - (2.0 * t));

                case ForceInterpolationMode.Quadratic:
                    return t * t;

                case ForceInterpolationMode.Cubic:
                    return t * t * t;

                case ForceInterpolationMode.Quartic:
                    return t * t * t * t;

                case ForceInterpolationMode.Quintic:
                    return t * t * t * t * t;

                case ForceInterpolationMode.Sinusoidal:
                    return 1.0 - System.Math.Cos(t * (System.Math.PI / 2.0));

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(mode),
                        mode,
                        "Unsupported force interpolation mode.");
            }
        }
    }
}
