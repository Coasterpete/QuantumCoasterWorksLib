using System;

namespace Quantum.Track
{
    public readonly struct SampledForceTarget
    {
        public SampledForceTarget(
            double distance,
            double normalizedT,
            double? targetNormalG,
            double? targetLateralG,
            double? targetLongitudinalG = null)
        {
            if (double.IsNaN(distance) || double.IsInfinity(distance))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(distance),
                    distance,
                    "Distance must be finite.");
            }

            if (double.IsNaN(normalizedT) || double.IsInfinity(normalizedT) || normalizedT < 0.0 || normalizedT > 1.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(normalizedT),
                    normalizedT,
                    "NormalizedT must be finite and within [0, 1].");
            }

            Distance = distance;
            NormalizedT = normalizedT;
            TargetNormalG = targetNormalG;
            TargetLateralG = targetLateralG;
            TargetLongitudinalG = targetLongitudinalG;
        }

        public double Distance { get; }

        public double NormalizedT { get; }

        public double? TargetNormalG { get; }

        public double? TargetLateralG { get; }

        public double? TargetLongitudinalG { get; }
    }
}
