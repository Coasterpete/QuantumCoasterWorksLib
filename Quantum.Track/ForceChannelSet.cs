namespace Quantum.Track
{
    public sealed class ForceChannelSet
    {
        public IForceChannel? NormalG { get; set; }

        public IForceChannel? LateralG { get; set; }

        public IForceChannel? RollRate { get; set; }
    }
}
