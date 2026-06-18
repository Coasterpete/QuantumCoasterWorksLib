using System;
using System.Collections.Generic;
using Quantum.Track.Internal;

namespace Quantum.Track
{
    /// <summary>
    /// Converts shorthand section models into normalized section definitions.
    /// </summary>
    public static class SectionNormalizer
    {
        /// <summary>
        /// Normalizes a resolved force section interval.
        /// </summary>
        public static SectionDefinition Normalize(ResolvedSectionInterval<ForceSection> interval)
        {
            return NormalizeForceSection(interval);
        }

        /// <summary>
        /// Normalizes a resolved geometric section interval.
        /// </summary>
        public static SectionDefinition Normalize(ResolvedSectionInterval<GeometricSection> interval)
        {
            return NormalizeGeometricSection(interval);
        }

        /// <summary>
        /// Normalizes a force section, preserving compatibility precedence for each channel.
        /// </summary>
        /// <remarks>
        /// Per channel, normalization chooses the first available source in this order:
        /// non-empty plural channel list, single <see cref="ForceChannelSet"/> channel,
        /// legacy <see cref="IForceEasingFunction"/> channel, then scalar target/start/end
        /// fields. The channel-set domain overrides the section domain when present.
        /// </remarks>
        public static SectionDefinition NormalizeForceSection(ResolvedSectionInterval<ForceSection> interval)
        {
            ValidateInterval(interval, out ForceSection section);

            var functions = new List<SectionFunction>();
            AddForceValueFunction(
                functions,
                SectionChannel.NormalG,
                interval.StartDistance,
                interval.EndDistance,
                section.Channels?.NormalGChannels,
                section.Channels?.NormalGBlendMode ?? ForceChannelBlendMode.Sum,
                section.Channels?.NormalG,
                section.NormalGChannel,
                section.InterpolationMode,
                section.EasingFunction,
                section.TargetNormalG,
                section.StartNormalG,
                section.EndNormalG);

            AddForceValueFunction(
                functions,
                SectionChannel.LateralG,
                interval.StartDistance,
                interval.EndDistance,
                section.Channels?.LateralGChannels,
                section.Channels?.LateralGBlendMode ?? ForceChannelBlendMode.Sum,
                section.Channels?.LateralG,
                section.LateralGChannel,
                section.InterpolationMode,
                section.EasingFunction,
                section.TargetLateralG,
                section.StartLateralG,
                section.EndLateralG);

            AddForceValueFunction(
                functions,
                SectionChannel.LongitudinalG,
                interval.StartDistance,
                interval.EndDistance,
                section.Channels?.LongitudinalGChannels,
                section.Channels?.LongitudinalGBlendMode ?? ForceChannelBlendMode.Sum,
                section.Channels?.LongitudinalG,
                section.LongitudinalGChannel,
                section.InterpolationMode,
                section.EasingFunction,
                section.TargetLongitudinalG,
                section.StartLongitudinalG,
                section.EndLongitudinalG);

            AddDirectForceFunction(
                functions,
                SectionChannel.RollRateDegPerSec,
                interval.StartDistance,
                interval.EndDistance,
                section.Channels?.RollRateChannels,
                section.Channels?.RollRateBlendMode ?? ForceChannelBlendMode.Sum,
                section.Channels?.RollRate,
                section.RollRateChannel);

            return new SectionDefinition(
                SectionKind.Force,
                ToSectionDomain(section.Channels?.Domain ?? section.Domain),
                interval.StartDistance,
                interval.EndDistance,
                functions);
        }

        /// <summary>
        /// Normalizes a geometric section into curvature and roll channels.
        /// </summary>
        public static SectionDefinition NormalizeGeometricSection(ResolvedSectionInterval<GeometricSection> interval)
        {
            ValidateInterval(interval, out GeometricSection section);

            double curvature = section.Curvature ?? 0.0;
            double roll = section.Roll ?? 0.0;

            var functions = new List<SectionFunction>
            {
                CreateConstantFunction(
                    SectionChannel.Curvature,
                    interval.StartDistance,
                    interval.EndDistance,
                    curvature),
                CreateConstantFunction(
                    SectionChannel.Roll,
                    interval.StartDistance,
                    interval.EndDistance,
                    roll)
            };

            return new SectionDefinition(
                SectionKind.Geometry,
                SectionDomain.Distance,
                interval.StartDistance,
                interval.EndDistance,
                functions);
        }

