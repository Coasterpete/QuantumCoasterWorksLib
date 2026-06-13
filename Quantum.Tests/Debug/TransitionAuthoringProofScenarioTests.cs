using Quantum.Debug;
using Quantum.IO.DebugViewport.V1;
using Quantum.IO.TrainPose.V1;
using Quantum.Math;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class TransitionAuthoringProofScenarioTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void CreateDeterministic_CompilesFiveExactSectionIntervals()
    {
        TransitionAuthoringProofScenario scenario = TransitionAuthoringProofScenario.CreateDeterministic();
        TrackAuthoringCompilation compilation = scenario.Compilation;
        string[] expectedIds =
        {
            "transition-entry",
            "transition-in",
            "transition-arc",
            "transition-out",
            "transition-exit"
        };
        double[] expectedLengths = { 12.0, 6.0, 12.0, 6.0, 12.0 };
        double[] expectedStarts = { 0.0, 12.0, 18.0, 30.0, 36.0 };
        double[] expectedEnds = { 12.0, 18.0, 30.0, 36.0, 48.0 };

        Assert.Same(scenario.Definition, compilation.Definition);
        Assert.Equal(5, scenario.Definition.Sections.Count);
        Assert.Equal(5, compilation.Document.Segments.Count);
        Assert.Equal(5, compilation.Document.Sections.Count);
        Assert.Equal(5, compilation.ResolvedSections.Count);

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
        Assert.IsType<CurvatureTransitionSectionDefinition>(scenario.Definition.Sections[1]);
        Assert.IsType<ConstantCurvatureSectionDefinition>(scenario.Definition.Sections[2]);
        Assert.IsType<CurvatureTransitionSectionDefinition>(scenario.Definition.Sections[3]);
        Assert.IsType<StraightSectionDefinition>(scenario.Definition.Sections[4]);
        Assert.IsType<StraightSegment>(compilation.Document.Segments[0]);
        Assert.IsType<CurvedSegment>(compilation.Document.Segments[1]);
        Assert.IsType<CurvedSegment>(compilation.Document.Segments[2]);
        Assert.IsType<CurvedSegment>(compilation.Document.Segments[3]);
        Assert.IsType<StraightSegment>(compilation.Document.Segments[4]);
        Assert.Equal(48.0, compilation.TotalLength);
        Assert.Equal(compilation.TotalLength, compilation.Document.TotalLength);
    }

    [Fact]
    public void CreateDeterministic_HasFourContinuousBoundariesWithoutDiagnostics()
    {
        TransitionAuthoringProofScenario scenario = TransitionAuthoringProofScenario.CreateDeterministic();

        Assert.Equal(4, scenario.Continuity.BoundaryCount);
        Assert.Equal(new[] { 12.0, 18.0, 30.0, 36.0 },
            scenario.Continuity.Boundaries.Select(boundary => boundary.Station));
        Assert.Empty(scenario.Continuity.Diagnostics);
        Assert.False(scenario.Continuity.HasDiagnostics);
    }

    [Fact]
    public void CreateDeterministic_EvaluatesSeventeenFiniteOrthonormalRightHandedFrames()
    {
        TransitionAuthoringProofScenario scenario = TransitionAuthoringProofScenario.CreateDeterministic();

        Assert.Equal(TransitionAuthoringProofScenario.FrameCount, scenario.Frames.Count);

        for (int i = 0; i < scenario.Frames.Count; i++)
        {
            TrackFrame frame = scenario.Frames[i];

            Assert.Equal(i * 3.0, frame.Distance);
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
    public void CreateDeterministic_PlacesFiveCarsAndStraddlesAllSectionBoundariesWithBogies()
    {
        TransitionAuthoringProofScenario scenario = TransitionAuthoringProofScenario.CreateDeterministic();
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
        AssertBogiePairCrossesBoundary(scenario.TrainPose.CarsReadOnly[1].Body, boundary: 30.0);
        AssertBogiePairCrossesBoundary(scenario.TrainPose.CarsReadOnly[3].Body, boundary: 18.0);
        AssertBogiePairCrossesBoundary(scenario.TrainPose.CarsReadOnly[4].Body, boundary: 12.0);
    }

    [Fact]
    public void CreateDeterministic_ExportsValidDeterministicTrainPoseV1()
    {
        TransitionAuthoringProofScenario first = TransitionAuthoringProofScenario.CreateDeterministic();
        TransitionAuthoringProofScenario second = TransitionAuthoringProofScenario.CreateDeterministic();

        bool isValid = TrainPoseExportV1Validator.TryValidate(
            first.TrainPoseExport,
            out IReadOnlyList<TrainPoseExportV1ValidationDiagnostic> diagnostics);

        Assert.True(isValid, FormatTrainPoseDiagnostics(diagnostics));
        Assert.Empty(diagnostics);
        Assert.Equal(TrainPoseExportV1Dto.ContractName, first.TrainPoseExport.Contract);
        Assert.Equal(TrainPoseExportV1Dto.ContractVersion, first.TrainPoseExport.Version);
        Assert.Equal(TransitionAuthoringProofScenario.TrainCarCount, first.TrainPoseExport.Cars.Length);
        Assert.Equal(
            TrainPoseExportV1Json.Serialize(first.TrainPoseExport, indented: true),
            TrainPoseExportV1Json.Serialize(second.TrainPoseExport, indented: true));
    }

    [Fact]
    public void CreateDeterministic_ExportsValidDeterministicDebugViewportSnapshotV1()
    {
        TransitionAuthoringProofScenario first = TransitionAuthoringProofScenario.CreateDeterministic();
        TransitionAuthoringProofScenario second = TransitionAuthoringProofScenario.CreateDeterministic();
        string firstJson = DebugViewportSnapshotV1Json.Serialize(first.Snapshot, indented: true);
        string secondJson = DebugViewportSnapshotV1Json.Serialize(second.Snapshot, indented: true);
        DebugViewportSnapshotV1Dto roundtrip = DebugViewportSnapshotV1Json.Deserialize(firstJson);

        bool isValid = DebugViewportSnapshotV1Validator.TryValidate(
            roundtrip,
            out IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics);

        Assert.True(isValid, FormatSnapshotDiagnostics(diagnostics));
        Assert.Empty(diagnostics);
        Assert.Equal(firstJson, secondJson);
        Assert.Equal(DebugViewportSnapshotV1Dto.ContractName, roundtrip.Contract);
        Assert.Equal(DebugViewportSnapshotV1Dto.ContractVersion, roundtrip.Version);
        Assert.Equal(TransitionAuthoringProofScenario.FixtureName, roundtrip.Metadata.SourceFixtureName);
        Assert.Equal(TransitionAuthoringProofScenario.FrameCount, roundtrip.CenterlinePoints.Length);
        Assert.Equal(TransitionAuthoringProofScenario.FrameCount, roundtrip.Frames.Length);
        Assert.Equal(TransitionAuthoringProofScenario.TrainCarCount, roundtrip.Boxes.Length);
        Assert.NotNull(roundtrip.TrainPose);
        Assert.Equal(first.TrainPoseExport.Contract, roundtrip.TrainPose!.Contract);
        Assert.Equal(first.TrainPoseExport.Version, roundtrip.TrainPose.Version);
        Assert.Equal(first.TrainPoseExport.Cars.Length, roundtrip.TrainPose.Cars.Length);
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
