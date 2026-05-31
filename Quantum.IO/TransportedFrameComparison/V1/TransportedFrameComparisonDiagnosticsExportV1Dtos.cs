using System;

namespace Quantum.IO.TransportedFrameComparison.V1
{
    /// <summary>
    /// Versioned backend-only export contract for transported frame comparison diagnostics.
    /// </summary>
    public sealed class TransportedFrameComparisonDiagnosticsExportV1Dto
    {
        public const string ContractName = "quantum.transported_frame_comparison_diagnostics";

        public const int ContractVersion = 1;

        public string Contract { get; set; } = ContractName;

        public int Version { get; set; } = ContractVersion;

        public bool BackendOnly { get; set; } = true;

        public TransportedFrameComparisonMetadataV1Dto Metadata { get; set; } =
            new TransportedFrameComparisonMetadataV1Dto();

        public TransportedFrameComparisonReportV1Dto[] Reports { get; set; } =
            Array.Empty<TransportedFrameComparisonReportV1Dto>();
    }

    public sealed class TransportedFrameComparisonMetadataV1Dto
    {
        public string Units { get; set; } = "meters";

        public string? SourceName { get; set; }

        public int ReportCount { get; set; }

        public string[] FixtureNames { get; set; } = Array.Empty<string>();
    }

    public sealed class TransportedFrameComparisonReportV1Dto
    {
        public string? SourceName { get; set; }

        public double TrackLength { get; set; }

        public TransportedFrameComparisonSummaryMetricsV1Dto SummaryMetrics { get; set; } =
            new TransportedFrameComparisonSummaryMetricsV1Dto();

        public TransportedFrameComparisonSampleDeltaV1Dto[] Samples { get; set; } =
            Array.Empty<TransportedFrameComparisonSampleDeltaV1Dto>();

        public TransportedFrameComparisonSmoothnessMetricsV1Dto SmoothnessMetrics { get; set; } =
            new TransportedFrameComparisonSmoothnessMetricsV1Dto();

        public TransportedFrameComparisonContinuityMetricsV1Dto ContinuityMetrics { get; set; } =
            new TransportedFrameComparisonContinuityMetricsV1Dto();
    }

    public sealed class TransportedFrameComparisonSummaryMetricsV1Dto
    {
        public int SampleCount { get; set; }

        public int StatelessContinuityIssueCount { get; set; }

        public int TransportedContinuityIssueCount { get; set; }

        public bool StatelessHasContinuityIssues { get; set; }

        public bool TransportedHasContinuityIssues { get; set; }

        public TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto TangentDegrees { get; set; } =
            new TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto();

        public TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto NormalDegrees { get; set; } =
            new TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto();

        public TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto BinormalDegrees { get; set; } =
            new TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto();

        public TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto FrameDegrees { get; set; } =
            new TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto();

        public TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto RollDegrees { get; set; } =
            new TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto();

        public TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto MatrixOrientationDegrees { get; set; } =
            new TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto();
    }

    public sealed class TransportedFrameComparisonSampleDeltaV1Dto
    {
        public int SampleIndex { get; set; }

        public double Distance { get; set; }

        public double TangentDegrees { get; set; }

        public double NormalDegrees { get; set; }

        public double BinormalDegrees { get; set; }

        public double FrameDegrees { get; set; }

        public double RollDegrees { get; set; }

        public double AbsoluteRollDegrees { get; set; }

        public double MatrixOrientationDegrees { get; set; }
    }

    public sealed class TransportedFrameComparisonSmoothnessMetricsV1Dto
    {
        public TransportedFrameComparisonSmoothnessReportV1Dto Stateless { get; set; } =
            new TransportedFrameComparisonSmoothnessReportV1Dto();

        public TransportedFrameComparisonSmoothnessReportV1Dto Transported { get; set; } =
            new TransportedFrameComparisonSmoothnessReportV1Dto();
    }

    public sealed class TransportedFrameComparisonSmoothnessReportV1Dto
    {
        public int IntervalCount { get; set; }

        public TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto TangentDegrees { get; set; } =
            new TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto();

        public TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto NormalDegrees { get; set; } =
            new TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto();

        public TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto BinormalDegrees { get; set; } =
            new TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto();

        public TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto FrameDegrees { get; set; } =
            new TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto();

        public TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto RollDegrees { get; set; } =
            new TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto();

        public TransportedFrameComparisonCurvatureMetricSummaryV1Dto CurvatureEstimate { get; set; } =
            new TransportedFrameComparisonCurvatureMetricSummaryV1Dto();

