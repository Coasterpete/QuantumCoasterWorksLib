using Dock.Model.Controls;
using Dock.Model.Core;

namespace Quantum.Editor.Avalonia.Services.Docking;

/// <summary>
/// Minimal layout-building surface supplied to a workspace composition.
/// </summary>
public interface IWorkspaceDockLayoutBuilder
{
    IDockable GetPane(string paneId);

    IList<IDockable> CreateDockableList(params IDockable[] dockables);

    IRootDock CreateWorkspaceRootDock();
}
