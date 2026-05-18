namespace Quantum.Track
{
    public readonly struct SectionChannelEvaluation
    {
        public SectionChannelEvaluation(SectionChannel channel, double value)
        {
            Channel = channel;
            Value = value;
        }

        public SectionChannel Channel { get; }

        public double Value { get; }
    }
}
