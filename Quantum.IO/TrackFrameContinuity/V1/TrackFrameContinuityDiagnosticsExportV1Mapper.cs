using System;
using System.Collections.Generic;
using Quantum.Math;
using Quantum.Track;

namespace Quantum.IO.TrackFrameContinuity.V1
{
    /// <summary>
    /// Source data for building a track-frame continuity diagnostics artifact.
    /// </summary>
    public sealed class TrackFrameContinuityDiagnosticsExportV1Source
    {
        public string Units { get; set; } = "meters";

        public string? SourceName { get; set; }

        public double TrackLength { get; set; }

        public IReadOnlyList<TrackFrame>? Frames { get; set; }

        public IReadOnlyList<double>? SampledDistances { get; set; }

        public TrackFrameContinuityReport? Report { get; set; }
    }

    /// <summary>
    /// Maps TrackFrameContinuityDiagnostics results into a stable JSON DTO contract.
    /// </summary>
    public static class TrackFrameContinuityDiagnosticsExportV1Mapper
    {
        private const double RadiansToDegrees = 180.0 / System.Math.PI;

        public static TrackFrameContinuityDiagnosticsExportV1Dto Export(
            TrackFrameContinuityDiagnosticsExportV1Source source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            TrackFrameContinuityReport report = source.Report ??
                throw new ArgumentException("Continuity report cannot be null.", nameof(source));

            IReadOnlyList<TrackFrame>? frames = source.Frames;
            IReadOnlyList<double>? distances = source.SampledDistances;

            if (frames != null && distances != null && frames.Count != distances.Count)
            {
                throw new ArgumentException(
                    "Sampled distance count must match frame count.",
                    nameof(source));
            }

            return new TrackFrameContinuityDiagnosticsExportV1Dto
            {
                Contract = TrackFrameContinuityDiagnosticsExportV1Dto.ContractName,
                Version = TrackFrameContinuityDiagnosticsExportV1Dto.ContractVersion,
                BackendOnly = true,
                Metadata = new TrackFrameContinuityMetadataV1Dto
                {
                    Units = string.IsNullOrWhiteSpace(source.Units) ? "meters" : source.Units,
                    SourceName = source.SourceName,
                    TrackLength = source.TrackLength
                },
                ThresholdsDegrees = MapThresholds(report.Thresholds),
                SummaryStatistics = MapSummaryStatistics(report, GetSampleCount(frames, distances)),
                Samples = MapSamples(frames, distances),
                Intervals = MapIntervals(report.Intervals),
                Issues = MapIssues(report.Issues),
                DiagnosticText = report.ToDiagnosticString()
            };
        }

        private static int GetSampleCount(IReadOnlyList<TrackFrame>? frames, IReadOnlyList<double>? distances)
        {
            if (frames != null)
            {
                return frames.Count;
            }

            if (distances != null)
            {
                return distances.Count;
            }

            return 0;
        }

        private static TrackFrameContinuityThresholdsDegreesV1Dto MapThresholds(
            TrackFrameContinuityThresholds thresholds)
        {
            return new TrackFrameContinuityThresholdsDegreesV1Dto
            {
                Tangent = ToDegrees(thresholds.TangentAngleRadians),
                Normal = ToDegrees(thresholds.NormalAngleRadians),
                Binormal = ToDegrees(thresholds.BinormalAngleRadians),
                Roll = ToDegrees(thresholds.RollAngleRadians),
                MatrixOrientation = ToDegrees(thresholds.MatrixOrientationAngleRadians)
            };
        }

        private static TrackFrameContinuitySummaryStatisticsV1Dto MapSummaryStatistics(
            TrackFrameContinuityReport report,
            int sampleCount)
        {
            return new TrackFrameContinuitySummaryStatisticsV1Dto
            {
                SampleCount = sampleCount,
                IntervalCount = report.IntervalCount,
                IssueCount = report.Issues.Count,
                HasIssues = report.HasDiscontinuities,
                TangentDegrees = MapMetric(report.SmoothnessReport.TangentAngleDelta),
                NormalDegrees = MapMetric(report.SmoothnessReport.NormalAngleDelta),
                BinormalDegrees = MapMetric(report.SmoothnessReport.BinormalAngleDelta),
                RollDegrees = MapMetric(report.SmoothnessReport.FrameTwistDelta),
                MatrixOrientationDegrees = MapMetric(report.MatrixOrientationAngleDelta)
            };
        }

