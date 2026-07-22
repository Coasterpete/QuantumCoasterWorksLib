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
    public void NewDocument_CreatesEmptyAuthoringGraphWithoutCompiledOrPersistablePreview()
    {
        var workspace = new EditorWorkspace();

        TrackEditorDocument document = workspace.NewDocument();

        Assert.True(document.IsDirty);
        Assert.NotNull(document.Graph);
        Assert.True(document.IsEmpty);
        Assert.False(document.CanSave);
        Assert.Null(document.GraphCompileResult);
        Assert.Null(document.Compilation);
        Assert.Null(document.Package);
        Assert.Empty(document.Graph!.Nodes);
        Assert.Empty(document.Graph.Edges);
        Assert.Empty(workspace.GraphNodes);
        Assert.Empty(workspace.ViewportSnapshot.Samples);
        Assert.Null(workspace.EngineeringSnapshot);
        Assert.Equal(EditorSelection.Track, workspace.CurrentSelection);
        Assert.Empty(workspace.OutlinerNodes);
    }

    [Fact]
    public void AddFirstSection_CompilesSelectsAndSupportsUndoRedoBackToEmpty()
    {
        var workspace = new EditorWorkspace();
        TrackEditorDocument document = workspace.NewDocument();

        bool added = workspace.AddSection(
            new StraightSectionDefinition("straight-1", 10.0));

        Assert.True(added);
        Assert.False(document.IsEmpty);
        Assert.True(document.CanSave);
        Assert.NotNull(document.Compilation);
        Assert.Equal(10.0, document.Compilation!.TotalLength, 9);
        Assert.Equal("straight-1", Assert.Single(workspace.GraphNodes).NodeId);
        Assert.Equal("straight-1", workspace.CurrentSelection!.NodeId);
        Assert.True(workspace.UndoRedo.CanUndo);

        Assert.True(workspace.UndoLast());
        Assert.True(document.IsEmpty);
        Assert.False(document.CanSave);
        Assert.Null(document.Compilation);
        Assert.Empty(workspace.GraphNodes);
        Assert.Empty(workspace.ViewportSnapshot.Samples);

        Assert.True(workspace.RedoLast());
        Assert.True(document.CanSave);
        Assert.Equal("straight-1", Assert.Single(workspace.GraphNodes).NodeId);
        Assert.Equal(10.0, document.Compilation!.TotalLength, 9);
    }

    [Fact]
    public void StructuralSectionWorkflow_InsertsMovesDeletesAndKeepsSelectionStable()
    {
        var workspace = new EditorWorkspace();
        TrackEditorDocument document = workspace.NewDocument();
        Assert.True(workspace.AddSection(
            new StraightSectionDefinition("a", 5.0)));
        Assert.True(workspace.InsertSectionAfter(
            "a",
            new ConstantCurvatureSectionDefinition("c", 5.0, 20.0)));
        Assert.True(workspace.InsertSectionBefore(
            "c",
            new CurvatureTransitionSectionDefinition("b", 5.0, 0.0, 0.05)));

        Assert.Equal(
            new[] { "a", "b", "c" },
            workspace.GraphNodes.Select(node => node.NodeId));
        Assert.Equal("b", workspace.CurrentSelection!.NodeId);
        Assert.Equal(15.0, document.Compilation!.TotalLength, 9);

        Assert.True(workspace.MoveSectionDown("a"));
        Assert.Equal(
            new[] { "b", "a", "c" },
            workspace.GraphNodes.Select(node => node.NodeId));
        Assert.Equal("a", workspace.CurrentSelection!.NodeId);

        Assert.True(workspace.DeleteSection("a"));
        Assert.Equal(
            new[] { "b", "c" },
            workspace.GraphNodes.Select(node => node.NodeId));
        Assert.Equal("c", workspace.CurrentSelection!.NodeId);
        Assert.Equal(10.0, document.Compilation!.TotalLength, 9);

        Assert.True(workspace.UndoLast());
        Assert.Equal(
            new[] { "b", "a", "c" },
            workspace.GraphNodes.Select(node => node.NodeId));
    }

    [Fact]
    public void AddSection_InvalidOrDuplicateDefinitionPreservesCompiledTrackAndHistory()
    {
        var workspace = new EditorWorkspace();
        TrackEditorDocument document = workspace.NewDocument();
        Assert.True(workspace.AddSection(
            new StraightSectionDefinition("section", 5.0)));
        workspace.UndoRedo.Clear();
        TrackAuthoringGraph beforeGraph = document.Graph!;
        TrackAuthoringCompilation beforeCompilation = document.Compilation!;
        TrackViewportSnapshot beforeViewport = workspace.ViewportSnapshot;

        bool duplicate = workspace.AddSection(
            new StraightSectionDefinition("section", 2.0));

        Assert.False(duplicate);
        Assert.Same(beforeGraph, document.Graph);
        Assert.Same(beforeCompilation, document.Compilation);
        Assert.Same(beforeViewport, workspace.ViewportSnapshot);
        Assert.False(workspace.UndoRedo.CanUndo);
        Assert.StartsWith("Edit rejected:", workspace.StatusMessage);
    }

    [Fact]
    public void LengthChangingStructuralEdit_WithExplicitBankingIsRejectedWithoutRemapping()
    {
        var workspace = new EditorWorkspace();
        TrackEditorDocument document =
            EditorTestDocumentFactory.ActivateShowcase(workspace);
        document.MarkClean();
        TrackAuthoringGraph beforeGraph = document.Graph!;
        TrackAuthoringCompilation beforeCompilation = document.Compilation!;

        bool inserted = workspace.InsertSectionAfter(
            "launch",
            new StraightSectionDefinition("inserted", 5.0));

        Assert.False(inserted);
        Assert.Same(beforeGraph, document.Graph);
        Assert.Same(beforeCompilation, document.Compilation);
        Assert.False(document.IsDirty);
        Assert.False(workspace.UndoRedo.CanUndo);
        Assert.Contains("AuthoringCompilationFailed", workspace.StatusMessage);
        Assert.Equal(5, document.Graph!.Banking!.Keys.Count);
    }

    [Fact]
    public void ApplyGraphEdit_CompilesRefreshesViewportAndSupportsAtomicUndoRedo()
    {
        var workspace = new EditorWorkspace();
        TrackEditorDocument document = EditorTestDocumentFactory.ActivateShowcase(workspace);
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
        TrackEditorDocument document = EditorTestDocumentFactory.ActivateShowcase(workspace);
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
        TrackEditorDocument document = EditorTestDocumentFactory.ActivateShowcase(workspace);
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
        TrackEditorDocument document = EditorTestDocumentFactory.ActivateShowcase(workspace);
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
        EditorTestDocumentFactory.ActivateShowcase(workspace);
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
        TrackEditorDocument document = EditorTestDocumentFactory.ActivateShowcase(workspace);
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
        EditorTestDocumentFactory.ActivateShowcase(workspace);
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
        TrackEditorDocument document = EditorTestDocumentFactory.ActivateShowcase(workspace);
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
        Assert.True(Assert.IsAssignableFrom<GeometricSectionDefinition>(section.Section).Length > 0.0);
    }

    [Fact]
    public void ApplyPackageEdit_AncillaryMutationIsRejectedWithoutHistory()
    {
        var workspace = new EditorWorkspace();
        TrackEditorDocument document = EditorTestDocumentFactory.ActivateShowcase(workspace);
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
        EditorTestDocumentFactory.ActivateShowcase(workspace);
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