        /// <summary>
        /// Normalizes resolved force intervals in order.
        /// </summary>
        public static IReadOnlyList<SectionDefinition> NormalizeForceSections(
            IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals)
        {
            if (intervals is null)
            {
                throw new ArgumentNullException(nameof(intervals));
            }

            var definitions = new List<SectionDefinition>(intervals.Count);
            for (int i = 0; i < intervals.Count; i++)
            {
                definitions.Add(NormalizeForceSection(intervals[i]));
            }

            return definitions;
        }

        /// <summary>
        /// Normalizes resolved geometric intervals in order.
        /// </summary>
        public static IReadOnlyList<SectionDefinition> NormalizeGeometricSections(
            IReadOnlyList<ResolvedSectionInterval<GeometricSection>> intervals)
        {
            if (intervals is null)
            {
                throw new ArgumentNullException(nameof(intervals));
            }

            var definitions = new List<SectionDefinition>(intervals.Count);
            for (int i = 0; i < intervals.Count; i++)
            {
                definitions.Add(NormalizeGeometricSection(intervals[i]));
            }

            return definitions;
        }

        private static void AddForceValueFunction(
            List<SectionFunction> functions,
            SectionChannel channel,
            double startX,
            double endX,
            IReadOnlyList<IForceChannel>? v3Channels,
            ForceChannelBlendMode v4BlendMode,
            IForceChannel? v2Channel,
            IForceEasingFunction? legacyChannel,
            ForceInterpolationMode interpolationMode,
            IForceEasingFunction? easingFunction,
            double? constantValue,
            double? startValue,
            double? endValue)
        {
            Func<double, double>? evaluator = CreateForceValueEvaluator(
                startX,
                endX,
                v3Channels,
                v4BlendMode,
                v2Channel,
                legacyChannel,
                interpolationMode,
                easingFunction,
                constantValue,
                startValue,
                endValue);

            if (evaluator is null)
            {
                return;
            }

            functions.Add(CreateFunction(channel, startX, endX, evaluator));
        }

        private static void AddDirectForceFunction(
            List<SectionFunction> functions,
            SectionChannel channel,
            double startX,
            double endX,
            IReadOnlyList<IForceChannel>? v3Channels,
            ForceChannelBlendMode v4BlendMode,
            IForceChannel? v2Channel,
            IForceEasingFunction? legacyChannel)
        {
            Func<double, double>? evaluator = CreateDirectForceEvaluator(
                startX,
                endX,
                v3Channels,
                v4BlendMode,
                v2Channel,
                legacyChannel);

            if (evaluator is null)
            {
                return;
            }

            functions.Add(CreateFunction(channel, startX, endX, evaluator));
        }

