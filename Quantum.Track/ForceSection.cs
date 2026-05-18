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
            double? endLateralG = null,
            IForceEasingFunction? easingFunction = null,
            IForceEasingFunction? normalGChannel = null,
            IForceEasingFunction? lateralGChannel = null,
            IForceEasingFunction? rollRateChannel = null,
            ForceChannelDomain domain = ForceChannelDomain.Distance,
            double? targetLongitudinalG = null,
            double? startLongitudinalG = null,
            double? endLongitudinalG = null,
            IForceEasingFunction? longitudinalGChannel = null)
        {
            TargetNormalG = targetNormalG;
            TargetLateralG = targetLateralG;
            TargetLongitudinalG = targetLongitudinalG;
            Length = length;
            Duration = duration;
            InterpolationMode = interpolationMode;
            StartNormalG = startNormalG;
            EndNormalG = endNormalG;
            StartLateralG = startLateralG;
            EndLateralG = endLateralG;
            StartLongitudinalG = startLongitudinalG;
            EndLongitudinalG = endLongitudinalG;
            EasingFunction = easingFunction;
            NormalGChannel = normalGChannel;
            LateralGChannel = lateralGChannel;
            RollRateChannel = rollRateChannel;
            LongitudinalGChannel = longitudinalGChannel;
            Domain = domain;
        }

        public double? TargetNormalG { get; }

        public double? TargetLateralG { get; }

        public double? TargetLongitudinalG { get; }

        public double? Length { get; }

        public double? Duration { get; }

        public ForceInterpolationMode InterpolationMode { get; }

        public double? StartNormalG { get; }

        public double? EndNormalG { get; }

        public double? StartLateralG { get; }

        public double? EndLateralG { get; }

        public double? StartLongitudinalG { get; }

        public double? EndLongitudinalG { get; }

        public IForceEasingFunction? EasingFunction { get; }

        public IForceEasingFunction? NormalGChannel { get; }

        public IForceEasingFunction? LateralGChannel { get; }

        public IForceEasingFunction? RollRateChannel { get; }

        public IForceEasingFunction? LongitudinalGChannel { get; }

        public ForceChannelDomain Domain { get; }

        public ForceChannelSet? Channels { get; set; }
    }
}
