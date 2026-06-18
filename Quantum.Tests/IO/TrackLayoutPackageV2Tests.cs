using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Quantum.IO.TrackLayout.V2;

namespace Quantum.Tests;

public sealed class TrackLayoutPackageV2Tests
{
    private static readonly Lazy<JsonSchema> TrackLayoutPackageSchema = new(CreateTrackLayoutPackageSchema);

    [Fact]
    public void ContractConstantsAndVocabulary_AreStable()
    {
        var dto = new TrackLayoutPackageV2Dto();

        Assert.Equal("quantum.track_layout_package", TrackLayoutPackageV2Dto.ContractName);
        Assert.Equal(2, TrackLayoutPackageV2Dto.ContractVersion);
        Assert.Equal(TrackLayoutPackageV2Dto.ContractName, dto.Contract);
        Assert.Equal(TrackLayoutPackageV2Dto.ContractVersion, dto.Version);

        Assert.Equal("straight", TrackLayoutPackageV2Vocabulary.StraightSectionKind);
        Assert.Equal("constantCurvature", TrackLayoutPackageV2Vocabulary.ConstantCurvatureSectionKind);
        Assert.Equal("curvatureTransition", TrackLayoutPackageV2Vocabulary.CurvatureTransitionSectionKind);
        Assert.Equal("spatial", TrackLayoutPackageV2Vocabulary.SpatialSectionKind);
        Assert.Equal("linear", TrackLayoutPackageV2Vocabulary.CurvatureInterpolationLinear);
        Assert.Equal("constantOffset", TrackLayoutPackageV2Vocabulary.HeartlineKindConstantOffset);
        Assert.Equal(
            "centerlineStation",
            TrackLayoutPackageV2Vocabulary.HeartlineDistanceDomainCenterlineStation);
        Assert.Equal("sampledFrame", TrackLayoutPackageV2Vocabulary.HeartlineAxisSourceSampledFrame);

        Assert.True(TrackLayoutPackageV2Vocabulary.IsKnownSectionKind("spatial"));
        Assert.True(TrackLayoutPackageV2Vocabulary.IsKnownCurvatureTransitionInterpolation("linear"));
        Assert.False(TrackLayoutPackageV2Vocabulary.IsKnownCurvatureTransitionInterpolation("smoothStep"));
        Assert.True(TrackLayoutPackageV2Vocabulary.IsKnownBankingInterpolation("sinusoidal"));
        Assert.True(TrackLayoutPackageV2Vocabulary.IsKnownHeartlineKind("constantOffset"));
        Assert.True(TrackLayoutPackageV2Vocabulary.IsKnownHeartlineDistanceDomain("centerlineStation"));
        Assert.True(TrackLayoutPackageV2Vocabulary.IsKnownHeartlineAxisSource("sampledFrame"));
    }

    [Fact]
    public void DefaultDtoShape_IsStable()
    {
        var dto = new TrackLayoutPackageV2Dto();

        Assert.Equal("meters", dto.Metadata.Units);
        Assert.Null(dto.Metadata.SourceName);
        Assert.Null(dto.Metadata.LayoutId);
        Assert.Equal(1.0, dto.StartPose.Tangent.X);
        Assert.Equal(1.0, dto.StartPose.Normal.Y);
        Assert.Equal(1.0, dto.StartPose.Binormal.Z);
        Assert.Empty(dto.Sections);
        Assert.Null(dto.Banking);
        Assert.Null(dto.Heartline);

        string json = TrackLayoutPackageV2Json.Serialize(dto, indented: true);
        Assert.Contains("\"layoutId\": null", json);
        Assert.Contains("\"banking\": null", json);
        Assert.Contains("\"heartline\": null", json);
    }

    [Fact]
    public void Serialize_RepresentativePackage_IsDeterministicCamelCaseJson()
    {
        TrackLayoutPackageV2Dto dto = CreateConstantHeartlineDto();

        string first = NormalizeLineEndings(TrackLayoutPackageV2Json.Serialize(dto, indented: true)).TrimEnd();
        string second = NormalizeLineEndings(TrackLayoutPackageV2Json.Serialize(dto, indented: true)).TrimEnd();
        string roundtrip = NormalizeLineEndings(
            TrackLayoutPackageV2Json.Serialize(
                TrackLayoutPackageV2Json.Deserialize(first),
                indented: true)).TrimEnd();

        Assert.Equal(first, second);
        Assert.Equal(first, roundtrip);
        Assert.Contains("\"startPose\":", first);
        Assert.Contains("\"layoutId\":", first);
        Assert.Contains("\"constantOffset\"", first);
        Assert.Contains("\"normalOffset\":", first);
        Assert.DoesNotContain("\"StartPose\":", first);
        Assert.DoesNotContain("\"NormalOffset\":", first);
    }

