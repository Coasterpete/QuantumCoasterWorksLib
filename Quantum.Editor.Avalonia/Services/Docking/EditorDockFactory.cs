using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using Quantum.Editor.Avalonia.Services.Workspaces;

namespace Quantum.Editor.Avalonia.Services.Docking;

/// <summary>
/// Dock.Avalonia model factory for a registered workspace composition.
/// </summary>
internal sealed class EditorDockFactory : Factory, IWorkspaceDockLayoutBuilder
{
    private readonly WorkspaceComposition composition;
    private readonly DockPaneRegistry registry;
    private readonly IReadOnlyDictionary<string, object> paneContexts;
    private readonly Dictionary<string, IDockable> panes = new(StringComparer.Ordinal);

    public EditorDockFactory(
        WorkspaceComposition composition,
        IReadOnlyDictionary<string, object> paneContexts)
    {
        this.composition = composition ?? throw new ArgumentNullException(nameof(composition));
        registry = composition.Panes;
        this.paneContexts = paneContexts ?? throw new ArgumentNullException(nameof(paneContexts));
        HideToolsOnClose = true;
    }

    public IDockable GetPane(string paneId) => panes[paneId];

    public override IRootDock CreateLayout()
    {
        panes.Clear();
        foreach (DockPaneRegistration pane in registry.Panes)
        {
            CreatePane(pane.Id);
        }

        return composition.CreateLayout(this);
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

    IList<IDockable> IWorkspaceDockLayoutBuilder.CreateDockableList(
        params IDockable[] dockables) => CreateList<IDockable>(dockables);

    IRootDock IWorkspaceDockLayoutBuilder.CreateWorkspaceRootDock() => CreateRootDock();
}
