using System;
using System.Collections.Generic;

namespace Quantum.Track
{
    public static class ForceTargetSampler
    {
        public static SampledForceTarget Sample(
            IEnumerable<(ForceSection Section, double Length)> sections,
            double distance)
        {
            ForceTargetSnapshot snapshot = ForceTargetResolver.Lookup(sections, distance);
            return BuildFromSnapshot(snapshot, distance);
        }

        public static SampledForceTarget Sample(
            IReadOnlyList<ResolvedSectionInterval<ForceSection>> resolvedIntervals,
            double distance)
        {
            ForceTargetSnapshot snapshot = ForceTargetResolver.Lookup(resolvedIntervals, distance);
            return BuildFromSnapshot(snapshot, distance);
        }

        private static SampledForceTarget BuildFromSnapshot(
            ForceTargetSnapshot snapshot,
            double distance)
        {
            if (double.IsNaN(distance) || double.IsInfinity(distance))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(distance),
                    distance,
                    "Distance must be finite.");
            }

            if (distance < snapshot.StartDistance || distance > snapshot.EndDistance)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(distance),
                    distance,
                    $"Distance must be within [{snapshot.StartDistance}, {snapshot.EndDistance}].");
            }

            ForceSection resolvedSection = snapshot.ResolvedSection
                ?? throw new InvalidOperationException("ForceTargetSnapshot.ResolvedSection cannot be null.");

            return new SampledForceTarget(
                distance,
                snapshot.NormalizedT,
                resolvedSection.TargetNormalG,
                resolvedSection.TargetLateralG,
                targetLongitudinalG: null);
        }
    }
}
