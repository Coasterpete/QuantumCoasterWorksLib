using System;

namespace Quantum.Track
{
    /// <summary>
    /// Evaluated value for one normalized section channel.
    /// </summary>
    public readonly struct SectionChannelEvaluation
    {
        /// <summary>
        /// Initializes a channel evaluation result.
        /// </summary>
        public SectionChannelEvaluation(SectionChannel channel, double value)
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

        /// <summary>
        /// Evaluated channel.
        /// </summary>
        public SectionChannel Channel { get; }

        /// <summary>
        /// Evaluated channel value.
        /// </summary>
        public double Value { get; }

        private static bool IsFinite(double value)
        {
            return !(double.IsNaN(value) || double.IsInfinity(value));
        }
    }
}
