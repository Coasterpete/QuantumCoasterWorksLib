using Quantum.Debug;
using Quantum.IO.DebugViewport.V1;
using Quantum.IO.TrainPose.V1;
using Quantum.Math;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class SpatialLayoutProofScenarioTests
{
    private const double Tolerance = 1e-8;

    [Fact]
    public void CreateDeterministic_CompilesFiveExactSectionIntervals()
    {
        SpatialLayoutProofScenario scenario = SpatialLayoutProofScenario.CreateDeterministic();
        TrackAuthoringCompilation compilation = scenario.Compilation;
        string[] expectedIds =
        {
            "spatial-layout-entry",
            "spatial-layout-rise-turn",
            "spatial-layout-elevated",
            "spatial-layout-descend-counter-turn",
            "spatial-layout-exit"
        };
        double[] expectedLengths = { 12.0, 18.0, 12.0, 18.0, 12.0 };
        double[] expectedStarts = { 0.0, 12.0, 30.0, 42.0, 60.0 };
        double[] expectedEnds = { 12.0, 30.0, 42.0, 60.0, 72.0 };

        Assert.Same(scenario.Definition, compilation.Definition);
        Assert.NotNull(compilation.Runtime);
        Assert.Equal(5, scenario.Definition.Sections.Count);
        Assert.Equal(5, compilation.Document.Segments.Count);
        Assert.Equal(5, compilation.ResolvedSections.Count);

        for (int i = 0; i < expectedIds.Length; i++)
        {
            GeometricSectionDefinition authored = scenario.Definition.Sections[i];
            ResolvedSectionInterval<GeometricSectionDefinition> resolved = compilation.ResolvedSections[i];

            Assert.Equal(expectedIds[i], authored.Id);
            Assert.Equal(expectedLengths[i], authored.Length);
            Assert.Equal(0.0, authored.RollRadians);
            Assert.Equal(expectedStarts[i], resolved.StartDistance);
            Assert.Equal(expectedEnds[i], resolved.EndDistance);
            Assert.Equal(expectedLengths[i], resolved.Length);
        }

        Assert.IsType<StraightSectionDefinition>(scenario.Definition.Sections[0]);
        SpatialSectionDefinition riseTurn =
            Assert.IsType<SpatialSectionDefinition>(scenario.Definition.Sections[1]);
        Assert.IsType<StraightSectionDefinition>(scenario.Definition.Sections[2]);
        SpatialSectionDefinition descendCounterTurn =
            Assert.IsType<SpatialSectionDefinition>(scenario.Definition.Sections[3]);
        Assert.IsType<StraightSectionDefinition>(scenario.Definition.Sections[4]);
        AssertStraightEndpointRuns(riseTurn);
        AssertStraightEndpointRuns(descendCounterTurn);
        Assert.Equal(72.0, compilation.TotalLength);
    }

    [Fact]
    public void CreateDeterministic_UsesTranslatedFortyFiveDegreeYawStartPose()
    {
        SpatialLayoutProofScenario scenario = SpatialLayoutProofScenario.CreateDeterministic();
        TrackStartPose pose = scenario.Definition.StartPose;
        double inverseSqrtTwo = 1.0 / System.Math.Sqrt(2.0);

        AssertVectorNear(new Vector3d(20.0, 4.0, -10.0), pose.Position);
        AssertVectorNear(new Vector3d(inverseSqrtTwo, 0.0, inverseSqrtTwo), pose.Tangent);
        AssertVectorNear(Vector3d.UnitY, pose.Normal);
        AssertVectorNear(new Vector3d(-inverseSqrtTwo, 0.0, inverseSqrtTwo), pose.Binormal);

        TrackFrame start = scenario.Frames[0];
        AssertVectorNear(pose.Position, start.Position);
        AssertVectorNear(pose.Tangent, start.Tangent);
        AssertVectorNear(pose.Normal, start.Normal);
        AssertVectorNear(pose.Binormal, start.Binormal);
    }

    [Fact]
    public void CreateDeterministic_RuntimeSamplesHaveMeaningfulThreeDimensionalExtent()
    {
        SpatialLayoutProofScenario scenario = SpatialLayoutProofScenario.CreateDeterministic();
        TrackStartPose pose = scenario.Definition.StartPose;
        var runtimeEvaluator = new TrackEvaluator(scenario.Compilation.Runtime);
        double maximumNormalDisplacement = 0.0;
        double maximumBinormalDisplacement = 0.0;

        for (int i = 0; i < scenario.Frames.Count; i++)
        {
            TrackFrame expected = runtimeEvaluator.EvaluateFrameAtDistance(i * 3.0);
            TrackFrame actual = scenario.Frames[i];
            Vector3d displacement = actual.Position - pose.Position;

            AssertFrameNear(expected, actual);
            maximumNormalDisplacement = System.Math.Max(
                maximumNormalDisplacement,
                System.Math.Abs(Vector3d.Dot(displacement, pose.Normal)));
            maximumBinormalDisplacement = System.Math.Max(
                maximumBinormalDisplacement,
                System.Math.Abs(Vector3d.Dot(displacement, pose.Binormal)));
        }

        Assert.True(maximumNormalDisplacement > 2.0);
        Assert.True(maximumBinormalDisplacement > 5.0);

        double elevatedNormalDisplacement = Vector3d.Dot(
            scenario.Frames[14].Position - pose.Position,
            pose.Normal);
        double afterDescentNormalDisplacement = Vector3d.Dot(
            scenario.Frames[20].Position - pose.Position,
            pose.Normal);
        Assert.True(elevatedNormalDisplacement > 3.0);
        Assert.True(elevatedNormalDisplacement - afterDescentNormalDisplacement > 2.0);
    }

    [Fact]
    public void CreateDeterministic_HasFourGeometryContinuousBoundariesWithoutDiagnostics()
    {
        SpatialLayoutProofScenario scenario = SpatialLayoutProofScenario.CreateDeterministic();

        Assert.Equal(4, scenario.GeometryContinuity.BoundaryCount);
        Assert.Equal(new[] { 12.0, 30.0, 42.0, 60.0 },
            scenario.GeometryContinuity.Boundaries.Select(boundary => boundary.Station));
        Assert.Empty(scenario.GeometryContinuity.Diagnostics);
        Assert.False(scenario.GeometryContinuity.HasDiagnostics);
    }

    [Fact]
    public void CreateDeterministic_EvaluatesTwentyFiveFiniteOrthonormalRightHandedFrames()
    {
        SpatialLayoutProofScenario scenario = SpatialLayoutProofScenario.CreateDeterministic();

        Assert.Equal(SpatialLayoutProofScenario.FrameCount, scenario.Frames.Count);

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
    public void CreateDeterministic_PlacesNineCarsAndStraddlesAllSectionBoundariesWithBogies()
    {
        SpatialLayoutProofScenario scenario = SpatialLayoutProofScenario.CreateDeterministic();
        double[] expectedBodyDistances = { 60.0, 54.0, 48.0, 42.0, 36.0, 30.0, 24.0, 18.0, 12.0 };

        Assert.Equal(expectedBodyDistances.Length, scenario.TrainPose.CarsReadOnly.Count);

        for (int i = 0; i < expectedBodyDistances.Length; i++)
        {
            ArticulatedTrainCarTransform body = scenario.TrainPose.CarsReadOnly[i].Body;

            Assert.Equal(expectedBodyDistances[i], body.OriginalBody.Distance);
            Assert.Equal(expectedBodyDistances[i], body.CenterDistance);
            Assert.Equal(expectedBodyDistances[i], body.ArticulatedFrame.Distance);
        }

        AssertBogiePairCrossesBoundary(scenario.TrainPose.CarsReadOnly[0].Body, boundary: 60.0);
        AssertBogiePairCrossesBoundary(scenario.TrainPose.CarsReadOnly[3].Body, boundary: 42.0);
        AssertBogiePairCrossesBoundary(scenario.TrainPose.CarsReadOnly[5].Body, boundary: 30.0);
        AssertBogiePairCrossesBoundary(scenario.TrainPose.CarsReadOnly[8].Body, boundary: 12.0);
    }

    [Fact]
    public void CreateDeterministic_ExportsValidDeterministicArtifacts()
    {
        SpatialLayoutProofScenario first = SpatialLayoutProofScenario.CreateDeterministic();
        SpatialLayoutProofScenario second = SpatialLayoutProofScenario.CreateDeterministic();
        string firstTrainJson = TrainPoseExportV1Json.Serialize(first.TrainPoseExport, indented: true);
        string secondTrainJson = TrainPoseExportV1Json.Serialize(second.TrainPoseExport, indented: true);
        string firstSnapshotJson = DebugViewportSnapshotV1Json.Serialize(first.Snapshot, indented: true);
        string secondSnapshotJson = DebugViewportSnapshotV1Json.Serialize(second.Snapshot, indented: true);

        Assert.True(
            TrainPoseExportV1Validator.TryValidate(first.TrainPoseExport, out var trainDiagnostics),
            FormatTrainPoseDiagnostics(trainDiagnostics));
        Assert.True(
            DebugViewportSnapshotV1Validator.TryValidate(first.Snapshot, out var snapshotDiagnostics),
            FormatSnapshotDiagnostics(snapshotDiagnostics));
        Assert.Empty(trainDiagnostics);
        Assert.Empty(snapshotDiagnostics);
        Assert.Equal(firstTrainJson, secondTrainJson);
        Assert.Equal(firstSnapshotJson, secondSnapshotJson);
        Assert.Equal(25, first.Snapshot.CenterlinePoints.Length);
        Assert.Equal(25, first.Snapshot.Frames.Length);
        Assert.Equal(3, first.Snapshot.Lines.Length);
        Assert.Equal(9, first.Snapshot.Boxes.Length);
        Assert.NotNull(first.Snapshot.TrainPose);
        Assert.Equal(9, first.Snapshot.TrainPose!.Cars.Length);
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

    private static void AssertFrameNear(TrackFrame expected, TrackFrame actual)
    {
        AssertNear(expected.Distance, actual.Distance);
        AssertVectorNear(expected.Position, actual.Position);
        AssertVectorNear(expected.Tangent, actual.Tangent);
        AssertVectorNear(expected.Normal, actual.Normal);
        AssertVectorNear(expected.Binormal, actual.Binormal);
    }

    private static void AssertStraightEndpointRuns(SpatialSectionDefinition section)
    {
        IReadOnlyList<Vector3d> points = section.ControlPoints;
        AssertNear(0.0, Vector3d.Cross(points[1] - points[0], points[2] - points[1]).Length);
        int last = points.Count - 1;
        AssertNear(0.0, Vector3d.Cross(points[last - 1] - points[last - 2], points[last] - points[last - 1]).Length);
    }

    private static void AssertFinite(Vector3d vector)
    {
        Assert.True(double.IsFinite(vector.X));
        Assert.True(double.IsFinite(vector.Y));
        Assert.True(double.IsFinite(vector.Z));
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual)
    {
        AssertNear(expected.X, actual.X);
        AssertNear(expected.Y, actual.Y);
        AssertNear(expected.Z, actual.Z);
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
