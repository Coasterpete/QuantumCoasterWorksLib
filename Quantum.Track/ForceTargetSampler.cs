using System;
using System.Collections.Generic;
using Quantum.Math;

namespace Quantum.Track
{
    public static class ForceTargetSampler
    {
        private static readonly ForceInterpolationEvaluator InterpolationEvaluator = new ForceInterpolationEvaluator();

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

            double? targetNormalG = SampleChannel(
                resolvedSection.InterpolationMode,
                resolvedSection.EasingFunction,
                resolvedSection.TargetNormalG,
                resolvedSection.StartNormalG,
                resolvedSection.EndNormalG,
                snapshot.NormalizedT);

            double? targetLateralG = SampleChannel(
                resolvedSection.InterpolationMode,
                resolvedSection.EasingFunction,
                resolvedSection.TargetLateralG,
                resolvedSection.StartLateralG,
                resolvedSection.EndLateralG,
                snapshot.NormalizedT);

            return new SampledForceTarget(
                distance,
                snapshot.NormalizedT,
                targetNormalG,
                targetLateralG,
                targetLongitudinalG: null);
        }

        private static double? SampleChannel(
            ForceInterpolationMode mode,
            IForceEasingFunction? easingFunction,
            double? constantValue,
            double? startValue,
            double? endValue,
            double normalizedT)
        {
            if (easingFunction is null && mode == ForceInterpolationMode.Constant)
            {
                return constantValue;
            }

            switch (mode)
            {
                case ForceInterpolationMode.Constant:
                case ForceInterpolationMode.Linear:
                case ForceInterpolationMode.SmoothStep:
                case ForceInterpolationMode.Quadratic:
                case ForceInterpolationMode.Cubic:
                case ForceInterpolationMode.Quartic:
                case ForceInterpolationMode.Quintic:
                case ForceInterpolationMode.Sinusoidal:
                    double? resolvedStart = startValue ?? constantValue;
                    double? resolvedEnd = endValue ?? constantValue;

                    if (!resolvedStart.HasValue || !resolvedEnd.HasValue)
                    {
                        return null;
                    }

                    double adjustedT = easingFunction?.Evaluate(normalizedT)
                        ?? InterpolationEvaluator.Evaluate(normalizedT, mode);

                    return MathUtil.Lerp(resolvedStart.Value, resolvedEnd.Value, adjustedT);

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(mode),
                        mode,
                        "Unsupported force interpolation mode.");
            }
        }
    }
}
