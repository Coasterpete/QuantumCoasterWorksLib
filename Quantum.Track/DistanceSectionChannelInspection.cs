using System;

namespace Quantum.Track
{
    /// <summary>
    /// Immutable UI-friendly snapshot of an evaluated section channel.
    /// </summary>
    public readonly struct DistanceSectionChannelInspection
    {
        public DistanceSectionChannelInspection(SectionChannel channel, double value)
        {
            if (!Enum.IsDefined(typeof(SectionChannel), channel))
            {
                throw new ArgumentOutOfRangeException(nameof(channel), channel, "Unsupported section channel.");
            }

            if (!IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be finite.");
            }

            Channel = channel;
            Value = value;
        }

        public SectionChannel Channel { get; }

        public double Value { get; }

        private static bool IsFinite(double value)
        {
            return !(double.IsNaN(value) || double.IsInfinity(value));
        }
    }
}
