namespace Quantum.Editor.Avalonia.Services.Workspaces;

/// <summary>
/// Registration and lookup boundary for editor workspace profiles.
/// </summary>
public sealed class WorkspaceProfileCatalog
{
    private readonly Dictionary<WorkspaceProfileId, WorkspaceProfile> profilesById = new();
    private readonly List<WorkspaceProfile> profiles = new();
    private WorkspaceProfileId defaultProfileId;

    public IReadOnlyList<WorkspaceProfile> Profiles => profiles.AsReadOnly();

    public IReadOnlyList<WorkspaceProfile> AvailableProfiles =>
        profiles.Where(profile => profile.IsAvailable).ToArray();

    public IReadOnlyList<WorkspaceProfile> VisibleProfiles =>
        profiles.Where(profile => profile.IsVisible).ToArray();

    public WorkspaceProfileId DefaultProfileId => defaultProfileId;

    public WorkspaceProfile DefaultProfile
    {
        get
        {
            if (defaultProfileId.IsEmpty)
            {
                throw new InvalidOperationException("The workspace profile catalog has no available default profile.");
            }

            return Get(defaultProfileId);
        }
    }

    public static WorkspaceProfileCatalog CreateDefault()
    {
        var result = new WorkspaceProfileCatalog();
        result.Register(CreateTrackProfile(), makeDefault: true);
        result.Register(CreateFutureProfile(WorkspaceProfileId.Train, "Train", "train"));
        result.Register(CreateFutureProfile(WorkspaceProfileId.Support, "Support", "support"));
        result.Register(CreateFutureProfile(WorkspaceProfileId.Terrain, "Terrain", "terrain"));
        result.Register(CreateFutureProfile(WorkspaceProfileId.Simulation, "Simulation", "simulation"));
        return result;
    }

    public void Register(WorkspaceProfile profile, bool makeDefault = false)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profilesById.ContainsKey(profile.Id))
        {
            throw new InvalidOperationException(
                $"Workspace profile '{profile.Id}' is already registered.");
        }

        if (makeDefault && !profile.IsAvailable)
        {
            throw new InvalidOperationException("An unavailable workspace profile cannot be the default.");
        }

        profilesById.Add(profile.Id, profile);
        profiles.Add(profile);

        if (makeDefault || (defaultProfileId.IsEmpty && profile.IsAvailable))
        {
            defaultProfileId = profile.Id;
        }
    }

    public void SetDefault(WorkspaceProfileId id)
    {
        WorkspaceProfile profile = Get(id);
        if (!profile.IsAvailable)
        {
            throw new InvalidOperationException("An unavailable workspace profile cannot be the default.");
        }

        defaultProfileId = id;
    }

    public bool Contains(WorkspaceProfileId id) => profilesById.ContainsKey(id);

    public bool TryGet(WorkspaceProfileId id, out WorkspaceProfile profile) =>
        profilesById.TryGetValue(id, out profile!);

    public WorkspaceProfile Get(WorkspaceProfileId id)
    {
        if (!profilesById.TryGetValue(id, out WorkspaceProfile? profile))
        {
            throw new KeyNotFoundException($"Workspace profile '{id}' is not registered.");
        }

        return profile;
    }

    private static WorkspaceProfile CreateTrackProfile()
    {
        string[] panes =
        {
            WorkspacePaneIds.Route,
            WorkspacePaneIds.Viewport,
            WorkspacePaneIds.Inspector,
            WorkspacePaneIds.MathPlots,
            WorkspacePaneIds.Diagnostics
        };

        return new WorkspaceProfile(
            WorkspaceProfileId.Track,
            "Track",
            icon: "track",
            availablePanes: panes,
            defaultVisiblePanes: panes,
            commandGroups: new[]
            {
                WorkspaceCommandGroupIds.File,
                WorkspaceCommandGroupIds.Edit,
                WorkspaceCommandGroupIds.View
            },
            overlayDefaults: new Dictionary<string, bool>
            {
                [WorkspaceOverlayIds.TransportedFrames] = true
            });
    }

    private static WorkspaceProfile CreateFutureProfile(
        WorkspaceProfileId id,
        string displayName,
        string icon)
    {
        return new WorkspaceProfile(
            id,
            displayName,
            icon,
            isAvailable: false,
            isVisible: false);
    }
}
