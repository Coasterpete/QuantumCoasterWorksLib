using Avalonia.Controls;
using Quantum.Editor.Avalonia.Controls;
using Quantum.Editor.Avalonia.Services;
using Quantum.Editor.Avalonia.Services.Viewport;

namespace Quantum.Tests;

public sealed class PaneExtractionContractTests
{
    public static TheoryData<Type> PaneTypes => new()
    {
        typeof(RoutePaneControl),
        typeof(ViewportPaneControl),
        typeof(InspectorPaneControl),
        typeof(MathPlotsPaneControl),
        typeof(DiagnosticsPaneControl),
        typeof(TrainConfigurationPaneControl),
        typeof(TrainPreviewPaneControl),
        typeof(TrainSummaryPaneControl)
    };

    [Theory]
    [MemberData(nameof(PaneTypes))]
    public void ExtractedPane_IsAReusableUserControl(Type paneType)
    {
        Assert.True(typeof(UserControl).IsAssignableFrom(paneType));
        Assert.True(paneType.IsPublic);
        Assert.NotNull(paneType.GetConstructor(Type.EmptyTypes));
    }

    [Fact]
    public void RoutePane_ExposesDocumentRouteSelectionAndInteractionContract()
    {
        AssertProperty<RoutePaneControl>(nameof(RoutePaneControl.DocumentTitle), canWrite: true);
        AssertProperty<RoutePaneControl>(nameof(RoutePaneControl.DocumentPath), canWrite: true);
        AssertProperty<RoutePaneControl>(nameof(RoutePaneControl.GraphNodes), canWrite: true);
        AssertProperty<RoutePaneControl>(nameof(RoutePaneControl.Selection), canWrite: true);
        AssertProperty<RoutePaneControl>(nameof(RoutePaneControl.HighlightedSectionIndex), canWrite: true);
        Assert.NotNull(typeof(RoutePaneControl).GetEvent(nameof(RoutePaneControl.NodeSelected)));
        Assert.NotNull(typeof(RoutePaneControl).GetEvent(nameof(RoutePaneControl.SectionPointerChanged)));
    }

    [Fact]
    public void ViewportPane_PreservesViewportSurfaceAndInteractionContract()
    {
        Assert.True(typeof(IViewportSurface).IsAssignableFrom(typeof(ViewportPaneControl)));
        AssertProperty<ViewportPaneControl>(nameof(ViewportPaneControl.Snapshot), canWrite: true);
        AssertProperty<ViewportPaneControl>(nameof(ViewportPaneControl.Selection), canWrite: true);
        AssertProperty<ViewportPaneControl>(nameof(ViewportPaneControl.StationCursorSampleIndex), canWrite: true);
        AssertProperty<ViewportPaneControl>(nameof(ViewportPaneControl.HighlightedSectionIndex), canWrite: true);
        AssertProperty<ViewportPaneControl>(nameof(ViewportPaneControl.Projection), canWrite: true);
        AssertProperty<ViewportPaneControl>(nameof(ViewportPaneControl.ShowFrames), canWrite: true);
        Assert.NotNull(typeof(ViewportPaneControl).GetMethod(nameof(ViewportPaneControl.FitToTrack)));
        Assert.NotNull(typeof(ViewportPaneControl).GetEvent(nameof(ViewportPaneControl.SampleSelected)));
        Assert.NotNull(typeof(ViewportPaneControl).GetEvent(nameof(ViewportPaneControl.SectionPointerChanged)));
    }

    [Fact]
    public void MathPlotsAndDiagnosticsPanes_ExposeExistingSnapshotContracts()
    {
        AssertProperty<MathPlotsPaneControl>(nameof(MathPlotsPaneControl.Snapshot), canWrite: true);
        AssertProperty<MathPlotsPaneControl>(nameof(MathPlotsPaneControl.CursorSampleIndex), canWrite: true);
        AssertProperty<MathPlotsPaneControl>(nameof(MathPlotsPaneControl.HighlightedSectionIndex), canWrite: true);
        AssertProperty<MathPlotsPaneControl>(nameof(MathPlotsPaneControl.StationReadout), canWrite: true);
        Assert.NotNull(typeof(MathPlotsPaneControl).GetEvent(nameof(MathPlotsPaneControl.StationChanged)));
        Assert.NotNull(typeof(MathPlotsPaneControl).GetEvent(nameof(MathPlotsPaneControl.StationSelected)));
        Assert.NotNull(typeof(MathPlotsPaneControl).GetEvent(nameof(MathPlotsPaneControl.SectionPointerChanged)));
        AssertProperty<DiagnosticsPaneControl>(nameof(DiagnosticsPaneControl.Snapshot), canWrite: true);
    }

    [Fact]
    public void InspectorPane_RefreshesFromTheExistingEditorWorkspace()
    {
        Assert.NotNull(typeof(InspectorPaneControl).GetMethod(
            nameof(InspectorPaneControl.Refresh),
            new[] { typeof(EditorWorkspace) }));
    }

    private static void AssertProperty<T>(string name, bool canWrite)
    {
        System.Reflection.PropertyInfo? property = typeof(T).GetProperty(name);
        Assert.NotNull(property);
        Assert.True(property!.CanRead);
        Assert.Equal(canWrite, property.CanWrite);
    }
}
