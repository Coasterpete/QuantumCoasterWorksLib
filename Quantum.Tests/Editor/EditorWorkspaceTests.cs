using Quantum.Editor.Avalonia.Models;
using Quantum.Editor.Avalonia.Services;
using Quantum.Editor.Avalonia.Services.Commands;
using Quantum.Editor.Avalonia.Services.Documents;
using Quantum.IO.TrackLayout.V2;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class EditorWorkspaceTests
{
    [Fact]
    public void NewDocument_CreatesAuthoritativeConnectedGraphAndVisibleCompilation()
    {
        var workspace = new EditorWorkspace();

        TrackEditorDocument document = workspace.NewDocument();

        Assert.True(document.IsDirty);
        Assert.NotNull(document.Graph);
        Assert.NotNull(document.GraphCompileResult);
        Assert.NotNull(document.Compilation);
        Assert.Equal(
            new[]
            {
                "launch",
                "curve-in",
                "sweeper",
                "reverse-transition",
                "return-curve",
                "curve-out",
                "brake-run"
            },
            workspace.GraphNodes.Select(node => node.NodeId));
        Assert.Equal(7, document.Graph!.Nodes.Count);
        Assert.Equal(6, document.Graph.Edges.Count);
        Assert.Equal(195.0, document.Compilation!.TotalLength, 9);
        Assert.True(workspace.ViewportSnapshot.Samples.Count > 100);
        Assert.Equal(document.Compilation.TotalLength, workspace.ViewportSnapshot.TotalLength, 9);
        Assert.NotEmpty(workspace.ViewportSnapshot.Diagnostics);
        Assert.Equal(EditorSelection.Track, workspace.CurrentSelection);
        Assert.Single(workspace.OutlinerNodes);
    }

    [Fact]
    public void ApplyGraphEdit_CompilesRefreshesViewportAndSupportsAtomicUndoRedo()
    {
        var workspace = new EditorWorkspace();
        TrackEditorDocument document = workspace.NewDocument();
        document.MarkClean();
        TrackAuthoringGraph beforeGraph = document.Graph!;
        var beforeCompilation = document.Compilation;
        TrackViewportSnapshot beforeViewport = workspace.ViewportSnapshot;
        Quantum.Math.Vector3d beforeEnd = beforeViewport.Samples[^1].Position;
        workspace.Select(EditorSelection.GraphNode("sweeper", 2));

        bool applied = workspace.ApplyGraphEdit(
            "Edit sweeper radius",
            graph => WithRadius(graph, "sweeper", 30.0));

        Assert.True(applied);
        Assert.NotSame(beforeGraph, document.Graph);
        Assert.NotSame(beforeCompilation, document.Compilation);
        Assert.NotSame(beforeViewport, workspace.ViewportSnapshot);
        Assert.Equal(30.0, Radius(document.Graph!, "sweeper"));
        Assert.Equal(30.0, document.Package!.Sections[2].Radius);
        Assert.Equal(195.0, document.Compilation!.TotalLength, 9);
        Assert.True(workspace.ViewportSnapshot.MaximumAbsoluteCurvature > 0.03);
        Assert.True((workspace.ViewportSnapshot.Samples[^1].Position - beforeEnd).Length > 1.0);
        Assert.True(document.IsDirty);
        Assert.Equal("sweeper", workspace.CurrentSelection!.NodeId);
        Assert.True(workspace.UndoRedo.CanUndo);

        Assert.True(workspace.Commands.Execute(EditorCommandIds.Undo));
        Assert.Same(beforeGraph, document.Graph);
        Assert.Equal(50.0, Radius(document.Graph!, "sweeper"));
        Assert.False(document.IsDirty);
        Assert.Equal("sweeper", workspace.CurrentSelection!.NodeId);
        Assert.True(workspace.UndoRedo.CanRedo);

        Assert.True(workspace.Commands.Execute(EditorCommandIds.Redo));
        Assert.Equal(30.0, Radius(document.Graph!, "sweeper"));
        Assert.True(document.IsDirty);
        Assert.Equal("sweeper", workspace.CurrentSelection!.NodeId);
    }

    [Fact]
    public void ApplyGraphEdit_InvalidTopologyCannotChangeDocumentDirtyStateViewportOrHistory()
    {
        var workspace = new EditorWorkspace();
        TrackEditorDocument document = workspace.NewDocument();
        document.MarkClean();
        TrackAuthoringGraph beforeGraph = document.Graph!;
        var beforeCompilation = document.Compilation;
        TrackViewportSnapshot beforeViewport = workspace.ViewportSnapshot;
        string beforeJson = document.CapturePackageJson();

        bool applied = workspace.ApplyGraphEdit(
            "Create branch",
            graph => new TrackAuthoringGraph(
                graph.Nodes,
                graph.Edges.Concat(new[]
                {
                    new TrackAuthoringGraphEdge("launch", "sweeper")
                }),
                graph.StartPose,
                graph.Banking));

        Assert.False(applied);
        Assert.Same(beforeGraph, document.Graph);
        Assert.Same(beforeCompilation, document.Compilation);
        Assert.Same(beforeViewport, workspace.ViewportSnapshot);
        Assert.Equal(beforeJson, document.CapturePackageJson());
        Assert.False(document.IsDirty);
        Assert.False(workspace.UndoRedo.CanUndo);
        Assert.Contains("MultipleOutgoingEdges", workspace.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyGraphEdit_InvalidSectionConstructionCannotEnterHistory()
    {
        var workspace = new EditorWorkspace();
        TrackEditorDocument document = workspace.NewDocument();
        document.MarkClean();
        TrackAuthoringGraph beforeGraph = document.Graph!;

        bool applied = workspace.ApplyGraphEdit(
            "Set zero radius",
            graph => WithRadius(graph, "sweeper", 0.0));

        Assert.False(applied);
        Assert.Same(beforeGraph, document.Graph);
        Assert.Equal(50.0, Radius(document.Graph!, "sweeper"));
        Assert.False(document.IsDirty);
        Assert.False(workspace.UndoRedo.CanUndo);
        Assert.StartsWith("Edit rejected:", workspace.StatusMessage);
    }

    [Fact]
    public void ApplyPackageEdit_CompatibilityBridgeCommitsOnlyGraphSnapshots()
    {
        var workspace = new EditorWorkspace();
        TrackEditorDocument document = workspace.NewDocument();
        document.MarkClean();
        TrackAuthoringGraph beforeGraph = document.Graph!;

        bool applied = workspace.ApplyPackageEdit(
            "Compatibility radius edit",
            package => package.Sections[2].Radius = 72.0);

        Assert.True(applied);
        Assert.Equal(72.0, Radius(document.Graph!, "sweeper"));
        Assert.True(workspace.UndoRedo.CanUndo);
        Assert.True(workspace.Commands.Execute(EditorCommandIds.Undo));
        Assert.Same(beforeGraph, document.Graph);
        Assert.Equal(50.0, Radius(document.Graph!, "sweeper"));
        Assert.False(document.IsDirty);
    }

    [Fact]
    public void ApplyPackageEdit_AncillaryMutationIsRejectedWithoutHistory()
    {
        var workspace = new EditorWorkspace();
        TrackEditorDocument document = workspace.NewDocument();
        document.MarkClean();
        TrackAuthoringGraph beforeGraph = document.Graph!;

        bool applied = workspace.ApplyPackageEdit(
            "Change metadata",
            package => package.Metadata.SourceName = "UI-owned metadata");

        Assert.False(applied);
        Assert.Same(beforeGraph, document.Graph);
        Assert.Equal("M156 Showcase Layout", document.AncillaryState!.SourceName);
        Assert.False(document.IsDirty);
        Assert.False(workspace.UndoRedo.CanUndo);
        Assert.Contains("cannot change metadata", workspace.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GraphNodeSelectionUsesStableNodeIdAndRouteIndex()
    {
        var workspace = new EditorWorkspace();
        workspace.NewDocument();
        EditorGraphNode sweeper = workspace.GraphNodes.Single(node => node.NodeId == "sweeper");

        workspace.Select(sweeper.Selection);

        Assert.Equal(EditorSelectionKind.Section, workspace.CurrentSelection!.Kind);
        Assert.Equal("sweeper", workspace.CurrentSelection.NodeId);
        Assert.Equal(2, workspace.CurrentSelection.SectionIndex);
    }

    [Fact]
    public void PackageCompatibilitySnapshotIsDetachedFromAuthoritativeGraph()
    {
        TrackEditorDocument document = TrackEditorDocument.Create(
            TrackPackageFactory.CreateShowcasePackage(),
            "Detached package");
        TrackLayoutPackageV2Dto firstSnapshot = document.Package!;

        firstSnapshot.Sections[2].Radius = 999.0;

        Assert.Equal(50.0, Radius(document.Graph!, "sweeper"));
        Assert.Equal(50.0, document.Package!.Sections[2].Radius);
    }

    [Fact]
    public void ShowcasePackage_RoundTripsThroughGraphAdapterAndCompiler()
    {
        TrackLayoutPackageV2Dto package = TrackPackageFactory.CreateShowcasePackage();
        string json = TrackLayoutPackageV2Json.Serialize(package, indented: true);

        TrackEditorDocument document = TrackEditorDocument.Create(
            TrackLayoutPackageV2Json.Deserialize(json),
            "Round trip");

        Assert.NotNull(document.Graph);
        Assert.NotNull(document.GraphCompileResult);
        Assert.NotNull(document.Compilation);
        Assert.Equal(195.0, document.Compilation!.TotalLength, 9);
        Assert.Equal("m156-showcase", document.AncillaryState!.LayoutId);
        Assert.Equal(5, document.Graph!.Banking!.Keys.Count);
        Assert.Equal(json, document.CapturePackageJson());
    }

    private static TrackAuthoringGraph WithRadius(
        TrackAuthoringGraph graph,
        string nodeId,
        double radius)
    {
        TrackAuthoringGraphNode node = graph.Nodes.Single(candidate => candidate.Id == nodeId);
        ConstantCurvatureSectionDefinition arc =
            Assert.IsType<ConstantCurvatureSectionDefinition>(node.Section);
        return graph.WithSection(
            nodeId,
            new ConstantCurvatureSectionDefinition(
                arc.Id,
                arc.Length,
                radius,
                arc.RollRadians));
    }

    private static double Radius(TrackAuthoringGraph graph, string nodeId)
    {
        TrackAuthoringGraphNode node = graph.Nodes.Single(candidate => candidate.Id == nodeId);
        return Assert.IsType<ConstantCurvatureSectionDefinition>(node.Section).Radius;
    }
}
