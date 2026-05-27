using System;

namespace Quantum.Core
{
    /// <summary>
    /// Shared argument validation helpers for small backend foundation APIs.
    /// </summary>
    public static class Guard
    {
        /// <summary>
        /// Requires <paramref name="value"/> to be neither NaN nor infinity.
        /// </summary>
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

        /// <summary>
        /// Requires <paramref name="value"/> to be finite and greater than zero.
        /// </summary>
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

        /// <summary>
        /// Requires <paramref name="value"/> to be finite and greater than or equal to zero.
        /// </summary>
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

        /// <summary>
        /// Requires <paramref name="value"/> to be at least <paramref name="minInclusive"/>.
        /// </summary>
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
