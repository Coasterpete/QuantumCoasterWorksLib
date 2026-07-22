using System.Text;
using Quantum.Editor.Avalonia.Services;
using Quantum.Editor.Avalonia.Services.Documents;
using Quantum.Editor.Avalonia.Services.Viewport;
using Quantum.IO.TrackLayout.V2;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class M167CompilationPipelineMeasurementTests
{
    [Fact]
    public void Baseline_ApplyGraphEdit_CompilesFiveTimesAndBuildsOneProjection()
    {
        var workspace = new EditorWorkspace();
        TrackEditorDocument document = EditorTestDocumentFactory.ActivateShowcase(workspace);
        TrackAuthoringGraph graph = document.Graph!;
        ConstantCurvatureSectionDefinition section = Assert.IsType<ConstantCurvatureSectionDefinition>(
            graph.Nodes.Single(node => node.Id == "sweeper").Section);

        using TrackAuthoringPipelineMeasurement authoring =
            TrackAuthoringPipelineMeasurement.Begin();
        using EditorViewportPipelineMeasurement viewport =
            EditorViewportPipelineMeasurement.Begin();

        bool applied = workspace.ApplyGraphEdit(
            "Edit sweeper radius",
            candidate => TrackAuthoringGraphOperations.Replace(
                candidate,
                section.Id,
                new ConstantCurvatureSectionDefinition(
                    section.Id,
                    section.Length,
                    radius: 30.0,
                    section.RollRadians)));

        Assert.True(applied);
        Assert.Equal(5, authoring.GraphCompilerInvocationCount);
        Assert.Equal(1, authoring.EngineeringSnapshotBuildCount);
        Assert.Equal(1, viewport.ViewportProjectionBuildCount);
        AssertPositiveTimings(authoring, viewport);
    }

    [Fact]
    public void Baseline_Save_CompilesTwiceWithoutRebuildingProjection()
    {
        var workspace = new EditorWorkspace();
        EditorTestDocumentFactory.ActivateShowcase(workspace);
        string filePath = TemporaryFilePath();

        try
        {
            using TrackAuthoringPipelineMeasurement authoring =
                TrackAuthoringPipelineMeasurement.Begin();
            using EditorViewportPipelineMeasurement viewport =
                EditorViewportPipelineMeasurement.Begin();

            workspace.SaveDocument(filePath);

            Assert.Equal(2, authoring.GraphCompilerInvocationCount);
            Assert.Equal(0, authoring.EngineeringSnapshotBuildCount);
            Assert.Equal(0, viewport.ViewportProjectionBuildCount);
            Assert.True(authoring.GraphCompilerElapsed > TimeSpan.Zero);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void Baseline_Open_CompilesThreeTimesAndBuildsOneProjection()
    {
        string filePath = TemporaryFilePath();
        File.WriteAllText(
            filePath,
            TrackLayoutPackageV2Json.Serialize(
                TrackPackageFactory.CreateShowcasePackage(),
                indented: true),
            new UTF8Encoding(false));

        try
        {
            var workspace = new EditorWorkspace();
            using TrackAuthoringPipelineMeasurement authoring =
                TrackAuthoringPipelineMeasurement.Begin();
            using EditorViewportPipelineMeasurement viewport =
                EditorViewportPipelineMeasurement.Begin();

            workspace.OpenDocument(filePath);

            Assert.Equal(3, authoring.GraphCompilerInvocationCount);
            Assert.Equal(1, authoring.EngineeringSnapshotBuildCount);
            Assert.Equal(1, viewport.ViewportProjectionBuildCount);
            AssertPositiveTimings(authoring, viewport);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void Baseline_UndoAndRedo_EachCompileTwiceAndBuildOneProjection()
    {
        var workspace = new EditorWorkspace();
        TrackEditorDocument document = EditorTestDocumentFactory.ActivateShowcase(workspace);
        ConstantCurvatureSectionDefinition section = Assert.IsType<ConstantCurvatureSectionDefinition>(
            document.Graph!.Nodes.Single(node => node.Id == "sweeper").Section);
        Assert.True(workspace.ApplyGraphEdit(
            "Edit sweeper radius",
            candidate => TrackAuthoringGraphOperations.Replace(
                candidate,
                section.Id,
                new ConstantCurvatureSectionDefinition(
                    section.Id,
                    section.Length,
                    radius: 30.0,
                    section.RollRadians))));

        AssertHistoryMeasurement(
            workspace.UndoLast,
            expectedCompilerInvocations: 2);
        AssertHistoryMeasurement(
            workspace.RedoLast,
            expectedCompilerInvocations: 2);
    }

    private static void AssertHistoryMeasurement(
        Func<bool> operation,
        int expectedCompilerInvocations)
    {
        using TrackAuthoringPipelineMeasurement authoring =
            TrackAuthoringPipelineMeasurement.Begin();
        using EditorViewportPipelineMeasurement viewport =
            EditorViewportPipelineMeasurement.Begin();

        Assert.True(operation());

        Assert.Equal(expectedCompilerInvocations, authoring.GraphCompilerInvocationCount);
        Assert.Equal(1, authoring.EngineeringSnapshotBuildCount);
        Assert.Equal(1, viewport.ViewportProjectionBuildCount);
        AssertPositiveTimings(authoring, viewport);
    }

    private static void AssertPositiveTimings(
        TrackAuthoringPipelineMeasurement authoring,
        EditorViewportPipelineMeasurement viewport)
    {
        Assert.True(authoring.GraphCompilerElapsed > TimeSpan.Zero);
        Assert.True(authoring.EngineeringSnapshotBuildElapsed > TimeSpan.Zero);
        Assert.True(viewport.ViewportProjectionBuildElapsed > TimeSpan.Zero);
    }

    private static string TemporaryFilePath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"quantum-m167-{Guid.NewGuid():N}.track-layout-v2.json");
    }
}
