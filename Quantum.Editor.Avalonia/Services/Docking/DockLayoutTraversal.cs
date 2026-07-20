using Dock.Model.Controls;
using Dock.Model.Core;

namespace Quantum.Editor.Avalonia.Services.Docking;

internal static class DockLayoutTraversal
{
    public static IEnumerable<IDockable> Enumerate(IDockable layout)
    {
        var visited = new HashSet<IDockable>(ReferenceEqualityComparer.Instance);
        return Enumerate(layout, visited);
    }

    private static IEnumerable<IDockable> Enumerate(
        IDockable dockable,
        HashSet<IDockable> visited)
    {
        if (!visited.Add(dockable))
        {
            yield break;
        }

        yield return dockable;

        if (dockable is IDock dock && dock.VisibleDockables is not null)
        {
            foreach (IDockable child in dock.VisibleDockables)
            {
                foreach (IDockable descendant in Enumerate(child, visited))
                {
                    yield return descendant;
                }
            }
        }

        if (dockable is ISplitViewDock splitViewDock)
        {
            foreach (IDockable splitChild in new[]
                     {
                         splitViewDock.PaneDockable,
                         splitViewDock.ContentDockable
                     }.OfType<IDockable>())
            {
                foreach (IDockable descendant in Enumerate(splitChild, visited))
                {
                    yield return descendant;
                }
            }
        }

        if (dockable is not IRootDock root)
        {
            yield break;
        }

        foreach (IList<IDockable>? collection in new[]
                 {
                     root.HiddenDockables,
                     root.LeftPinnedDockables,
                     root.RightPinnedDockables,
                     root.TopPinnedDockables,
                     root.BottomPinnedDockables
                 })
        {
            if (collection is null)
            {
                continue;
            }

            foreach (IDockable child in collection)
            {
                foreach (IDockable descendant in Enumerate(child, visited))
                {
                    yield return descendant;
                }
            }
        }

        if (root.PinnedDock is not null)
        {
            foreach (IDockable descendant in Enumerate(root.PinnedDock, visited))
            {
                yield return descendant;
            }
        }

        if (root.Windows is null)
        {
            yield break;
        }

        foreach (IDockWindow window in root.Windows)
        {
            if (window.Layout is null)
            {
                continue;
            }

            foreach (IDockable descendant in Enumerate(window.Layout, visited))
            {
                yield return descendant;
            }
        }
    }
}
