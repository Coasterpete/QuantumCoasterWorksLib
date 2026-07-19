using Dock.Model.Core;
using Quantum.Editor.Avalonia.Services.Docking;
using Quantum.Editor.Avalonia.Services.Workspaces;

namespace Quantum.Tests;

public sealed class DockingInfrastructureTests
{
    [Fact]
    public void Registry_RegistersTrackPanesAndKeepsViewportNonCloseable()
    {
        DockPaneRegistry registry = DockPaneRegistry.CreateDefaultTrack();

        Assert.Collection(
            registry.Panes,
            pane => AssertPane(pane, WorkspacePaneIds.Route, "Route", canClose: true),
            pane => AssertPane(pane, WorkspacePaneIds.Viewport, "Viewport", canClose: false),
            pane => AssertPane(pane, WorkspacePaneIds.Inspector, "Inspector", canClose: true),
            pane => AssertPane(pane, WorkspacePaneIds.MathPlots, "Math Plots", canClose: true),
            pane => AssertPane(pane, WorkspacePaneIds.Diagnostics, "Diagnostics", canClose: true));
        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(new DockPaneRegistration(WorkspacePaneIds.Route, "Duplicate")));
    }

    [Fact]
    public void Adapter_InitializesDockFactoryWithRegisteredFrontendContexts()
    {
        DockPaneRegistry registry = DockPaneRegistry.CreateDefaultTrack();
        IReadOnlyDictionary<string, object> contexts = CreateContexts(registry);

        var adapter = new EditorDockingAdapter(registry, contexts);

        Assert.True(adapter.IsInitialized);
        Assert.Equal(DockingLayoutIds.Root, adapter.Layout.Id);
        Assert.Same(adapter.Factory, adapter.Layout.Factory);
        foreach (DockPaneRegistration registration in registry.Panes)
        {
            IDockable pane = adapter.GetPane(registration.Id);
            Assert.Same(contexts[registration.Id], pane.Context);
            Assert.Same(adapter.Factory, pane.Factory);
            Assert.True(adapter.IsPaneOpen(registration.Id));
        }
    }

    [Fact]
    public void DefaultTrackLayout_PreservesFivePaneWorkbenchComposition()
    {
        EditorDockingAdapter adapter = CreateAdapter();

        Assert.Equal(
            DockingLayoutIds.RouteHost,
            adapter.GetPane(WorkspacePaneIds.Route).Owner?.Id);
        Assert.Equal(
            DockingLayoutIds.ViewportHost,
            adapter.GetPane(WorkspacePaneIds.Viewport).Owner?.Id);
        Assert.Equal(
            DockingLayoutIds.InspectorHost,
            adapter.GetPane(WorkspacePaneIds.Inspector).Owner?.Id);
        Assert.Equal(
            DockingLayoutIds.BottomHost,
            adapter.GetPane(WorkspacePaneIds.MathPlots).Owner?.Id);
        Assert.Equal(
            DockingLayoutIds.BottomHost,
            adapter.GetPane(WorkspacePaneIds.Diagnostics).Owner?.Id);

        IDock bottomHost = Assert.IsAssignableFrom<IDock>(
            adapter.GetPane(WorkspacePaneIds.MathPlots).Owner);
        Assert.Equal(2, bottomHost.VisibleDockables?.Count);
        Assert.Same(adapter.GetPane(WorkspacePaneIds.MathPlots), bottomHost.ActiveDockable);
        Assert.False(adapter.GetPane(WorkspacePaneIds.Viewport).CanClose);
        Assert.All(
            new[]
            {
                WorkspacePaneIds.Route,
                WorkspacePaneIds.Inspector,
                WorkspacePaneIds.MathPlots,
                WorkspacePaneIds.Diagnostics
            },
            paneId => Assert.True(adapter.GetPane(paneId).CanClose));
    }

    [Fact]
    public void ClosedToolPane_CanBeReopenedInItsPreviousDock()
    {
        EditorDockingAdapter adapter = CreateAdapter();
        IDockable diagnostics = adapter.GetPane(WorkspacePaneIds.Diagnostics);

        Assert.True(adapter.TryClosePane(WorkspacePaneIds.Diagnostics));
        Assert.False(adapter.IsPaneOpen(WorkspacePaneIds.Diagnostics));

        adapter.ShowPane(WorkspacePaneIds.Diagnostics);

        Assert.True(adapter.IsPaneOpen(WorkspacePaneIds.Diagnostics));
        Assert.Equal(DockingLayoutIds.BottomHost, diagnostics.Owner?.Id);
        IDock bottomHost = Assert.IsAssignableFrom<IDock>(diagnostics.Owner);
        Assert.Same(diagnostics, bottomHost.ActiveDockable);
    }

    [Fact]
    public void ApplyingDefaultVisibleState_KeepsEveryPaneOpenAndMathPlotsActive()
    {
        EditorDockingAdapter adapter = CreateAdapter();

        foreach (DockPaneRegistration pane in adapter.Registry.Panes)
        {
            adapter.SetPaneVisible(pane.Id, isVisible: true);
        }

        Assert.All(adapter.Registry.Panes, pane => Assert.True(adapter.IsPaneOpen(pane.Id)));
        IDock bottomHost = Assert.IsAssignableFrom<IDock>(
            adapter.GetPane(WorkspacePaneIds.MathPlots).Owner);
        Assert.Same(adapter.GetPane(WorkspacePaneIds.MathPlots), bottomHost.ActiveDockable);
    }

    [Fact]
    public void PrimaryViewport_CannotBeClosedByPaneLifecycleCommand()
    {
        EditorDockingAdapter adapter = CreateAdapter();

        Assert.False(adapter.TryClosePane(WorkspacePaneIds.Viewport));
        Assert.True(adapter.IsPaneOpen(WorkspacePaneIds.Viewport));
    }

    private static EditorDockingAdapter CreateAdapter()
    {
        DockPaneRegistry registry = DockPaneRegistry.CreateDefaultTrack();
        return new EditorDockingAdapter(registry, CreateContexts(registry));
    }

    private static IReadOnlyDictionary<string, object> CreateContexts(DockPaneRegistry registry) =>
        registry.Panes.ToDictionary(
            pane => pane.Id,
            _ => (object)new object(),
            StringComparer.Ordinal);

    private static void AssertPane(
        DockPaneRegistration pane,
        string id,
        string title,
        bool canClose)
    {
        Assert.Equal(id, pane.Id);
        Assert.Equal(title, pane.Title);
        Assert.Equal(canClose, pane.CanClose);
    }
}
