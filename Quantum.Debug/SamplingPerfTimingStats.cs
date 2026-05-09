using System;
using System.Collections.Generic;

namespace Quantum.Debug
{
    public readonly struct SamplingPerfTimingStats
    {
        public SamplingPerfTimingStats(
            double meanMilliseconds,
            double minMilliseconds,
            double maxMilliseconds)
        {
            MeanMilliseconds = meanMilliseconds;
            MinMilliseconds = minMilliseconds;
            MaxMilliseconds = maxMilliseconds;
        }

        public double MeanMilliseconds { get; }

        public double MinMilliseconds { get; }

        public double MaxMilliseconds { get; }

        public static SamplingPerfTimingStats Compute(IReadOnlyList<double> elapsedMilliseconds)
        {
            if (elapsedMilliseconds is null)
            {
                throw new ArgumentNullException(nameof(elapsedMilliseconds));
            }

            if (elapsedMilliseconds.Count == 0)
            {
                throw new ArgumentException("At least one sample is required.", nameof(elapsedMilliseconds));
            }

            double sum = 0.0;
            double min = double.MaxValue;
            double max = double.MinValue;

            for (int i = 0; i < elapsedMilliseconds.Count; i++)
            {
                double sample = elapsedMilliseconds[i];
                if (double.IsNaN(sample) || double.IsInfinity(sample) || sample < 0.0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(elapsedMilliseconds),
                        sample,
                        "Elapsed timing samples must be finite and non-negative.");
                }

                sum += sample;
                min = System.Math.Min(min, sample);
                max = System.Math.Max(max, sample);
            }

            return new SamplingPerfTimingStats(
                sum / elapsedMilliseconds.Count,
                min,
                max);
        }

        public double ComputeThroughputOperationsPerSecond(int operationsPerIteration)
        {
            if (operationsPerIteration <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(operationsPerIteration),
                    operationsPerIteration,
                    "Operations per iteration must be greater than zero.");
            }

            if (MeanMilliseconds <= 0.0)
            {
                return double.PositiveInfinity;
            }

            return operationsPerIteration / (MeanMilliseconds / 1000.0);
        }
    }
}
