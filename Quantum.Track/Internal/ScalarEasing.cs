using System;

namespace Quantum.Track.Internal
{
    internal enum ScalarEasingMode
    {
        Constant = 0,
        Linear = 1,
        SmoothStep = 2,
        Quadratic = 3,
        Cubic = 4,
        Quartic = 5,
        Quintic = 6,
        Sinusoidal = 7
    }

    internal static class ScalarEasing
    {
        internal static double Evaluate(double t, ScalarEasingMode mode)
        {
            switch (mode)
            {
                case ScalarEasingMode.Constant:
                    return 0.0;

                case ScalarEasingMode.Linear:
                    return t;

                case ScalarEasingMode.SmoothStep:
                    return t * t * (3.0 - (2.0 * t));

                case ScalarEasingMode.Quadratic:
                    return t * t;

                case ScalarEasingMode.Cubic:
                    return t * t * t;

                case ScalarEasingMode.Quartic:
                    return t * t * t * t;

                case ScalarEasingMode.Quintic:
                    return t * t * t * t * t;

                case ScalarEasingMode.Sinusoidal:
                    return 1.0 - System.Math.Cos(t * (System.Math.PI / 2.0));

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(mode),
                        mode,
                        "Unsupported scalar easing mode.");
            }
        }

        internal static double Interpolate(double start, double end, double t, ScalarEasingMode mode)
        {
            return start + ((end - start) * Evaluate(t, mode));
        }

        internal static ScalarEasingMode MapForceInterpolationMode(ForceInterpolationMode mode)
        {
            if (TryMapForceInterpolationMode(mode, out ScalarEasingMode scalarMode))
            {
                return scalarMode;
            }

            throw new ArgumentOutOfRangeException(
                nameof(mode),
                mode,
                "Unsupported force interpolation mode.");
        }

        internal static bool TryMapForceInterpolationMode(
            ForceInterpolationMode mode,
            out ScalarEasingMode scalarMode)
        {
            switch (mode)
            {
                case ForceInterpolationMode.Constant:
                    scalarMode = ScalarEasingMode.Constant;
                    return true;

                case ForceInterpolationMode.Linear:
                    scalarMode = ScalarEasingMode.Linear;
                    return true;

                case ForceInterpolationMode.SmoothStep:
                    scalarMode = ScalarEasingMode.SmoothStep;
                    return true;

                case ForceInterpolationMode.Quadratic:
                    scalarMode = ScalarEasingMode.Quadratic;
                    return true;

                case ForceInterpolationMode.Cubic:
                    scalarMode = ScalarEasingMode.Cubic;
                    return true;

                case ForceInterpolationMode.Quartic:
                    scalarMode = ScalarEasingMode.Quartic;
                    return true;

                case ForceInterpolationMode.Quintic:
                    scalarMode = ScalarEasingMode.Quintic;
                    return true;

                case ForceInterpolationMode.Sinusoidal:
                    scalarMode = ScalarEasingMode.Sinusoidal;
                    return true;

                default:
                    scalarMode = default;
                    return false;
            }
        }

        internal static bool IsSupported(ForceInterpolationMode mode)
        {
            return TryMapForceInterpolationMode(mode, out _);
        }

        internal static ScalarEasingMode MapBankingProfileInterpolationMode(
            BankingProfileInterpolationMode mode)
        {
            if (TryMapBankingProfileInterpolationMode(mode, out ScalarEasingMode scalarMode))
            {
                return scalarMode;
            }

            throw new ArgumentOutOfRangeException(
                nameof(mode),
                mode,
                "Unsupported banking profile interpolation mode.");
        }

        internal static bool TryMapBankingProfileInterpolationMode(
            BankingProfileInterpolationMode mode,
            out ScalarEasingMode scalarMode)
        {
            switch (mode)
            {
                case BankingProfileInterpolationMode.Constant:
                    scalarMode = ScalarEasingMode.Constant;
                    return true;

                case BankingProfileInterpolationMode.Linear:
                    scalarMode = ScalarEasingMode.Linear;
                    return true;

                case BankingProfileInterpolationMode.SmoothStep:
                    scalarMode = ScalarEasingMode.SmoothStep;
                    return true;

                case BankingProfileInterpolationMode.Quadratic:
                    scalarMode = ScalarEasingMode.Quadratic;
                    return true;

                case BankingProfileInterpolationMode.Cubic:
                    scalarMode = ScalarEasingMode.Cubic;
                    return true;

                case BankingProfileInterpolationMode.Quartic:
                    scalarMode = ScalarEasingMode.Quartic;
                    return true;

                case BankingProfileInterpolationMode.Quintic:
                    scalarMode = ScalarEasingMode.Quintic;
                    return true;

                case BankingProfileInterpolationMode.Sinusoidal:
                    scalarMode = ScalarEasingMode.Sinusoidal;
                    return true;

                default:
                    scalarMode = default;
                    return false;
            }
        }

        internal static bool IsSupported(BankingProfileInterpolationMode mode)
        {
            return TryMapBankingProfileInterpolationMode(mode, out _);
        }
    }
}
