using System;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Quantum.IO.TrackFrameContinuity.V1;
using Quantum.Math;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class TrackFrameContinuityDiagnosticsExportV1Tests
{
    [Fact]
    public void Export_FlippedFrameSyntheticReport_MatchesStableContractSnapshot()
    {
        TrackFrameContinuityDiagnosticsExportV1Dto export = CreateFlippedFrameExport();
        TrackFrameContinuityDiagnosticsExportV1Dto repeatedExport = CreateFlippedFrameExport();

        string actual = FormatExportSnapshot(export);
        string repeated = FormatExportSnapshot(repeatedExport);
        const string expected = """
export contract=quantum.track_frame_continuity_diagnostics version=1 backendOnly=True source=flip-sample trackLength=5.000000
summary samples=2 intervals=1 issues=4 hasIssues=True
thresholds tangent=20.000000 normal=20.000000 binormal=20.000000 roll=20.000000 matrix=20.000000
stats tangent=(0.000000,0.000000) normal=(180.000000,180.000000) binormal=(180.000000,180.000000) roll=(180.000000,180.000000) matrix=(180.000000,180.000000)
sample[0] d=0.000000 p=(0.000000,0.000000,0.000000) t=(1.000000,0.000000,0.000000) n=(0.000000,1.000000,0.000000) b=(0.000000,0.000000,1.000000)
sample[1] d=5.000000 p=(5.000000,0.000000,0.000000) t=(1.000000,0.000000,0.000000) n=(0.000000,-1.000000,0.000000) b=(0.000000,0.000000,-1.000000)
interval[0] samples=0->1 distance=0.000000->5.000000 delta=5.000000 tangent=0.000000 normal=180.000000 binormal=180.000000 roll=180.000000 matrix=180.000000
issue[0] type=Normal sample=1 distance=5.000000 actual=180.000000 threshold=20.000000 exceeded=160.000000
issue[1] type=Binormal sample=1 distance=5.000000 actual=180.000000 threshold=20.000000 exceeded=160.000000
issue[2] type=Roll sample=1 distance=5.000000 actual=180.000000 threshold=20.000000 exceeded=160.000000
issue[3] type=MatrixOrientation sample=1 distance=5.000000 actual=180.000000 threshold=20.000000 exceeded=160.000000
""";

        AssertSnapshot(expected, actual);
        AssertSnapshot(actual, repeated);
    }

    [Fact]
    public void SerializeDeserialize_RoundtripPreservesIssueAndSummaryFields()
    {
        TrackFrameContinuityDiagnosticsExportV1Dto expected = CreateFlippedFrameExport();

        string json = TrackFrameContinuityDiagnosticsExportV1Json.Serialize(expected, indented: true);
        TrackFrameContinuityDiagnosticsExportV1Dto actual =
            TrackFrameContinuityDiagnosticsExportV1Json.Deserialize(json);

        Assert.Contains("\"contract\":", json);
        Assert.Contains("\"summaryStatistics\":", json);
        Assert.Contains("\"issueType\":", json);
        Assert.Contains("\"thresholdDegrees\":", json);
        Assert.DoesNotContain("\"IssueType\":", json);

        Assert.Equal(expected.Contract, actual.Contract);
        Assert.Equal(expected.Version, actual.Version);
        Assert.True(actual.BackendOnly);
        Assert.Equal(expected.Metadata.SourceName, actual.Metadata.SourceName);
        Assert.Equal(expected.SummaryStatistics.SampleCount, actual.SummaryStatistics.SampleCount);
        Assert.Equal(expected.SummaryStatistics.IssueCount, actual.SummaryStatistics.IssueCount);
        Assert.Equal(expected.ThresholdsDegrees.Roll, actual.ThresholdsDegrees.Roll);

        TrackFrameContinuityIssueV1Dto issue = actual.Issues[2];
        Assert.Equal("Roll", issue.IssueType);
        Assert.Equal(1, issue.SampleIndex);
        Assert.Equal(5.0, issue.Distance);
        Assert.Equal(20.0, issue.ThresholdDegrees, 10);
        Assert.Equal(160.0, issue.ExceededByDegrees, 10);
    }

    [Fact]
    public void Deserialize_RejectsWrongContract()
    {
        const string json = @"{""contract"":""wrong.contract"",""version"":1}";

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            TrackFrameContinuityDiagnosticsExportV1Json.Deserialize(json));

        Assert.Contains("contract", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_RejectsMalformedJson()
    {
        const string json = @"{""contract"":""quantum.track_frame_continuity_diagnostics"",""version"":1,";

        JsonException ex = Assert.Throws<JsonException>(() =>
            TrackFrameContinuityDiagnosticsExportV1Json.Deserialize(json));

        Assert.Contains("malformed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_WithDistanceAndFrameCountMismatch_Throws()
    {
        ExportTrackFrame[] frames = CreateFlippedFrames();
        TrackFrameContinuityReport report = TrackFrameContinuityDiagnostics.Analyze(
            frames,
            new[] { 0.0, 5.0 },
            TrackFrameContinuityThresholds.UniformDegrees(20.0));

        var source = new TrackFrameContinuityDiagnosticsExportV1Source
        {
            Frames = frames,
            SampledDistances = new[] { 0.0 },
            Report = report
        };

        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            TrackFrameContinuityDiagnosticsExportV1Mapper.Export(source));

        Assert.Contains("distance count", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static TrackFrameContinuityDiagnosticsExportV1Dto CreateFlippedFrameExport()
    {
        ExportTrackFrame[] frames = CreateFlippedFrames();
        double[] distances = { 0.0, 5.0 };
        TrackFrameContinuityReport report = TrackFrameContinuityDiagnostics.Analyze(
            frames,
            distances,
            TrackFrameContinuityThresholds.UniformDegrees(20.0));

        return TrackFrameContinuityDiagnosticsExportV1Mapper.Export(
            new TrackFrameContinuityDiagnosticsExportV1Source
            {
                Units = "meters",
                SourceName = "flip-sample",
                TrackLength = 5.0,
                Frames = frames,
                SampledDistances = distances,
                Report = report
            });
    }

    private static ExportTrackFrame[] CreateFlippedFrames()
    {
        return new[]
        {
            new ExportTrackFrame(
                distance: 0.0,
                position: new Vector3d(0.0, 0.0, 0.0),
                tangent: Vector3d.UnitX,
                normal: Vector3d.UnitY,
                binormal: Vector3d.UnitZ),
            new ExportTrackFrame(
                distance: 5.0,
                position: new Vector3d(5.0, 0.0, 0.0),
                tangent: Vector3d.UnitX,
                normal: Vector3d.UnitY * -1.0,
                binormal: Vector3d.UnitZ * -1.0)
        };
    }

    private static string FormatExportSnapshot(TrackFrameContinuityDiagnosticsExportV1Dto export)
    {
        var builder = new StringBuilder();
        TrackFrameContinuitySummaryStatisticsV1Dto summary = export.SummaryStatistics;
        TrackFrameContinuityThresholdsDegreesV1Dto thresholds = export.ThresholdsDegrees;

        builder.Append("export contract=").Append(export.Contract)
            .Append(" version=").Append(export.Version)
            .Append(" backendOnly=").Append(export.BackendOnly)
            .Append(" source=").Append(export.Metadata.SourceName)
            .Append(" trackLength=").Append(F(export.Metadata.TrackLength))
            .AppendLine();
        builder.Append("summary samples=").Append(summary.SampleCount)
            .Append(" intervals=").Append(summary.IntervalCount)
            .Append(" issues=").Append(summary.IssueCount)
            .Append(" hasIssues=").Append(summary.HasIssues)
            .AppendLine();
        builder.Append("thresholds tangent=").Append(F(thresholds.Tangent))
            .Append(" normal=").Append(F(thresholds.Normal))
            .Append(" binormal=").Append(F(thresholds.Binormal))
            .Append(" roll=").Append(F(thresholds.Roll))
            .Append(" matrix=").Append(F(thresholds.MatrixOrientation))
            .AppendLine();
        builder.Append("stats tangent=").Append(FormatMetric(summary.TangentDegrees))
            .Append(" normal=").Append(FormatMetric(summary.NormalDegrees))
            .Append(" binormal=").Append(FormatMetric(summary.BinormalDegrees))
            .Append(" roll=").Append(FormatMetric(summary.RollDegrees))
            .Append(" matrix=").Append(FormatMetric(summary.MatrixOrientationDegrees))
            .AppendLine();

        for (int i = 0; i < export.Samples.Length; i++)
        {
            TrackFrameContinuitySampleV1Dto sample = export.Samples[i];
            builder.Append("sample[").Append(i).Append("] d=").Append(F(sample.Distance))
                .Append(" p=").Append(FormatVector(sample.Position))
                .Append(" t=").Append(FormatVector(sample.Tangent))
                .Append(" n=").Append(FormatVector(sample.Normal))
                .Append(" b=").Append(FormatVector(sample.Binormal))
                .AppendLine();
        }

        for (int i = 0; i < export.Intervals.Length; i++)
        {
            TrackFrameContinuityIntervalV1Dto interval = export.Intervals[i];
            builder.Append("interval[").Append(i).Append("] samples=")
                .Append(interval.StartSampleIndex).Append("->").Append(interval.EndSampleIndex)
                .Append(" distance=").Append(F(interval.StartDistance))
                .Append("->").Append(F(interval.EndDistance))
                .Append(" delta=").Append(F(interval.DistanceDelta))
                .Append(" tangent=").Append(F(interval.TangentDegrees))
                .Append(" normal=").Append(F(interval.NormalDegrees))
                .Append(" binormal=").Append(F(interval.BinormalDegrees))
                .Append(" roll=").Append(F(interval.RollDegrees))
                .Append(" matrix=").Append(F(interval.MatrixOrientationDegrees))
                .AppendLine();
        }

        for (int i = 0; i < export.Issues.Length; i++)
        {
            TrackFrameContinuityIssueV1Dto issue = export.Issues[i];
            builder.Append("issue[").Append(i).Append("] type=").Append(issue.IssueType)
                .Append(" sample=").Append(issue.SampleIndex)
                .Append(" distance=").Append(F(issue.Distance))
                .Append(" actual=").Append(F(issue.ActualDegrees))
                .Append(" threshold=").Append(F(issue.ThresholdDegrees))
                .Append(" exceeded=").Append(F(issue.ExceededByDegrees))
                .AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatMetric(TrackFrameContinuityMetricSummaryDegreesV1Dto metric)
    {
        return "(" + F(metric.MaxAbsolute) + "," + F(metric.AverageAbsolute) + ")";
    }

    private static string FormatVector(TrackFrameContinuityVector3dV1Dto vector)
    {
        return "(" + F(vector.X) + "," + F(vector.Y) + "," + F(vector.Z) + ")";
    }

    private static void AssertSnapshot(string expected, string actual)
    {
        string normalizedExpected = NormalizeSnapshot(expected);
        string normalizedActual = NormalizeSnapshot(actual);

        if (!string.Equals(normalizedExpected, normalizedActual, StringComparison.Ordinal))
        {
            Assert.Fail("Snapshot mismatch. Actual snapshot:" + Environment.NewLine + normalizedActual);
        }
    }

    private static string NormalizeSnapshot(string value)
    {
        return value.ReplaceLineEndings("\n").Trim();
    }

    private static string F(double value)
    {
        if (global::System.Math.Abs(value) < 0.0000005)
        {
            value = 0.0;
        }

        return value.ToString("0.000000", CultureInfo.InvariantCulture);
    }
}
