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
                resolvedSection.Channels?.NormalGChannels,
                resolvedSection.Channels?.NormalG,
                resolvedSection.NormalGChannel,
                resolvedSection.InterpolationMode,
                resolvedSection.EasingFunction,
                resolvedSection.TargetNormalG,
                resolvedSection.StartNormalG,
                resolvedSection.EndNormalG,
                snapshot.NormalizedT);

            double? targetLateralG = SampleChannel(
                resolvedSection.Channels?.LateralGChannels,
                resolvedSection.Channels?.LateralG,
                resolvedSection.LateralGChannel,
                resolvedSection.InterpolationMode,
                resolvedSection.EasingFunction,
                resolvedSection.TargetLateralG,
                resolvedSection.StartLateralG,
                resolvedSection.EndLateralG,
                snapshot.NormalizedT);

            double? targetRollRateDegPerSec = SampleDirectChannel(
                resolvedSection.Channels?.RollRateChannels,
                resolvedSection.Channels?.RollRate,
                resolvedSection.RollRateChannel,
                snapshot.NormalizedT);

            return new SampledForceTarget(
                distance,
                snapshot.NormalizedT,
                targetNormalG,
                targetLateralG,
                targetLongitudinalG: null,
                targetRollRateDegPerSec: targetRollRateDegPerSec);
        }

        private static double? SampleChannel(
            IReadOnlyList<IForceChannel>? v3Channels,
            IForceChannel? v2Channel,
            IForceEasingFunction? legacyChannel,
            ForceInterpolationMode mode,
            IForceEasingFunction? easingFunction,
            double? constantValue,
            double? startValue,
            double? endValue,
            double normalizedT)
        {
            if (HasChannels(v3Channels))
            {
                return SumChannels(v3Channels!, normalizedT);
            }

            if (v2Channel != null)
            {
                double? resolvedStart = startValue ?? constantValue;
                double? resolvedEnd = endValue ?? constantValue;

                if (!resolvedStart.HasValue || !resolvedEnd.HasValue)
                {
                    return null;
                }

                double adjustedT = v2Channel.Evaluate(normalizedT);
                return MathUtil.Lerp(resolvedStart.Value, resolvedEnd.Value, adjustedT);
            }

            if (legacyChannel != null)
            {
                double? resolvedStart = startValue ?? constantValue;
                double? resolvedEnd = endValue ?? constantValue;

                if (!resolvedStart.HasValue || !resolvedEnd.HasValue)
                {
                    return null;
                }

                double adjustedT = legacyChannel.Evaluate(normalizedT);
                return MathUtil.Lerp(resolvedStart.Value, resolvedEnd.Value, adjustedT);
            }

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

        private static double? SampleDirectChannel(
            IReadOnlyList<IForceChannel>? v3Channels,
            IForceChannel? v2Channel,
            IForceEasingFunction? legacyChannel,
            double normalizedT)
        {
            if (HasChannels(v3Channels))
            {
                return SumChannels(v3Channels!, normalizedT);
            }

            if (v2Channel != null)
            {
                return v2Channel.Evaluate(normalizedT);
            }

            if (legacyChannel != null)
            {
                return legacyChannel.Evaluate(normalizedT);
            }

            return null;
        }

        private static bool HasChannels(IReadOnlyList<IForceChannel>? channels)
        {
            return channels != null && channels.Count > 0;
        }

        private static double SumChannels(IReadOnlyList<IForceChannel> channels, double normalizedT)
        {
            double sum = 0.0;

            for (int i = 0; i < channels.Count; i++)
            {
                IForceChannel? channel = channels[i];

                if (channel is null)
                {
                    throw new InvalidOperationException("Force channel list cannot contain null entries.");
                }

                sum += channel.Evaluate(normalizedT);
            }

            return sum;
        }
    }
}
