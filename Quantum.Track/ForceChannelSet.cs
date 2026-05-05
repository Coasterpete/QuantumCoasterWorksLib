using System;
using System.Collections.Generic;

namespace Quantum.Track
{
    public sealed class ForceChannelSet
    {
        private IReadOnlyList<IForceChannel>? _normalGChannels;
        private IReadOnlyList<IForceChannel>? _lateralGChannels;
        private IReadOnlyList<IForceChannel>? _rollRateChannels;

        public IForceChannel? NormalG { get; set; }

        public IForceChannel? LateralG { get; set; }

        public IForceChannel? RollRate { get; set; }

        public ForceChannelDomain? Domain { get; set; }

        public IReadOnlyList<IForceChannel>? NormalGChannels
        {
            get => _normalGChannels;
            set => _normalGChannels = ValidateChannels(value, nameof(NormalGChannels));
        }

        public IReadOnlyList<IForceChannel>? LateralGChannels
        {
            get => _lateralGChannels;
            set => _lateralGChannels = ValidateChannels(value, nameof(LateralGChannels));
        }

        public IReadOnlyList<IForceChannel>? RollRateChannels
        {
            get => _rollRateChannels;
            set => _rollRateChannels = ValidateChannels(value, nameof(RollRateChannels));
        }

        public ForceChannelBlendMode NormalGBlendMode { get; set; } = ForceChannelBlendMode.Sum;

        public ForceChannelBlendMode LateralGBlendMode { get; set; } = ForceChannelBlendMode.Sum;

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
