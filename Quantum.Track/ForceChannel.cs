using System;

namespace Quantum.Track
{
    public sealed class ForceChannel : IForceChannel
    {
        private readonly IForceEasingFunction _easing;

        public ForceChannel(IForceEasingFunction easing)
        {
            _easing = easing ?? throw new ArgumentNullException(nameof(easing));
        }

        public double Evaluate(double t)
        {
            return _easing.Evaluate(t);
        }
    }
}
