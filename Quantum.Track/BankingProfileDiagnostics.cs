using System;
using System.Collections.Generic;

namespace Quantum.Track
{
    /// <summary>
    /// Identifies how a BankingProfile sample resolved its roll source.
    /// </summary>
    public enum BankingProfileSampleSourceKind
    {
        SingleKey = 0,
        ClampBeforeFirstKey = 1,
        KeyInterval = 2,
        ClampAfterLastKey = 3
    }

    internal readonly struct BankingProfileSampleInfo
    {
        public BankingProfileSampleInfo(
            double rollRadians,
            BankingProfileInterpolationMode interpolationMode,
            BankingProfileSampleSourceKind sourceKind,
            int sourceStartKeyIndex,
            int sourceEndKeyIndex,
            double sourceStartDistance,
            double sourceEndDistance)
        {
            RollRadians = rollRadians;
            InterpolationMode = interpolationMode;
            SourceKind = sourceKind;
            SourceStartKeyIndex = sourceStartKeyIndex;
            SourceEndKeyIndex = sourceEndKeyIndex;
            SourceStartDistance = sourceStartDistance;
            SourceEndDistance = sourceEndDistance;
        }

        public double RollRadians { get; }

        public BankingProfileInterpolationMode InterpolationMode { get; }

        public BankingProfileSampleSourceKind SourceKind { get; }

        public int SourceStartKeyIndex { get; }

        public int SourceEndKeyIndex { get; }

        public double SourceStartDistance { get; }

        public double SourceEndDistance { get; }
    }

    /// <summary>
    /// Backend-only diagnostics for inspecting BankingProfile roll samples by station distance.
    /// </summary>
    public static class BankingProfileDiagnostics
    {
        private const double RadiansToDegrees = 180.0 / System.Math.PI;

        public static BankingProfileDiagnosticsReport Sample(
            BankingProfile profile,
            IReadOnlyList<double> distances)
        {
            if (profile is null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (distances is null)
            {
                throw new ArgumentNullException(nameof(distances));
            }

            ValidateOrderedDistances(distances);

            var sampleInfos = new BankingProfileSampleInfo[distances.Count];
            var rollRadians = new double[distances.Count];

            for (int i = 0; i < distances.Count; i++)
            {
                BankingProfileSampleInfo info = BankingProfileSampler.SampleRollInfo(profile, distances[i]);
                sampleInfos[i] = info;
                rollRadians[i] = info.RollRadians;
            }

            double?[] slopes = EstimateRollSlopes(distances, rollRadians);
            var samples = new BankingProfileDiagnosticsSample[distances.Count];

            for (int i = 0; i < distances.Count; i++)
            {
                BankingProfileSampleInfo info = sampleInfos[i];
                samples[i] = new BankingProfileDiagnosticsSample(
                    i,
                    distances[i],
                    info.RollRadians,
                    ToDegrees(info.RollRadians),
                    info.InterpolationMode,
                    info.SourceKind,
                    info.SourceStartKeyIndex,
                    info.SourceEndKeyIndex,
                    info.SourceStartDistance,
                    info.SourceEndDistance,
                    slopes[i]);
            }

            return new BankingProfileDiagnosticsReport(
                Array.AsReadOnly(samples),
                BuildSummary(samples));
        }

        private static BankingProfileDiagnosticsSummary BuildSummary(
            IReadOnlyList<BankingProfileDiagnosticsSample> samples)
        {
            if (samples.Count == 0)
            {
                return new BankingProfileDiagnosticsSummary(
                    0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0);
            }

            double minRollRadians = samples[0].RollRadians;
            double maxRollRadians = samples[0].RollRadians;
            double maxAbsoluteRollSlopeRadPerMeter = 0.0;

            for (int i = 0; i < samples.Count; i++)
            {
                BankingProfileDiagnosticsSample sample = samples[i];
                minRollRadians = System.Math.Min(minRollRadians, sample.RollRadians);
                maxRollRadians = System.Math.Max(maxRollRadians, sample.RollRadians);

                if (sample.ApproximateRollSlopeRadPerMeter.HasValue)
                {
                    maxAbsoluteRollSlopeRadPerMeter = System.Math.Max(
                        maxAbsoluteRollSlopeRadPerMeter,
                        System.Math.Abs(sample.ApproximateRollSlopeRadPerMeter.Value));
                }
            }

            return new BankingProfileDiagnosticsSummary(
                samples.Count,
                minRollRadians,
                maxRollRadians,
                ToDegrees(minRollRadians),
                ToDegrees(maxRollRadians),
                maxAbsoluteRollSlopeRadPerMeter);
        }

        private static double?[] EstimateRollSlopes(
            IReadOnlyList<double> distances,
            IReadOnlyList<double> rollRadians)
        {
            var slopes = new double?[distances.Count];
            if (distances.Count < 2)
            {
                return slopes;
            }

            for (int i = 0; i < distances.Count; i++)
            {
                slopes[i] = EstimateRollSlopeAtSample(i, distances, rollRadians);
            }

            return slopes;
        }

        private static double? EstimateRollSlopeAtSample(
            int sampleIndex,
            IReadOnlyList<double> distances,
            IReadOnlyList<double> rollRadians)
        {
            int previousIndex = sampleIndex - 1;
            while (previousIndex >= 0 && distances[previousIndex] == distances[sampleIndex])
            {
                previousIndex--;
            }

            int nextIndex = sampleIndex + 1;
            while (nextIndex < distances.Count && distances[nextIndex] == distances[sampleIndex])
            {
                nextIndex++;
            }

            if (previousIndex >= 0 && nextIndex < distances.Count)
            {
                return EstimateSlope(
                    distances[previousIndex],
                    rollRadians[previousIndex],
                    distances[nextIndex],
                    rollRadians[nextIndex]);
            }

            if (nextIndex < distances.Count)
            {
                return EstimateSlope(
                    distances[sampleIndex],
                    rollRadians[sampleIndex],
                    distances[nextIndex],
                    rollRadians[nextIndex]);
            }

            if (previousIndex >= 0)
            {
                return EstimateSlope(
                    distances[previousIndex],
                    rollRadians[previousIndex],
                    distances[sampleIndex],
                    rollRadians[sampleIndex]);
            }

            return null;
        }

        private static double? EstimateSlope(
            double startDistance,
            double startRollRadians,
            double endDistance,
            double endRollRadians)
        {
            double distanceDelta = endDistance - startDistance;
            if (distanceDelta <= 0.0)
            {
                return null;
            }

            return (endRollRadians - startRollRadians) / distanceDelta;
        }

        private static void ValidateOrderedDistances(IReadOnlyList<double> distances)
        {
            double previousDistance = double.NegativeInfinity;

            for (int i = 0; i < distances.Count; i++)
            {
                double distance = distances[i];
                if (double.IsNaN(distance) || double.IsInfinity(distance))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(distances),
                        distance,
                        $"Distance at index {i} must be finite.");
                }

                if (distance < previousDistance)
                {
                    throw new ArgumentException(
                        $"Distances must be in non-decreasing station order. Distance at index {i} is less than the previous distance.",
                        nameof(distances));
                }

                previousDistance = distance;
            }
        }

