namespace Quantum.Track
{
    public sealed class ForceSection : TrackSection
    {
        public ForceSection(
            double? targetNormalG = null,
            double? targetLateralG = null,
            double? length = null,
            double? duration = null,
            ForceInterpolationMode interpolationMode = ForceInterpolationMode.Constant,
            double? startNormalG = null,
            double? endNormalG = null,
            double? startLateralG = null,
            double? endLateralG = null)
        {
            TargetNormalG = targetNormalG;
            TargetLateralG = targetLateralG;
            Length = length;
            Duration = duration;
            InterpolationMode = interpolationMode;
            StartNormalG = startNormalG;
            EndNormalG = endNormalG;
            StartLateralG = startLateralG;
            EndLateralG = endLateralG;
        }

        public double? TargetNormalG { get; }

        public double? TargetLateralG { get; }

        public double? Length { get; }

        public double? Duration { get; }

        public ForceInterpolationMode InterpolationMode { get; }

        public double? StartNormalG { get; }

        public double? EndNormalG { get; }

        public double? StartLateralG { get; }

        public double? EndLateralG { get; }
    }
}
