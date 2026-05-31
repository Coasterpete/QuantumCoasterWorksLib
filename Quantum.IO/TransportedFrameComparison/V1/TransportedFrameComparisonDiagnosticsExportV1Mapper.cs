using System;
using System.Collections.Generic;
using Quantum.Track;

namespace Quantum.IO.TransportedFrameComparison.V1
{
    /// <summary>
    /// Source data for building a transported frame comparison diagnostics artifact.
    /// </summary>
    public sealed class TransportedFrameComparisonDiagnosticsExportV1Source
    {
        public string Units { get; set; } = "meters";

        public string? SourceName { get; set; }

        public IReadOnlyList<TransportedFrameComparisonDiagnosticsExportV1ReportSource>? Reports { get; set; }
    }

    public sealed class TransportedFrameComparisonDiagnosticsExportV1ReportSource
    {
        public string? SourceName { get; set; }

        public double TrackLength { get; set; }

        public TransportedFrameComparisonReport? Report { get; set; }
    }

    /// <summary>
    /// Maps TransportedFrameComparisonDiagnostics results into a stable JSON DTO contract.
    /// </summary>
    public static class TransportedFrameComparisonDiagnosticsExportV1Mapper
    {
        private const double RadiansToDegrees = 180.0 / System.Math.PI;

        public static TransportedFrameComparisonDiagnosticsExportV1Dto Export(
            TransportedFrameComparisonDiagnosticsExportV1Source source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            IReadOnlyList<TransportedFrameComparisonDiagnosticsExportV1ReportSource> reports =
                source.Reports ?? throw new ArgumentException("Reports cannot be null.", nameof(source));

            return new TransportedFrameComparisonDiagnosticsExportV1Dto
            {
                Contract = TransportedFrameComparisonDiagnosticsExportV1Dto.ContractName,
                Version = TransportedFrameComparisonDiagnosticsExportV1Dto.ContractVersion,
                BackendOnly = true,
                Metadata = new TransportedFrameComparisonMetadataV1Dto
                {
                    Units = string.IsNullOrWhiteSpace(source.Units) ? "meters" : source.Units,
                    SourceName = source.SourceName,
                    ReportCount = reports.Count,
                    FixtureNames = MapFixtureNames(reports)
                },
                Reports = MapReports(reports)
            };
        }

        private static string[] MapFixtureNames(
            IReadOnlyList<TransportedFrameComparisonDiagnosticsExportV1ReportSource> reports)
        {
            var result = new string[reports.Count];

            for (int i = 0; i < reports.Count; i++)
            {
                result[i] = reports[i].SourceName ?? string.Empty;
            }

            return result;
        }

        private static TransportedFrameComparisonReportV1Dto[] MapReports(
            IReadOnlyList<TransportedFrameComparisonDiagnosticsExportV1ReportSource> reports)
        {
            var result = new TransportedFrameComparisonReportV1Dto[reports.Count];

            for (int i = 0; i < reports.Count; i++)
            {
                TransportedFrameComparisonDiagnosticsExportV1ReportSource source = reports[i];
                TransportedFrameComparisonReport report = source.Report ??
                    throw new ArgumentException("Comparison report cannot be null.", nameof(reports));

                result[i] = new TransportedFrameComparisonReportV1Dto
                {
                    SourceName = source.SourceName,
                    TrackLength = source.TrackLength,
                    SummaryMetrics = MapSummaryMetrics(report),
                    Samples = MapSamples(report.Samples),
                    SmoothnessMetrics = new TransportedFrameComparisonSmoothnessMetricsV1Dto
                    {
                        Stateless = MapSmoothnessReport(report.StatelessSmoothnessReport),
                        Transported = MapSmoothnessReport(report.TransportedSmoothnessReport)
                    },
                    ContinuityMetrics = new TransportedFrameComparisonContinuityMetricsV1Dto
                    {
                        ThresholdsDegrees = MapThresholds(report.StatelessContinuityReport.Thresholds),
                        Stateless = MapContinuityReport(report.StatelessContinuityReport),
                        Transported = MapContinuityReport(report.TransportedContinuityReport)
                    }
                };
            }

            return result;
        }

