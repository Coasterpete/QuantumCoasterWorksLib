using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Quantum.IO.TrackLayout.V1;
using Quantum.Math;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class TrackLayoutPackageV1ProofFixtureTests
{
    private const double DistanceTolerance = 1e-7;
    private const double AxisTolerance = 1e-6;
    private const double ScalarTolerance = 1e-8;
    private static readonly Lazy<JsonSchema> TrackLayoutPackageSchema = new(CreateTrackLayoutPackageSchema);

    [Fact]
    public void ProofFixtures_ExportedDefinitions_MatchCommittedDeterministicJson()
    {
        foreach (TrackLayoutPackageV1ProofFixture fixture in TrackLayoutPackageV1ProofFixtures.All())
        {
            string actualJson = NormalizeJson(TrackLayoutPackageV1Json.Serialize(
                TrackLayoutPackageV1Mapper.Export(fixture.SourceDefinition),
                indented: true));
            string expectedJson = NormalizeJson(LoadGoldenFixtureJson(fixture));

            Assert.Equal(expectedJson, actualJson);

            string reserializedJson = NormalizeJson(TrackLayoutPackageV1Json.Serialize(
                TrackLayoutPackageV1Json.Deserialize(expectedJson),
                indented: true));
            Assert.Equal(expectedJson, reserializedJson);
        }
    }

    [Fact]
    public void ProofFixtures_CommittedJson_IsSchemaValidAndImports()
    {
        foreach (TrackLayoutPackageV1ProofFixture fixture in TrackLayoutPackageV1ProofFixtures.All())
        {
            string json = LoadGoldenFixtureJson(fixture);

            Assert.True(
                IsValidAgainstSchema(json),
                fixture.Name + " golden JSON should validate against track-layout-package-v1.schema.json.");

            TrackLayoutPackageV1ImportResult result = TrackLayoutPackageV1Json.Import(json);

            Assert.True(result.Success, FormatDiagnostics(result.Diagnostics));
            Assert.Empty(result.Diagnostics);
            Assert.NotNull(result.Definition);
        }
    }

    [Fact]
    public void ProofFixtures_DefinitionDtoJsonDtoDefinition_RoundTripsAuthoringValues()
    {
        foreach (TrackLayoutPackageV1ProofFixture fixture in TrackLayoutPackageV1ProofFixtures.All())
        {
            TrackLayoutPackageV1Dto dto = TrackLayoutPackageV1Mapper.Export(fixture.SourceDefinition);
            string json = TrackLayoutPackageV1Json.Serialize(dto, indented: true);
            TrackLayoutPackageV1Dto dtoRoundTrip = TrackLayoutPackageV1Json.Deserialize(json);
            TrackLayoutPackageV1ImportResult result = TrackLayoutPackageV1Mapper.Import(dtoRoundTrip);

            Assert.True(result.Success, FormatDiagnostics(result.Diagnostics));
            Assert.NotNull(result.Definition);
            AssertAuthoringDefinitionsEquivalent(fixture.SourceDefinition, result.Definition!);
        }
    }

    [Fact]
    public void ProofFixtures_RoundTrippedDefinitions_CompileWithParity()
    {
        foreach (TrackLayoutPackageV1ProofFixture fixture in TrackLayoutPackageV1ProofFixtures.All())
        {
            TrackAuthoringCompilation source = TrackAuthoringDocumentBuilder.Compile(
                fixture.SourceDefinition);
            TrackAuthoringCompilation roundTrip = TrackAuthoringDocumentBuilder.Compile(
                RoundTripDefinition(fixture.SourceDefinition));

            AssertNear(source.TotalLength, roundTrip.TotalLength, ScalarTolerance);
            AssertNear(source.Runtime.TotalLength, roundTrip.Runtime.TotalLength, ScalarTolerance);
            Assert.Equal(source.Runtime.SegmentCount, roundTrip.Runtime.SegmentCount);
            AssertResolvedSectionsEquivalent(source.ResolvedSections, roundTrip.ResolvedSections);
            AssertSegmentsEquivalent(source.Document.Segments, roundTrip.Document.Segments);
        }
    }

    [Fact]
    public void ProofFixtures_RuntimeFrames_MatchAtParityStations()
    {
        foreach (TrackLayoutPackageV1ProofFixture fixture in TrackLayoutPackageV1ProofFixtures.All())
        {
            TrackAuthoringCompilation source = TrackAuthoringDocumentBuilder.Compile(
                fixture.SourceDefinition);
            TrackAuthoringCompilation roundTrip = TrackAuthoringDocumentBuilder.Compile(
                RoundTripDefinition(fixture.SourceDefinition));
            TrackFrame[] expectedFrames = new TrackEvaluator(source.Runtime)
                .EvaluateFramesAtDistances(fixture.ParityStations);
            TrackFrame[] actualFrames = new TrackEvaluator(roundTrip.Runtime)
                .EvaluateFramesAtDistances(fixture.ParityStations);

            Assert.Equal(expectedFrames.Length, actualFrames.Length);
            for (int i = 0; i < expectedFrames.Length; i++)
            {
                AssertFrameNear(expectedFrames[i], actualFrames[i]);
            }
        }
    }

    [Fact]
    public void ProofFixtures_BankingProfiles_MatchAtKeysAndStations()
    {
        foreach (TrackLayoutPackageV1ProofFixture fixture in TrackLayoutPackageV1ProofFixtures.All())
        {
            TrackAuthoringCompilation source = TrackAuthoringDocumentBuilder.Compile(
                fixture.SourceDefinition);
            TrackAuthoringCompilation roundTrip = TrackAuthoringDocumentBuilder.Compile(
                RoundTripDefinition(fixture.SourceDefinition));
            IReadOnlyList<double> sampleStations = BuildBankingSampleStations(
                source.BankingProfile,
                fixture.ParityStations);

            AssertBankingProfilesEquivalent(source.BankingProfile, roundTrip.BankingProfile);
            for (int i = 0; i < sampleStations.Count; i++)
            {
                double station = sampleStations[i];
                AssertNear(
                    BankingProfileSampler.SampleRollRadians(source.BankingProfile, station),
                    BankingProfileSampler.SampleRollRadians(roundTrip.BankingProfile, station),
                    ScalarTolerance);
            }
        }
    }

    [Fact]
    public void ProofFixtures_ProfileBackedTrainPoses_MatchAcrossRoundTrip()
    {
        TrainConsistDefinition consist = TrackLayoutPackageV1ProofFixtures.CreateSharedTrainConsist();

        foreach (TrackLayoutPackageV1ProofFixture fixture in TrackLayoutPackageV1ProofFixtures.All())
        {
            TrackAuthoringCompilation source = TrackAuthoringDocumentBuilder.Compile(
                fixture.SourceDefinition);
            TrackAuthoringCompilation roundTrip = TrackAuthoringDocumentBuilder.Compile(
                RoundTripDefinition(fixture.SourceDefinition));
            var sourceProvider = new TrainCarTransformProvider(new TrackEvaluator(source.Runtime));
            var roundTripProvider = new TrainCarTransformProvider(new TrackEvaluator(roundTrip.Runtime));

            TrainPoseResult expected = sourceProvider.EvaluateTrainPose(
                TrackLayoutPackageV1ProofFixtures.SharedTrainLeadDistance,
                consist,
                source.BankingProfile);
            TrainPoseResult actual = roundTripProvider.EvaluateTrainPose(
                TrackLayoutPackageV1ProofFixtures.SharedTrainLeadDistance,
                consist,
                roundTrip.BankingProfile);

            AssertTrainPosesEquivalent(expected, actual);
        }
    }

    private static TrackAuthoringDefinition RoundTripDefinition(
        TrackAuthoringDefinition source)
    {
        TrackLayoutPackageV1Dto dto = TrackLayoutPackageV1Mapper.Export(source);
        string json = TrackLayoutPackageV1Json.Serialize(dto, indented: true);
        TrackLayoutPackageV1Dto dtoRoundTrip = TrackLayoutPackageV1Json.Deserialize(json);
        TrackLayoutPackageV1ImportResult result = TrackLayoutPackageV1Mapper.Import(dtoRoundTrip);

        Assert.True(result.Success, FormatDiagnostics(result.Diagnostics));
        Assert.NotNull(result.Definition);
        return result.Definition!;
    }

    private static void AssertAuthoringDefinitionsEquivalent(
        TrackAuthoringDefinition expected,
        TrackAuthoringDefinition actual)
    {
        AssertVectorExact(expected.StartPose.Position, actual.StartPose.Position);
        AssertVectorExact(expected.StartPose.Tangent, actual.StartPose.Tangent);
        AssertVectorExact(expected.StartPose.Normal, actual.StartPose.Normal);
        AssertVectorExact(expected.StartPose.Binormal, actual.StartPose.Binormal);
        Assert.Equal(expected.Sections.Count, actual.Sections.Count);

        for (int i = 0; i < expected.Sections.Count; i++)
        {
            AssertSectionEquivalent(expected.Sections[i], actual.Sections[i]);
        }

        if (expected.Banking == null)
        {
            Assert.Null(actual.Banking);
            return;
        }

        Assert.NotNull(actual.Banking);
        AssertBankingKeysEquivalent(expected.Banking.Keys, actual.Banking!.Keys);
    }

    private static void AssertSectionEquivalent(
        GeometricSectionDefinition expected,
        GeometricSectionDefinition actual)
    {
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Length, actual.Length);
        Assert.Equal(expected.RollRadians, actual.RollRadians);

        if (expected is ConstantCurvatureSectionDefinition expectedArc)
        {
            Assert.Equal(
                expectedArc.Radius,
                Assert.IsType<ConstantCurvatureSectionDefinition>(actual).Radius);
            return;
        }

        if (expected is CurvatureTransitionSectionDefinition expectedTransition)
        {
            CurvatureTransitionSectionDefinition actualTransition =
                Assert.IsType<CurvatureTransitionSectionDefinition>(actual);
            Assert.Equal(expectedTransition.StartCurvature, actualTransition.StartCurvature);
            Assert.Equal(expectedTransition.EndCurvature, actualTransition.EndCurvature);
            Assert.Equal(expectedTransition.InterpolationMode, actualTransition.InterpolationMode);
            return;
        }

        if (expected is SpatialSectionDefinition expectedSpatial)
        {
            SpatialSectionDefinition actualSpatial = Assert.IsType<SpatialSectionDefinition>(actual);
            Assert.Equal(expectedSpatial.Degree, actualSpatial.Degree);
            Assert.Equal(expectedSpatial.ControlPoints.Count, actualSpatial.ControlPoints.Count);
            Assert.Equal(expectedSpatial.Weights.Count, actualSpatial.Weights.Count);
            for (int i = 0; i < expectedSpatial.ControlPoints.Count; i++)
            {
                AssertVectorExact(expectedSpatial.ControlPoints[i], actualSpatial.ControlPoints[i]);
                Assert.Equal(expectedSpatial.Weights[i], actualSpatial.Weights[i]);
            }
        }
    }

    private static void AssertResolvedSectionsEquivalent(
        IReadOnlyList<ResolvedSectionInterval<GeometricSectionDefinition>> expected,
        IReadOnlyList<ResolvedSectionInterval<GeometricSectionDefinition>> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            AssertSectionEquivalent(expected[i].Section, actual[i].Section);
            AssertNear(expected[i].StartDistance, actual[i].StartDistance, ScalarTolerance);
            AssertNear(expected[i].EndDistance, actual[i].EndDistance, ScalarTolerance);
            AssertNear(expected[i].Length, actual[i].Length, ScalarTolerance);
            Assert.Equal(expected[i].IncludeEndDistance, actual[i].IncludeEndDistance);
        }
    }

    private static void AssertSegmentsEquivalent(
        IList<TrackSegment> expected,
        IList<TrackSegment> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].GetType(), actual[i].GetType());
            Assert.Equal(expected[i].Id, actual[i].Id);
            AssertNear(expected[i].Length, actual[i].Length, ScalarTolerance);
            AssertNear(expected[i].RollRadians, actual[i].RollRadians, ScalarTolerance);
        }
    }

    private static void AssertBankingProfilesEquivalent(
        BankingProfile expected,
        BankingProfile actual)
    {
        AssertBankingKeysEquivalent(expected.Keys, actual.Keys);
    }

    private static void AssertBankingKeysEquivalent(
        IReadOnlyList<BankingProfileKey> expected,
        IReadOnlyList<BankingProfileKey> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            AssertNear(expected[i].Distance, actual[i].Distance, ScalarTolerance);
            AssertNear(expected[i].RollRadians, actual[i].RollRadians, ScalarTolerance);
            Assert.Equal(expected[i].InterpolationToNext, actual[i].InterpolationToNext);
        }
    }

    private static IReadOnlyList<double> BuildBankingSampleStations(
        BankingProfile profile,
        IReadOnlyList<double> parityStations)
    {
        var stations = new List<double>();
        IReadOnlyList<BankingProfileKey> keys = profile.Keys;
        for (int i = 0; i < keys.Count; i++)
        {
            AddUnique(stations, keys[i].Distance);
            if (i < keys.Count - 1)
            {
                AddUnique(stations, (keys[i].Distance + keys[i + 1].Distance) * 0.5);
            }
        }

        for (int i = 0; i < parityStations.Count; i++)
        {
            AddUnique(stations, parityStations[i]);
        }

        stations.Sort();
        return stations;
    }

    private static void AssertTrainPosesEquivalent(
        TrainPoseResult expected,
        TrainPoseResult actual)
    {
        AssertNear(expected.LeadDistance, actual.LeadDistance, DistanceTolerance);
        Assert.Equal(expected.CarsReadOnly.Count, actual.CarsReadOnly.Count);

        for (int i = 0; i < expected.CarsReadOnly.Count; i++)
        {
            ArticulatedTrainCarWithWheelsTransform expectedCar = expected.CarsReadOnly[i];
            ArticulatedTrainCarWithWheelsTransform actualCar = actual.CarsReadOnly[i];

            AssertArticulatedCarNear(expectedCar.Body, actualCar.Body);
            AssertBogieWithWheelsNear(expectedCar.FrontBogie, actualCar.FrontBogie);
            AssertBogieWithWheelsNear(expectedCar.RearBogie, actualCar.RearBogie);
        }
    }

    private static void AssertArticulatedCarNear(
        ArticulatedTrainCarTransform expected,
        ArticulatedTrainCarTransform actual)
    {
        AssertCarTransformNear(expected.OriginalBody, actual.OriginalBody);
        AssertBogieTransformNear(expected.FrontBogie, actual.FrontBogie);
        AssertBogieTransformNear(expected.RearBogie, actual.RearBogie);
        AssertNear(expected.CenterDistance, actual.CenterDistance, DistanceTolerance);
        AssertFrameNear(expected.ArticulatedFrame, actual.ArticulatedFrame);
        AssertMatrixNear(expected.ArticulatedMatrix, actual.ArticulatedMatrix);
    }

    private static void AssertBogieWithWheelsNear(
        TrainBogieWithWheelsTransform expected,
        TrainBogieWithWheelsTransform actual)
    {
        AssertBogieTransformNear(expected.Bogie, actual.Bogie);
        Assert.Equal(expected.WheelsReadOnly.Count, actual.WheelsReadOnly.Count);

        for (int i = 0; i < expected.WheelsReadOnly.Count; i++)
        {
            WheelTransform expectedWheel = expected.WheelsReadOnly[i];
            WheelTransform actualWheel = actual.WheelsReadOnly[i];

            Assert.Equal(expectedWheel.CarIndex, actualWheel.CarIndex);
            Assert.Equal(expectedWheel.BogieIndex, actualWheel.BogieIndex);
            Assert.Equal(expectedWheel.WheelIndex, actualWheel.WheelIndex);
            AssertNear(expectedWheel.LocalOffsetX, actualWheel.LocalOffsetX, ScalarTolerance);
            AssertNear(expectedWheel.LocalOffsetY, actualWheel.LocalOffsetY, ScalarTolerance);
            AssertNear(expectedWheel.LocalOffsetZ, actualWheel.LocalOffsetZ, ScalarTolerance);
            AssertFrameNear(expectedWheel.Frame, actualWheel.Frame);
            AssertMatrixNear(expectedWheel.Matrix, actualWheel.Matrix);
        }
    }

    private static void AssertCarTransformNear(
        TrainCarTransform expected,
        TrainCarTransform actual)
    {
        Assert.Equal(expected.CarIndex, actual.CarIndex);
        AssertNear(expected.Distance, actual.Distance, DistanceTolerance);
        AssertFrameNear(expected.Frame, actual.Frame);
        AssertMatrixNear(expected.Matrix, actual.Matrix);
    }

    private static void AssertBogieTransformNear(
        BogieTransform expected,
        BogieTransform actual)
    {
        Assert.Equal(expected.CarIndex, actual.CarIndex);
        Assert.Equal(expected.BogieIndex, actual.BogieIndex);
        AssertNear(expected.Distance, actual.Distance, DistanceTolerance);
        AssertFrameNear(expected.Frame, actual.Frame);
        AssertMatrixNear(expected.Matrix, actual.Matrix);
    }

    private static void AssertFrameNear(TrackFrame expected, TrackFrame actual)
    {
        AssertNear(expected.Distance, actual.Distance, DistanceTolerance);
        AssertVectorNear(expected.Position, actual.Position);
        AssertVectorNear(expected.Tangent, actual.Tangent);
        AssertVectorNear(expected.Normal, actual.Normal);
        AssertVectorNear(expected.Binormal, actual.Binormal);
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual)
    {
        AssertNear(expected.X, actual.X, AxisTolerance);
        AssertNear(expected.Y, actual.Y, AxisTolerance);
        AssertNear(expected.Z, actual.Z, AxisTolerance);
    }

    private static void AssertVectorExact(Vector3d expected, Vector3d actual)
    {
        Assert.Equal(expected.X, actual.X);
        Assert.Equal(expected.Y, actual.Y);
        Assert.Equal(expected.Z, actual.Z);
    }

    private static void AssertMatrixNear(Matrix4x4 expected, Matrix4x4 actual)
    {
        AssertNear(expected.M11, actual.M11, AxisTolerance);
        AssertNear(expected.M12, actual.M12, AxisTolerance);
        AssertNear(expected.M13, actual.M13, AxisTolerance);
        AssertNear(expected.M14, actual.M14, AxisTolerance);
        AssertNear(expected.M21, actual.M21, AxisTolerance);
        AssertNear(expected.M22, actual.M22, AxisTolerance);
        AssertNear(expected.M23, actual.M23, AxisTolerance);
        AssertNear(expected.M24, actual.M24, AxisTolerance);
        AssertNear(expected.M31, actual.M31, AxisTolerance);
        AssertNear(expected.M32, actual.M32, AxisTolerance);
        AssertNear(expected.M33, actual.M33, AxisTolerance);
        AssertNear(expected.M34, actual.M34, AxisTolerance);
        AssertNear(expected.M41, actual.M41, AxisTolerance);
        AssertNear(expected.M42, actual.M42, AxisTolerance);
        AssertNear(expected.M43, actual.M43, AxisTolerance);
        AssertNear(expected.M44, actual.M44, AxisTolerance);
    }

    private static void AssertMatrixNear(Matrix4x4d expected, Matrix4x4d actual)
    {
        AssertNear(expected.M11, actual.M11, AxisTolerance);
        AssertNear(expected.M12, actual.M12, AxisTolerance);
        AssertNear(expected.M13, actual.M13, AxisTolerance);
        AssertNear(expected.M14, actual.M14, AxisTolerance);
        AssertNear(expected.M21, actual.M21, AxisTolerance);
        AssertNear(expected.M22, actual.M22, AxisTolerance);
        AssertNear(expected.M23, actual.M23, AxisTolerance);
        AssertNear(expected.M24, actual.M24, AxisTolerance);
        AssertNear(expected.M31, actual.M31, AxisTolerance);
        AssertNear(expected.M32, actual.M32, AxisTolerance);
        AssertNear(expected.M33, actual.M33, AxisTolerance);
        AssertNear(expected.M34, actual.M34, AxisTolerance);
        AssertNear(expected.M41, actual.M41, AxisTolerance);
        AssertNear(expected.M42, actual.M42, AxisTolerance);
        AssertNear(expected.M43, actual.M43, AxisTolerance);
        AssertNear(expected.M44, actual.M44, AxisTolerance);
    }

    private static void AssertNear(double expected, double actual, double tolerance)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, tolerance);
    }

    private static void AddUnique(ICollection<double> stations, double station)
    {
        if (!stations.Contains(station))
        {
            stations.Add(station);
        }
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
        JsonNode? node = JsonNode.Parse(schemaJson);
        JsonObject schemaObject = Assert.IsType<JsonObject>(node);
        schemaObject.Remove("$id");
        return JsonSchema.FromText(schemaObject.ToJsonString());
    }

    private static string LoadGoldenFixtureJson(TrackLayoutPackageV1ProofFixture fixture)
    {
        string fixturePath = Path.Combine(AppContext.BaseDirectory, "IO", "Fixtures", fixture.GoldenFileName);
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

    private static string NormalizeJson(string value)
    {
        return value.ReplaceLineEndings("\n").TrimEnd('\n');
    }

    private static string FormatDiagnostics(
        IReadOnlyList<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
    {
        return string.Join(
            Environment.NewLine,
            diagnostics.Select(diagnostic =>
                diagnostic.Code + " " + diagnostic.Path + ": " + diagnostic.Message));
    }
}
