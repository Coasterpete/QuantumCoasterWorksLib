using System;

namespace Quantum.Track
{
    public readonly struct ForceTargetSnapshot
    {
        public ForceTargetSnapshot(
            ForceSection resolvedSection,
            double startDistance,
            double endDistance,
            double localDistance,
            double normalizedT)
        {
            if (resolvedSection is null)
            {
                throw new ArgumentNullException(nameof(resolvedSection));
            }

            ResolvedSection = resolvedSection;
            StartDistance = startDistance;
            EndDistance = endDistance;
            LocalDistance = localDistance;
            NormalizedT = normalizedT;
        }

        public ForceSection ResolvedSection { get; }

        public double StartDistance { get; }

        public double EndDistance { get; }

        public double LocalDistance { get; }

        public double NormalizedT { get; }
    }
}
