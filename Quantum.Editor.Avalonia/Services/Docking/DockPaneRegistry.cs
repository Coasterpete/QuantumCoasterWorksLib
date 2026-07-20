using Quantum.Editor.Avalonia.Services.Workspaces;

namespace Quantum.Editor.Avalonia.Services.Docking;

/// <summary>
/// Registers the panes understood by the Avalonia docking composition root.
/// </summary>
public sealed class DockPaneRegistry
{
    private readonly Dictionary<string, DockPaneRegistration> panesById =
        new(StringComparer.Ordinal);
    private readonly List<DockPaneRegistration> panes = new();

    public IReadOnlyList<DockPaneRegistration> Panes => panes.AsReadOnly();

    public static DockPaneRegistry CreateDefaultTrack()
    {
        var result = new DockPaneRegistry();
        result.Register(new DockPaneRegistration(WorkspacePaneIds.Route, "Route"));
        result.Register(new DockPaneRegistration(
            WorkspacePaneIds.Viewport,
            "Viewport",
            canClose: false));
        result.Register(new DockPaneRegistration(WorkspacePaneIds.Inspector, "Inspector"));
        result.Register(new DockPaneRegistration(WorkspacePaneIds.MathPlots, "Math Plots"));
        result.Register(new DockPaneRegistration(WorkspacePaneIds.Diagnostics, "Diagnostics"));
        return result;
    }

    public static DockPaneRegistry CreateDefaultTrain()
    {
        var result = new DockPaneRegistry();
        result.Register(new DockPaneRegistration(
            WorkspacePaneIds.TrainConfiguration,
            "Train Configuration"));
        result.Register(new DockPaneRegistration(
            WorkspacePaneIds.TrainPreview,
            "Train Preview",
            canClose: false));
        result.Register(new DockPaneRegistration(
            WorkspacePaneIds.TrainSummary,
            "Train Summary"));
        return result;
    }

    public void Register(DockPaneRegistration pane)
    {
        ArgumentNullException.ThrowIfNull(pane);
        if (panesById.ContainsKey(pane.Id))
        {
            throw new InvalidOperationException($"Dock pane '{pane.Id}' is already registered.");
        }

        panesById.Add(pane.Id, pane);
        panes.Add(pane);
    }

    public bool Contains(string paneId) =>
        !string.IsNullOrWhiteSpace(paneId) && panesById.ContainsKey(paneId);

    public bool TryGet(string paneId, out DockPaneRegistration pane) =>
        panesById.TryGetValue(paneId, out pane!);

    public DockPaneRegistration Get(string paneId)
    {
        if (!panesById.TryGetValue(paneId, out DockPaneRegistration? pane))
        {
            throw new KeyNotFoundException($"Dock pane '{paneId}' is not registered.");
        }

        return pane;
    }
}
