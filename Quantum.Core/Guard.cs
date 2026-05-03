using System;

namespace Quantum.Core
{
    public static class Guard
    {
        public static void RequireFinite(double value, string paramName, string? message = null)
        {
            if (!Numeric.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    value,
                    message ?? "Value must be finite.");
            }
        }

        public static void RequirePositiveFinite(double value, string paramName, string? message = null)
        {
            if (!Numeric.IsFinite(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    value,
                    message ?? "Value must be positive and finite.");
            }
        }

        public static void RequireNonNegativeFinite(double value, string paramName, string? message = null)
        {
            if (!Numeric.IsFinite(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    value,
                    message ?? "Value must be non-negative and finite.");
            }
        }

        public static void RequireAtLeast(int value, int minInclusive, string paramName, string? message = null)
        {
            if (value < minInclusive)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    value,
                    message ?? $"Value must be at least {minInclusive}.");
            }
        }
    }
}