        private static TrackFrameContinuityMetricSummaryDegreesV1Dto MapMetric(
            TrackFrameSmoothnessMetricSummary summary)
        {
            return new TrackFrameContinuityMetricSummaryDegreesV1Dto
            {
                MaxAbsolute = summary.MaxAbsoluteDegrees,
                AverageAbsolute = summary.AverageAbsoluteDegrees
            };
        }

        private static TrackFrameContinuitySampleV1Dto[] MapSamples(
            IReadOnlyList<TrackFrame>? frames,
            IReadOnlyList<double>? distances)
        {
            if (frames == null || frames.Count == 0)
            {
                return Array.Empty<TrackFrameContinuitySampleV1Dto>();
            }

            var result = new TrackFrameContinuitySampleV1Dto[frames.Count];

            for (int i = 0; i < frames.Count; i++)
            {
                TrackFrame frame = frames[i];
                result[i] = new TrackFrameContinuitySampleV1Dto
                {
                    SampleIndex = i,
                    Distance = distances == null ? frame.Distance : distances[i],
                    Position = MapVector(frame.Position),
                    Tangent = MapVector(frame.Tangent),
                    Normal = MapVector(frame.Normal),
                    Binormal = MapVector(frame.Binormal)
                };
            }

            return result;
        }

        private static TrackFrameContinuityIntervalV1Dto[] MapIntervals(
            IReadOnlyList<TrackFrameContinuityInterval> intervals)
        {
            var result = new TrackFrameContinuityIntervalV1Dto[intervals.Count];

            for (int i = 0; i < intervals.Count; i++)
            {
                TrackFrameContinuityInterval interval = intervals[i];
                result[i] = new TrackFrameContinuityIntervalV1Dto
                {
                    StartSampleIndex = interval.StartSampleIndex,
                    EndSampleIndex = interval.EndSampleIndex,
                    StartDistance = interval.StartDistance,
                    EndDistance = interval.EndDistance,
                    DistanceDelta = interval.DistanceDelta,
                    TangentDegrees = ToDegrees(interval.TangentAngleDeltaRadians),
                    NormalDegrees = ToDegrees(interval.NormalAngleDeltaRadians),
                    BinormalDegrees = ToDegrees(interval.BinormalAngleDeltaRadians),
                    RollDegrees = ToDegrees(interval.AbsoluteRollAngleDeltaRadians),
                    MatrixOrientationDegrees = ToDegrees(interval.MatrixOrientationAngleDeltaRadians)
                };
            }

            return result;
        }

        private static TrackFrameContinuityIssueV1Dto[] MapIssues(
            IReadOnlyList<TrackFrameContinuityIssue> issues)
        {
            var result = new TrackFrameContinuityIssueV1Dto[issues.Count];

            for (int i = 0; i < issues.Count; i++)
            {
                TrackFrameContinuityIssue issue = issues[i];
                TrackFrameContinuityInterval interval = issue.Interval;
                result[i] = new TrackFrameContinuityIssueV1Dto
                {
                    IssueType = issue.Kind.ToString(),
                    SampleIndex = interval.EndSampleIndex,
                    Distance = interval.EndDistance,
                    StartSampleIndex = interval.StartSampleIndex,
                    EndSampleIndex = interval.EndSampleIndex,
                    StartDistance = interval.StartDistance,
                    EndDistance = interval.EndDistance,
                    DistanceDelta = interval.DistanceDelta,
                    ActualDegrees = issue.ActualAngleDegrees,
                    ThresholdDegrees = issue.ThresholdAngleDegrees,
                    ExceededByDegrees = issue.ActualAngleDegrees - issue.ThresholdAngleDegrees
                };
            }

            return result;
        }

        private static TrackFrameContinuityVector3dV1Dto MapVector(Vector3d vector)
        {
            return new TrackFrameContinuityVector3dV1Dto
            {
                X = vector.X,
                Y = vector.Y,
                Z = vector.Z
            };
        }

        private static double ToDegrees(double radians)
        {
            return radians * RadiansToDegrees;
        }
    }
}
