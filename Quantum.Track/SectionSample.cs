namespace Quantum.Track
{
    public readonly struct SectionSample
    {
        public SectionSample(double x, double value)
        {
            X = x;
            Value = value;
        }

        public double X { get; }

        public double Value { get; }
    }
}
