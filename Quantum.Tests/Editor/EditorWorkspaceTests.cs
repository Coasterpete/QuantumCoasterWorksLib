using Quantum.Editor.Avalonia.Models;
using Quantum.Editor.Avalonia.Services;
using Quantum.Editor.Avalonia.Services.Commands;
using Quantum.Editor.Avalonia.Services.Documents;
using Quantum.Editor.Avalonia.Services.Plots;
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
        EngineeringSnapshot snapshot = Assert.IsType<EngineeringSnapshot>(workspace.EngineeringSnapshot);
        Assert.Equal(snapshot.SampleCount, workspace.ViewportSnapshot.Samples.Count);
        Assert.Equal(snapshot.Geometry[20].Position, workspace.ViewportSnapshot.Samples[20].Position);
        Assert.Equal(snapshot.Geometry[20].Tangent, workspace.ViewportSnapshot.Samples[20].Tangent);
        Assert.Equal(snapshot.BankingRollRadians[20] * 180.0 / System.Math.PI,
            workspace.ViewportSnapshot.Samples[20].RollDegrees,
            9);
        Assert.NotEmpty(workspace.ViewportSnapshot.Diagnostics);
        Assert.Contains(
            workspace.ViewportSnapshot.Diagnostics,
            diagnostic => diagnostic.StartsWith("Math Plot snapshot ", StringComparison.Ordinal));
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
    public void SetStationCursor_UsesCanonicalEngineeringStationAndNotifiesOnce()
    {
        var workspace = new EditorWorkspace();
        workspace.NewDocument();
        EngineeringSnapshot snapshot = workspace.EngineeringSnapshot!;
        int notifications = 0;
        workspace.StationCursorChanged += (_, _) => notifications++;

        workspace.SetStationCursor(25);
        workspace.SetStationCursor(25);

        EngineeringStationCursor cursor = Assert.IsType<EngineeringStationCursor>(
            workspace.StationCursor);
        Assert.Equal(25, cursor.SampleIndex);
        Assert.Equal(snapshot.StationGrid[25], cursor.Station, 12);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void SelectStationAt_UsesNearestCanonicalSampleAndSynchronizesEditorSelection()
    {
        var workspace = new EditorWorkspace();
        TrackEditorDocument document = workspace.NewDocument();
        EngineeringSnapshot snapshot = workspace.EngineeringSnapshot!;
        TrackViewportSnapshot viewport = workspace.ViewportSnapshot;
        TrackAuthoringGraph graph = document.Graph!;
        const int lowerIndex = 36;
        int expectedIndex = lowerIndex + 1;
        double requestedStation = snapshot.StationGrid[lowerIndex] +
            ((snapshot.StationGrid[expectedIndex] - snapshot.StationGrid[lowerIndex]) * 0.75);

        workspace.SelectStationAt(requestedStation);

        EngineeringStationCursor cursor = Assert.IsType<EngineeringStationCursor>(workspace.StationCursor);
        TrackViewportSample expectedSample = viewport.Samples[expectedIndex];
        Assert.Equal(expectedIndex, cursor.SampleIndex);
        Assert.Equal(snapshot.StationGrid[expectedIndex], cursor.Station, 12);
        Assert.Equal(EditorSelectionKind.Sample, workspace.CurrentSelection!.Kind);
        Assert.Equal(expectedIndex, workspace.CurrentSelection.SampleIndex);
        Assert.Equal(expectedSample.SectionIndex, workspace.CurrentSelection.SectionIndex);
        Assert.Equal(
            workspace.GraphNodes.Single(node => node.RouteIndex == expectedSample.SectionIndex).NodeId,
            workspace.CurrentSelection.NodeId);
        Assert.Equal(expectedSample.SectionIndex, workspace.HighlightedSectionIndex);
        Assert.Same(graph, document.Graph);
        Assert.Same(snapshot, workspace.EngineeringSnapshot);
        Assert.Same(viewport, workspace.ViewportSnapshot);
        Assert.Contains("Math Plot sample", workspace.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void SectionHighlight_HoverOverridesSelectionAndRestoresItAcrossAllViews()
    {
        var workspace = new EditorWorkspace();
        workspace.NewDocument();
        EditorGraphNode selected = workspace.GraphNodes.Single(node => node.NodeId == "sweeper");
        EditorSelection selectedSection = selected.Selection;
        EngineeringSnapshot snapshot = workspace.EngineeringSnapshot!;
        TrackViewportSnapshot viewport = workspace.ViewportSnapshot;
        int notifications = 0;
        workspace.SectionHighlightChanged += (_, _) => notifications++;
        workspace.Select(selectedSection);

        workspace.SetHoveredSection(4);

        Assert.Equal(4, workspace.HighlightedSectionIndex);
        Assert.Same(selectedSection, workspace.CurrentSelection);
        Assert.Same(snapshot, workspace.EngineeringSnapshot);
        Assert.Same(viewport, workspace.ViewportSnapshot);

        workspace.SetHoveredSection(null);

        Assert.Equal(selected.RouteIndex, workspace.HighlightedSectionIndex);
        Assert.Same(selectedSection, workspace.CurrentSelection);
        Assert.Equal(2, notifications);
    }

    [Fact]
    public void SelectedMathPlotStation_ExposesCanonicalInspectorValuesFromSharedSnapshot()
    {
        var workspace = new EditorWorkspace();
        TrackEditorDocument document = workspace.NewDocument();
        const int sampleIndex = 64;

        workspace.SelectStationSample(sampleIndex);

        EngineeringSnapshot snapshot = workspace.EngineeringSnapshot!;
        TrackViewportSample viewportSample = workspace.ViewportSnapshot.Samples[sampleIndex];
        TrackAuthoringGraphNode section = document.GraphCompileResult!
            .OrderedNodes[workspace.CurrentSelection!.SectionIndex];
        Assert.Equal(sampleIndex, workspace.CurrentSelection.SampleIndex);
        Assert.Equal(snapshot.Geometry[sampleIndex].Position.Y,
            EngineeringPlotProjection.GetValue(snapshot, EngineeringPlotKind.Elevation, sampleIndex));
        Assert.Equal(snapshot.Geometry[sampleIndex].CurvatureMagnitude,
            EngineeringPlotProjection.GetValue(snapshot, EngineeringPlotKind.Curvature, sampleIndex));
        Assert.Equal(viewportSample.RollDegrees,
            EngineeringPlotProjection.GetValue(snapshot, EngineeringPlotKind.Roll, sampleIndex)!.Value,
            12);
        Assert.True(double.IsFinite(
            EngineeringPlotProjection.GetValue(snapshot, EngineeringPlotKind.Pitch, sampleIndex)!.Value));
        Assert.True(double.IsFinite(
            EngineeringPlotProjection.GetValue(snapshot, EngineeringPlotKind.Yaw, sampleIndex)!.Value));
        Assert.Equal(workspace.CurrentSelection.NodeId, section.Id);
        Assert.True(section.Section.Length > 0.0);
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
