using System;

namespace Quantum.IO.TrackFrameContinuity.V1
{
    /// <summary>
    /// Versioned backend-only export contract for track-frame continuity diagnostics.
    /// </summary>
    public sealed class TrackFrameContinuityDiagnosticsExportV1Dto
    {
        public const string ContractName = "quantum.track_frame_continuity_diagnostics";

        public const int ContractVersion = 1;

        public string Contract { get; set; } = ContractName;

        public int Version { get; set; } = ContractVersion;

        public bool BackendOnly { get; set; } = true;

        public TrackFrameContinuityMetadataV1Dto Metadata { get; set; } =
            new TrackFrameContinuityMetadataV1Dto();

        public TrackFrameContinuityThresholdsDegreesV1Dto ThresholdsDegrees { get; set; } =
            new TrackFrameContinuityThresholdsDegreesV1Dto();

        public TrackFrameContinuitySummaryStatisticsV1Dto SummaryStatistics { get; set; } =
            new TrackFrameContinuitySummaryStatisticsV1Dto();

        public TrackFrameContinuitySampleV1Dto[] Samples { get; set; } =
            Array.Empty<TrackFrameContinuitySampleV1Dto>();

        public TrackFrameContinuityIntervalV1Dto[] Intervals { get; set; } =
            Array.Empty<TrackFrameContinuityIntervalV1Dto>();

        public TrackFrameContinuityIssueV1Dto[] Issues { get; set; } =
            Array.Empty<TrackFrameContinuityIssueV1Dto>();

        public string DiagnosticText { get; set; } = string.Empty;
    }

    public sealed class TrackFrameContinuityMetadataV1Dto
    {
        public string Units { get; set; } = "meters";

        public string? SourceName { get; set; }

        public double TrackLength { get; set; }
    }

    public sealed class TrackFrameContinuityThresholdsDegreesV1Dto
    {
        public double Tangent { get; set; }

        public double Normal { get; set; }

        public double Binormal { get; set; }

        public double Roll { get; set; }

        public double MatrixOrientation { get; set; }
    }

    public sealed class TrackFrameContinuitySummaryStatisticsV1Dto
    {
        public int SampleCount { get; set; }

        public int IntervalCount { get; set; }

        public int IssueCount { get; set; }

        public bool HasIssues { get; set; }

        public TrackFrameContinuityMetricSummaryDegreesV1Dto TangentDegrees { get; set; } =
            new TrackFrameContinuityMetricSummaryDegreesV1Dto();

        public TrackFrameContinuityMetricSummaryDegreesV1Dto NormalDegrees { get; set; } =
            new TrackFrameContinuityMetricSummaryDegreesV1Dto();

        public TrackFrameContinuityMetricSummaryDegreesV1Dto BinormalDegrees { get; set; } =
            new TrackFrameContinuityMetricSummaryDegreesV1Dto();

        public TrackFrameContinuityMetricSummaryDegreesV1Dto RollDegrees { get; set; } =
            new TrackFrameContinuityMetricSummaryDegreesV1Dto();

        public TrackFrameContinuityMetricSummaryDegreesV1Dto MatrixOrientationDegrees { get; set; } =
            new TrackFrameContinuityMetricSummaryDegreesV1Dto();
    }

    public sealed class TrackFrameContinuityMetricSummaryDegreesV1Dto
    {
        public double MaxAbsolute { get; set; }

        public double AverageAbsolute { get; set; }
    }

    public sealed class TrackFrameContinuitySampleV1Dto
    {
        public int SampleIndex { get; set; }

        public double Distance { get; set; }

        public TrackFrameContinuityVector3dV1Dto Position { get; set; } =
            new TrackFrameContinuityVector3dV1Dto();

        public TrackFrameContinuityVector3dV1Dto Tangent { get; set; } =
            new TrackFrameContinuityVector3dV1Dto();

        public TrackFrameContinuityVector3dV1Dto Normal { get; set; } =
            new TrackFrameContinuityVector3dV1Dto();

        public TrackFrameContinuityVector3dV1Dto Binormal { get; set; } =
            new TrackFrameContinuityVector3dV1Dto();
    }

    public sealed class TrackFrameContinuityIntervalV1Dto
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

        public double MatrixOrientationDegrees { get; set; }
    }

    public sealed class TrackFrameContinuityIssueV1Dto
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

    public sealed class TrackFrameContinuityVector3dV1Dto
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double Z { get; set; }
    }
}
