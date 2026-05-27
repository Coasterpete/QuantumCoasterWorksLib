using System;
using System.Collections.Generic;

namespace Quantum.Track
{
    /// <summary>
    /// Optional channel container for a <see cref="ForceSection"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Channel entries are resolved per force component. Non-empty plural channel lists
    /// take precedence over the matching single channel property. Null or empty plural
    /// lists do not mask lower-precedence compatibility paths.
    /// </para>
    /// <para>
    /// For normal, lateral, and longitudinal G, plural lists are direct target-value
    /// channels and single channel properties are normalized-t remappers over the
    /// section scalar values. Roll-rate channels always evaluate direct roll-rate
    /// targets because there are no scalar roll-rate compatibility fields.
    /// </para>
    /// </remarks>
    public sealed class ForceChannelSet
    {
        private IReadOnlyList<IForceChannel>? _normalGChannels;
        private IReadOnlyList<IForceChannel>? _lateralGChannels;
        private IReadOnlyList<IForceChannel>? _longitudinalGChannels;
        private IReadOnlyList<IForceChannel>? _rollRateChannels;

        /// <summary>
        /// Single normal-G normalized-t channel used when <see cref="NormalGChannels"/>
        /// is null or empty and scalar normal-G values can be resolved.
        /// </summary>
        public IForceChannel? NormalG { get; set; }

        /// <summary>
        /// Single lateral-G normalized-t channel used when <see cref="LateralGChannels"/>
        /// is null or empty and scalar lateral-G values can be resolved.
        /// </summary>
        public IForceChannel? LateralG { get; set; }

        /// <summary>
        /// Single longitudinal-G normalized-t channel used when <see cref="LongitudinalGChannels"/>
        /// is null or empty and scalar longitudinal-G values can be resolved.
        /// </summary>
        public IForceChannel? LongitudinalG { get; set; }

        /// <summary>
        /// Single direct roll-rate target channel in degrees per second.
        /// </summary>
        public IForceChannel? RollRate { get; set; }

        /// <summary>
        /// Optional section-wide domain override for this channel set.
        /// </summary>
        public ForceChannelDomain? Domain { get; set; }

        /// <summary>
        /// Direct normal-G target channels. When non-empty, these override the single
        /// normal-G channel and all scalar normal-G compatibility fields.
        /// </summary>
        public IReadOnlyList<IForceChannel>? NormalGChannels
        {
            get => _normalGChannels;
            set => _normalGChannels = ValidateChannels(value, nameof(NormalGChannels));
        }

        /// <summary>
        /// Direct lateral-G target channels. When non-empty, these override the single
        /// lateral-G channel and all scalar lateral-G compatibility fields.
        /// </summary>
        public IReadOnlyList<IForceChannel>? LateralGChannels
        {
            get => _lateralGChannels;
            set => _lateralGChannels = ValidateChannels(value, nameof(LateralGChannels));
        }

        /// <summary>
        /// Direct longitudinal-G target channels. When non-empty, these override the single
        /// longitudinal-G channel and all scalar longitudinal-G compatibility fields.
        /// </summary>
        public IReadOnlyList<IForceChannel>? LongitudinalGChannels
        {
            get => _longitudinalGChannels;
            set => _longitudinalGChannels = ValidateChannels(value, nameof(LongitudinalGChannels));
        }

        /// <summary>
        /// Direct roll-rate target channels in degrees per second. When non-empty, these
        /// override <see cref="RollRate"/>.
        /// </summary>
        public IReadOnlyList<IForceChannel>? RollRateChannels
        {
            get => _rollRateChannels;
            set => _rollRateChannels = ValidateChannels(value, nameof(RollRateChannels));
        }

        /// <summary>
        /// Blend mode for <see cref="NormalGChannels"/> when that list is non-empty.
        /// </summary>
        public ForceChannelBlendMode NormalGBlendMode { get; set; } = ForceChannelBlendMode.Sum;

        /// <summary>
        /// Blend mode for <see cref="LateralGChannels"/> when that list is non-empty.
        /// </summary>
        public ForceChannelBlendMode LateralGBlendMode { get; set; } = ForceChannelBlendMode.Sum;

        /// <summary>
        /// Blend mode for <see cref="LongitudinalGChannels"/> when that list is non-empty.
        /// </summary>
        public ForceChannelBlendMode LongitudinalGBlendMode { get; set; } = ForceChannelBlendMode.Sum;

        /// <summary>
        /// Blend mode for <see cref="RollRateChannels"/> when that list is non-empty.
        /// </summary>
        public ForceChannelBlendMode RollRateBlendMode { get; set; } = ForceChannelBlendMode.Sum;

        private static IReadOnlyList<IForceChannel>? ValidateChannels(
            IReadOnlyList<IForceChannel>? channels,
            string paramName)
        {
            if (channels is null)
            {
                return null;
            }

            if (channels.Count == 0)
            {
                return Array.Empty<IForceChannel>();
            }

            var copy = new IForceChannel[channels.Count];

            for (int i = 0; i < channels.Count; i++)
            {
                IForceChannel? channel = channels[i];

                if (channel is null)
                {
                    throw new ArgumentException(
                        "Channel list cannot contain null entries.",
                        paramName);
                }

                copy[i] = channel;
            }

            return copy;
        }
    }
}
