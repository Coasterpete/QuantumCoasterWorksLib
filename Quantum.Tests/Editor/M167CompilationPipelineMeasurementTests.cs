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
    public void ApplyGraphEdit_CompilesOnceAndBuildsOneProjection()
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
        Assert.Equal(1, authoring.GraphCompilerInvocationCount);
        Assert.Equal(1, authoring.EngineeringSnapshotBuildCount);
        Assert.Equal(1, viewport.ViewportProjectionBuildCount);
        Assert.Same(document.Graph, document.GraphCompileResult!.SourceGraph);
        AssertPositiveTimings(authoring, viewport);
    }

    [Fact]
    public void Save_DoesNotCompileOrRebuildProjection()
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

            Assert.Equal(0, authoring.GraphCompilerInvocationCount);
            Assert.Equal(0, authoring.EngineeringSnapshotBuildCount);
            Assert.Equal(0, viewport.ViewportProjectionBuildCount);
            Assert.Equal(TimeSpan.Zero, authoring.GraphCompilerElapsed);
            Assert.Equal(TimeSpan.Zero, authoring.EngineeringSnapshotBuildElapsed);
            Assert.Equal(TimeSpan.Zero, viewport.ViewportProjectionBuildElapsed);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void Open_CompilesOnceAndBuildsOneProjection()
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

            Assert.Equal(1, authoring.GraphCompilerInvocationCount);
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
    public void UndoAndRedo_ReusePreparedStatesAndBuildOneProjection()
    {
        var workspace = new EditorWorkspace();
        TrackEditorDocument document = EditorTestDocumentFactory.ActivateShowcase(workspace);
        TrackAuthoringCompilation beforeCompilation = document.Compilation!;
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
        TrackAuthoringCompilation afterCompilation = document.Compilation!;

        AssertHistoryMeasurement(
            workspace.UndoLast,
            expectedCompilerInvocations: 0);
        Assert.Same(beforeCompilation, document.Compilation);
        AssertHistoryMeasurement(
            workspace.RedoLast,
            expectedCompilerInvocations: 0);
        Assert.Same(afterCompilation, document.Compilation);
    }

    [Fact]
    public void ReplaceGraph_CompilesOnceAndBuildsOneProjection()
    {
        var workspace = new EditorWorkspace();
        TrackEditorDocument document = EditorTestDocumentFactory.ActivateShowcase(workspace);
        ConstantCurvatureSectionDefinition section = Assert.IsType<ConstantCurvatureSectionDefinition>(
            document.Graph!.Nodes.Single(node => node.Id == "sweeper").Section);
        TrackAuthoringGraph replacement = TrackAuthoringGraphOperations.Replace(
            document.Graph,
            section.Id,
            new ConstantCurvatureSectionDefinition(
                section.Id,
                section.Length,
                radius: 30.0,
                section.RollRadians));

        using TrackAuthoringPipelineMeasurement authoring =
            TrackAuthoringPipelineMeasurement.Begin();
        using EditorViewportPipelineMeasurement viewport =
            EditorViewportPipelineMeasurement.Begin();

        document.ReplaceGraph(replacement);

        Assert.Equal(1, authoring.GraphCompilerInvocationCount);
        Assert.Equal(1, authoring.EngineeringSnapshotBuildCount);
        Assert.Equal(1, viewport.ViewportProjectionBuildCount);
        Assert.Same(replacement, document.GraphCompileResult!.SourceGraph);
        AssertPositiveTimings(authoring, viewport);
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
        Assert.Equal(TimeSpan.Zero, authoring.GraphCompilerElapsed);
        Assert.True(authoring.EngineeringSnapshotBuildElapsed > TimeSpan.Zero);
        Assert.True(viewport.ViewportProjectionBuildElapsed > TimeSpan.Zero);
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