    [Fact]
    public void SchemaValidation_MinimalV2Json_IsValid()
    {
        string json = TrackLayoutPackageV2Json.Serialize(CreateMinimalDto());

        Assert.True(IsValidAgainstSchema(json));
    }

    [Fact]
    public void SchemaValidation_ConstantHeartlineV2Json_IsValid()
    {
        string json = TrackLayoutPackageV2Json.Serialize(CreateConstantHeartlineDto());

        Assert.True(IsValidAgainstSchema(json));
    }

    [Fact]
    public void SchemaValidation_RejectsWrongVersion()
    {
        JsonObject json = ParseJsonObject(TrackLayoutPackageV2Json.Serialize(CreateMinimalDto()));
        json["version"] = 1;

        Assert.False(IsValidAgainstSchema(json.ToJsonString()));
    }

    [Fact]
    public void SchemaValidation_RejectsUnknownRootFields()
    {
        JsonObject json = ParseJsonObject(TrackLayoutPackageV2Json.Serialize(CreateMinimalDto()));
        json["train"] = new JsonObject();

        Assert.False(IsValidAgainstSchema(json.ToJsonString()));
    }

    private static TrackLayoutPackageV2Dto CreateMinimalDto()
    {
        return new TrackLayoutPackageV2Dto
        {
            Metadata = new TrackLayoutMetadataV2Dto
            {
                Units = "meters",
                SourceName = "Minimal V2 layout",
                LayoutId = null
            },
            Sections = new[]
            {
                new TrackLayoutSectionV2Dto
                {
                    Kind = TrackLayoutPackageV2Vocabulary.StraightSectionKind,
                    Id = "entry",
                    Length = 12.0,
                    RollRadians = 0.0
                }
            },
            Banking = null,
            Heartline = null
        };
    }

    private static TrackLayoutPackageV2Dto CreateConstantHeartlineDto()
    {
        TrackLayoutPackageV2Dto dto = CreateMinimalDto();
        dto.Metadata.SourceName = "Constant heartline V2 layout";
        dto.Metadata.LayoutId = "layout.m147.constant-heartline";
        dto.Sections = new[]
        {
            new TrackLayoutSectionV2Dto
            {
                Kind = TrackLayoutPackageV2Vocabulary.StraightSectionKind,
                Id = "entry",
                Length = 12.0,
                RollRadians = 0.0
            },
            new TrackLayoutSectionV2Dto
            {
                Kind = TrackLayoutPackageV2Vocabulary.ConstantCurvatureSectionKind,
                Id = "turn",
                Length = 18.0,
                RollRadians = 0.2,
                Radius = 30.0
            }
        };
        dto.Banking = new TrackBankingV2Dto
        {
            Keys = new[]
            {
                new TrackBankingKeyV2Dto
                {
                    Distance = 0.0,
                    RollRadians = 0.0,
                    InterpolationToNext = TrackLayoutPackageV2Vocabulary.BankingInterpolationLinear
                },
                new TrackBankingKeyV2Dto
                {
                    Distance = 12.0,
                    RollRadians = 0.2,
                    InterpolationToNext = TrackLayoutPackageV2Vocabulary.BankingInterpolationConstant
                },
                new TrackBankingKeyV2Dto
                {
                    Distance = 30.0,
                    RollRadians = 0.2,
                    InterpolationToNext = TrackLayoutPackageV2Vocabulary.BankingInterpolationConstant
                }
            }
        };
        dto.Heartline = new TrackHeartlineV2Dto
        {
            NormalOffset = 1.1,
            LateralOffset = 0.0
        };

        return dto;
    }

    private static bool IsValidAgainstSchema(string instanceJson)
    {
        using JsonDocument instanceDocument = JsonDocument.Parse(instanceJson);

        EvaluationResults results = TrackLayoutPackageSchema.Value.Evaluate(
            instanceDocument.RootElement,
            new EvaluationOptions
            {
                OutputFormat = OutputFormat.List
            });

        return results.IsValid;
    }

    private static JsonSchema CreateTrackLayoutPackageSchema()
    {
        string schemaPath = FindContractFile("track-layout-package-v2.schema.json");
        string schemaJson = File.ReadAllText(schemaPath);
        return JsonSchema.FromText(schemaJson);
    }

    private static string FindContractFile(string fileName)
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);

        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, "docs", "contracts", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException(
            "Contract file '" + fileName + "' was not found from '" + AppContext.BaseDirectory + "'.");
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
}
