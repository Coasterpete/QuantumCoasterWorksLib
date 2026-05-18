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
            IEnumerable<(ForceSection Section, double Length)> sections,
            double distance,
            double elapsedTime)
        {
            ForceTargetSnapshot snapshot = ForceTargetResolver.Lookup(sections, distance);
            return BuildFromSnapshot(snapshot, distance, elapsedTime, useTimeDomainSampling: true);
        }

        public static SampledForceTarget Sample(
            IReadOnlyList<ResolvedSectionInterval<ForceSection>> resolvedIntervals,
            double distance)
        {
            ForceTargetSnapshot snapshot = ForceTargetResolver.Lookup(resolvedIntervals, distance);
            return BuildFromSnapshot(snapshot, distance);
        }

        public static SampledForceTarget Sample(
            IReadOnlyList<ResolvedSectionInterval<ForceSection>> resolvedIntervals,
            double distance,
            double elapsedTime)
        {
            ForceTargetSnapshot snapshot = ForceTargetResolver.Lookup(resolvedIntervals, distance);
            return BuildFromSnapshot(snapshot, distance, elapsedTime, useTimeDomainSampling: true);
        }

        private static SampledForceTarget BuildFromSnapshot(
            ForceTargetSnapshot snapshot,
            double distance)
        {
            return BuildFromSnapshot(snapshot, distance, elapsedTime: 0.0, useTimeDomainSampling: false);
        }

        private static SampledForceTarget BuildFromSnapshot(
            ForceTargetSnapshot snapshot,
            double distance,
            double elapsedTime,
            bool useTimeDomainSampling)
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

            ForceChannelDomain domain = resolvedSection.Channels?.Domain ?? resolvedSection.Domain;
            double channelT = ResolveSamplingParameter(
                resolvedSection,
                domain,
                snapshot.NormalizedT,
                elapsedTime,
                useTimeDomainSampling);

            double? targetNormalG = SampleChannel(
                resolvedSection.Channels?.NormalGChannels,
                resolvedSection.Channels?.NormalGBlendMode ?? ForceChannelBlendMode.Sum,
                resolvedSection.Channels?.NormalG,
                resolvedSection.NormalGChannel,
                resolvedSection.InterpolationMode,
                resolvedSection.EasingFunction,
                resolvedSection.TargetNormalG,
                resolvedSection.StartNormalG,
                resolvedSection.EndNormalG,
                channelT);

            double? targetLateralG = SampleChannel(
                resolvedSection.Channels?.LateralGChannels,
                resolvedSection.Channels?.LateralGBlendMode ?? ForceChannelBlendMode.Sum,
                resolvedSection.Channels?.LateralG,
                resolvedSection.LateralGChannel,
                resolvedSection.InterpolationMode,
                resolvedSection.EasingFunction,
                resolvedSection.TargetLateralG,
                resolvedSection.StartLateralG,
                resolvedSection.EndLateralG,
                channelT);

            double? targetLongitudinalG = SampleChannel(
                resolvedSection.Channels?.LongitudinalGChannels,
                resolvedSection.Channels?.LongitudinalGBlendMode ?? ForceChannelBlendMode.Sum,
                resolvedSection.Channels?.LongitudinalG,
                resolvedSection.LongitudinalGChannel,
                resolvedSection.InterpolationMode,
                resolvedSection.EasingFunction,
                resolvedSection.TargetLongitudinalG,
                resolvedSection.StartLongitudinalG,
                resolvedSection.EndLongitudinalG,
                channelT);

            double? targetRollRateDegPerSec = SampleDirectChannel(
                resolvedSection.Channels?.RollRateChannels,
                resolvedSection.Channels?.RollRateBlendMode ?? ForceChannelBlendMode.Sum,
                resolvedSection.Channels?.RollRate,
                resolvedSection.RollRateChannel,
                channelT);

            return new SampledForceTarget(
                distance,
                channelT,
                targetNormalG,
                targetLateralG,
                targetLongitudinalG: targetLongitudinalG,
                targetRollRateDegPerSec: targetRollRateDegPerSec);
        }

        private static double ResolveSamplingParameter(
            ForceSection resolvedSection,
            ForceChannelDomain domain,
            double distanceNormalizedT,
            double elapsedTime,
            bool useTimeDomainSampling)
        {
            switch (domain)
            {
                case ForceChannelDomain.Distance:
                    return distanceNormalizedT;
                case ForceChannelDomain.Time:
                    if (!useTimeDomainSampling)
                    {
                        return distanceNormalizedT;
                    }

                    return ResolveTimeNormalizedT(resolvedSection, elapsedTime);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(domain),
                        domain,
                        "Unsupported force channel domain.");
            }
        }

        private static double ResolveTimeNormalizedT(ForceSection resolvedSection, double elapsedTime)
        {
            if (double.IsNaN(elapsedTime) || double.IsInfinity(elapsedTime))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(elapsedTime),
                    elapsedTime,
                    "Elapsed time must be finite.");
            }

            double? duration = resolvedSection.Duration;

            if (!duration.HasValue)
            {
                throw new InvalidOperationException(
                    "Time-domain force sampling requires ForceSection.Duration to be set to a positive finite value.");
            }

            double durationValue = duration.Value;

            if (double.IsNaN(durationValue) || double.IsInfinity(durationValue) || durationValue <= 0.0)
            {
                throw new InvalidOperationException(
                    "Time-domain force sampling requires ForceSection.Duration to be set to a positive finite value.");
            }

            if (elapsedTime < 0.0 || elapsedTime > durationValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(elapsedTime),
                    elapsedTime,
                    $"Elapsed time must be within [0, {durationValue}] for time-domain force sampling.");
            }

            return elapsedTime / durationValue;
        }

        private static double? SampleChannel(
            IReadOnlyList<IForceChannel>? v3Channels,
            ForceChannelBlendMode v4BlendMode,
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
                return BlendChannels(v3Channels!, normalizedT, v4BlendMode);
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
            ForceChannelBlendMode v4BlendMode,
            IForceChannel? v2Channel,
            IForceEasingFunction? legacyChannel,
            double normalizedT)
        {
            if (HasChannels(v3Channels))
            {
                return BlendChannels(v3Channels!, normalizedT, v4BlendMode);
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

        private static double BlendChannels(
            IReadOnlyList<IForceChannel> channels,
            double normalizedT,
            ForceChannelBlendMode blendMode)
        {
            switch (blendMode)
            {
                case ForceChannelBlendMode.Sum:
                    return SumChannels(channels, normalizedT);
                case ForceChannelBlendMode.Max:
                    return MaxChannels(channels, normalizedT);
                case ForceChannelBlendMode.Override:
                    return OverrideChannels(channels, normalizedT);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(blendMode),
                        blendMode,
                        "Unsupported force channel blend mode.");
            }
        }

        private static double SumChannels(IReadOnlyList<IForceChannel> channels, double normalizedT)
        {
            double sum = 0.0;

            for (int i = 0; i < channels.Count; i++)
            {
                sum += EvaluateChannel(channels, i, normalizedT);
            }

            return sum;
        }

        private static double MaxChannels(IReadOnlyList<IForceChannel> channels, double normalizedT)
        {
            double max = EvaluateChannel(channels, 0, normalizedT);

            for (int i = 1; i < channels.Count; i++)
            {
                double value = EvaluateChannel(channels, i, normalizedT);

                if (value > max)
                {
                    max = value;
                }
            }

            return max;
        }

        private static double OverrideChannels(IReadOnlyList<IForceChannel> channels, double normalizedT)
        {
            return EvaluateChannel(channels, channels.Count - 1, normalizedT);
        }

        private static double EvaluateChannel(
            IReadOnlyList<IForceChannel> channels,
            int index,
            double normalizedT)
        {
            IForceChannel? channel = channels[index];

            if (channel is null)
            {
                throw new InvalidOperationException("Force channel list cannot contain null entries.");
            }

            return channel.Evaluate(normalizedT);
        }
    }
}