        private static TransportedFrameComparisonSummaryMetricsV1Dto MapSummaryMetrics(
            TransportedFrameComparisonReport report)
        {
            return new TransportedFrameComparisonSummaryMetricsV1Dto
            {
                SampleCount = report.SampleCount,
                StatelessContinuityIssueCount = report.StatelessContinuityReport.Issues.Count,
                TransportedContinuityIssueCount = report.TransportedContinuityReport.Issues.Count,
                StatelessHasContinuityIssues = report.StatelessContinuityReport.HasDiscontinuities,
                TransportedHasContinuityIssues = report.TransportedContinuityReport.HasDiscontinuities,
                TangentDegrees = MapAngleMetric(report.TangentAngleDelta),
                NormalDegrees = MapAngleMetric(report.NormalAngleDelta),
                BinormalDegrees = MapAngleMetric(report.BinormalAngleDelta),
                FrameDegrees = MapAngleMetric(report.FrameAngleDelta),
                RollDegrees = MapAngleMetric(report.RollAngleDelta),
                MatrixOrientationDegrees = MapAngleMetric(report.MatrixOrientationAngleDelta)
            };
        }

        private static TransportedFrameComparisonSampleDeltaV1Dto[] MapSamples(
            IReadOnlyList<TransportedFrameComparisonSample> samples)
        {
            var result = new TransportedFrameComparisonSampleDeltaV1Dto[samples.Count];

            for (int i = 0; i < samples.Count; i++)
            {
                TransportedFrameComparisonSample sample = samples[i];
                result[i] = new TransportedFrameComparisonSampleDeltaV1Dto
                {
                    SampleIndex = sample.SampleIndex,
                    Distance = sample.Distance,
                    TangentDegrees = sample.TangentAngleDeltaDegrees,
                    NormalDegrees = sample.NormalAngleDeltaDegrees,
                    BinormalDegrees = sample.BinormalAngleDeltaDegrees,
                    FrameDegrees = sample.FrameAngleDeltaDegrees,
                    RollDegrees = sample.RollAngleDeltaDegrees,
                    AbsoluteRollDegrees = ToDegrees(sample.AbsoluteRollAngleDeltaRadians),
                    MatrixOrientationDegrees = sample.MatrixOrientationAngleDeltaDegrees
                };
            }

            return result;
        }

        private static TransportedFrameComparisonSmoothnessReportV1Dto MapSmoothnessReport(
            TrackFrameSmoothnessReport report)
        {
            return new TransportedFrameComparisonSmoothnessReportV1Dto
            {
                IntervalCount = report.IntervalCount,
                TangentDegrees = MapAngleMetric(report.TangentAngleDelta),
                NormalDegrees = MapAngleMetric(report.NormalAngleDelta),
                BinormalDegrees = MapAngleMetric(report.BinormalAngleDelta),
                FrameDegrees = MapAngleMetric(report.FrameAngleDelta),
                RollDegrees = MapAngleMetric(report.FrameTwistDelta),
                CurvatureEstimate = MapCurvatureMetric(report.CurvatureEstimate),
                CurvatureEstimateDelta = MapCurvatureMetric(report.CurvatureEstimateDelta),
                Intervals = MapSmoothnessIntervals(report.Intervals)
            };
        }

        private static TransportedFrameComparisonSmoothnessIntervalV1Dto[] MapSmoothnessIntervals(
            IReadOnlyList<TrackFrameSmoothnessInterval> intervals)
        {
            var result = new TransportedFrameComparisonSmoothnessIntervalV1Dto[intervals.Count];

            for (int i = 0; i < intervals.Count; i++)
            {
                TrackFrameSmoothnessInterval interval = intervals[i];
                result[i] = new TransportedFrameComparisonSmoothnessIntervalV1Dto
                {
                    StartSampleIndex = interval.StartSampleIndex,
                    EndSampleIndex = interval.EndSampleIndex,
                    StartDistance = interval.StartDistance,
                    EndDistance = interval.EndDistance,
                    DistanceDelta = interval.DistanceDelta,
                    TangentDegrees = ToDegrees(interval.TangentAngleDeltaRadians),
                    NormalDegrees = ToDegrees(interval.NormalAngleDeltaRadians),
                    BinormalDegrees = ToDegrees(interval.BinormalAngleDeltaRadians),
                    FrameDegrees = ToDegrees(interval.FrameAngleDeltaRadians),
                    RollDegrees = ToDegrees(interval.FrameTwistDeltaRadians),
                    AbsoluteRollDegrees = ToDegrees(System.Math.Abs(interval.FrameTwistDeltaRadians)),
                    CurvatureEstimate = interval.CurvatureEstimate,
                    CurvatureEstimateDelta = interval.CurvatureEstimateDelta
                };
            }

            return result;
        }

