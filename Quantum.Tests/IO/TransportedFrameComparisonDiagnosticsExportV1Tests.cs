using System.Text.Json;
using Quantum.Debug;
using Quantum.IO.TransportedFrameComparison.V1;
using Quantum.Track;

namespace Quantum.Tests;

public sealed class TransportedFrameComparisonDiagnosticsExportV1Tests
{
    [Fact]
    public void SerializeDeserialize_ConstantRadiusTurn_PreservesDeterministicComparisonContent()
    {
        DiagnosticTrackFixture fixture = DiagnosticTrackFixtures.ConstantRadiusTurn();
        TransportedFrameComparisonDiagnosticsExportV1Dto expected = CreateExport(fixture);

        string json = TransportedFrameComparisonDiagnosticsExportV1Json.Serialize(expected, indented: true);
        TransportedFrameComparisonDiagnosticsExportV1Dto actual =
            TransportedFrameComparisonDiagnosticsExportV1Json.Deserialize(json);

        Assert.Contains("\"contract\":", json);
        Assert.Contains("\"summaryMetrics\":", json);
        Assert.Contains("\"smoothnessMetrics\":", json);
        Assert.Contains("\"continuityMetrics\":", json);
        Assert.Contains("\"matrixOrientationDegrees\":", json);
        Assert.DoesNotContain("\"SummaryMetrics\":", json);

        Assert.Equal(TransportedFrameComparisonDiagnosticsExportV1Dto.ContractName, actual.Contract);
        Assert.Equal(TransportedFrameComparisonDiagnosticsExportV1Dto.ContractVersion, actual.Version);
        Assert.True(actual.BackendOnly);
        Assert.Equal("diagnostic-track-fixtures", actual.Metadata.SourceName);
        Assert.Equal(1, actual.Metadata.ReportCount);
        Assert.Equal(new[] { DiagnosticTrackFixtures.ConstantRadiusTurnName }, actual.Metadata.FixtureNames);

        TransportedFrameComparisonReportV1Dto report = Assert.Single(actual.Reports);
        Assert.Equal(DiagnosticTrackFixtures.ConstantRadiusTurnName, report.SourceName);
        Assert.Equal(fixture.Document.TotalLength, report.TrackLength, 10);
        Assert.Equal(fixture.SampleDistances.Count, report.SummaryMetrics.SampleCount);
        Assert.Equal(fixture.SampleDistances.Count, report.Samples.Length);
        Assert.Equal(fixture.SampleDistances.Count - 1, report.SmoothnessMetrics.Stateless.IntervalCount);
        Assert.Equal(fixture.SampleDistances.Count - 1, report.SmoothnessMetrics.Transported.IntervalCount);
        Assert.Equal(fixture.SampleDistances.Count - 1, report.ContinuityMetrics.Stateless.IntervalCount);
        Assert.Equal(fixture.SampleDistances.Count - 1, report.ContinuityMetrics.Transported.IntervalCount);
        Assert.False(report.ContinuityMetrics.Stateless.HasIssues);
        Assert.False(report.ContinuityMetrics.Transported.HasIssues);

        Assert.Equal(0.0, report.Samples[0].Distance, 10);
        Assert.Equal(fixture.Document.TotalLength, report.Samples[report.Samples.Length - 1].Distance, 10);
        Assert.InRange(report.SummaryMetrics.FrameDegrees.MaxAbsolute, 0.0, 1e-6);
        Assert.Equal(181.0, report.ContinuityMetrics.ThresholdsDegrees.Tangent, 10);
        Assert.Contains("Frame continuity:", report.ContinuityMetrics.Transported.DiagnosticText);
    }

    [Fact]
    public void Export_QuarterLoopLike_CapturesTransportedSmoothnessReduction()
    {
        DiagnosticTrackFixture fixture = DiagnosticTrackFixtures.QuarterLoopLike();
        TransportedFrameComparisonDiagnosticsExportV1Dto export = CreateExport(fixture);

        TransportedFrameComparisonReportV1Dto report = Assert.Single(export.Reports);

        Assert.True(
            report.SmoothnessMetrics.Transported.NormalDegrees.MaxAbsolute <
            report.SmoothnessMetrics.Stateless.NormalDegrees.MaxAbsolute);
        Assert.True(
            report.SmoothnessMetrics.Transported.BinormalDegrees.MaxAbsolute <
            report.SmoothnessMetrics.Stateless.BinormalDegrees.MaxAbsolute);
        Assert.True(report.SummaryMetrics.NormalDegrees.MaxAbsolute > 0.0);
        Assert.True(report.SummaryMetrics.BinormalDegrees.MaxAbsolute > 0.0);
    }

    [Fact]
    public void Deserialize_RejectsWrongContract()
    {
        const string json = @"{""contract"":""wrong.contract"",""version"":1}";

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            TransportedFrameComparisonDiagnosticsExportV1Json.Deserialize(json));

        Assert.Contains("contract", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_RejectsMalformedJson()
    {
        const string json = @"{""contract"":""quantum.transported_frame_comparison_diagnostics"",""version"":1,";

        JsonException ex = Assert.Throws<JsonException>(() =>
            TransportedFrameComparisonDiagnosticsExportV1Json.Deserialize(json));

        Assert.Contains("malformed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static TransportedFrameComparisonDiagnosticsExportV1Dto CreateExport(
        DiagnosticTrackFixture fixture)
    {
        TransportedFrameComparisonReport report = TransportedFrameComparisonDiagnostics.Compare(
            fixture.Document,
            fixture.SampleDistances,
            TrackFrameContinuityThresholds.UniformDegrees(181.0));

        return TransportedFrameComparisonDiagnosticsExportV1Mapper.Export(
            new TransportedFrameComparisonDiagnosticsExportV1Source
            {
                Units = "meters",
                SourceName = "diagnostic-track-fixtures",
                Reports = new[]
                {
                    new TransportedFrameComparisonDiagnosticsExportV1ReportSource
                    {
                        SourceName = fixture.Name,
                        TrackLength = fixture.Document.TotalLength,
                        Report = report
                    }
                }
            });
    }
}
