using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using Quantum.Editor.Avalonia.Services.Docking;

namespace Quantum.Editor.Avalonia.Services.Workspaces;

/// <summary>
/// Frontend-only pane registry and default docking layout owned by a workspace profile.
/// </summary>
public sealed class WorkspaceComposition
{
    private readonly Func<IWorkspaceDockLayoutBuilder, IRootDock> createDefaultLayout;

    public WorkspaceComposition(
        DockPaneRegistry panes,
        Func<IWorkspaceDockLayoutBuilder, IRootDock> createDefaultLayout)
    {
        Panes = panes ?? throw new ArgumentNullException(nameof(panes));
        this.createDefaultLayout = createDefaultLayout ??
            throw new ArgumentNullException(nameof(createDefaultLayout));
    }

    public DockPaneRegistry Panes { get; }

    public static WorkspaceComposition CreateTrack()
    {
        return new WorkspaceComposition(
            DockPaneRegistry.CreateDefaultTrack(),
            CreateDefaultTrackLayout);
    }

    public static WorkspaceComposition CreateComingSoon(
        WorkspaceProfileId workspaceId,
        string displayName)
    {
        if (workspaceId.IsEmpty)
        {
            throw new ArgumentException("A workspace identifier is required.", nameof(workspaceId));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("A workspace display name is required.", nameof(displayName));
        }

        return new WorkspaceComposition(
            new DockPaneRegistry(),
            builder => CreateComingSoonLayout(builder, workspaceId, displayName.Trim()));
    }

    internal IRootDock CreateLayout(IWorkspaceDockLayoutBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return createDefaultLayout(builder) ??
            throw new InvalidOperationException("A workspace composition returned no docking layout.");
    }

    private static IRootDock CreateDefaultTrackLayout(IWorkspaceDockLayoutBuilder builder)
    {
        IDockable route = builder.GetPane(WorkspacePaneIds.Route);
        IDockable viewport = builder.GetPane(WorkspacePaneIds.Viewport);
        IDockable inspector = builder.GetPane(WorkspacePaneIds.Inspector);
        IDockable mathPlots = builder.GetPane(WorkspacePaneIds.MathPlots);
        IDockable diagnostics = builder.GetPane(WorkspacePaneIds.Diagnostics);

        var routeHost = new ToolDock
        {
            Id = DockingLayoutIds.RouteHost,
            Title = "Route",
            Alignment = Alignment.Left,
            Proportion = 0.22,
            ActiveDockable = route,
            VisibleDockables = builder.CreateDockableList(route)
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
            VisibleDockables = builder.CreateDockableList(viewport)
        };
        var inspectorHost = new ToolDock
        {
            Id = DockingLayoutIds.InspectorHost,
            Title = "Inspector",
            Alignment = Alignment.Right,
            Proportion = 0.23,
            ActiveDockable = inspector,
            VisibleDockables = builder.CreateDockableList(inspector)
        };
        var top = new ProportionalDock
        {
            Id = DockingLayoutIds.Top,
            Title = "Track workbench",
            Orientation = Orientation.Horizontal,
            Proportion = 0.64,
            ActiveDockable = viewportHost,
            VisibleDockables = builder.CreateDockableList(
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
            VisibleDockables = builder.CreateDockableList(mathPlots, diagnostics)
        };
        var main = new ProportionalDock
        {
            Id = DockingLayoutIds.Main,
            Title = "Track workspace",
            Orientation = Orientation.Vertical,
            ActiveDockable = top,
            VisibleDockables = builder.CreateDockableList(
                top,
                new ProportionalDockSplitter(),
                bottomHost)
        };

        IRootDock root = builder.CreateWorkspaceRootDock();
        root.Id = DockingLayoutIds.Root;
        root.Title = "Track workspace";
        root.IsCollapsable = false;
        root.DefaultDockable = main;
        root.ActiveDockable = main;
        root.VisibleDockables = builder.CreateDockableList(main);
        return root;
    }

    private static IRootDock CreateComingSoonLayout(
        IWorkspaceDockLayoutBuilder builder,
        WorkspaceProfileId workspaceId,
        string displayName)
    {
        IRootDock root = builder.CreateWorkspaceRootDock();
        root.Id = workspaceId.Value + "-root";
        root.Title = displayName + " workspace (Coming Soon)";
        root.IsCollapsable = false;
        root.VisibleDockables = builder.CreateDockableList();
        return root;
    }
}
