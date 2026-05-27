namespace Quantum.Track
{
    /// <summary>
    /// Coaster-domain force target section attached to a track document or resolved interval.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The scalar fields on this type are compatibility shorthand. During normalization,
    /// they become channel functions only when no newer channel representation exists for
    /// the same force component.
    /// </para>
    /// <para>
    /// Each force component resolves independently. Precedence is: a non-empty
    /// <see cref="ForceChannelSet"/> plural list such as <see cref="ForceChannelSet.NormalGChannels"/>,
    /// then the matching single channel on <see cref="Channels"/>, then the legacy
    /// <see cref="IForceEasingFunction"/> channel property, then scalar target/start/end fields.
    /// Empty plural lists intentionally fall through to the single-channel or scalar path.
    /// </para>
    /// <para>
    /// For normal, lateral, and longitudinal G, plural channel lists evaluate to direct
    /// target values that are blended by the channel-set blend mode. Single channel
    /// properties are normalized-t remappers over the scalar target/start/end values.
    /// Roll rate has no scalar compatibility fields, so its single and plural channels
    /// evaluate direct roll-rate targets.
    /// </para>
    /// <para>
    /// <see cref="Domain"/> is section-wide. <see cref="ForceChannelSet.Domain"/> overrides
    /// it when present. Distance is the default domain, and elapsed-time sampling remains
    /// an explicit opt-in call path.
    /// </para>
    /// </remarks>
    public sealed class ForceSection : TrackSection
    {
        /// <summary>
        /// Initializes a force section using compatibility scalar fields and optional legacy channels.
        /// </summary>
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

        /// <summary>
        /// Constant normal-G target used by the scalar compatibility path.
        /// </summary>
        public double? TargetNormalG { get; }

        /// <summary>
        /// Constant lateral-G target used by the scalar compatibility path.
        /// </summary>
        public double? TargetLateralG { get; }

        /// <summary>
        /// Constant longitudinal-G target used by the scalar compatibility path.
        /// </summary>
        public double? TargetLongitudinalG { get; }

        /// <summary>
        /// Authoring length for this force section. Resolved intervals provide the actual
        /// normalized section start and end distances.
        /// </summary>
        public double? Length { get; }

        /// <summary>
        /// Duration used by explicit elapsed-time sampling for time-domain force sections.
        /// </summary>
        public double? Duration { get; }

        /// <summary>
        /// Scalar compatibility interpolation mode used when no channel override exists.
        /// </summary>
        public ForceInterpolationMode InterpolationMode { get; }

        /// <summary>
        /// Scalar compatibility normal-G start value.
        /// </summary>
        public double? StartNormalG { get; }

        /// <summary>
        /// Scalar compatibility normal-G end value.
        /// </summary>
        public double? EndNormalG { get; }

        /// <summary>
        /// Scalar compatibility lateral-G start value.
        /// </summary>
        public double? StartLateralG { get; }

        /// <summary>
        /// Scalar compatibility lateral-G end value.
        /// </summary>
        public double? EndLateralG { get; }

        /// <summary>
        /// Scalar compatibility longitudinal-G start value.
        /// </summary>
        public double? StartLongitudinalG { get; }

        /// <summary>
        /// Scalar compatibility longitudinal-G end value.
        /// </summary>
        public double? EndLongitudinalG { get; }

        /// <summary>
        /// Optional easing applied by the scalar compatibility interpolation path.
        /// </summary>
        public IForceEasingFunction? EasingFunction { get; }

        /// <summary>
        /// Legacy normal-G normalized-t channel. Used only when no matching channel-set
        /// entry exists, and only when scalar normal-G values can be resolved.
        /// </summary>
        public IForceEasingFunction? NormalGChannel { get; }

        /// <summary>
        /// Legacy lateral-G normalized-t channel. Used only when no matching channel-set
        /// entry exists, and only when scalar lateral-G values can be resolved.
        /// </summary>
        public IForceEasingFunction? LateralGChannel { get; }

        /// <summary>
        /// Legacy direct roll-rate target channel in degrees per second.
        /// </summary>
        public IForceEasingFunction? RollRateChannel { get; }

        /// <summary>
        /// Legacy longitudinal-G normalized-t channel. Used only when no matching channel-set
        /// entry exists, and only when scalar longitudinal-G values can be resolved.
        /// </summary>
        public IForceEasingFunction? LongitudinalGChannel { get; }

        /// <summary>
        /// Section sampling domain. This is overridden by <see cref="ForceChannelSet.Domain"/>
        /// when <see cref="Channels"/> is present and its domain is set.
        /// </summary>
        public ForceChannelDomain Domain { get; }

        /// <summary>
        /// Optional normalized channel container. Entries here take precedence over legacy
        /// channel properties and scalar compatibility fields for their matching force component.
        /// </summary>
        public ForceChannelSet? Channels { get; set; }
    }
}