        private static TransportedFrameComparisonContinuityReportV1Dto MapContinuityReport(
            TrackFrameContinuityReport report)
        {
            return new TransportedFrameComparisonContinuityReportV1Dto
            {
                IntervalCount = report.IntervalCount,
                IssueCount = report.Issues.Count,
                HasIssues = report.HasDiscontinuities,
                TangentDegrees = MapAngleMetric(report.SmoothnessReport.TangentAngleDelta),
                NormalDegrees = MapAngleMetric(report.SmoothnessReport.NormalAngleDelta),
                BinormalDegrees = MapAngleMetric(report.SmoothnessReport.BinormalAngleDelta),
                RollDegrees = MapAngleMetric(report.SmoothnessReport.FrameTwistDelta),
                MatrixOrientationDegrees = MapAngleMetric(report.MatrixOrientationAngleDelta),
                Intervals = MapContinuityIntervals(report.Intervals),
                Issues = MapContinuityIssues(report.Issues),
                DiagnosticText = report.ToDiagnosticString()
            };
        }

        private static TransportedFrameComparisonContinuityIntervalV1Dto[] MapContinuityIntervals(
            IReadOnlyList<TrackFrameContinuityInterval> intervals)
        {
            var result = new TransportedFrameComparisonContinuityIntervalV1Dto[intervals.Count];

            for (int i = 0; i < intervals.Count; i++)
            {
                TrackFrameContinuityInterval interval = intervals[i];
                result[i] = new TransportedFrameComparisonContinuityIntervalV1Dto
                {
                    StartSampleIndex = interval.StartSampleIndex,
                    EndSampleIndex = interval.EndSampleIndex,
                    StartDistance = interval.StartDistance,
                    EndDistance = interval.EndDistance,
                    DistanceDelta = interval.DistanceDelta,
                    TangentDegrees = ToDegrees(interval.TangentAngleDeltaRadians),
                    NormalDegrees = ToDegrees(interval.NormalAngleDeltaRadians),
                    BinormalDegrees = ToDegrees(interval.BinormalAngleDeltaRadians),
                    RollDegrees = ToDegrees(interval.RollAngleDeltaRadians),
                    AbsoluteRollDegrees = ToDegrees(interval.AbsoluteRollAngleDeltaRadians),
                    MatrixOrientationDegrees = ToDegrees(interval.MatrixOrientationAngleDeltaRadians)
                };
            }

            return result;
        }

        private static TransportedFrameComparisonContinuityIssueV1Dto[] MapContinuityIssues(
            IReadOnlyList<TrackFrameContinuityIssue> issues)
        {
            var result = new TransportedFrameComparisonContinuityIssueV1Dto[issues.Count];

            for (int i = 0; i < issues.Count; i++)
            {
                TrackFrameContinuityIssue issue = issues[i];
                TrackFrameContinuityInterval interval = issue.Interval;
                result[i] = new TransportedFrameComparisonContinuityIssueV1Dto
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

        private static TransportedFrameComparisonThresholdsDegreesV1Dto MapThresholds(
            TrackFrameContinuityThresholds thresholds)
        {
            return new TransportedFrameComparisonThresholdsDegreesV1Dto
            {
                Tangent = ToDegrees(thresholds.TangentAngleRadians),
                Normal = ToDegrees(thresholds.NormalAngleRadians),
                Binormal = ToDegrees(thresholds.BinormalAngleRadians),
                Roll = ToDegrees(thresholds.RollAngleRadians),
                MatrixOrientation = ToDegrees(thresholds.MatrixOrientationAngleRadians)
            };
        }

        private static TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto MapAngleMetric(
            TrackFrameSmoothnessMetricSummary summary)
        {
            return new TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto
            {
                MaxAbsolute = summary.MaxAbsoluteDegrees,
                AverageAbsolute = summary.AverageAbsoluteDegrees
            };
        }

        private static TransportedFrameComparisonCurvatureMetricSummaryV1Dto MapCurvatureMetric(
            TrackFrameCurvatureMetricSummary summary)
        {
            return new TransportedFrameComparisonCurvatureMetricSummaryV1Dto
            {
                MaxAbsolute = summary.MaxAbsolute,
                AverageAbsolute = summary.AverageAbsolute
            };
        }

        private static double ToDegrees(double radians)
        {
            return radians * RadiansToDegrees;
        }
    }
}
