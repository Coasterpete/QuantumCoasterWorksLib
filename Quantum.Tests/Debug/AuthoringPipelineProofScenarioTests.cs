using Quantum.Debug;
using Quantum.IO.DebugViewport.V1;
using Quantum.IO.TrainPose.V1;
using Quantum.Math;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class AuthoringPipelineProofScenarioTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void CreateDeterministic_CompilesAlignedSectionsAndResolvedIntervals()
    {
        AuthoringPipelineProofScenario scenario = AuthoringPipelineProofScenario.CreateDeterministic();
        TrackAuthoringCompilation compilation = scenario.Compilation;
        string[] expectedIds = { "authoring-entry", "authoring-arc", "authoring-exit" };
        double[] expectedLengths = { 12.0, 24.0, 12.0 };
        double[] expectedStarts = { 0.0, 12.0, 36.0 };
        double[] expectedEnds = { 12.0, 36.0, 48.0 };

        Assert.Same(scenario.Definition, compilation.Definition);
        Assert.Equal(3, scenario.Definition.Sections.Count);
        Assert.Equal(3, compilation.Document.Segments.Count);
        Assert.Equal(3, compilation.Document.Sections.Count);
        Assert.Equal(3, compilation.ResolvedSections.Count);

        for (int i = 0; i < expectedIds.Length; i++)
        {
            GeometricSectionDefinition authored = scenario.Definition.Sections[i];
            TrackSegment segment = compilation.Document.Segments[i];
            GeometricSection geometricSection = Assert.IsType<GeometricSection>(
                compilation.Document.Sections[i]);
            ResolvedSectionInterval<GeometricSectionDefinition> resolved = compilation.ResolvedSections[i];

            Assert.Equal(expectedIds[i], authored.Id);
            Assert.Equal(expectedIds[i], segment.Id);
            Assert.Equal(expectedLengths[i], authored.Length);
            Assert.Equal(expectedLengths[i], segment.Length);
            Assert.Equal(expectedLengths[i], geometricSection.Length);
            Assert.Equal(0.0, authored.RollRadians);
            Assert.Equal(0.0, segment.RollRadians);
            Assert.Same(authored, resolved.Section);
            Assert.Equal(expectedStarts[i], resolved.StartDistance);
            Assert.Equal(expectedEnds[i], resolved.EndDistance);
            Assert.Equal(expectedLengths[i], resolved.Length);
            Assert.Equal(i == expectedIds.Length - 1, resolved.IncludeEndDistance);
        }

        Assert.IsType<StraightSectionDefinition>(scenario.Definition.Sections[0]);
        Assert.IsType<ConstantCurvatureSectionDefinition>(scenario.Definition.Sections[1]);
        Assert.IsType<StraightSectionDefinition>(scenario.Definition.Sections[2]);
        Assert.IsType<StraightSegment>(compilation.Document.Segments[0]);
        Assert.IsType<CurvedSegment>(compilation.Document.Segments[1]);
        Assert.IsType<StraightSegment>(compilation.Document.Segments[2]);
        Assert.Equal(48.0, compilation.TotalLength);
        Assert.Equal(compilation.TotalLength, compilation.Document.TotalLength);
    }

    [Fact]
    public void CreateDeterministic_EvaluatesFiniteOrthonormalFramesAtGlobalDistances()
    {
        AuthoringPipelineProofScenario scenario = AuthoringPipelineProofScenario.CreateDeterministic();

        Assert.Equal(AuthoringPipelineProofScenario.FrameCount, scenario.Frames.Count);

        for (int i = 0; i < scenario.Frames.Count; i++)
        {
            TrackFrame frame = scenario.Frames[i];

            Assert.Equal(i * 6.0, frame.Distance);
            AssertFinite(frame.Position);
            AssertFinite(frame.Tangent);
            AssertFinite(frame.Normal);
            AssertFinite(frame.Binormal);
            AssertNear(1.0, frame.Tangent.Length);
            AssertNear(1.0, frame.Normal.Length);
            AssertNear(1.0, frame.Binormal.Length);
            AssertNear(0.0, Vector3d.Dot(frame.Tangent, frame.Normal));
            AssertNear(0.0, Vector3d.Dot(frame.Tangent, frame.Binormal));
            AssertNear(0.0, Vector3d.Dot(frame.Normal, frame.Binormal));
            AssertNear(1.0, Vector3d.Dot(Vector3d.Cross(frame.Tangent, frame.Normal), frame.Binormal));
        }
    }

    [Fact]
    public void CreateDeterministic_PlacesFiveCarsByDistanceAndCrossesBothBoundariesWithBogies()
    {
        AuthoringPipelineProofScenario scenario = AuthoringPipelineProofScenario.CreateDeterministic();
        double[] expectedBodyDistances = { 36.0, 30.0, 24.0, 18.0, 12.0 };

        Assert.Equal(expectedBodyDistances.Length, scenario.TrainPose.CarsReadOnly.Count);

        for (int i = 0; i < expectedBodyDistances.Length; i++)
        {
            ArticulatedTrainCarTransform body = scenario.TrainPose.CarsReadOnly[i].Body;

            Assert.Equal(expectedBodyDistances[i], body.OriginalBody.Distance);
            Assert.Equal(expectedBodyDistances[i], body.CenterDistance);
            Assert.Equal(expectedBodyDistances[i], body.ArticulatedFrame.Distance);
        }

        AssertBogiePairCrossesBoundary(scenario.TrainPose.CarsReadOnly[0].Body, boundary: 36.0);
        AssertBogiePairCrossesBoundary(scenario.TrainPose.CarsReadOnly[4].Body, boundary: 12.0);
    }

    [Fact]
    public void CreateDeterministic_ExportsValidTrainPoseV1WithoutChangingContractIdentity()
    {
        AuthoringPipelineProofScenario scenario = AuthoringPipelineProofScenario.CreateDeterministic();

        bool isValid = TrainPoseExportV1Validator.TryValidate(
            scenario.TrainPoseExport,
            out IReadOnlyList<TrainPoseExportV1ValidationDiagnostic> diagnostics);

        Assert.True(isValid, FormatTrainPoseDiagnostics(diagnostics));
        Assert.Empty(diagnostics);
        Assert.Equal("quantum.train_pose", TrainPoseExportV1Dto.ContractName);
        Assert.Equal(1, TrainPoseExportV1Dto.ContractVersion);
        Assert.Equal(TrainPoseExportV1Dto.ContractName, scenario.TrainPoseExport.Contract);
        Assert.Equal(TrainPoseExportV1Dto.ContractVersion, scenario.TrainPoseExport.Version);
        Assert.Equal(AuthoringPipelineProofScenario.TrainCarCount, scenario.TrainPoseExport.Cars.Length);
    }

    [Fact]
    public void CreateDeterministic_ExportsValidRoundTrippableDebugSnapshotV1()
    {
        AuthoringPipelineProofScenario scenario = AuthoringPipelineProofScenario.CreateDeterministic();
        string json = DebugViewportSnapshotV1Json.Serialize(scenario.Snapshot, indented: true);
        DebugViewportSnapshotV1Dto roundtrip = DebugViewportSnapshotV1Json.Deserialize(json);

        bool isValid = DebugViewportSnapshotV1Validator.TryValidate(
            roundtrip,
            out IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics);

        Assert.True(isValid, FormatSnapshotDiagnostics(diagnostics));
        Assert.Empty(diagnostics);
        Assert.Equal("quantum.debug_viewport_snapshot", DebugViewportSnapshotV1Dto.ContractName);
        Assert.Equal(1, DebugViewportSnapshotV1Dto.ContractVersion);
        Assert.Equal(DebugViewportSnapshotV1Dto.ContractName, roundtrip.Contract);
        Assert.Equal(DebugViewportSnapshotV1Dto.ContractVersion, roundtrip.Version);
        Assert.Equal(AuthoringPipelineProofScenario.FixtureName, roundtrip.Metadata.SourceFixtureName);
        Assert.Equal(AuthoringPipelineProofScenario.FrameCount, roundtrip.CenterlinePoints.Length);
        Assert.Equal(AuthoringPipelineProofScenario.FrameCount, roundtrip.Frames.Length);
        Assert.Equal(AuthoringPipelineProofScenario.TrainCarCount, roundtrip.Boxes.Length);
        Assert.NotNull(roundtrip.TrainPose);
        Assert.Equal(scenario.TrainPoseExport.Contract, roundtrip.TrainPose!.Contract);
        Assert.Equal(scenario.TrainPoseExport.Version, roundtrip.TrainPose.Version);
        Assert.Equal(scenario.TrainPoseExport.Cars.Length, roundtrip.TrainPose.Cars.Length);
    }

    private static void AssertBogiePairCrossesBoundary(
        ArticulatedTrainCarTransform body,
        double boundary)
    {
        double lower = System.Math.Min(body.FrontBogie.Distance, body.RearBogie.Distance);
        double upper = System.Math.Max(body.FrontBogie.Distance, body.RearBogie.Distance);

        Assert.True(lower < boundary, $"Expected a bogie below boundary {boundary}, found {lower}.");
        Assert.True(upper > boundary, $"Expected a bogie above boundary {boundary}, found {upper}.");
    }

    private static void AssertFinite(Vector3d vector)
    {
        Assert.True(double.IsFinite(vector.X));
        Assert.True(double.IsFinite(vector.Y));
        Assert.True(double.IsFinite(vector.Z));
    }

    private static void AssertNear(double expected, double actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, Tolerance);
    }

    private static string FormatTrainPoseDiagnostics(
        IReadOnlyList<TrainPoseExportV1ValidationDiagnostic> diagnostics)
    {
        return string.Join(Environment.NewLine, diagnostics.Select(d => $"{d.Code} {d.Path}: {d.Message}"));
    }

    private static string FormatSnapshotDiagnostics(
        IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics)
    {
        return string.Join(Environment.NewLine, diagnostics.Select(d => $"{d.Code} {d.Path}: {d.Message}"));
    }
}
