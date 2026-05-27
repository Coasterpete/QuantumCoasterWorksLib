using System;

namespace Quantum.Core
{
    /// <summary>
    /// Numeric predicates shared by backend validation code.
    /// </summary>
    public static class Numeric
    {
        /// <summary>
        /// Returns whether <paramref name="value"/> is neither NaN nor infinity.
        /// </summary>
        public static bool IsFinite(double value)
        {
            return !(double.IsNaN(value) || double.IsInfinity(value));
        }
    }
}
