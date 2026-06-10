using System;

namespace Quantum.Track.Authoring
{
    internal static class AuthoringValidation
    {
        public static string RequireId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Section ID cannot be null, empty, or whitespace.", nameof(id));
            }

            return id;
        }

        public static double RequirePositiveFinite(double value, string paramName, string label)
        {
            if (!IsFinite(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    value,
                    $"{label} must be finite and greater than zero.");
            }

            return value;
        }

        public static double RequireFinite(double value, string paramName, string label)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    value,
                    $"{label} must be finite.");
            }

            return value;
        }

        public static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
