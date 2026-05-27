using System;

namespace Quantum.Track
{
    /// <summary>
    /// Lightweight <see cref="IForceChannel"/> adapter over an <see cref="IForceEasingFunction"/>.
    /// </summary>
    public sealed class ForceChannel : IForceChannel
    {
        private readonly IForceEasingFunction _easing;

        /// <summary>
        /// Initializes a force channel from an easing function.
        /// </summary>
        public ForceChannel(IForceEasingFunction easing)
        {
            _easing = easing ?? throw new ArgumentNullException(nameof(easing));
        }

        /// <inheritdoc />
        public double Evaluate(double t)
        {
            return _easing.Evaluate(t);
        }
    }
}
