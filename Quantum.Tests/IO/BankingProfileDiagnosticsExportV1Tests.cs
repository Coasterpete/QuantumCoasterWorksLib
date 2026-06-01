using System.Text.Json;
using Quantum.IO.BankingProfile.V1;
using Quantum.Track;

namespace Quantum.Tests;

public sealed class BankingProfileDiagnosticsExportV1Tests
{
    [Fact]
    public void SerializeDeserialize_ProfileDiagnostics_PreservesStableContractContent()
    {
        BankingProfileDiagnosticsExportV1Dto expected = CreateExport();

        string json = BankingProfileDiagnosticsExportV1Json.Serialize(expected, indented: true);
        BankingProfileDiagnosticsExportV1Dto actual =
            BankingProfileDiagnosticsExportV1Json.Deserialize(json);

        Assert.Contains("\"contract\":", json);
        Assert.Contains("\"summaryMetrics\":", json);
        Assert.Contains("\"rollRadians\":", json);
        Assert.Contains("\"rollDegrees\":", json);
        Assert.Contains("\"approximateRollSlopeRadPerMeter\":", json);
        Assert.DoesNotContain("\"SummaryMetrics\":", json);

        Assert.Equal(BankingProfileDiagnosticsExportV1Dto.ContractName, actual.Contract);
        Assert.Equal(BankingProfileDiagnosticsExportV1Dto.ContractVersion, actual.Version);
        Assert.True(actual.BackendOnly);
        Assert.Equal("unit-test-banking-profile", actual.Metadata.SourceName);
        Assert.Equal(3, actual.Metadata.ProfileKeyCount);
        Assert.Equal("meters", actual.Metadata.DistanceUnit);
        Assert.Equal("radians_per_meter", actual.Metadata.RollSlopeUnit);

        Assert.Equal(5, actual.SummaryMetrics.SampleCount);
        Assert.Equal(0.0, actual.SummaryMetrics.MinRollRadians, 10);
        Assert.Equal(3.0, actual.SummaryMetrics.MaxRollRadians, 10);
        Assert.True(actual.SummaryMetrics.MaxAbsoluteRollSlopeRadPerMeter > 0.0);

        BankingProfileDiagnosticsSampleV1Dto sample = actual.Samples[3];
        Assert.Equal(3, sample.SampleIndex);
        Assert.Equal(15.0, sample.Distance, 10);
        Assert.Equal(2.0, sample.RollRadians, 10);
        Assert.Equal("Linear", sample.InterpolationMode);
        Assert.Equal("KeyInterval", sample.SourceKind);
        Assert.Equal(1, sample.SourceInterval.StartKeyIndex);
        Assert.Equal(2, sample.SourceInterval.EndKeyIndex);
        Assert.Equal(10.0, sample.SourceInterval.StartDistance, 10);
        Assert.Equal(20.0, sample.SourceInterval.EndDistance, 10);
        Assert.Equal(0.2, sample.ApproximateRollSlopeRadPerMeter!.Value, 10);
    }

    [Fact]
    public void Deserialize_RejectsWrongContract()
    {
        const string json = @"{""contract"":""wrong.contract"",""version"":1}";

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            BankingProfileDiagnosticsExportV1Json.Deserialize(json));

        Assert.Contains("contract", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_RejectsMalformedJson()
    {
        const string json = @"{""contract"":""quantum.banking_profile_diagnostics"",""version"":1,";

        JsonException ex = Assert.Throws<JsonException>(() =>
            BankingProfileDiagnosticsExportV1Json.Deserialize(json));

        Assert.Contains("malformed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_WithNullReport_Throws()
    {
        var source = new BankingProfileDiagnosticsExportV1Source();

        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            BankingProfileDiagnosticsExportV1Mapper.Export(source));

        Assert.Contains("report", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static BankingProfileDiagnosticsExportV1Dto CreateExport()
    {
        var profile = new BankingProfile(new[]
        {
            new BankingProfileKey(0.0, 0.0, BankingProfileInterpolationMode.Constant),
            new BankingProfileKey(10.0, 1.0, BankingProfileInterpolationMode.Linear),
            new BankingProfileKey(20.0, 3.0, BankingProfileInterpolationMode.Linear)
        });

        BankingProfileDiagnosticsReport report = BankingProfileDiagnostics.Sample(
            profile,
            new[] { 0.0, 5.0, 10.0, 15.0, 20.0 });

        return BankingProfileDiagnosticsExportV1Mapper.Export(
            new BankingProfileDiagnosticsExportV1Source
            {
                Units = "meters,radians",
                SourceName = "unit-test-banking-profile",
                ProfileKeyCount = profile.Keys.Count,
                Report = report
            });
    }
}
