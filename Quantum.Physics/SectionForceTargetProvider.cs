using System;
using System.Collections.Generic;
using Quantum.Track;

namespace Quantum.Physics
{
    /// <summary>
    /// Adapter that exposes resolved force sections through physics force target provider contracts.
    /// </summary>
    public sealed class SectionForceTargetProvider : IForceTargetProvider
    {
        private readonly IReadOnlyList<ResolvedSectionInterval<ForceSection>> _resolvedIntervals;

        public SectionForceTargetProvider(IReadOnlyList<ResolvedSectionInterval<ForceSection>> resolvedIntervals)
        {
            _resolvedIntervals = resolvedIntervals ?? throw new ArgumentNullException(nameof(resolvedIntervals));
        }

        public SampledForceTarget Sample(double distance)
        {
            return ForceTargetSampler.Sample(_resolvedIntervals, distance);
        }

        public bool TryGetForceTargets(double x, out ForceTargets targets)
        {
            SampledForceTarget sampled = Sample(x);

            if (!sampled.TargetNormalG.HasValue)
            {
                targets = default;
                return false;
            }

            targets = new ForceTargets(
                sampled.TargetNormalG.Value,
                sampled.TargetLateralG ?? 0.0,
                rollRateDegPerSec: 0.0);
            return true;
        }
    }
}
