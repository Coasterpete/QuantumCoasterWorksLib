using Quantum.Editor.Avalonia.Services;
using Quantum.Editor.Avalonia.Services.Documents;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class TrackDocumentFileServiceTests
{
    [Fact]
    public void EmptyToAuthoredWorkflow_SaveOpenPreservesCreatedTypesOrderAndParameters()
    {
        string tempDirectory = CreateTempDirectory();
        string path = Path.Combine(tempDirectory, "m166-authored.qcwtrack.json");
        var workspace = new EditorWorkspace();

        try
        {
            TrackEditorDocument document = workspace.NewDocument();
            Assert.False(document.CanSave);
            Assert.True(workspace.AddSection(
                new StraightSectionDefinition("entry", 8.0, 0.1)));
            Assert.True(workspace.InsertSectionAfter(
                "entry",
                new CurvatureTransitionSectionDefinition(
                    "transition",
                    6.0,
                    0.0,
                    0.04,
                    rollRadians: 0.2)));
            Assert.True(workspace.InsertSectionAfter(
                "transition",
                new ConstantCurvatureSectionDefinition(
                    "curve",
                    10.0,
                    -25.0,
                    0.3)));

            workspace.SaveDocument(path);
            TrackEditorDocument reopened = workspace.OpenDocument(path);

            Assert.False(reopened.IsDirty);
            Assert.True(reopened.CanSave);
            Assert.Equal(
                new[] { "entry", "transition", "curve" },
                workspace.GraphNodes.Select(node => node.NodeId));
            Assert.IsType<StraightSectionDefinition>(
                reopened.GraphCompileResult!.OrderedNodes[0].Section);
            CurvatureTransitionSectionDefinition transition =
                Assert.IsType<CurvatureTransitionSectionDefinition>(
                    reopened.GraphCompileResult.OrderedNodes[1].Section);
            ConstantCurvatureSectionDefinition curve =
                Assert.IsType<ConstantCurvatureSectionDefinition>(
                    reopened.GraphCompileResult.OrderedNodes[2].Section);
            Assert.Equal(0.04, transition.EndCurvature, 12);
            Assert.Equal(-25.0, curve.Radius, 12);
            Assert.Equal(24.0, reopened.Compilation!.TotalLength, 12);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveAndOpen_PreservesEditedGraphPackageCompilationAndCleanState()
    {
        string tempDirectory = CreateTempDirectory();
        string path = Path.Combine(tempDirectory, "showcase.qcwtrack.json");
        var fileService = new TrackDocumentFileService();

        try
        {
            TrackEditorDocument source = TrackEditorDocument.Create(
                TrackPackageFactory.CreateShowcasePackage(),
                "Untitled");
            source.ReplaceGraph(WithSweeperRadius(source.Graph!, 30.0));
            Assert.True(source.IsDirty);

            fileService.Save(source, path);
            TrackEditorDocument reopened = fileService.Open(path);

            Assert.True(File.Exists(path));
            Assert.False(source.IsDirty);
            Assert.False(reopened.IsDirty);
            Assert.Equal(Path.GetFullPath(path), reopened.FilePath);
            Assert.Equal("showcase.qcwtrack", reopened.DisplayName);
            Assert.Equal(source.CapturePackageJson(), reopened.CapturePackageJson());
            Assert.Equal(30.0, SweeperRadius(reopened.Graph!));
            Assert.Equal(30.0, reopened.Package!.Sections[2].Radius);
            Assert.Equal("m156-showcase", reopened.AncillaryState!.LayoutId);
            Assert.Equal(5, reopened.Graph!.Banking!.Keys.Count);
            Assert.Equal(195.0, reopened.Compilation!.TotalLength, 9);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void SavepointDirtyStateTracksUndoAndRedoByPersistedGraphContent()
    {
        string tempDirectory = CreateTempDirectory();
        string path = Path.Combine(tempDirectory, "savepoint.qcwtrack.json");
        var workspace = new EditorWorkspace();

        try
        {
            TrackEditorDocument document =
                EditorTestDocumentFactory.ActivateShowcase(workspace, markDirty: true);
            workspace.SaveDocument(path);
            Assert.False(document.IsDirty);

            Assert.True(workspace.ApplyGraphEdit(
                "Edit sweeper radius",
                graph => WithSweeperRadius(graph, 30.0)));
            Assert.True(document.IsDirty);

            Assert.True(workspace.UndoLast());
            Assert.False(document.IsDirty);

            Assert.True(workspace.RedoLast());
            Assert.True(document.IsDirty);

            workspace.SaveDocument();
            Assert.False(document.IsDirty);

            Assert.True(workspace.UndoLast());
            Assert.True(document.IsDirty);

            Assert.True(workspace.RedoLast());
            Assert.False(document.IsDirty);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void Open_InvalidPackage_ReportsValidationDiagnostics()
    {
        string tempDirectory = CreateTempDirectory();
        string path = Path.Combine(tempDirectory, "invalid.json");

        try
        {
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllText(
                path,
                "{\"contract\":\"quantum.track_layout_package\",\"version\":2,\"metadata\":{},\"startPose\":{},\"sections\":[]}");

            TrackEditorDocumentException exception = Assert.Throws<TrackEditorDocumentException>(
                () => new TrackDocumentFileService().Open(path));

            Assert.NotEmpty(exception.Diagnostics);
            Assert.Contains("section", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void ReplacePackageJson_ExplicitDirtyFlagPreservesCompatibilityBehavior()
    {
        TrackEditorDocument document = TrackEditorDocument.Create(
            TrackPackageFactory.CreateShowcasePackage(),
            "Compatibility dirty flag");
        string unchangedJson = document.CapturePackageJson();

        document.ReplacePackageJson(unchangedJson, markDirty: true);

        Assert.True(document.IsDirty);
        Assert.Equal(unchangedJson, document.CapturePackageJson());
    }

    private static TrackAuthoringGraph WithSweeperRadius(
        TrackAuthoringGraph graph,
        double radius)
    {
        TrackAuthoringGraphNode node = graph.Nodes.Single(candidate => candidate.Id == "sweeper");
        ConstantCurvatureSectionDefinition arc =
            Assert.IsType<ConstantCurvatureSectionDefinition>(node.Section);
        return graph.WithSection(
            "sweeper",
            new ConstantCurvatureSectionDefinition(
                arc.Id,
                arc.Length,
                radius,
                arc.RollRadians));
    }

    private static double SweeperRadius(TrackAuthoringGraph graph)
    {
        return Assert.IsType<ConstantCurvatureSectionDefinition>(
            graph.Nodes.Single(node => node.Id == "sweeper").Section).Radius;
    }

    private static string CreateTempDirectory()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "QuantumCoasterWorks.EditorTests",
            Guid.NewGuid().ToString("N"));
    }
}
