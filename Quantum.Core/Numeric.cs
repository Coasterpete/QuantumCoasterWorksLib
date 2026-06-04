using System;

namespace Quantum.Core
{
    /// <summary>
    /// Provides numeric predicates shared by backend validation code.
    /// </summary>
    public static class Numeric
    {
        /// <summary>
        /// Returns whether <paramref name="value"/> is neither NaN nor infinity.
        /// </summary>
        /// <param name="value">The numeric value to test.</param>
        /// <returns>
        /// True when <paramref name="value"/> is finite; otherwise, false.
        /// </returns>
        public static bool IsFinite(double value)
        {
            return !(double.IsNaN(value) || double.IsInfinity(value));
        }
    }
}
