using System;

namespace Quantum.Core
{
    public static class Numeric
    {
        public static bool IsFinite(double value)
        {
            return !(double.IsNaN(value) || double.IsInfinity(value));
        }
    }
}
