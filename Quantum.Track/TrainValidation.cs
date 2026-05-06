using System;

namespace Quantum.Track
{
    internal static class TrainValidation
    {
        internal static void ValidateFiniteDouble(double value, string parameterName, string message)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, value, message);
            }
        }

        internal static void ValidatePositiveDouble(double value, string parameterName, string message)
        {
            ValidateFiniteDouble(value, parameterName, message);

            if (value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, message);
            }
        }

        internal static void ValidateNonNegativeDouble(double value, string parameterName, string message)
        {
            ValidateFiniteDouble(value, parameterName, message);

            if (value < 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, message);
            }
        }

        internal static void ValidatePositiveInt(int value, string parameterName, string message)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, message);
            }
        }
    }
}
