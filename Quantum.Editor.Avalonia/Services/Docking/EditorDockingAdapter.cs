using Dock.Model.Controls;
using Dock.Model.Core;

namespace Quantum.Editor.Avalonia.Services.Docking;

/// <summary>
/// Isolates Dock.Avalonia initialization and pane lifecycle from the editor shell.
/// </summary>
public sealed class EditorDockingAdapter
{
    private readonly EditorDockFactory factory;

    public EditorDockingAdapter(
        DockPaneRegistry registry,
        IReadOnlyDictionary<string, object> paneContexts)
    {
        Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        ArgumentNullException.ThrowIfNull(paneContexts);
        ValidatePaneContexts(registry, paneContexts);

        factory = new EditorDockFactory(registry, paneContexts);
        Layout = factory.CreateLayout();
        factory.InitLayout(Layout);
        IsInitialized = true;
    }

    public DockPaneRegistry Registry { get; }

    public IFactory Factory => factory;

    public IRootDock Layout { get; }

    public bool IsInitialized { get; }

    public IDockable GetPane(string paneId)
    {
        Registry.Get(paneId);
        return factory.GetPane(paneId);
    }

    public bool IsPaneOpen(string paneId)
    {
        IDockable pane = GetPane(paneId);
        return !IsHidden(pane) && pane.Owner is not null;
    }

    public void ShowPane(string paneId)
    {
        IDockable pane = GetPane(paneId);
        if (IsHidden(pane))
        {
            factory.RestoreDockable(pane);
        }

        factory.SetActiveDockable(pane);
        if (pane.Owner is IDock owner)
        {
            factory.SetFocusedDockable(owner, pane);
        }
    }

    public bool TryClosePane(string paneId)
    {
        DockPaneRegistration registration = Registry.Get(paneId);
        if (!registration.CanClose || !IsPaneOpen(paneId))
        {
            return false;
        }

        factory.CloseDockable(GetPane(paneId));
        return !IsPaneOpen(paneId);
    }

    public void SetPaneVisible(string paneId, bool isVisible)
    {
        if (isVisible)
        {
            if (!IsPaneOpen(paneId))
            {
                ShowPane(paneId);
            }

            return;
        }

        if (IsPaneOpen(paneId))
        {
            factory.HideDockable(GetPane(paneId));
        }
    }

    private static bool IsHidden(IDockable pane) =>
        pane.Owner is IRootDock root && root.HiddenDockables?.Contains(pane) == true;

    private static void ValidatePaneContexts(
        DockPaneRegistry registry,
        IReadOnlyDictionary<string, object> paneContexts)
    {
        foreach (DockPaneRegistration pane in registry.Panes)
        {
            if (!paneContexts.TryGetValue(pane.Id, out object? context) || context is null)
            {
                throw new ArgumentException(
                    $"No frontend content was supplied for dock pane '{pane.Id}'.",
                    nameof(paneContexts));
            }
        }
    }
}
