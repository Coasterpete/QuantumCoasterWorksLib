using System;

namespace Quantum.Track
{
    /// <summary>
    /// Sample point for a normalized section function.
    /// </summary>
    public readonly struct SectionSample
    {
        /// <summary>
        /// Initializes a section sample.
        /// </summary>
        public SectionSample(double x, double value)
        {
            if (!IsFinite(x))
            {
                throw new ArgumentOutOfRangeException(nameof(x), x, "X must be finite.");
            }

            if (!IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be finite.");
            }

            X = x;
            Value = value;
        }

        /// <summary>
        /// Coordinate in the owning section domain.
        /// </summary>
        public double X { get; }

        /// <summary>
        /// Channel value at <see cref="X"/>.
        /// </summary>
        public double Value { get; }

        private static bool IsFinite(double value)
        {
            return !(double.IsNaN(value) || double.IsInfinity(value));
        }
    }
}
