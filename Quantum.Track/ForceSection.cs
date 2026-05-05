namespace Quantum.Track
{
    public sealed class ForceSection : TrackSection
    {
        public ForceSection(
            double? targetNormalG = null,
            double? targetLateralG = null,
            double? length = null,
            double? duration = null)
        {
            TargetNormalG = targetNormalG;
            TargetLateralG = targetLateralG;
            Length = length;
            Duration = duration;
        }

        public double? TargetNormalG { get; }

        public double? TargetLateralG { get; }

        public double? Length { get; }

        public double? Duration { get; }
    }
}