        private static Func<double, double>? CreateForceValueEvaluator(
            double startX,
            double endX,
            IReadOnlyList<IForceChannel>? v3Channels,
            ForceChannelBlendMode v4BlendMode,
            IForceChannel? v2Channel,
            IForceEasingFunction? legacyChannel,
            ForceInterpolationMode interpolationMode,
            IForceEasingFunction? easingFunction,
            double? constantValue,
            double? startValue,
            double? endValue)
        {
            if (HasChannels(v3Channels))
            {
                return x => BlendChannels(v3Channels!, NormalizeX(x, startX, endX), v4BlendMode);
            }

            if (v2Channel != null)
            {
                double? resolvedStart = startValue ?? constantValue;
                double? resolvedEnd = endValue ?? constantValue;

                if (!resolvedStart.HasValue || !resolvedEnd.HasValue)
                {
                    return null;
                }

                return x =>
                {
                    double t = NormalizeX(x, startX, endX);
                    double adjustedT = v2Channel.Evaluate(t);
                    return Lerp(resolvedStart.Value, resolvedEnd.Value, adjustedT);
                };
            }

            if (legacyChannel != null)
            {
                double? resolvedStart = startValue ?? constantValue;
                double? resolvedEnd = endValue ?? constantValue;

                if (!resolvedStart.HasValue || !resolvedEnd.HasValue)
                {
                    return null;
                }

                return x =>
                {
                    double t = NormalizeX(x, startX, endX);
                    double adjustedT = legacyChannel.Evaluate(t);
                    return Lerp(resolvedStart.Value, resolvedEnd.Value, adjustedT);
                };
            }

            if (easingFunction is null && interpolationMode == ForceInterpolationMode.Constant)
            {
                if (!constantValue.HasValue)
                {
                    return null;
                }

                return _ => constantValue.Value;
            }

            if (!ScalarEasing.TryMapForceInterpolationMode(
                interpolationMode,
                out ScalarEasingMode scalarMode))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(interpolationMode),
                    interpolationMode,
                    "Unsupported force interpolation mode.");
            }

            double? scalarStart = startValue ?? constantValue;
            double? scalarEnd = endValue ?? constantValue;

            if (!scalarStart.HasValue || !scalarEnd.HasValue)
            {
                return null;
            }

            return x =>
            {
                double t = NormalizeX(x, startX, endX);
                double scalarT = easingFunction?.Evaluate(t)
                    ?? ScalarEasing.Evaluate(t, scalarMode);
                return Lerp(scalarStart.Value, scalarEnd.Value, scalarT);
            };
        }

        private static Func<double, double>? CreateDirectForceEvaluator(
            double startX,
            double endX,
            IReadOnlyList<IForceChannel>? v3Channels,
            ForceChannelBlendMode v4BlendMode,
            IForceChannel? v2Channel,
            IForceEasingFunction? legacyChannel)
        {
            if (HasChannels(v3Channels))
            {
                return x => BlendChannels(v3Channels!, NormalizeX(x, startX, endX), v4BlendMode);
            }

            if (v2Channel != null)
            {
                return x => v2Channel.Evaluate(NormalizeX(x, startX, endX));
            }

            if (legacyChannel != null)
            {
                return x => legacyChannel.Evaluate(NormalizeX(x, startX, endX));
            }

            return null;
        }

        private static SectionFunction CreateConstantFunction(
            SectionChannel channel,
            double startX,
            double endX,
            double value)
        {
            return CreateFunction(channel, startX, endX, _ => value);
        }

        private static SectionFunction CreateFunction(
            SectionChannel channel,
            double startX,
            double endX,
            Func<double, double> evaluator)
        {
            return new SectionFunction(
                channel,
                new[]
                {
                    new SectionSample(startX, evaluator(startX)),
                    new SectionSample(endX, evaluator(endX))
                },
                evaluator);
        }

        private static void ValidateInterval<TSection>(
            ResolvedSectionInterval<TSection> interval,
            out TSection section)
            where TSection : class
        {
            if (interval is null)
            {
                throw new ArgumentNullException(nameof(interval));
            }

            if (interval.Section is null)
            {
                throw new ArgumentException("Resolved interval section cannot be null.", nameof(interval));
            }

            section = interval.Section;
        }

        private static SectionDomain ToSectionDomain(ForceChannelDomain domain)
        {
            switch (domain)
            {
                case ForceChannelDomain.Distance:
                    return SectionDomain.Distance;
                case ForceChannelDomain.Time:
                    return SectionDomain.Time;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(domain),
                        domain,
                        "Unsupported force channel domain.");
            }
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

        private static double NormalizeX(double x, double startX, double endX)
        {
            double span = endX - startX;
            if (span <= 0.0)
            {
                throw new InvalidOperationException("Section interval length must be greater than zero.");
            }

            return Clamp((x - startX) / span, 0.0, 1.0);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static double Lerp(double start, double end, double t)
        {
            return start + ((end - start) * t);
        }
    }
}
