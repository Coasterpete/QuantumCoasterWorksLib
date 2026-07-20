using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using Dock.Model.Mvvm.Core;
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
        WorkspaceComposition composition = WorkspaceComposition.CreateTrack();
        DockPaneRegistry registry = composition.Panes;
        IReadOnlyDictionary<string, object> contexts = CreateContexts(registry);

        var adapter = new EditorDockingAdapter(composition, contexts);

        Assert.True(adapter.IsInitialized);
        Assert.Same(composition, adapter.Composition);
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

    [Fact]
    public void PersistenceService_SavesAndRestoresLayoutStateAndFrontendContexts()
    {
        WithTemporaryLayoutPath(layoutPath =>
        {
            var persistence = new DockLayoutPersistenceService(layoutPath);
            EditorDockingAdapter source = CreateAdapter(persistence, out _);
            IDockable routeHost = Assert.IsAssignableFrom<IDockable>(
                source.GetPane(WorkspacePaneIds.Route).Owner);
            routeHost.Proportion = 0.31;

            IDock bottomHost = Assert.IsAssignableFrom<IDock>(
                source.GetPane(WorkspacePaneIds.MathPlots).Owner);
            source.Factory.SetActiveDockable(source.GetPane(WorkspacePaneIds.Diagnostics));
            source.SetPaneVisible(WorkspacePaneIds.Inspector, isVisible: false);

            Assert.True(source.TrySaveLayout(), persistence.LastErrorMessage);
            Assert.True(File.Exists(layoutPath));

            EditorDockingAdapter restored = CreateAdapter(persistence, out var restoredContexts);

            Assert.Equal(DockLayoutLoadStatus.Restored, restored.LoadStatus);
            Assert.True(restored.RestoredSavedLayout);
            Assert.Equal(
                0.31,
                Assert.IsAssignableFrom<IDockable>(
                    restored.GetPane(WorkspacePaneIds.Route).Owner).Proportion,
                precision: 10);
            IDock restoredBottomHost = Assert.IsAssignableFrom<IDock>(
                restored.GetPane(WorkspacePaneIds.MathPlots).Owner);
            Assert.Equal(DockingLayoutIds.BottomHost, restoredBottomHost.Id);
            Assert.Same(
                restored.GetPane(WorkspacePaneIds.Diagnostics),
                restoredBottomHost.ActiveDockable);
            Assert.False(restored.IsPaneOpen(WorkspacePaneIds.Inspector));
            Assert.Same(
                restoredContexts[WorkspacePaneIds.Viewport],
                restored.GetPane(WorkspacePaneIds.Viewport).Context);

            restored.ShowPane(WorkspacePaneIds.Inspector);

            Assert.True(restored.IsPaneOpen(WorkspacePaneIds.Inspector));
            Assert.Equal(
                DockingLayoutIds.InspectorHost,
                restored.GetPane(WorkspacePaneIds.Inspector).Owner?.Id);
            Assert.Same(source.GetPane(WorkspacePaneIds.Diagnostics), bottomHost.ActiveDockable);
        });
    }

    [Fact]
    public void PersistenceService_PreservesFloatingWindowBoundsAndContents()
    {
        WithTemporaryLayoutPath(layoutPath =>
        {
            var persistence = new DockLayoutPersistenceService(layoutPath);
            EditorDockingAdapter source = CreateAdapter();
            IDockable diagnostics = source.GetPane(WorkspacePaneIds.Diagnostics);
            IDock sourceHost = Assert.IsAssignableFrom<IDock>(diagnostics.Owner);
            sourceHost.VisibleDockables!.Remove(diagnostics);

            var floatingHost = new ToolDock
            {
                Id = "track-floating-host",
                Title = "Floating diagnostics",
                ActiveDockable = diagnostics,
                VisibleDockables = source.Factory.CreateList(diagnostics)
            };
            diagnostics.Owner = floatingHost;
            var floatingRoot = (RootDock)source.Factory.CreateRootDock();
            floatingRoot.Id = "track-floating-root";
            floatingRoot.ActiveDockable = floatingHost;
            floatingRoot.VisibleDockables = source.Factory.CreateList<IDockable>(floatingHost);
            var window = new DockWindow
            {
                X = 120,
                Y = 85,
                Width = 640,
                Height = 480,
                WindowState = DockWindowState.Maximized,
                Layout = floatingRoot
            };
            source.Layout.Windows = source.Factory.CreateList<IDockWindow>(window);

            Assert.True(persistence.TrySaveLayout(source.Layout), persistence.LastErrorMessage);
            Assert.Equal(
                DockLayoutLoadStatus.Restored,
                persistence.TryLoadLayout(out IRootDock? restored));

            IDockWindow restoredWindow = Assert.Single(restored!.Windows!);
            Assert.Equal(120, restoredWindow.X);
            Assert.Equal(85, restoredWindow.Y);
            Assert.Equal(640, restoredWindow.Width);
            Assert.Equal(480, restoredWindow.Height);
            Assert.Equal(DockWindowState.Maximized, restoredWindow.WindowState);
            IDock restoredFloatingHost = Assert.IsAssignableFrom<IDock>(
                Assert.Single(restoredWindow.Layout!.VisibleDockables!));
            Assert.Equal(
                WorkspacePaneIds.Diagnostics,
                Assert.Single(restoredFloatingHost.VisibleDockables!).Id);
        });
    }

    [Fact]
    public void MissingLayoutFile_UsesDocumentedDefaultTrackLayout()
    {
        WithTemporaryLayoutPath(layoutPath =>
        {
            EditorDockingAdapter adapter = CreateAdapter(
                new DockLayoutPersistenceService(layoutPath),
                out _);

            Assert.Equal(DockLayoutLoadStatus.Missing, adapter.LoadStatus);
            Assert.False(adapter.RestoredSavedLayout);
            AssertDefaultTrackLayout(adapter);
        });
    }

    [Fact]
    public void CorruptedLayoutFile_FallsBackToDocumentedDefaultTrackLayout()
    {
        WithTemporaryLayoutPath(layoutPath =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(layoutPath)!);
            File.WriteAllText(layoutPath, "{ definitely not a docking layout");

            EditorDockingAdapter adapter = CreateAdapter(
                new DockLayoutPersistenceService(layoutPath),
                out _);

            Assert.Equal(DockLayoutLoadStatus.Failed, adapter.LoadStatus);
            Assert.False(adapter.RestoredSavedLayout);
            AssertDefaultTrackLayout(adapter);
        });
    }

    [Fact]
    public void ResetLayout_DiscardsSavedLayoutAndRecreatesDefaultWithoutReplacingContexts()
    {
        WithTemporaryLayoutPath(layoutPath =>
        {
            var persistence = new DockLayoutPersistenceService(layoutPath);
            EditorDockingAdapter adapter = CreateAdapter(persistence, out var contexts);
            adapter.SetPaneVisible(WorkspacePaneIds.Diagnostics, isVisible: false);
            Assert.True(adapter.TrySaveLayout(), persistence.LastErrorMessage);
            IRootDock previousLayout = adapter.Layout;

            IRootDock resetLayout = adapter.ResetLayout();

            Assert.NotSame(previousLayout, resetLayout);
            Assert.Same(resetLayout, adapter.Layout);
            Assert.False(File.Exists(layoutPath));
            AssertDefaultTrackLayout(adapter);
            foreach (DockPaneRegistration pane in adapter.Registry.Panes)
            {
                Assert.Same(contexts[pane.Id], adapter.GetPane(pane.Id).Context);
            }
        });
    }

    private static EditorDockingAdapter CreateAdapter()
    {
        WorkspaceComposition composition = WorkspaceProfileCatalog.CreateDefault()
            .GetComposition(WorkspaceProfileId.Track);
        return new EditorDockingAdapter(
            composition,
            CreateContexts(composition.Panes));
    }

    private static EditorDockingAdapter CreateAdapter(
        DockLayoutPersistenceService persistence,
        out IReadOnlyDictionary<string, object> contexts)
    {
        WorkspaceComposition composition = WorkspaceProfileCatalog.CreateDefault()
            .GetComposition(WorkspaceProfileId.Track);
        contexts = CreateContexts(composition.Panes);
        return new EditorDockingAdapter(composition, contexts, persistence);
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

    private static void AssertDefaultTrackLayout(EditorDockingAdapter adapter)
    {
        Assert.Equal(DockingLayoutIds.Root, adapter.Layout.Id);
        Assert.All(adapter.Registry.Panes, pane => Assert.True(adapter.IsPaneOpen(pane.Id)));
        Assert.Equal(
            DockingLayoutIds.RouteHost,
            adapter.GetPane(WorkspacePaneIds.Route).Owner?.Id);
        Assert.Equal(
            DockingLayoutIds.ViewportHost,
            adapter.GetPane(WorkspacePaneIds.Viewport).Owner?.Id);
        Assert.Equal(
            DockingLayoutIds.InspectorHost,
            adapter.GetPane(WorkspacePaneIds.Inspector).Owner?.Id);
        IDock bottomHost = Assert.IsAssignableFrom<IDock>(
            adapter.GetPane(WorkspacePaneIds.MathPlots).Owner);
        Assert.Equal(DockingLayoutIds.BottomHost, bottomHost.Id);
        Assert.Same(adapter.GetPane(WorkspacePaneIds.MathPlots), bottomHost.ActiveDockable);
    }

    private static void WithTemporaryLayoutPath(Action<string> test)
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            "QuantumCoasterWorks.Tests",
            Guid.NewGuid().ToString("N"));
        string layoutPath = Path.Combine(directoryPath, DockLayoutPersistenceService.LayoutFileName);
        try
        {
            test(layoutPath);
        }
        finally
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
    }
}
