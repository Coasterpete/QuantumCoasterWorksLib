using System;
using System.Collections.Generic;
using Quantum.Core;

namespace Quantum.Physics
{
    /// <summary>
    /// Lightweight helpers for extracting basic metrics from sampled follower states.
    /// </summary>
    public static class TrainSampleAnalytics
    {
        private const double StandardGravityMetersPerSecondSquared = 9.81;

        public static double GetMaxSpeed(IReadOnlyList<TrainFollowerState> samples)
        {
            RequireNonEmpty(samples);

            double maxSpeed = samples[0].Speed;
            for (int i = 1; i < samples.Count; i++)
            {
                maxSpeed = System.Math.Max(maxSpeed, samples[i].Speed);
            }

            return maxSpeed;
        }

        public static double GetTotalDistance(IReadOnlyList<TrainFollowerState> samples)
        {
            if (samples is null)
                throw new ArgumentNullException(nameof(samples));

            if (samples.Count == 0)
                return 0.0;

            return samples[samples.Count - 1].Distance;
        }

        public static double GetMinHeight(IReadOnlyList<TrainFollowerState> samples)
        {
            RequireNonEmpty(samples);

            double minHeight = samples[0].Position.Y;
            for (int i = 1; i < samples.Count; i++)
            {
                minHeight = System.Math.Min(minHeight, samples[i].Position.Y);
            }

            return minHeight;
        }

        public static double GetMaxHeight(IReadOnlyList<TrainFollowerState> samples)
        {
            RequireNonEmpty(samples);

            double maxHeight = samples[0].Position.Y;
            for (int i = 1; i < samples.Count; i++)
            {
                maxHeight = System.Math.Max(maxHeight, samples[i].Position.Y);
            }

            return maxHeight;
        }

        public static (double MeanAbsoluteError, double RmsError) ComputeNormalGError(
            IReadOnlyList<TrainFollowerState> samples,
            IForceTargetProvider provider,
            double dt)
        {
            RequireNonEmpty(samples);
            if (provider is null)
                throw new ArgumentNullException(nameof(provider));

            Guard.RequirePositiveFinite(
                dt,
                nameof(dt),
                "Delta time must be a finite, positive value.");

            double absoluteErrorSum = 0.0;
            double squaredErrorSum = 0.0;

            for (int i = 0; i < samples.Count; i++)
            {
                TrainFollowerState sample = samples[i];
                double x = sample.Distance;

                if (!provider.TryGetForceTargets(x, out ForceTargets targets))
                {
                    throw new InvalidOperationException(
                        $"No force targets available at sample distance x={x:0.######}.");
                }

                double realizedNormalG = sample.Acceleration / StandardGravityMetersPerSecondSquared;
                double error = realizedNormalG - targets.NormalG;

                absoluteErrorSum += System.Math.Abs(error);
                squaredErrorSum += error * error;
            }

            double sampleCount = samples.Count;
            return (
                MeanAbsoluteError: absoluteErrorSum / sampleCount,
                RmsError: System.Math.Sqrt(squaredErrorSum / sampleCount));
        }

        private static void RequireNonEmpty(IReadOnlyList<TrainFollowerState> samples)
        {
            if (samples is null)
                throw new ArgumentNullException(nameof(samples));
            Guard.RequireAtLeast(samples.Count, 1, nameof(samples), "At least one sample is required.");
        }
    }
}
