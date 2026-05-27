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
    }
}
