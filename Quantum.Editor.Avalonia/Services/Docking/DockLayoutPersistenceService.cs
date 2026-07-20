using System.Text.Json;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Serializer;
using Quantum.Editor.Avalonia.Services.Workspaces;

namespace Quantum.Editor.Avalonia.Services.Docking;

/// <summary>
/// Persists only the Avalonia frontend docking graph.
/// </summary>
public sealed class DockLayoutPersistenceService
{
    public const string LayoutFileName = "track-docking-layout.json";
    private const int CurrentFormatVersion = 1;

    private readonly IDockSerializer serializer;

    public DockLayoutPersistenceService()
        : this(GetDefaultLayoutFilePath())
    {
    }

    public DockLayoutPersistenceService(string layoutFilePath)
        : this(layoutFilePath, new DockSerializer())
    {
    }

    internal DockLayoutPersistenceService(
        string layoutFilePath,
        IDockSerializer serializer)
    {
        if (string.IsNullOrWhiteSpace(layoutFilePath))
        {
            throw new ArgumentException("A docking layout file path is required.", nameof(layoutFilePath));
        }

        LayoutFilePath = Path.GetFullPath(layoutFilePath);
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public string LayoutFilePath { get; }

    public string? LastErrorMessage { get; private set; }

    public static string GetDefaultLayoutFilePath()
    {
        return GetDefaultLayoutFilePath(WorkspaceProfileId.Track);
    }

    public static string GetDefaultLayoutFilePath(WorkspaceProfileId workspaceId)
    {
        if (workspaceId.IsEmpty)
        {
            throw new ArgumentException("A workspace identifier is required.", nameof(workspaceId));
        }

        string localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        string fileName = workspaceId == WorkspaceProfileId.Track
            ? LayoutFileName
            : workspaceId.Value + "-docking-layout.json";
        return Path.Combine(
            localApplicationData,
            "QuantumCoasterWorks",
            "Editor",
            fileName);
    }

    public DockLayoutLoadStatus TryLoadLayout(out IRootDock? layout)
    {
        LastErrorMessage = null;
        layout = null;
        if (!File.Exists(LayoutFilePath))
        {
            return DockLayoutLoadStatus.Missing;
        }

        try
        {
            using FileStream stream = File.OpenRead(LayoutFilePath);
            PersistedDockLayout? persisted = JsonSerializer.Deserialize<PersistedDockLayout>(stream);
            if (persisted is null ||
                persisted.FormatVersion != CurrentFormatVersion ||
                string.IsNullOrWhiteSpace(persisted.DockLayout))
            {
                LastErrorMessage = "The docking layout file has an unsupported or incomplete format.";
                return DockLayoutLoadStatus.Failed;
            }

            layout = serializer.Deserialize<IRootDock>(persisted.DockLayout);
            if (layout is null)
            {
                LastErrorMessage = "The docking layout payload did not contain a root dock.";
                return DockLayoutLoadStatus.Failed;
            }

            RestoreHiddenPaneOwners(layout, persisted.HiddenPaneOwnerIds);
            return DockLayoutLoadStatus.Restored;
        }
        catch (Exception exception)
        {
            LastErrorMessage = exception.Message;
            layout = null;
            return DockLayoutLoadStatus.Failed;
        }
    }

    public bool TrySaveLayout(IRootDock layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        LastErrorMessage = null;

        string? directoryPath = Path.GetDirectoryName(LayoutFilePath);
        string temporaryPath = LayoutFilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            if (!string.IsNullOrEmpty(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            using (FileStream stream = new(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                var persisted = new PersistedDockLayout
                {
                    FormatVersion = CurrentFormatVersion,
                    DockLayout = serializer.Serialize(layout),
                    HiddenPaneOwnerIds = CaptureHiddenPaneOwners(layout)
                };
                JsonSerializer.Serialize(
                    stream,
                    persisted,
                    new JsonSerializerOptions { WriteIndented = true });
            }

            File.Move(temporaryPath, LayoutFilePath, overwrite: true);
            return true;
        }
        catch (Exception exception)
        {
            LastErrorMessage = exception.Message;
            TryDeleteFile(temporaryPath);
            return false;
        }
    }

    public bool TryDiscardSavedLayout()
    {
        LastErrorMessage = null;
        if (!File.Exists(LayoutFilePath))
        {
            return true;
        }

        try
        {
            File.Delete(LayoutFilePath);
            return true;
        }
        catch (Exception exception)
        {
            LastErrorMessage = exception.Message;
            return false;
        }
    }

    private static bool TryDeleteFile(string filePath)
    {
        try
        {
            File.Delete(filePath);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static Dictionary<string, string> CaptureHiddenPaneOwners(IRootDock layout)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (IRootDock root in EnumerateRoots(layout))
        {
            if (root.HiddenDockables is null)
            {
                continue;
            }

            foreach (IDockable pane in root.HiddenDockables)
            {
                if (!string.IsNullOrWhiteSpace(pane.Id) &&
                    pane.OriginalOwner is IDock { Id: { Length: > 0 } ownerId })
                {
                    result[pane.Id] = ownerId;
                }
            }
        }

        return result;
    }

    private static void RestoreHiddenPaneOwners(
        IRootDock layout,
        IReadOnlyDictionary<string, string>? ownerIds)
    {
        if (ownerIds is null || ownerIds.Count == 0)
        {
            return;
        }

        Dictionary<string, IDockable> dockables = DockLayoutTraversal.Enumerate(layout)
            .Where(dockable => !string.IsNullOrWhiteSpace(dockable.Id))
            .GroupBy(dockable => dockable.Id, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

        foreach (IRootDock root in EnumerateRoots(layout))
        {
            if (root.HiddenDockables is null)
            {
                continue;
            }

            foreach (IDockable pane in root.HiddenDockables)
            {
                if (ownerIds.TryGetValue(pane.Id, out string? ownerId) &&
                    dockables.TryGetValue(ownerId, out IDockable? owner) &&
                    owner is IDock)
                {
                    pane.OriginalOwner = owner;
                }
            }
        }
    }

    private static IEnumerable<IRootDock> EnumerateRoots(IRootDock layout) =>
        DockLayoutTraversal.Enumerate(layout).OfType<IRootDock>();

    private sealed class PersistedDockLayout
    {
        public int FormatVersion { get; init; }

        public string DockLayout { get; init; } = string.Empty;

        public Dictionary<string, string> HiddenPaneOwnerIds { get; init; } =
            new(StringComparer.Ordinal);
    }
}
