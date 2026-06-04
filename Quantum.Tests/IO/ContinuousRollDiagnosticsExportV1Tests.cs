
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

using Quantum.IO.ContinuousRollDiagnostics.V1;
using Quantum.Track;
using SystemMath = System.Math;

namespace Quantum.Tests;

public sealed class ContinuousRollDiagnosticsExportV1Tests
{
    private const double Tolerance = 1e-9;


    private static readonly Lazy<JsonSchema> ContinuousRollDiagnosticsSchema = new(
        CreateContinuousRollDiagnosticsSchema);


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

    public void Serialize_Indented_MatchesGoldenFixture()
    {
        ContinuousRollDiagnosticsExportV1Dto dto = CreateGoldenFixtureExport();

        string actual = NormalizeLineEndings(ContinuousRollDiagnosticsExportV1Json.Serialize(dto, indented: true)).TrimEnd();
        string expected = NormalizeLineEndings(LoadGoldenFixtureJson()).TrimEnd();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Deserialize_GoldenFixture_PreservesContractVersionAndRepresentativeValues()
    {
        ContinuousRollDiagnosticsExportV1Dto dto =
            ContinuousRollDiagnosticsExportV1Json.Deserialize(LoadGoldenFixtureJson());

        Assert.Equal(ContinuousRollDiagnosticsExportV1Dto.ContractName, dto.Contract);
        Assert.Equal(ContinuousRollDiagnosticsExportV1Dto.ContractVersion, dto.Version);
        Assert.Equal(7, dto.SampleCount);
        Assert.True(dto.WrapHandlingEnabled);
        Assert.Equal(1, dto.WarningCount);
        Assert.Equal(7, dto.Samples.Length);
        AssertNear(ToRadians(10.0), dto.Samples[1].DeltaRadians);
        AssertNear(361.0, dto.Samples[3].RollDegrees);
        AssertNear(ToRadians(100.0), dto.Samples[6].DeltaRadians);
        Assert.NotNull(dto.Samples[6].Warning);
        Assert.Contains("RollDelta exceeded", dto.Samples[6].Warning, StringComparison.Ordinal);
        Assert.Contains("samples=5->6", dto.Samples[6].Warning, StringComparison.Ordinal);
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


    [Fact]
    public void SchemaValidation_GoldenFixture_IsValid()
    {
        string json = LoadGoldenFixtureJson();

        Assert.True(IsValidAgainstSchema(json));
    }

    [Fact]
    public void SchemaValidation_RuntimeGeneratedExport_IsValid()
    {
        ContinuousRollDiagnosticsExportV1Dto dto = CreateExport(
            new[] { 0.0, 10.0, 20.0 },
            new[] { ToRadians(340.0), ToRadians(350.0), ToRadians(359.0) });

        string json = ContinuousRollDiagnosticsExportV1Json.Serialize(dto);

        Assert.True(IsValidAgainstSchema(json));
    }

    [Fact]
    public void SchemaValidation_RejectsMissingRequiredField()
    {
        JsonObject json = ParseJsonObject(LoadGoldenFixtureJson());
        Assert.True(json.Remove("sampleCount"));

        Assert.False(IsValidAgainstSchema(json.ToJsonString()));
    }

    [Fact]
    public void SchemaValidation_RejectsWrongTypedRequiredField()
    {
        JsonObject json = ParseJsonObject(LoadGoldenFixtureJson());
        json["version"] = "1";

        Assert.False(IsValidAgainstSchema(json.ToJsonString()));
    }

    private static ContinuousRollDiagnosticsExportV1Dto CreateGoldenFixtureExport()
    {
        return CreateExport(
            new[] { 0.0, 10.0, 20.0, 30.0, 40.0, 50.0, 60.0 },
            new[]
            {
                ToRadians(340.0),
                ToRadians(350.0),
                ToRadians(359.0),
                ToRadians(1.0),
                ToRadians(10.0),
                ToRadians(20.0),
                ToRadians(120.0)
            });
    }



    private static ContinuousRollDiagnosticsExportV1Dto CreateExport(
        IReadOnlyList<double> distances,
        IReadOnlyList<double> rollRadians)
    {
        ContinuousRollDiagnosticsReport report =
            ContinuousRollDiagnostics.AnalyzeRollRadians(distances, rollRadians);

        return ContinuousRollDiagnosticsExportV1Mapper.Export(report);
    }


    private static string LoadGoldenFixtureJson()
    {
        string fixturePath = Path.Combine(AppContext.BaseDirectory, "IO", "Fixtures", "ContinuousRollDiagnosticsExportV1.golden.json");
        Assert.True(File.Exists(fixturePath), $"Golden fixture file was not found at '{fixturePath}'.");
        return File.ReadAllText(fixturePath);
    }

    private static bool IsValidAgainstSchema(string instanceJson)
    {
        using JsonDocument instanceDocument = JsonDocument.Parse(instanceJson);

        EvaluationResults results = ContinuousRollDiagnosticsSchema.Value.Evaluate(
            instanceDocument.RootElement,
            new EvaluationOptions
            {
                OutputFormat = OutputFormat.List
            });

        return results.IsValid;
    }

    private static JsonSchema CreateContinuousRollDiagnosticsSchema()
    {
        string schemaPath = Path.Combine(AppContext.BaseDirectory, "IO", "Fixtures", "ContinuousRollDiagnosticsExportV1.schema.json");
        if (!File.Exists(schemaPath))
        {
            throw new FileNotFoundException($"Schema file was not found at '{schemaPath}'.", schemaPath);
        }

        string schemaJson = File.ReadAllText(schemaPath);
        return JsonSchema.FromText(schemaJson);
    }

    private static JsonObject ParseJsonObject(string json)
    {
        JsonNode? node = JsonNode.Parse(json);
        Assert.NotNull(node);
        return Assert.IsType<JsonObject>(node);
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.ReplaceLineEndings("\n");
    }

    private static double ToRadians(double degrees)
    {
        return degrees * (SystemMath.PI / 180.0);
    }

    private static void AssertNear(double expected, double actual)
    {
        Assert.InRange(SystemMath.Abs(expected - actual), 0.0, Tolerance);
    }
}
