using System.Text.Json;
using Quantum.IO.ContinuousRollDiagnostics.V1;
using Quantum.Track;
using SystemMath = System.Math;

namespace Quantum.Tests;

public sealed class ContinuousRollDiagnosticsExportV1Tests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void SerializeDeserialize_ReportDiagnostics_PreservesContractAndCamelCaseContent()
    {
        ContinuousRollDiagnosticsExportV1Dto expected = CreateExport(
            new[] { 0.0, 10.0, 20.0 },
            new[] { ToRadians(0.0), ToRadians(10.0), ToRadians(120.0) });

        string json = ContinuousRollDiagnosticsExportV1Json.Serialize(expected, indented: true);
        ContinuousRollDiagnosticsExportV1Dto actual =
            ContinuousRollDiagnosticsExportV1Json.Deserialize(json);

        Assert.Contains("\"contract\":", json);
        Assert.Contains("\"version\":", json);
        Assert.Contains("\"sampleCount\":", json);
        Assert.Contains("\"maxRollRateRadiansPerMeter\":", json);
        Assert.Contains("\"wrapHandlingEnabled\":", json);
        Assert.Contains("\"stationDistance\":", json);
        Assert.Contains("\"rollRateRadiansPerMeter\":", json);
        Assert.DoesNotContain("\"Contract\":", json);
        Assert.DoesNotContain("\"warning\": null", json);

        Assert.Equal(ContinuousRollDiagnosticsExportV1Dto.ContractName, actual.Contract);
        Assert.Equal(ContinuousRollDiagnosticsExportV1Dto.ContractVersion, actual.Version);
        Assert.Equal(3, actual.SampleCount);
        Assert.Equal(3, actual.Samples.Length);
        Assert.True(actual.WrapHandlingEnabled);
        Assert.Equal(1, actual.WarningCount);
        AssertNear(ToRadians(11.0), actual.MaxRollRateRadiansPerMeter);
        AssertNear(ToRadians(6.0), actual.AverageRollRateRadiansPerMeter);
    }

    [Fact]
    public void Serialize_SameReportTwice_IsDeterministic()
    {
        string firstJson = ContinuousRollDiagnosticsExportV1Json.Serialize(
            CreateExport(
                new[] { 0.0, 10.0, 20.0 },
                new[] { ToRadians(340.0), ToRadians(350.0), ToRadians(359.0) }),
            indented: true);

        string secondJson = ContinuousRollDiagnosticsExportV1Json.Serialize(
            CreateExport(
                new[] { 0.0, 10.0, 20.0 },
                new[] { ToRadians(340.0), ToRadians(350.0), ToRadians(359.0) }),
            indented: true);

        Assert.Equal(firstJson, secondJson);
    }

    [Fact]
    public void Export_WrappedFullTurnTransition_Treats359To1AsSmallContinuousDelta()
    {
        ContinuousRollDiagnosticsExportV1Dto artifact = CreateExport(
            new[] { 0.0, 1.0 },
            new[] { ToRadians(359.0), ToRadians(1.0) });

        Assert.Equal(2, artifact.SampleCount);
        Assert.True(artifact.WrapHandlingEnabled);
        Assert.Equal(0, artifact.WarningCount);

        ContinuousRollDiagnosticsSampleV1Dto first = artifact.Samples[0];
        ContinuousRollDiagnosticsSampleV1Dto second = artifact.Samples[1];

        AssertNear(359.0, first.RollDegrees);
        AssertNear(361.0, second.RollDegrees);
        AssertNear(ToRadians(2.0), second.DeltaRadians);
        AssertNear(2.0, second.DeltaDegrees);
        AssertNear(ToRadians(2.0), second.RollRateRadiansPerMeter);
        Assert.Null(second.Warning);
    }

    [Fact]
    public void Serialize_DiscontinuityWarning_OmitsCleanWarningsAndWritesSampleWarningText()
    {
        ContinuousRollDiagnosticsExportV1Dto artifact = CreateExport(
            new[] { 0.0, 10.0 },
            new[] { ToRadians(0.0), ToRadians(120.0) });

        string json = ContinuousRollDiagnosticsExportV1Json.Serialize(artifact, indented: true);
        ContinuousRollDiagnosticsExportV1Dto actual =
            ContinuousRollDiagnosticsExportV1Json.Deserialize(json);

        Assert.Equal(1, actual.WarningCount);
        Assert.Null(actual.Samples[0].Warning);
        Assert.NotNull(actual.Samples[1].Warning);
        Assert.Contains("RollDelta exceeded", actual.Samples[1].Warning, StringComparison.Ordinal);
        Assert.Contains("samples=0->1", actual.Samples[1].Warning, StringComparison.Ordinal);
        Assert.Contains("actualRadians=", actual.Samples[1].Warning, StringComparison.Ordinal);
        Assert.Contains("\"warning\": \"RollDelta exceeded", json);
        Assert.DoesNotContain("\"warning\": null", json);
    }

    [Fact]
    public void Deserialize_RejectsWrongContract()
    {
        const string json = @"{""contract"":""wrong.contract"",""version"":1}";

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ContinuousRollDiagnosticsExportV1Json.Deserialize(json));

        Assert.Contains("contract", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_RejectsMalformedJson()
    {
        const string json = @"{""contract"":""quantum.continuous_roll_diagnostics"",""version"":1,";

        JsonException ex = Assert.Throws<JsonException>(() =>
            ContinuousRollDiagnosticsExportV1Json.Deserialize(json));

        Assert.Contains("malformed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ContinuousRollDiagnosticsExportV1Dto CreateExport(
        IReadOnlyList<double> distances,
        IReadOnlyList<double> rollRadians)
    {
        ContinuousRollDiagnosticsReport report =
            ContinuousRollDiagnostics.AnalyzeRollRadians(distances, rollRadians);

        return ContinuousRollDiagnosticsExportV1Mapper.Export(report);
    }

    private static double ToRadians(double degrees)
    {
        return degrees * SystemMath.PI / 180.0;
    }

    private static void AssertNear(double expected, double actual)
    {
        Assert.InRange(SystemMath.Abs(expected - actual), 0.0, Tolerance);
    }
}
