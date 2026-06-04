using System;

namespace Quantum.Core
{
    /// <summary>
    /// Provides shared argument validation helpers for Quantum backend foundation APIs.
    /// </summary>
    public static class Guard
    {
        /// <summary>
        /// Requires <paramref name="value"/> to be neither NaN nor infinity.
        /// </summary>
        /// <param name="value">The numeric value to validate.</param>
        /// <param name="paramName">The name of the argument represented by <paramref name="value"/>.</param>
        /// <param name="message">An optional exception message. When null, a default message is used.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="value"/> is NaN or infinity.
        /// </exception>
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
        /// <param name="value">The numeric value to validate.</param>
        /// <param name="paramName">The name of the argument represented by <paramref name="value"/>.</param>
        /// <param name="message">An optional exception message. When null, a default message is used.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="value"/> is NaN, infinity, or less than or equal to zero.
        /// </exception>
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
        /// <param name="value">The numeric value to validate.</param>
        /// <param name="paramName">The name of the argument represented by <paramref name="value"/>.</param>
        /// <param name="message">An optional exception message. When null, a default message is used.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="value"/> is NaN, infinity, or less than zero.
        /// </exception>
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
        /// <param name="value">The integer value to validate.</param>
        /// <param name="minInclusive">The inclusive minimum allowed value.</param>
        /// <param name="paramName">The name of the argument represented by <paramref name="value"/>.</param>
        /// <param name="message">An optional exception message. When null, a default message is used.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="value"/> is less than <paramref name="minInclusive"/>.
        /// </exception>
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
