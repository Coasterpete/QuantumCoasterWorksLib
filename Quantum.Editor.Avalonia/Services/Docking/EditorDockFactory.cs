using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using Quantum.Editor.Avalonia.Services.Workspaces;

namespace Quantum.Editor.Avalonia.Services.Docking;

/// <summary>
/// Dock.Avalonia model factory for the current Track workspace composition.
/// </summary>
internal sealed class EditorDockFactory : Factory
{
    private readonly DockPaneRegistry registry;
    private readonly IReadOnlyDictionary<string, object> paneContexts;
    private readonly Dictionary<string, IDockable> panes = new(StringComparer.Ordinal);

    public EditorDockFactory(
        DockPaneRegistry registry,
        IReadOnlyDictionary<string, object> paneContexts)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.paneContexts = paneContexts ?? throw new ArgumentNullException(nameof(paneContexts));
        HideToolsOnClose = true;
    }

    public IDockable GetPane(string paneId) => panes[paneId];

    public override IRootDock CreateLayout()
    {
        panes.Clear();

        IDockable route = CreatePane(WorkspacePaneIds.Route);
        IDockable viewport = CreatePane(WorkspacePaneIds.Viewport);
        IDockable inspector = CreatePane(WorkspacePaneIds.Inspector);
        IDockable mathPlots = CreatePane(WorkspacePaneIds.MathPlots);
        IDockable diagnostics = CreatePane(WorkspacePaneIds.Diagnostics);

        var routeHost = new ToolDock
        {
            Id = DockingLayoutIds.RouteHost,
            Title = "Route",
            Alignment = Alignment.Left,
            Proportion = 0.22,
            ActiveDockable = route,
            VisibleDockables = CreateList(route)
        };
        var viewportHost = new DocumentDock
        {
            Id = DockingLayoutIds.ViewportHost,
            Title = "Viewport",
            IsCollapsable = false,
            CanCreateDocument = false,
            EnableWindowDrag = false,
            Proportion = 0.55,
            ActiveDockable = viewport,
            VisibleDockables = CreateList(viewport)
        };
        var inspectorHost = new ToolDock
        {
            Id = DockingLayoutIds.InspectorHost,
            Title = "Inspector",
            Alignment = Alignment.Right,
            Proportion = 0.23,
            ActiveDockable = inspector,
            VisibleDockables = CreateList(inspector)
        };
        var top = new ProportionalDock
        {
            Id = DockingLayoutIds.Top,
            Title = "Track workbench",
            Orientation = Orientation.Horizontal,
            Proportion = 0.64,
            ActiveDockable = viewportHost,
            VisibleDockables = CreateList<IDockable>(
                routeHost,
                new ProportionalDockSplitter(),
                viewportHost,
                new ProportionalDockSplitter(),
                inspectorHost)
        };
        var bottomHost = new ToolDock
        {
            Id = DockingLayoutIds.BottomHost,
            Title = "Engineering",
            Alignment = Alignment.Bottom,
            Proportion = 0.36,
            ActiveDockable = mathPlots,
            VisibleDockables = CreateList(mathPlots, diagnostics)
        };
        var main = new ProportionalDock
        {
            Id = DockingLayoutIds.Main,
            Title = "Track workspace",
            Orientation = Orientation.Vertical,
            ActiveDockable = top,
            VisibleDockables = CreateList<IDockable>(
                top,
                new ProportionalDockSplitter(),
                bottomHost)
        };

        var root = (RootDock)CreateRootDock();
        root.Id = DockingLayoutIds.Root;
        root.Title = "Track workspace";
        root.IsCollapsable = false;
        root.DefaultDockable = main;
        root.ActiveDockable = main;
        root.VisibleDockables = CreateList<IDockable>(main);
        return root;
    }

    public override void InitLayout(IDockable layout)
    {
        RebindRegisteredPanes(layout);
        ContextLocator = registry.Panes.ToDictionary(
            pane => pane.Id,
            pane => new Func<object?>(() => paneContexts[pane.Id]),
            StringComparer.Ordinal);
        DockableLocator = registry.Panes.ToDictionary(
            pane => pane.Id,
            pane => new Func<IDockable?>(() => panes[pane.Id]),
            StringComparer.Ordinal);
        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new HostWindow()
        };

        base.InitLayout(layout);
    }

    private void RebindRegisteredPanes(IDockable layout)
    {
        var registeredPanes = new Dictionary<string, IDockable>(StringComparer.Ordinal);
        foreach (IDockable dockable in DockLayoutTraversal.Enumerate(layout))
        {
            if (!registry.TryGet(dockable.Id, out DockPaneRegistration? registration))
            {
                continue;
            }

            if (!registeredPanes.TryAdd(registration.Id, dockable))
            {
                throw new InvalidDataException(
                    $"Docking layout contains duplicate pane '{registration.Id}'.");
            }

            dockable.Title = registration.Title;
            dockable.CanClose = registration.CanClose;
        }

        foreach (DockPaneRegistration registration in registry.Panes)
        {
            if (!registeredPanes.ContainsKey(registration.Id))
            {
                throw new InvalidDataException(
                    $"Docking layout is missing pane '{registration.Id}'.");
            }
        }

        panes.Clear();
        foreach ((string paneId, IDockable pane) in registeredPanes)
        {
            panes.Add(paneId, pane);
        }
    }

    private IDockable CreatePane(string paneId)
    {
        DockPaneRegistration registration = registry.Get(paneId);
        var result = new Tool
        {
            Id = registration.Id,
            Title = registration.Title,
            CanClose = registration.CanClose,
            CanPin = false,
            CanDrag = true,
            CanFloat = true,
            CanDockAsDocument = true
        };
        panes.Add(paneId, result);
        return result;
    }
}
