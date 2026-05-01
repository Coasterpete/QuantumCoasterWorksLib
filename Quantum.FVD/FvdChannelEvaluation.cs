namespace Quantum.FVD
{
    public readonly struct FvdChannelEvaluation
    {
        public FvdSectionChannel Channel { get; }

        public double Value { get; }

        public FvdChannelEvaluation(FvdSectionChannel channel, double value)
        {
            Channel = channel;
            Value = value;
        }
    }
}
