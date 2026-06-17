using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Quantum.IO.TrackLayout.V1;
using Quantum.Math;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class TrackLayoutPackageV1Tests
{
    private static readonly Lazy<JsonSchema> TrackLayoutPackageSchema = new(CreateTrackLayoutPackageSchema);

    [Fact]
    public void ContractConstantsAndDefaultDtoShape_AreStable()
    {
        var dto = new TrackLayoutPackageV1Dto();

        Assert.Equal("quantum.track_layout_package", TrackLayoutPackageV1Dto.ContractName);
        Assert.Equal(1, TrackLayoutPackageV1Dto.ContractVersion);
        Assert.Equal(TrackLayoutPackageV1Dto.ContractName, dto.Contract);
        Assert.Equal(TrackLayoutPackageV1Dto.ContractVersion, dto.Version);
        Assert.Equal("meters", dto.Metadata.Units);
        Assert.Null(dto.Metadata.SourceName);
        Assert.Equal(1.0, dto.StartPose.Tangent.X);
        Assert.Equal(1.0, dto.StartPose.Normal.Y);
        Assert.Equal(1.0, dto.StartPose.Binormal.Z);
        Assert.Empty(dto.Sections);
        Assert.Null(dto.Banking);

        Assert.False(TrackLayoutPackageV1Validator.TryValidate(dto, out var diagnostics));
        Assert.Contains(
            diagnostics,
            d => d.Code == TrackLayoutPackageV1ValidationCode.EmptySections &&
                 d.Path == "sections");
        Assert.DoesNotContain(
            diagnostics,
            d => d.Code == TrackLayoutPackageV1ValidationCode.InvalidContract ||
                 d.Code == TrackLayoutPackageV1ValidationCode.InvalidVersion);
    }

    [Fact]
    public void Serialize_RepresentativePackage_IsDeterministicCamelCaseJson()
    {
        TrackLayoutPackageV1Dto dto = CreateRepresentativeDto();

        string first = NormalizeLineEndings(TrackLayoutPackageV1Json.Serialize(dto, indented: true)).TrimEnd();
        string second = NormalizeLineEndings(TrackLayoutPackageV1Json.Serialize(dto, indented: true)).TrimEnd();
        string roundtrip = NormalizeLineEndings(
            TrackLayoutPackageV1Json.Serialize(
                TrackLayoutPackageV1Json.Deserialize(first),
                indented: true)).TrimEnd();

        Assert.Equal(first, second);
        Assert.Equal(first, roundtrip);
        Assert.Contains("\"startPose\":", first);
        Assert.Contains("\"constantCurvature\"", first);
        Assert.Contains("\"controlPoints\":", first);
        Assert.DoesNotContain("\"StartPose\":", first);
    }

    [Fact]
    public void Deserialize_RoundTripDto_PreservesRepresentativeValues()
    {
        TrackLayoutPackageV1Dto expected = CreateRepresentativeDto();

        TrackLayoutPackageV1Dto actual = TrackLayoutPackageV1Json.Deserialize(
            TrackLayoutPackageV1Json.Serialize(expected));

        Assert.Equal(expected.Contract, actual.Contract);
        Assert.Equal(expected.Metadata.SourceName, actual.Metadata.SourceName);
        Assert.Equal(4, actual.Sections.Length);
        Assert.Equal("turn", actual.Sections[1].Id);
        Assert.Equal(-30.0, actual.Sections[1].Radius);
        Assert.Equal("linear", actual.Sections[2].InterpolationMode);
        Assert.Equal(3, actual.Sections[3].Degree);
        Assert.Equal(4.0, actual.Sections[3].ControlPoints![2].X);
        Assert.Equal(4, actual.Sections[3].Weights!.Length);
        Assert.NotNull(actual.Banking);
        Assert.Equal("smoothStep", actual.Banking!.Keys[1].InterpolationToNext);
    }

    [Fact]
    public void JsonImport_WrongContractAndMalformedJson_ReturnDiagnostics()
    {
        TrackLayoutPackageV1Dto dto = CreateRepresentativeDto();
        dto.Contract = "wrong.contract";

        TrackLayoutPackageV1ImportResult wrongContract = TrackLayoutPackageV1Json.Import(
            TrackLayoutPackageV1Json.Serialize(dto));
        TrackLayoutPackageV1ImportResult malformed = TrackLayoutPackageV1Json.Import("{");

        Assert.False(wrongContract.Success);
        Assert.Null(wrongContract.Definition);
        Assert.Contains(
            wrongContract.Diagnostics,
            d => d.Code == TrackLayoutPackageV1ValidationCode.InvalidContract &&
                 d.Path == "contract");

        Assert.False(malformed.Success);
        Assert.Null(malformed.Definition);
        TrackLayoutPackageV1ValidationDiagnostic malformedDiagnostic = AssertDiagnostic(
            malformed.Diagnostics,
            TrackLayoutPackageV1ValidationCode.MalformedJson,
            "json");
        AssertMessageContains(
            malformedDiagnostic,
            "Failed to deserialize TrackLayoutPackageV1Dto",
            "JSON parser detail",
            "line",
            "byte position");
    }

    [Fact]
    public void JsonImport_UnmappedMember_ReturnsMalformedJsonDiagnosticWithParserContext()
    {
        JsonObject json = ParseJsonObject(TrackLayoutPackageV1Json.Serialize(CreateRepresentativeDto()));
        json["debugOnly"] = true;

        TrackLayoutPackageV1ImportResult result = TrackLayoutPackageV1Json.Import(json.ToJsonString());

        Assert.False(result.Success);
        Assert.Null(result.Definition);
        TrackLayoutPackageV1ValidationDiagnostic diagnostic = AssertDiagnostic(
            result.Diagnostics,
            TrackLayoutPackageV1ValidationCode.MalformedJson,
            "json");
        AssertMessageContains(
            diagnostic,
            "Failed to deserialize TrackLayoutPackageV1Dto",
            "debugOnly",
            "could not be mapped");
    }

    [Fact]
    public void SchemaValidation_RepresentativePackage_IsValid()
    {
        string json = TrackLayoutPackageV1Json.Serialize(CreateRepresentativeDto());

        Assert.True(IsValidAgainstSchema(json));
    }

    [Fact]
    public void SchemaValidation_RejectsWrongContractAndVersion()
    {
        JsonObject contractJson = ParseJsonObject(TrackLayoutPackageV1Json.Serialize(CreateRepresentativeDto()));
        contractJson["contract"] = "wrong.contract";

        JsonObject versionJson = ParseJsonObject(TrackLayoutPackageV1Json.Serialize(CreateRepresentativeDto()));
        versionJson["version"] = 2;

        Assert.False(IsValidAgainstSchema(contractJson.ToJsonString()));
        Assert.False(IsValidAgainstSchema(versionJson.ToJsonString()));
    }

    [Fact]
    public void SchemaValidation_RejectsInvalidDiscriminatorShapes()
    {
        JsonObject extraField = ParseJsonObject(TrackLayoutPackageV1Json.Serialize(CreateRepresentativeDto()));
        extraField["sections"]![0]!["radius"] = 10.0;

        JsonObject missingField = ParseJsonObject(TrackLayoutPackageV1Json.Serialize(CreateRepresentativeDto()));
        Assert.True(missingField["sections"]![1]!.AsObject().Remove("radius"));

        Assert.False(IsValidAgainstSchema(extraField.ToJsonString()));
        Assert.False(IsValidAgainstSchema(missingField.ToJsonString()));
    }

    [Fact]
    public void Validator_CatchesEmptySectionsDuplicateIdsAndInvalidStartPose()
    {
        TrackLayoutPackageV1Dto empty = CreateRepresentativeDto();
        empty.Sections = Array.Empty<TrackLayoutSectionV1Dto>();

        TrackLayoutPackageV1Dto duplicate = CreateRepresentativeDto();
        duplicate.Sections[1].Id = duplicate.Sections[0].Id;

        TrackLayoutPackageV1Dto badStartPose = CreateRepresentativeDto();
        badStartPose.StartPose.Tangent = new TrackLayoutVector3dV1Dto { X = 2.0 };

        Assert.Contains(
            TrackLayoutPackageV1Validator.Validate(empty),
            d => d.Code == TrackLayoutPackageV1ValidationCode.EmptySections);
        Assert.Contains(
            TrackLayoutPackageV1Validator.Validate(duplicate),
            d => d.Code == TrackLayoutPackageV1ValidationCode.DuplicateSectionId &&
                 d.Path == "sections[1].id");
        Assert.Contains(
            TrackLayoutPackageV1Validator.Validate(badStartPose),
            d => d.Code == TrackLayoutPackageV1ValidationCode.InvalidStartPoseBasis &&
                 d.Path == "startPose.tangent");
    }

    [Fact]
    public void Validator_SectionDiagnostics_IncludeIndexIdKindAndDuplicateSource()
    {
        TrackLayoutPackageV1Dto duplicate = CreateRepresentativeDto();
        duplicate.Sections[1].Id = duplicate.Sections[0].Id;

        TrackLayoutPackageV1ValidationDiagnostic duplicateId = AssertDiagnostic(
            TrackLayoutPackageV1Validator.Validate(duplicate),
            TrackLayoutPackageV1ValidationCode.DuplicateSectionId,
            "sections[1].id");
        AssertMessageContains(
            duplicateId,
            "Section index 1",
            "id 'entry'",
            "kind 'constantCurvature'",
            "Previous section index 0");

        TrackLayoutPackageV1Dto invalid = CreateRepresentativeDto();
        invalid.Sections[0].Radius = 10.0;
        invalid.Sections[1].Radius = 0.0;
        invalid.Sections[2].InterpolationMode = "smoothStep";
        invalid.Sections[3].Length = -1.0;
        invalid.Sections = invalid.Sections.Concat(new[]
        {
            new TrackLayoutSectionV1Dto
            {
                Kind = "legacy",
                Id = "legacy",
                Length = 1.0
            }
        }).ToArray();

        IReadOnlyList<TrackLayoutPackageV1ValidationDiagnostic> diagnostics =
            TrackLayoutPackageV1Validator.Validate(invalid);

        AssertMessageContains(
            AssertDiagnostic(
                diagnostics,
                TrackLayoutPackageV1ValidationCode.UnexpectedSectionField,
                "sections[0].radius"),
            "Section index 0",
            "id 'entry'",
            "kind 'straight'");
        AssertMessageContains(
            AssertDiagnostic(
                diagnostics,
                TrackLayoutPackageV1ValidationCode.InvalidRadius,
                "sections[1].radius"),
            "Section index 1",
            "id 'turn'",
            "kind 'constantCurvature'");
        AssertMessageContains(
            AssertDiagnostic(
                diagnostics,
                TrackLayoutPackageV1ValidationCode.InvalidCurvatureInterpolation,
                "sections[2].interpolationMode"),
            "Section index 2",
            "id 'transition'",
            "kind 'curvatureTransition'");
        AssertMessageContains(
            AssertDiagnostic(
                diagnostics,
                TrackLayoutPackageV1ValidationCode.NonPositiveLength,
                "sections[3].length"),
            "Section index 3",
            "id 'spatial'",
            "kind 'spatial'");
        AssertMessageContains(
            AssertDiagnostic(
                diagnostics,
                TrackLayoutPackageV1ValidationCode.UnknownSectionKind,
                "sections[4].kind"),
            "Section index 4",
            "id 'legacy'",
            "kind 'legacy'");
    }

    [Fact]
    public void Validator_CatchesInvalidSectionFields()
    {
        TrackLayoutPackageV1Dto invalid = CreateRepresentativeDto();
        invalid.Sections[0].Radius = 10.0;
        invalid.Sections[1].Radius = 0.0;
        invalid.Sections[2].InterpolationMode = "smoothStep";
        invalid.Sections[3].Length = -1.0;
        invalid.Sections = invalid.Sections.Concat(new[]
        {
            new TrackLayoutSectionV1Dto
            {
                Kind = "legacy",
                Id = "legacy",
                Length = 1.0
            }
        }).ToArray();

        IReadOnlyList<TrackLayoutPackageV1ValidationDiagnostic> diagnostics =
            TrackLayoutPackageV1Validator.Validate(invalid);

        Assert.Contains(
            diagnostics,
            d => d.Code == TrackLayoutPackageV1ValidationCode.UnexpectedSectionField &&
                 d.Path == "sections[0].radius");
        Assert.Contains(
            diagnostics,
            d => d.Code == TrackLayoutPackageV1ValidationCode.InvalidRadius &&
                 d.Path == "sections[1].radius");
        Assert.Contains(
            diagnostics,
            d => d.Code == TrackLayoutPackageV1ValidationCode.InvalidCurvatureInterpolation &&
                 d.Path == "sections[2].interpolationMode");
        Assert.Contains(
            diagnostics,
            d => d.Code == TrackLayoutPackageV1ValidationCode.NonPositiveLength &&
                 d.Path == "sections[3].length");
        Assert.Contains(
            diagnostics,
            d => d.Code == TrackLayoutPackageV1ValidationCode.UnknownSectionKind &&
                 d.Path == "sections[4].kind");
    }

    [Fact]
    public void Validator_CatchesInvalidSpatialFields()
    {
        TrackLayoutPackageV1Dto invalid = CreateRepresentativeDto();
        TrackLayoutSectionV1Dto spatial = invalid.Sections[3];
        spatial.Degree = 4;
        spatial.ControlPoints![1].Y = 0.1;
        spatial.Weights = new[] { 1.0, 0.0 };

        IReadOnlyList<TrackLayoutPackageV1ValidationDiagnostic> diagnostics =
            TrackLayoutPackageV1Validator.Validate(invalid);

        Assert.Contains(
            diagnostics,
            d => d.Code == TrackLayoutPackageV1ValidationCode.InvalidSpatialControlPoints &&
                 d.Path == "sections[3].controlPoints");
        Assert.Contains(
            diagnostics,
            d => d.Code == TrackLayoutPackageV1ValidationCode.InvalidSpatialStartContract &&
                 d.Path == "sections[3].controlPoints[1]");
        Assert.Contains(
            diagnostics,
            d => d.Code == TrackLayoutPackageV1ValidationCode.InvalidSpatialWeights &&
                 d.Path == "sections[3].weights");
        Assert.Contains(
            diagnostics,
            d => d.Code == TrackLayoutPackageV1ValidationCode.InvalidSpatialWeights &&
                 d.Path == "sections[3].weights[1]");
    }

    [Fact]
    public void Validator_SpatialDiagnostics_IncludeSectionContext()
    {
        TrackLayoutPackageV1Dto invalid = CreateRepresentativeDto();
        TrackLayoutSectionV1Dto spatial = invalid.Sections[3];
        spatial.Degree = 4;
        spatial.ControlPoints![1].Y = 0.1;
        spatial.Weights = new[] { 1.0, 0.0 };

        IReadOnlyList<TrackLayoutPackageV1ValidationDiagnostic> diagnostics =
            TrackLayoutPackageV1Validator.Validate(invalid);

        foreach (TrackLayoutPackageV1ValidationDiagnostic diagnostic in new[]
                 {
                     AssertDiagnostic(
                         diagnostics,
                         TrackLayoutPackageV1ValidationCode.InvalidSpatialControlPoints,
                         "sections[3].controlPoints"),
                     AssertDiagnostic(
                         diagnostics,
                         TrackLayoutPackageV1ValidationCode.InvalidSpatialStartContract,
                         "sections[3].controlPoints[1]"),
                     AssertDiagnostic(
                         diagnostics,
                         TrackLayoutPackageV1ValidationCode.InvalidSpatialWeights,
                         "sections[3].weights"),
                     AssertDiagnostic(
                         diagnostics,
                         TrackLayoutPackageV1ValidationCode.InvalidSpatialWeights,
                         "sections[3].weights[1]")
                 })
        {
            AssertMessageContains(
                diagnostic,
                "Section index 3",
                "id 'spatial'",
                "kind 'spatial'");
        }
    }

    [Fact]
    public void Validator_CatchesInvalidBankingDomainOrderAndInterpolation()
    {
        TrackLayoutPackageV1Dto invalid = CreateRepresentativeDto();
        invalid.Banking!.Keys[0].Distance = 1.0;
        invalid.Banking.Keys[1].Distance = 0.5;
        invalid.Banking.Keys[2].InterpolationToNext = "legacyCubic";
        invalid.Banking.Keys[3].Distance = 35.0;

        IReadOnlyList<TrackLayoutPackageV1ValidationDiagnostic> diagnostics =
            TrackLayoutPackageV1Validator.Validate(invalid);

        Assert.Contains(
            diagnostics,
            d => d.Code == TrackLayoutPackageV1ValidationCode.InvalidBankingDomain &&
                 d.Path == "banking.keys[0].distance");
        Assert.Contains(
            diagnostics,
            d => d.Code == TrackLayoutPackageV1ValidationCode.InvalidBankingKeyOrder &&
                 d.Path == "banking.keys[1].distance");
        Assert.Contains(
            diagnostics,
            d => d.Code == TrackLayoutPackageV1ValidationCode.InvalidBankingInterpolation &&
                 d.Path == "banking.keys[2].interpolationToNext");
        Assert.Contains(
            diagnostics,
            d => d.Code == TrackLayoutPackageV1ValidationCode.InvalidBankingDomain &&
                 d.Path == "banking.keys[3].distance");
    }

    [Fact]
    public void Validator_BankingDiagnostics_IncludeKeyContextAndDomainDetails()
    {
        TrackLayoutPackageV1Dto invalid = CreateRepresentativeDto();
        invalid.Banking!.Keys[0].Distance = 1.0;
        invalid.Banking.Keys[1].Distance = 0.5;
        invalid.Banking.Keys[2].InterpolationToNext = "legacyCubic";
        invalid.Banking.Keys[3].Distance = 35.0;

        IReadOnlyList<TrackLayoutPackageV1ValidationDiagnostic> diagnostics =
            TrackLayoutPackageV1Validator.Validate(invalid);

        AssertMessageContains(
            AssertDiagnostic(
                diagnostics,
                TrackLayoutPackageV1ValidationCode.InvalidBankingDomain,
                "banking.keys[0].distance"),
            "Banking key index 0");
        AssertMessageContains(
            AssertDiagnostic(
                diagnostics,
                TrackLayoutPackageV1ValidationCode.InvalidBankingKeyOrder,
                "banking.keys[1].distance"),
            "Banking key index 1",
            "previous key index 0",
            "distance 1");
        AssertMessageContains(
            AssertDiagnostic(
                diagnostics,
                TrackLayoutPackageV1ValidationCode.InvalidBankingInterpolation,
                "banking.keys[2].interpolationToNext"),
            "Banking key index 2");
        AssertMessageContains(
            AssertDiagnostic(
                diagnostics,
                TrackLayoutPackageV1ValidationCode.InvalidBankingDomain,
                "banking.keys[3].distance"),
            "Banking key index 3",
            "expected total length 36");
    }

    [Fact]
    public void Validator_DiagnosticPaths_RemainStableForRepresentativeFailures()
    {
        TrackLayoutPackageV1Dto invalid = CreateRepresentativeDto();
        invalid.StartPose.Tangent = new TrackLayoutVector3dV1Dto { X = 2.0 };
        invalid.Sections[0].Radius = 10.0;
        invalid.Sections[3].ControlPoints![1].Y = 0.1;
        invalid.Banking!.Keys[1].Distance = 0.0;

        IReadOnlyList<TrackLayoutPackageV1ValidationDiagnostic> diagnostics =
            TrackLayoutPackageV1Validator.Validate(invalid);

        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV1ValidationCode.UnexpectedSectionField,
            "sections[0].radius");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV1ValidationCode.InvalidSpatialStartContract,
            "sections[3].controlPoints[1]");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV1ValidationCode.InvalidStartPoseBasis,
            "startPose.tangent");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV1ValidationCode.InvalidBankingKeyOrder,
            "banking.keys[1].distance");
    }

    [Fact]
    public void SchemaAndValidator_RejectSharedInvalidCases()
    {
        foreach ((string name, string json) in CreateSharedSchemaAndValidatorInvalidCases())
        {
            Assert.False(IsValidAgainstSchema(json), name + " should be rejected by schema validation.");

            TrackLayoutPackageV1ImportResult result = TrackLayoutPackageV1Json.Import(json);

            Assert.False(result.Success);
            Assert.Null(result.Definition);
            Assert.NotEmpty(result.Diagnostics);
        }
    }

    [Fact]
    public void SchemaAndValidator_CharacterizeDtoDefaultGapsWithoutChangingBehavior()
    {
        foreach ((string name, string json) in CreateKnownDtoDefaultSchemaGapCases())
        {
            Assert.False(IsValidAgainstSchema(json), name + " should remain a schema-only rejection.");

            TrackLayoutPackageV1ImportResult result = TrackLayoutPackageV1Json.Import(json);

            Assert.True(result.Success, name + " should still import through existing DTO defaults.");
            Assert.NotNull(result.Definition);
            Assert.Empty(result.Diagnostics);
        }
    }

    [Fact]
    public void Import_DtoToAuthoring_PreservesStartPoseSectionsSpatialDataAndExplicitBanking()
    {
        TrackLayoutPackageV1ImportResult result =
            TrackLayoutPackageV1Mapper.Import(CreateRepresentativeDto());

        Assert.True(result.Success);
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Definition);
        TrackAuthoringDefinition definition = result.Definition!;

        Assert.Equal(1.0, definition.StartPose.Position.X);
        Assert.Equal(1.0, definition.StartPose.Tangent.Y);
        Assert.Equal(1.0, definition.StartPose.Normal.Z);
        Assert.Equal(1.0, definition.StartPose.Binormal.X);
        Assert.Equal(4, definition.Sections.Count);
        Assert.IsType<StraightSectionDefinition>(definition.Sections[0]);
        Assert.Equal(-30.0, Assert.IsType<ConstantCurvatureSectionDefinition>(definition.Sections[1]).Radius);
        Assert.Equal(-0.01, Assert.IsType<CurvatureTransitionSectionDefinition>(definition.Sections[2]).EndCurvature);

        SpatialSectionDefinition spatial = Assert.IsType<SpatialSectionDefinition>(definition.Sections[3]);
        Assert.Equal(3, spatial.Degree);
        Assert.Equal(4, spatial.ControlPoints.Count);
        Assert.Equal(6.0, spatial.ControlPoints[3].X);
        Assert.Equal(1.0, spatial.Weights[2]);

        Assert.NotNull(definition.Banking);
        Assert.Equal(4, definition.Banking!.Keys.Count);
        Assert.Equal(BankingProfileInterpolationMode.SmoothStep, definition.Banking.Keys[1].InterpolationToNext);
    }

    [Fact]
    public void ExportImportJsonImport_RoundTripsAuthoringDefinition()
    {
        TrackAuthoringDefinition source = CreateAuthoringDefinition();

        TrackLayoutPackageV1Dto exported = TrackLayoutPackageV1Mapper.Export(source);
        string json = TrackLayoutPackageV1Json.Serialize(exported);
        TrackLayoutPackageV1Dto dtoRoundtrip = TrackLayoutPackageV1Json.Deserialize(json);
        TrackLayoutPackageV1ImportResult result = TrackLayoutPackageV1Mapper.Import(dtoRoundtrip);

        Assert.True(result.Success);
        Assert.NotNull(result.Definition);
        TrackAuthoringDefinition actual = result.Definition!;
        AssertAuthoringEquivalent(source, actual);
    }

    [Fact]
    public void ExpandedBankingInterpolationModes_ValidateExportSchemaAndImport()
    {
        (BankingProfileInterpolationMode Mode, string Token)[] modes =
        {
            (
                BankingProfileInterpolationMode.Quadratic,
                TrackLayoutPackageV1Vocabulary.BankingInterpolationQuadratic),
            (
                BankingProfileInterpolationMode.Cubic,
                TrackLayoutPackageV1Vocabulary.BankingInterpolationCubic),
            (
                BankingProfileInterpolationMode.Quartic,
                TrackLayoutPackageV1Vocabulary.BankingInterpolationQuartic),
            (
                BankingProfileInterpolationMode.Quintic,
                TrackLayoutPackageV1Vocabulary.BankingInterpolationQuintic),
            (
                BankingProfileInterpolationMode.Sinusoidal,
                TrackLayoutPackageV1Vocabulary.BankingInterpolationSinusoidal)
        };

        double totalLength = modes.Length * 10.0;
        var keys = new BankingProfileKey[modes.Length + 1];
        for (int i = 0; i < modes.Length; i++)
        {
            keys[i] = new BankingProfileKey(i * 10.0, i * 0.1, modes[i].Mode);
        }

        keys[keys.Length - 1] = new BankingProfileKey(
            totalLength,
            0.5,
            BankingProfileInterpolationMode.Constant);
        var source = new TrackAuthoringDefinition(
            new[] { new StraightSectionDefinition("expanded-banking", totalLength) },
            TrackStartPose.Identity,
            new TrackBankingDefinition(keys));

        TrackLayoutPackageV1Dto exported = TrackLayoutPackageV1Mapper.Export(source);
        string json = TrackLayoutPackageV1Json.Serialize(exported);
        IReadOnlyList<TrackLayoutPackageV1ValidationDiagnostic> diagnostics =
            TrackLayoutPackageV1Validator.Validate(exported);

        Assert.Empty(diagnostics);
        Assert.True(IsValidAgainstSchema(json));
        Assert.NotNull(exported.Banking);
        for (int i = 0; i < modes.Length; i++)
        {
            Assert.Equal(modes[i].Token, exported.Banking!.Keys[i].InterpolationToNext);
        }

        TrackLayoutPackageV1Dto dtoRoundTrip = TrackLayoutPackageV1Json.Deserialize(json);
        TrackLayoutPackageV1ImportResult result = TrackLayoutPackageV1Mapper.Import(dtoRoundTrip);

        Assert.True(result.Success);
        Assert.NotNull(result.Definition);
        Assert.NotNull(result.Definition!.Banking);
        for (int i = 0; i < modes.Length; i++)
        {
            Assert.Equal(modes[i].Mode, result.Definition.Banking!.Keys[i].InterpolationToNext);
        }
    }

    [Fact]
    public void Export_WhenDefinitionBankingIsNull_OmitsCompileGeneratedFallbackBanking()
    {
        var definition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            new StraightSectionDefinition("first", 4.0, rollRadians: 0.2),
            new StraightSectionDefinition("second", 6.0, rollRadians: -0.3)
        });

        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(definition);
        TrackLayoutPackageV1Dto dto = TrackLayoutPackageV1Mapper.Export(compilation.Definition);
        string json = TrackLayoutPackageV1Json.Serialize(dto, indented: true);

        Assert.Null(compilation.Definition.Banking);
        Assert.NotNull(compilation.BankingProfile);
        Assert.Null(dto.Banking);
        Assert.Contains("\"banking\": null", json);
        Assert.DoesNotContain("\"keys\"", json);
    }

    [Fact]
    public void GoldenPackage_ImportsAndCompilesThroughAuthoringBuilder()
    {
        string json = LoadGoldenFixtureJson();

        TrackLayoutPackageV1ImportResult result = TrackLayoutPackageV1Json.Import(json);

        Assert.True(result.Success);
        Assert.NotNull(result.Definition);
        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(
            result.Definition!);
        Assert.Equal(36.0, compilation.TotalLength);
        Assert.NotNull(compilation.Runtime);
    }

    [Fact]
    public void PublicIoApiBoundary_DoesNotExposeFrontendRendererEditorOrFvdTypes()
    {
        Type[] types = typeof(TrackLayoutPackageV1Dto).Assembly.GetExportedTypes()
            .Where(type => string.Equals(type.Namespace, "Quantum.IO.TrackLayout.V1", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(types);
        foreach (Type type in types)
        {
            AssertNoForbiddenType(type, type.Name);

            foreach (ConstructorInfo constructor in type.GetConstructors())
            {
                foreach (ParameterInfo parameter in constructor.GetParameters())
                {
                    AssertNoForbiddenType(parameter.ParameterType, constructor.Name);
                }
            }

            foreach (PropertyInfo property in type.GetProperties(
                         BindingFlags.Public |
                         BindingFlags.Instance |
                         BindingFlags.Static |
                         BindingFlags.DeclaredOnly))
            {
                AssertNoForbiddenType(property.PropertyType, property.Name);
            }

            foreach (MethodInfo method in type.GetMethods(
                         BindingFlags.Public |
                         BindingFlags.Instance |
                         BindingFlags.Static |
                         BindingFlags.DeclaredOnly))
            {
                AssertNoForbiddenType(method.ReturnType, method.Name);
                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    AssertNoForbiddenType(parameter.ParameterType, method.Name);
                }
            }
        }
    }

    private static TrackLayoutPackageV1Dto CreateRepresentativeDto()
    {
        return new TrackLayoutPackageV1Dto
        {
            Metadata = new TrackLayoutMetadataV1Dto
            {
                Units = "meters",
                SourceName = "representative"
            },
            StartPose = new TrackStartPoseV1Dto
            {
                Position = new TrackLayoutVector3dV1Dto { X = 1.0, Y = 2.0, Z = 3.0 },
                Tangent = new TrackLayoutVector3dV1Dto { X = 0.0, Y = 1.0, Z = 0.0 },
                Normal = new TrackLayoutVector3dV1Dto { X = 0.0, Y = 0.0, Z = 1.0 },
                Binormal = new TrackLayoutVector3dV1Dto { X = 1.0, Y = 0.0, Z = 0.0 }
            },
            Sections = new[]
            {
                new TrackLayoutSectionV1Dto
                {
                    Kind = TrackLayoutPackageV1Vocabulary.StraightSectionKind,
                    Id = "entry",
                    Length = 10.0,
                    RollRadians = 0.1
                },
                new TrackLayoutSectionV1Dto
                {
                    Kind = TrackLayoutPackageV1Vocabulary.ConstantCurvatureSectionKind,
                    Id = "turn",
                    Length = 12.0,
                    RollRadians = -0.2,
                    Radius = -30.0
                },
                new TrackLayoutSectionV1Dto
                {
                    Kind = TrackLayoutPackageV1Vocabulary.CurvatureTransitionSectionKind,
                    Id = "transition",
                    Length = 8.0,
                    RollRadians = 0.05,
                    StartCurvature = 0.02,
                    EndCurvature = -0.01,
                    InterpolationMode = TrackLayoutPackageV1Vocabulary.CurvatureInterpolationLinear
                },
                new TrackLayoutSectionV1Dto
                {
                    Kind = TrackLayoutPackageV1Vocabulary.SpatialSectionKind,
                    Id = "spatial",
                    Length = 6.0,
                    RollRadians = 0.25,
                    Degree = 3,
                    ControlPoints = new[]
                    {
                        new TrackLayoutVector3dV1Dto { X = 0.0, Y = 0.0, Z = 0.0 },
                        new TrackLayoutVector3dV1Dto { X = 2.0, Y = 0.0, Z = 0.0 },
                        new TrackLayoutVector3dV1Dto { X = 4.0, Y = 0.0, Z = 0.0 },
                        new TrackLayoutVector3dV1Dto { X = 6.0, Y = 0.0, Z = 0.0 }
                    },
                    Weights = new[] { 1.0, 1.0, 1.0, 1.0 }
                }
            },
            Banking = new TrackBankingV1Dto
            {
                Keys = new[]
                {
                    new TrackBankingKeyV1Dto
                    {
                        Distance = 0.0,
                        RollRadians = 0.0,
                        InterpolationToNext = TrackLayoutPackageV1Vocabulary.BankingInterpolationLinear
                    },
                    new TrackBankingKeyV1Dto
                    {
                        Distance = 10.0,
                        RollRadians = 0.2,
                        InterpolationToNext = TrackLayoutPackageV1Vocabulary.BankingInterpolationSmoothStep
                    },
                    new TrackBankingKeyV1Dto
                    {
                        Distance = 22.0,
                        RollRadians = -0.35,
                        InterpolationToNext = TrackLayoutPackageV1Vocabulary.BankingInterpolationConstant
                    },
                    new TrackBankingKeyV1Dto
                    {
                        Distance = 36.0,
                        RollRadians = 0.1,
                        InterpolationToNext = TrackLayoutPackageV1Vocabulary.BankingInterpolationConstant
                    }
                }
            }
        };
    }

    private static TrackAuthoringDefinition CreateAuthoringDefinition()
    {
        TrackLayoutPackageV1ImportResult result = TrackLayoutPackageV1Mapper.Import(CreateRepresentativeDto());
        Assert.True(result.Success);
        Assert.NotNull(result.Definition);
        return result.Definition!;
    }

    private static IEnumerable<(string Name, string Json)> CreateSharedSchemaAndValidatorInvalidCases()
    {
        JsonObject wrongContract = ParseJsonObject(TrackLayoutPackageV1Json.Serialize(CreateRepresentativeDto()));
        wrongContract["contract"] = "wrong.contract";
        yield return ("wrong contract", wrongContract.ToJsonString());

        JsonObject emptySections = ParseJsonObject(TrackLayoutPackageV1Json.Serialize(CreateRepresentativeDto()));
        emptySections["sections"] = new JsonArray();
        yield return ("empty sections", emptySections.ToJsonString());

        JsonObject unexpectedStraightRadius =
            ParseJsonObject(TrackLayoutPackageV1Json.Serialize(CreateRepresentativeDto()));
        unexpectedStraightRadius["sections"]![0]!["radius"] = 10.0;
        yield return ("straight section radius", unexpectedStraightRadius.ToJsonString());

        JsonObject spatialZeroWeight =
            ParseJsonObject(TrackLayoutPackageV1Json.Serialize(CreateRepresentativeDto()));
        spatialZeroWeight["sections"]![3]!["weights"]![1] = 0.0;
        yield return ("spatial zero weight", spatialZeroWeight.ToJsonString());

        JsonObject shortBanking = ParseJsonObject(TrackLayoutPackageV1Json.Serialize(CreateRepresentativeDto()));
        shortBanking["banking"]!["keys"] = new JsonArray(shortBanking["banking"]!["keys"]![0]!.DeepClone());
        yield return ("short banking keys", shortBanking.ToJsonString());
    }

    private static IEnumerable<(string Name, string Json)> CreateKnownDtoDefaultSchemaGapCases()
    {
        JsonObject missingSourceName =
            ParseJsonObject(TrackLayoutPackageV1Json.Serialize(CreateRepresentativeDto()));
        Assert.True(missingSourceName["metadata"]!.AsObject().Remove("sourceName"));
        yield return ("missing metadata.sourceName", missingSourceName.ToJsonString());

        JsonObject missingBankingRoot =
            ParseJsonObject(TrackLayoutPackageV1Json.Serialize(CreateRepresentativeDto()));
        Assert.True(missingBankingRoot.Remove("banking"));
        yield return ("missing root banking", missingBankingRoot.ToJsonString());

        JsonObject missingPositionZ =
            ParseJsonObject(TrackLayoutPackageV1Json.Serialize(CreateRepresentativeDto()));
        Assert.True(missingPositionZ["startPose"]!["position"]!.AsObject().Remove("z"));
        yield return ("missing startPose.position.z", missingPositionZ.ToJsonString());

        JsonObject missingBankingInterpolation =
            ParseJsonObject(TrackLayoutPackageV1Json.Serialize(CreateRepresentativeDto()));
        Assert.True(
            missingBankingInterpolation["banking"]!["keys"]![1]!
                .AsObject()
                .Remove("interpolationToNext"));
        yield return ("missing banking interpolationToNext", missingBankingInterpolation.ToJsonString());
    }

    private static void AssertAuthoringEquivalent(
        TrackAuthoringDefinition expected,
        TrackAuthoringDefinition actual)
    {
        AssertVectorEqual(expected.StartPose.Position, actual.StartPose.Position);
        AssertVectorEqual(expected.StartPose.Tangent, actual.StartPose.Tangent);
        AssertVectorEqual(expected.StartPose.Normal, actual.StartPose.Normal);
        AssertVectorEqual(expected.StartPose.Binormal, actual.StartPose.Binormal);
        Assert.Equal(expected.Sections.Count, actual.Sections.Count);

        for (int i = 0; i < expected.Sections.Count; i++)
        {
            GeometricSectionDefinition expectedSection = expected.Sections[i];
            GeometricSectionDefinition actualSection = actual.Sections[i];
            Assert.Equal(expectedSection.GetType(), actualSection.GetType());
            Assert.Equal(expectedSection.Id, actualSection.Id);
            Assert.Equal(expectedSection.Length, actualSection.Length);
            Assert.Equal(expectedSection.RollRadians, actualSection.RollRadians);

            if (expectedSection is ConstantCurvatureSectionDefinition expectedArc)
            {
                Assert.Equal(expectedArc.Radius, Assert.IsType<ConstantCurvatureSectionDefinition>(actualSection).Radius);
            }
            else if (expectedSection is CurvatureTransitionSectionDefinition expectedTransition)
            {
                CurvatureTransitionSectionDefinition actualTransition =
                    Assert.IsType<CurvatureTransitionSectionDefinition>(actualSection);
                Assert.Equal(expectedTransition.StartCurvature, actualTransition.StartCurvature);
                Assert.Equal(expectedTransition.EndCurvature, actualTransition.EndCurvature);
                Assert.Equal(expectedTransition.InterpolationMode, actualTransition.InterpolationMode);
            }
            else if (expectedSection is SpatialSectionDefinition expectedSpatial)
            {
                SpatialSectionDefinition actualSpatial = Assert.IsType<SpatialSectionDefinition>(actualSection);
                Assert.Equal(expectedSpatial.Degree, actualSpatial.Degree);
                Assert.Equal(expectedSpatial.ControlPoints.Count, actualSpatial.ControlPoints.Count);
                for (int pointIndex = 0; pointIndex < expectedSpatial.ControlPoints.Count; pointIndex++)
                {
                    AssertVectorEqual(
                        expectedSpatial.ControlPoints[pointIndex],
                        actualSpatial.ControlPoints[pointIndex]);
                    Assert.Equal(expectedSpatial.Weights[pointIndex], actualSpatial.Weights[pointIndex]);
                }
            }
        }

        Assert.NotNull(expected.Banking);
        Assert.NotNull(actual.Banking);
        Assert.Equal(expected.Banking!.Keys.Count, actual.Banking!.Keys.Count);
        for (int i = 0; i < expected.Banking.Keys.Count; i++)
        {
            Assert.Equal(expected.Banking.Keys[i].Distance, actual.Banking.Keys[i].Distance);
            Assert.Equal(expected.Banking.Keys[i].RollRadians, actual.Banking.Keys[i].RollRadians);
            Assert.Equal(expected.Banking.Keys[i].InterpolationToNext, actual.Banking.Keys[i].InterpolationToNext);
        }
    }

    private static void AssertVectorEqual(Vector3d expected, Vector3d actual)
    {
        Assert.Equal(expected.X, actual.X);
        Assert.Equal(expected.Y, actual.Y);
        Assert.Equal(expected.Z, actual.Z);
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
        string schemaPath = FindContractFile("track-layout-package-v1.schema.json");
        string schemaJson = File.ReadAllText(schemaPath);
        return JsonSchema.FromText(schemaJson);
    }

    private static string LoadGoldenFixtureJson()
    {
        string fixturePath = Path.Combine(AppContext.BaseDirectory, "IO", "Fixtures", "TrackLayoutPackageV1.golden.json");
        Assert.True(File.Exists(fixturePath), "Golden fixture file was not found at '" + fixturePath + "'.");
        return File.ReadAllText(fixturePath);
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

    private static TrackLayoutPackageV1ValidationDiagnostic AssertDiagnostic(
        IReadOnlyList<TrackLayoutPackageV1ValidationDiagnostic> diagnostics,
        TrackLayoutPackageV1ValidationCode code,
        string path)
    {
        TrackLayoutPackageV1ValidationDiagnostic? diagnostic = diagnostics.FirstOrDefault(
            d => d.Code == code && d.Path == path);

        Assert.NotNull(diagnostic);
        return diagnostic!;
    }

    private static void AssertMessageContains(
        TrackLayoutPackageV1ValidationDiagnostic diagnostic,
        params string[] substrings)
    {
        foreach (string substring in substrings)
        {
            Assert.True(
                diagnostic.Message.Contains(substring, StringComparison.Ordinal),
                "Expected diagnostic message for " +
                diagnostic.Code +
                " at '" +
                diagnostic.Path +
                "' to contain '" +
                substring +
                "', but was: " +
                diagnostic.Message);
        }
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.ReplaceLineEndings("\n");
    }

    private static void AssertNoForbiddenType(Type type, string memberName)
    {
        string typeNamespace = type.Namespace ?? string.Empty;
        string[] forbiddenNamespacePrefixes =
        {
            "Quantum.FVD",
            "Quantum.Debug",
            "UnityEngine",
            "UnityEditor",
            "Avalonia",
            "Silk.NET",
            "OpenTK",
            "Veldrid"
        };

        foreach (string prefix in forbiddenNamespacePrefixes)
        {
            Assert.False(
                string.Equals(typeNamespace, prefix, StringComparison.Ordinal) ||
                typeNamespace.StartsWith(prefix + ".", StringComparison.Ordinal),
                memberName + " exposes forbidden IO dependency type " + type.FullName + ".");
        }

        if (type.IsGenericType)
        {
            foreach (Type argumentType in type.GetGenericArguments())
            {
                AssertNoForbiddenType(argumentType, memberName);
            }
        }

        if (type.HasElementType)
        {
            Type? elementType = type.GetElementType();
            Assert.NotNull(elementType);
            AssertNoForbiddenType(elementType, memberName);
        }
    }
}