        public TransportedFrameComparisonCurvatureMetricSummaryV1Dto CurvatureEstimateDelta { get; set; } =
            new TransportedFrameComparisonCurvatureMetricSummaryV1Dto();

        public TransportedFrameComparisonSmoothnessIntervalV1Dto[] Intervals { get; set; } =
            Array.Empty<TransportedFrameComparisonSmoothnessIntervalV1Dto>();
    }

    public sealed class TransportedFrameComparisonSmoothnessIntervalV1Dto
    {
        public int StartSampleIndex { get; set; }

        public int EndSampleIndex { get; set; }

        public double StartDistance { get; set; }

        public double EndDistance { get; set; }

        public double DistanceDelta { get; set; }

        public double TangentDegrees { get; set; }

        public double NormalDegrees { get; set; }

        public double BinormalDegrees { get; set; }

        public double FrameDegrees { get; set; }

        public double RollDegrees { get; set; }

        public double AbsoluteRollDegrees { get; set; }

        public double CurvatureEstimate { get; set; }

        public double CurvatureEstimateDelta { get; set; }
    }

    public sealed class TransportedFrameComparisonContinuityMetricsV1Dto
    {
        public TransportedFrameComparisonThresholdsDegreesV1Dto ThresholdsDegrees { get; set; } =
            new TransportedFrameComparisonThresholdsDegreesV1Dto();

        public TransportedFrameComparisonContinuityReportV1Dto Stateless { get; set; } =
            new TransportedFrameComparisonContinuityReportV1Dto();

        public TransportedFrameComparisonContinuityReportV1Dto Transported { get; set; } =
            new TransportedFrameComparisonContinuityReportV1Dto();
    }

    public sealed class TransportedFrameComparisonContinuityReportV1Dto
    {
        public int IntervalCount { get; set; }

        public int IssueCount { get; set; }

        public bool HasIssues { get; set; }

        public TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto TangentDegrees { get; set; } =
            new TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto();

        public TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto NormalDegrees { get; set; } =
            new TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto();

        public TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto BinormalDegrees { get; set; } =
            new TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto();

        public TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto RollDegrees { get; set; } =
            new TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto();

        public TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto MatrixOrientationDegrees { get; set; } =
            new TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto();

        public TransportedFrameComparisonContinuityIntervalV1Dto[] Intervals { get; set; } =
            Array.Empty<TransportedFrameComparisonContinuityIntervalV1Dto>();

        public TransportedFrameComparisonContinuityIssueV1Dto[] Issues { get; set; } =
            Array.Empty<TransportedFrameComparisonContinuityIssueV1Dto>();

        public string DiagnosticText { get; set; } = string.Empty;
    }

    public sealed class TransportedFrameComparisonContinuityIntervalV1Dto
    {
        public int StartSampleIndex { get; set; }

        public int EndSampleIndex { get; set; }

        public double StartDistance { get; set; }

        public double EndDistance { get; set; }

        public double DistanceDelta { get; set; }

        public double TangentDegrees { get; set; }

        public double NormalDegrees { get; set; }

        public double BinormalDegrees { get; set; }

        public double RollDegrees { get; set; }

        public double AbsoluteRollDegrees { get; set; }

        public double MatrixOrientationDegrees { get; set; }
    }

    public sealed class TransportedFrameComparisonContinuityIssueV1Dto
    {
        public string IssueType { get; set; } = string.Empty;

        public int SampleIndex { get; set; }

        public double Distance { get; set; }

        public int StartSampleIndex { get; set; }

        public int EndSampleIndex { get; set; }

        public double StartDistance { get; set; }

        public double EndDistance { get; set; }

        public double DistanceDelta { get; set; }

        public double ActualDegrees { get; set; }

        public double ThresholdDegrees { get; set; }

        public double ExceededByDegrees { get; set; }
    }

    public sealed class TransportedFrameComparisonThresholdsDegreesV1Dto
    {
        public double Tangent { get; set; }

        public double Normal { get; set; }

        public double Binormal { get; set; }

        public double Roll { get; set; }

        public double MatrixOrientation { get; set; }
    }

    public sealed class TransportedFrameComparisonAngleMetricSummaryDegreesV1Dto
    {
        public double MaxAbsolute { get; set; }

        public double AverageAbsolute { get; set; }
    }

    public sealed class TransportedFrameComparisonCurvatureMetricSummaryV1Dto
    {
        public double MaxAbsolute { get; set; }

        public double AverageAbsolute { get; set; }
    }
}
