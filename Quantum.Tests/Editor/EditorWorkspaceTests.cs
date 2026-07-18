using Quantum.Editor.Avalonia.Models;
using Quantum.Editor.Avalonia.Services;
using Quantum.Editor.Avalonia.Services.Commands;
using Quantum.Editor.Avalonia.Services.Documents;
using Quantum.IO.TrackLayout.V2;

namespace Quantum.Tests;

public sealed class EditorWorkspaceTests
{
    [Fact]
    public void NewDocument_CreatesCompiledVisibleWorkspace()
    {
        var workspace = new EditorWorkspace();

        TrackEditorDocument document = workspace.NewDocument();

        Assert.True(document.IsDirty);
        Assert.NotNull(document.Package);
        Assert.NotNull(document.Compilation);
        Assert.Equal(7, document.Package!.Sections.Length);
        Assert.Equal(195.0, document.Compilation!.TotalLength, 9);
        Assert.True(workspace.ViewportSnapshot.Samples.Count > 100);
        Assert.Equal(document.Compilation.TotalLength, workspace.ViewportSnapshot.TotalLength, 9);
        Assert.NotEmpty(workspace.ViewportSnapshot.Diagnostics);
        Assert.Equal(EditorSelection.Track, workspace.CurrentSelection);
        Assert.Single(workspace.OutlinerNodes);
    }

    [Fact]
    public void ApplyPackageEdit_CompilesAndSupportsUndoRedo()
    {
        var workspace = new EditorWorkspace();
        TrackEditorDocument document = workspace.NewDocument();
        double originalRadius = document.Package!.Sections[2].Radius!.Value;

        bool applied = workspace.ApplyPackageEdit(
            "Change sweeper radius",
            package => package.Sections[2].Radius = 72.0);

        Assert.True(applied);
        Assert.Equal(72.0, document.Package!.Sections[2].Radius);
        Assert.True(workspace.UndoRedo.CanUndo);
        Assert.True(workspace.Commands.Execute(EditorCommandIds.Undo));
        Assert.Equal(originalRadius, document.Package!.Sections[2].Radius);
        Assert.True(workspace.UndoRedo.CanRedo);
        Assert.True(workspace.Commands.Execute(EditorCommandIds.Redo));
        Assert.Equal(72.0, document.Package!.Sections[2].Radius);
    }

    [Fact]
    public void ApplyPackageEdit_InvalidGeometryIsRejectedWithoutChangingDocument()
    {
        var workspace = new EditorWorkspace();
        TrackEditorDocument document = workspace.NewDocument();
        string beforeJson = document.CapturePackageJson();

        bool applied = workspace.ApplyPackageEdit(
            "Break section length",
            package => package.Sections[0].Length = -1.0);

        Assert.False(applied);
        Assert.Equal(beforeJson, document.CapturePackageJson());
        Assert.False(workspace.UndoRedo.CanUndo);
        Assert.StartsWith("Edit rejected:", workspace.StatusMessage);
    }

    [Fact]
    public void SelectSample_SynchronizesWorkspaceSelection()
    {
        var workspace = new EditorWorkspace();
        workspace.NewDocument();
        TrackViewportSample sample = workspace.ViewportSnapshot.Samples[10];

        workspace.Select(EditorSelection.Sample(sample.SampleIndex, sample.SectionIndex));

        Assert.Equal(EditorSelectionKind.Sample, workspace.CurrentSelection!.Kind);
        Assert.Equal(sample.SampleIndex, workspace.CurrentSelection.SampleIndex);
        Assert.Equal(sample.SectionIndex, workspace.CurrentSelection.SectionIndex);
    }

    [Fact]
    public void ShowcasePackage_RoundTripsThroughV2JsonAndCompiler()
    {
        TrackLayoutPackageV2Dto package = TrackPackageFactory.CreateShowcasePackage();
        string json = TrackLayoutPackageV2Json.Serialize(package, indented: true);

        TrackEditorDocument document = TrackEditorDocument.Create(
            TrackLayoutPackageV2Json.Deserialize(json),
            "Round trip");

        Assert.NotNull(document.Compilation);
        Assert.Equal(195.0, document.Compilation!.TotalLength, 9);
        Assert.Equal("m156-showcase", document.Package!.Metadata.LayoutId);
        Assert.Equal(5, document.Package.Banking!.Keys.Length);
    }
}