        private static double ToDegrees(double radians)
        {
            return radians * RadiansToDegrees;
        }
    }

    public sealed class BankingProfileDiagnosticsReport
    {
        public BankingProfileDiagnosticsReport(
            IReadOnlyList<BankingProfileDiagnosticsSample> samples,
            BankingProfileDiagnosticsSummary summary)
        {
            Samples = samples ?? throw new ArgumentNullException(nameof(samples));
            Summary = summary ?? throw new ArgumentNullException(nameof(summary));
        }

        public IReadOnlyList<BankingProfileDiagnosticsSample> Samples { get; }

        public BankingProfileDiagnosticsSummary Summary { get; }
    }

    public sealed class BankingProfileDiagnosticsSummary
    {
        public BankingProfileDiagnosticsSummary(
            int sampleCount,
            double minRollRadians,
            double maxRollRadians,
            double minRollDegrees,
            double maxRollDegrees,
            double maxAbsoluteRollSlopeRadPerMeter)
        {
            SampleCount = sampleCount;
            MinRollRadians = minRollRadians;
            MaxRollRadians = maxRollRadians;
            MinRollDegrees = minRollDegrees;
            MaxRollDegrees = maxRollDegrees;
            MaxAbsoluteRollSlopeRadPerMeter = maxAbsoluteRollSlopeRadPerMeter;
        }

        public int SampleCount { get; }

        public double MinRollRadians { get; }

        public double MaxRollRadians { get; }

        public double MinRollDegrees { get; }

        public double MaxRollDegrees { get; }

        public double MaxAbsoluteRollSlopeRadPerMeter { get; }
    }

    public sealed class BankingProfileDiagnosticsSample
    {
        public BankingProfileDiagnosticsSample(
            int sampleIndex,
            double distance,
            double rollRadians,
            double rollDegrees,
            BankingProfileInterpolationMode interpolationMode,
            BankingProfileSampleSourceKind sourceKind,
            int sourceStartKeyIndex,
            int sourceEndKeyIndex,
            double sourceStartDistance,
            double sourceEndDistance,
            double? approximateRollSlopeRadPerMeter)
        {
            SampleIndex = sampleIndex;
            Distance = distance;
            RollRadians = rollRadians;
            RollDegrees = rollDegrees;
            InterpolationMode = interpolationMode;
            SourceKind = sourceKind;
            SourceStartKeyIndex = sourceStartKeyIndex;
            SourceEndKeyIndex = sourceEndKeyIndex;
            SourceStartDistance = sourceStartDistance;
            SourceEndDistance = sourceEndDistance;
            ApproximateRollSlopeRadPerMeter = approximateRollSlopeRadPerMeter;
        }

        public int SampleIndex { get; }

        public double Distance { get; }

        public double RollRadians { get; }

        public double RollDegrees { get; }

        public BankingProfileInterpolationMode InterpolationMode { get; }

        public BankingProfileSampleSourceKind SourceKind { get; }

        public int SourceStartKeyIndex { get; }

        public int SourceEndKeyIndex { get; }

        public double SourceStartDistance { get; }

        public double SourceEndDistance { get; }

        public double? ApproximateRollSlopeRadPerMeter { get; }
    }
}
