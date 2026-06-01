using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Quantum.Debug;
using Quantum.IO.DebugViewport.V1;
using Quantum.IO.TrainPose.V1;
using Quantum.Math;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class DebugViewportSnapshotV1Tests
{
    private static readonly Lazy<JsonSchema> DebugViewportSnapshotSchema = new(CreateDebugViewportSnapshotSchema);

    [Fact]
    public void Export_SetsContractMetadataAndSampledTrackData()
    {
        ExportTrackFrame[] frames =
        {
            CreateFrame(distance: 0.0, x: 1.0),
            CreateFrame(distance: 5.0, x: 6.0)
        };
        DebugLineSegment[] lines = TrackFrameDebugGizmoBuilder.BuildAxes(frames[0], axisLength: 2.0);
        var boxes = new[]
        {
            new DebugViewportBoxSource(
                role: DebugViewportSnapshotV1Vocabulary.TrainBodyRole,
                label: "car-0",
                frame: frames[1],
                length: 4.5,
                width: 1.8,
                height: 2.1)
        };

        DebugViewportSnapshotV1Dto dto = DebugViewportSnapshotV1Mapper.Export(new DebugViewportSnapshotV1Source
        {
            Units = "meters",
            SourceFixtureName = "deterministic-track",
            SampledFrames = frames,
            Lines = lines,
            Boxes = boxes
        });

        Assert.Equal(DebugViewportSnapshotV1Dto.ContractName, dto.Contract);
        Assert.Equal(DebugViewportSnapshotV1Dto.ContractVersion, dto.Version);
        Assert.Equal("meters", dto.Metadata.Units);
        Assert.Equal("deterministic-track", dto.Metadata.SourceFixtureName);
        Assert.Equal(2, dto.Metadata.SampleCount);

        Assert.Equal(2, dto.CenterlinePoints.Length);
        Assert.Equal(5.0, dto.CenterlinePoints[1].Distance);
        Assert.Equal(6.0, dto.CenterlinePoints[1].Position.X);

        Assert.Equal(2, dto.Frames.Length);
        Assert.Equal(1.0, dto.Frames[0].Tangent.X);
        Assert.Equal(1.0, dto.Frames[0].Normal.Y);
        Assert.Equal(1.0, dto.Frames[0].Binormal.Z);

        Assert.Equal(3, dto.Lines.Length);
        Assert.Equal(DebugViewportSnapshotV1Vocabulary.FrameAxisTangentKind, dto.Lines[0].Kind);
        Assert.Equal(DebugViewportSnapshotV1Vocabulary.FrameAxisNormalKind, dto.Lines[1].Kind);
        Assert.Equal(DebugViewportSnapshotV1Vocabulary.FrameAxisBinormalKind, dto.Lines[2].Kind);

        DebugViewportBoxV1Dto box = Assert.Single(dto.Boxes);
        Assert.Equal(DebugViewportSnapshotV1Vocabulary.TrainBodyRole, box.Role);
        Assert.Equal("car-0", box.Label);
        Assert.Equal(4.5, box.Size.Length);
        Assert.Equal(1.8, box.Size.Width);
        Assert.Equal(2.1, box.Size.Height);
        Assert.Null(dto.TrainPose);
    }

    [Fact]
    public void Export_WithTrainPose_NestsExistingTrainPoseContract()
    {
        TrainPoseResult pose = CreateTrainPose();

        DebugViewportSnapshotV1Dto dto = DebugViewportSnapshotV1Mapper.Export(new DebugViewportSnapshotV1Source
        {
            TrainPose = pose
        });

        Assert.NotNull(dto.TrainPose);
        Assert.Equal(TrainPoseExportV1Dto.ContractName, dto.TrainPose!.Contract);
        Assert.Equal(TrainPoseExportV1Dto.ContractVersion, dto.TrainPose.Version);
        Assert.Equal(12.0, dto.TrainPose.LeadDistance);
        Assert.Single(dto.TrainPose.Cars);
    }

    [Fact]
    public void SerializeDeserialize_RoundtripPreservesRepresentativeValues()
    {
        ExportTrackFrame[] frames =
        {
            CreateFrame(distance: 0.0, x: 0.0),
            CreateFrame(distance: 10.0, x: 10.0)
        };
        var boxes = new[]
        {
            new DebugViewportBoxSource(DebugViewportSnapshotV1Vocabulary.TrainWheelRole, "wheel-0", frames[0], 0.8, 0.25, 0.8)
        };
        DebugViewportSnapshotV1Dto expected = DebugViewportSnapshotV1Mapper.Export(new DebugViewportSnapshotV1Source
        {
            SourceFixtureName = "roundtrip",
            SampledFrames = frames,
            Boxes = boxes,
            TrainPose = CreateTrainPose()
        });

        string json = DebugViewportSnapshotV1Json.Serialize(expected);
        DebugViewportSnapshotV1Dto actual = DebugViewportSnapshotV1Json.Deserialize(json);

        Assert.Contains("\"centerlinePoints\":", json);
        Assert.Contains("\"trainPose\":", json);
        Assert.DoesNotContain("\"CenterlinePoints\":", json);

        Assert.Equal(expected.Contract, actual.Contract);
        Assert.Equal(expected.Metadata.SourceFixtureName, actual.Metadata.SourceFixtureName);
        Assert.Equal(expected.Metadata.SampleCount, actual.Metadata.SampleCount);
        Assert.Equal(expected.CenterlinePoints[1].Position.X, actual.CenterlinePoints[1].Position.X);
        Assert.Equal(expected.Frames[1].Distance, actual.Frames[1].Distance);
        Assert.Equal(expected.Boxes[0].Role, actual.Boxes[0].Role);
        Assert.Equal(expected.Boxes[0].Size.Width, actual.Boxes[0].Size.Width);
        Assert.NotNull(actual.TrainPose);
        Assert.Equal(TrainPoseExportV1Dto.ContractName, actual.TrainPose!.Contract);
    }

    [Fact]
    public void Deserialize_RejectsWrongSnapshotContract()
    {
        const string json = @"{""contract"":""wrong.contract"",""version"":1}";

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            DebugViewportSnapshotV1Json.Deserialize(json));

        Assert.Contains("contract", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_RejectsWrongNestedTrainPoseContract()
    {
        DebugViewportSnapshotV1Dto dto = DebugViewportSnapshotV1Mapper.Export(new DebugViewportSnapshotV1Source
        {
            TrainPose = CreateTrainPose()
        });
        dto.TrainPose!.Contract = "wrong.train.pose";

        string json = DebugViewportSnapshotV1Json.Serialize(dto);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            DebugViewportSnapshotV1Json.Deserialize(json));

        Assert.Contains("nested", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_RejectsMalformedJson()
    {
        const string json = @"{""contract"":""quantum.debug_viewport_snapshot"",""version"":1,";

        JsonException ex = Assert.Throws<JsonException>(() =>
            DebugViewportSnapshotV1Json.Deserialize(json));

        Assert.Contains("malformed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validation_ValidSnapshotJson_Passes()
    {
        DebugViewportSnapshotV1Dto expected = CreateValidationSnapshot();
        string json = DebugViewportSnapshotV1Json.Serialize(expected, indented: true);
        DebugViewportSnapshotV1Dto actual = DebugViewportSnapshotV1Json.Deserialize(json);

        bool isValid = DebugViewportSnapshotV1Validator.TryValidate(
            actual,
            out IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics);

        Assert.True(isValid);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Validation_InvalidContractAndVersion_ProducesDiagnostics()
    {
        DebugViewportSnapshotV1Dto dto = CreateValidationSnapshot();
        dto.Contract = "wrong.contract";
        dto.Version = 999;

        IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics =
            DebugViewportSnapshotV1Validator.Validate(dto);

        Assert.Contains(
            diagnostics,
            d => d.Code == DebugViewportSnapshotV1ValidationCode.InvalidContract &&
                 d.Path == "contract");
        Assert.Contains(
            diagnostics,
            d => d.Code == DebugViewportSnapshotV1ValidationCode.InvalidVersion &&
                 d.Path == "version");
    }

    [Fact]
    public void Validation_BadSampleCount_ProducesDiagnostic()
    {
        DebugViewportSnapshotV1Dto dto = CreateValidationSnapshot();
        dto.Metadata.SampleCount = dto.CenterlinePoints.Length + 1;

        IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics =
            DebugViewportSnapshotV1Validator.Validate(dto);

        Assert.Contains(
            diagnostics,
            d => d.Code == DebugViewportSnapshotV1ValidationCode.SampleCountMismatch &&
                 d.Path == "metadata.sampleCount");
    }

    [Fact]
    public void Validation_DecreasingCenterlineDistance_ProducesDiagnostic()
    {
        DebugViewportSnapshotV1Dto dto = CreateValidationSnapshot();
        dto.Frames = Array.Empty<DebugViewportFrameV1Dto>();
        dto.CenterlinePoints[0].Distance = 5.0;
        dto.CenterlinePoints[1].Distance = 4.0;

        IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics =
            DebugViewportSnapshotV1Validator.Validate(dto);

        Assert.Contains(
            diagnostics,
            d => d.Code == DebugViewportSnapshotV1ValidationCode.DecreasingDistance &&
                 d.Path == "centerlinePoints[1].distance");
    }

    [Fact]
    public void Validation_FrameCountAndDistanceMismatch_ProducesDiagnostics()
    {
        DebugViewportSnapshotV1Dto dto = CreateValidationSnapshot();
        dto.Frames[1].Distance = dto.Frames[1].Distance + 0.5;

        IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics =
            DebugViewportSnapshotV1Validator.Validate(dto);

        Assert.Contains(
            diagnostics,
            d => d.Code == DebugViewportSnapshotV1ValidationCode.FrameDistanceMismatch &&
                 d.Path == "frames[1].distance");

        dto = CreateValidationSnapshot();
        dto.Frames = new[] { dto.Frames[0] };

        diagnostics = DebugViewportSnapshotV1Validator.Validate(dto);

        Assert.Contains(
            diagnostics,
            d => d.Code == DebugViewportSnapshotV1ValidationCode.FrameCountMismatch &&
                 d.Path == "frames");
    }

    [Fact]
    public void Validation_MalformedNumericPayload_ProducesExpectedDiagnostics()
    {
        DebugViewportSnapshotV1Dto dto = CreateValidationSnapshot();
        dto.CenterlinePoints[0].Position.X = double.PositiveInfinity;
        dto.Frames[0].Tangent = new DebugViewportVector3dV1Dto();
        dto.Lines[0].End.Z = double.NaN;
        dto.Boxes[0].Size.Width = 0.0;

        IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics =
            DebugViewportSnapshotV1Validator.Validate(dto);

        Assert.Contains(
            diagnostics,
            d => d.Code == DebugViewportSnapshotV1ValidationCode.NonFiniteNumber &&
                 d.Path == "centerlinePoints[0].position.x");
        Assert.Contains(
            diagnostics,
            d => d.Code == DebugViewportSnapshotV1ValidationCode.NonFiniteNumber &&
                 d.Path == "lines[0].end.z");
        Assert.Contains(
            diagnostics,
            d => d.Code == DebugViewportSnapshotV1ValidationCode.ZeroLengthVector &&
                 d.Path == "frames[0].tangent");
        Assert.Contains(
            diagnostics,
            d => d.Code == DebugViewportSnapshotV1ValidationCode.NonPositiveBoxDimension &&
                 d.Path == "boxes[0].size.width");
    }

    [Fact]
    public void SchemaValidation_BuiltInSample_IsValid()
    {
        string json = DebugViewportSnapshotV1Json.Serialize(DebugViewportSnapshotV1SampleCommand.BuildSample());

        Assert.True(IsValidAgainstSchema(json));
    }

    [Theory]
    [MemberData(
        nameof(DebugViewportSnapshotV1FixtureTestHelper.Milestone7FixtureData),
        MemberType = typeof(DebugViewportSnapshotV1FixtureTestHelper))]
    public void SchemaValidation_CsvDerivedSnapshot_IsValid(CenterlineFrameCsvFixtureSpec fixtureSpec)
    {
        DebugViewportSnapshotV1Dto dto = DebugViewportSnapshotV1FixtureTestHelper.BuildSnapshot(fixtureSpec);
        string json = DebugViewportSnapshotV1Json.Serialize(dto);

        Assert.True(IsValidAgainstSchema(json));
    }

    [Fact]
    public void SchemaValidation_BankingProfileSnapshot_IsValid()
    {
        string json = DebugViewportSnapshotV1Json.Serialize(
            DebugViewportSnapshotV1BankingProfileSampleCommand.BuildSample());

        Assert.True(IsValidAgainstSchema(json));
    }

    [Fact]
    public void SchemaValidation_RejectsMissingRequiredField()
    {
        JsonObject json = ParseJsonObject(DebugViewportSnapshotV1Json.Serialize(CreateValidationSnapshot()));
        Assert.True(json.Remove("frames"));

        Assert.False(IsValidAgainstSchema(json.ToJsonString()));
    }

    [Fact]
    public void SchemaValidation_RejectsUnknownVocabulary()
    {
        JsonObject json = ParseJsonObject(DebugViewportSnapshotV1Json.Serialize(CreateValidationSnapshot()));
        json["boxes"]![0]!["role"] = "renderer.mesh";

        Assert.False(IsValidAgainstSchema(json.ToJsonString()));
    }

    [Fact]
    public void Validation_BadFrameOrthonormality_ProducesDiagnostic()
    {
        DebugViewportSnapshotV1Dto dto = CreateValidationSnapshot();
        dto.Frames[0].Normal = new DebugViewportVector3dV1Dto { X = 0.25, Y = 1.0, Z = 0.0 };

        IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics =
            DebugViewportSnapshotV1Validator.Validate(dto);

        Assert.Contains(
            diagnostics,
            d => d.Code == DebugViewportSnapshotV1ValidationCode.NonOrthonormalFrame &&
                 d.Path == "frames[0].tangentNormalDot");
    }

    [Fact]
    public void Validation_BadFrameHandedness_ProducesDiagnostic()
    {
        DebugViewportSnapshotV1Dto dto = CreateValidationSnapshot();
        dto.Frames[0].Binormal = new DebugViewportVector3dV1Dto { X = 0.0, Y = 0.0, Z = -1.0 };

        IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics =
            DebugViewportSnapshotV1Validator.Validate(dto);

        Assert.Contains(
            diagnostics,
            d => d.Code == DebugViewportSnapshotV1ValidationCode.InvalidFrameHandedness &&
                 d.Path == "frames[0].handedness");
    }

    [Fact]
    public void Validation_UnknownRoleAndKind_ProduceDiagnostics()
    {
        DebugViewportSnapshotV1Dto dto = CreateValidationSnapshot();
        dto.Lines[0].Kind = "legacy.tangent";
        dto.Boxes[0].Role = "renderer.mesh";

        IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics =
            DebugViewportSnapshotV1Validator.Validate(dto);

        Assert.Contains(
            diagnostics,
            d => d.Code == DebugViewportSnapshotV1ValidationCode.UnknownLineKind &&
                 d.Path == "lines[0].kind");
        Assert.Contains(
            diagnostics,
            d => d.Code == DebugViewportSnapshotV1ValidationCode.UnknownBoxRole &&
                 d.Path == "boxes[0].role");
    }

    [Fact]
    public void Validation_DegenerateLine_ProducesDiagnostic()
    {
        DebugViewportSnapshotV1Dto dto = CreateValidationSnapshot();
        dto.Lines[0].End = new DebugViewportVector3dV1Dto
        {
            X = dto.Lines[0].Start.X,
            Y = dto.Lines[0].Start.Y,
            Z = dto.Lines[0].Start.Z
        };

        IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics =
            DebugViewportSnapshotV1Validator.Validate(dto);

        Assert.Contains(
            diagnostics,
            d => d.Code == DebugViewportSnapshotV1ValidationCode.DegenerateLineSegment &&
                 d.Path == "lines[0]");
    }

    [Fact]
    public void Validation_InvalidNestedTrainPose_ProducesDiagnostic()
    {
        DebugViewportSnapshotV1Dto dto = CreateValidationSnapshot();
        dto.TrainPose = TrainPoseExportV1Mapper.Export(CreateTrainPose());
        dto.TrainPose.Cars[0].Body.OriginalBody.Matrix.M14 += 0.25;

        IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics =
            DebugViewportSnapshotV1Validator.Validate(dto);

        Assert.Contains(
            diagnostics,
            d => d.Code == DebugViewportSnapshotV1ValidationCode.NestedTrainPoseValidationError &&
                 d.Path == "trainPose.cars[0].body.originalBody.matrix.m14");
    }

    private static TrainPoseResult CreateTrainPose()
    {
        ExportTrackFrame frame = CreateFrame(distance: 12.0, x: 12.0);
        Matrix4x4d matrix = Matrix4x4d.FromMatrix4x4(frame.ToMatrix4x4());

        var definition = new TrainConsistDefinition(
            carCount: 1,
            carSpacing: 3.25,
            carGeometry: new TrainCarGeometry(length: 4.5, width: 1.8, height: 2.1),
            bogieLayout: new TrainBogieLayout(bogieSpacing: 2.75),
            wheelLayout: null);

        var body = new TrainCarTransform(
            carIndex: 0,
            distance: 12.0,
            frame: frame,
            matrix: frame.ToMatrix4x4());
        var frontBogie = new BogieTransform(0, 0, 13.0, frame, matrix);
        var rearBogie = new BogieTransform(0, 1, 11.0, frame, matrix);
        var articulatedBody = new ArticulatedTrainCarTransform(
            originalBody: body,
            frontBogie: frontBogie,
            rearBogie: rearBogie,
            articulatedFrame: frame,
            articulatedMatrix: matrix,
            centerDistance: 12.0);

        var car = new ArticulatedTrainCarWithWheelsTransform(
            body: articulatedBody,
            frontBogie: new TrainBogieWithWheelsTransform(frontBogie, Array.Empty<WheelTransform>()),
            rearBogie: new TrainBogieWithWheelsTransform(rearBogie, Array.Empty<WheelTransform>()));

        return new TrainPoseResult(leadDistance: 12.0, definition: definition, cars: new[] { car });
    }

    private static DebugViewportSnapshotV1Dto CreateValidationSnapshot()
    {
        ExportTrackFrame[] frames =
        {
            CreateFrame(distance: 0.0, x: 0.0),
            CreateFrame(distance: 5.0, x: 5.0)
        };
        DebugLineSegment[] lines = TrackFrameDebugGizmoBuilder.BuildAxes(frames[0], axisLength: 2.0);
        var boxes = new[]
        {
            new DebugViewportBoxSource(
                role: DebugViewportSnapshotV1Vocabulary.TrainBodyRole,
                label: "car-0",
                frame: frames[1],
                length: 4.5,
                width: 1.8,
                height: 2.1)
        };

        return DebugViewportSnapshotV1Mapper.Export(new DebugViewportSnapshotV1Source
        {
            Units = "meters",
            SourceFixtureName = "validation",
            SampledFrames = frames,
            Lines = lines,
            Boxes = boxes
        });
    }

    private static bool IsValidAgainstSchema(string instanceJson)
    {
        using JsonDocument instanceDocument = JsonDocument.Parse(instanceJson);

        EvaluationResults results = DebugViewportSnapshotSchema.Value.Evaluate(
            instanceDocument.RootElement,
            new EvaluationOptions
            {
                OutputFormat = OutputFormat.List
            });

        return results.IsValid;
    }

    private static JsonSchema CreateDebugViewportSnapshotSchema()
    {
        string schemaPath = Path.Combine(AppContext.BaseDirectory, "IO", "Fixtures", "DebugViewportSnapshotV1.schema.json");
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

    private static ExportTrackFrame CreateFrame(double distance, double x)
    {
        return new ExportTrackFrame(
            distance: distance,
            position: new Vector3d(x, 2.0, 3.0),
            tangent: Vector3d.UnitX,
            normal: Vector3d.UnitY,
            binormal: Vector3d.UnitZ);
    }
}
