namespace Quantum.FVD
{
    public readonly struct FvdSectionSample
    {
        public double X { get; }

        public double Value { get; }

        public FvdSectionSample(double x, double value)
        {
            X = x;
            Value = value;
        }
    }
}
