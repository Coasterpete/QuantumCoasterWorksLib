namespace Quantum.Editor.Avalonia.Services.Workspaces;

/// <summary>
/// Owns the active editor composition profile without owning editor documents or state.
/// </summary>
public sealed class WorkspaceProfileManager
{
    private WorkspaceProfile currentProfile;

    public WorkspaceProfileManager()
        : this(WorkspaceProfileCatalog.CreateDefault())
    {
    }

    public WorkspaceProfileManager(
        WorkspaceProfileCatalog catalog,
        WorkspaceProfileId? initialProfileId = null)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        WorkspaceProfile selectedProfile = initialProfileId.HasValue
            ? Catalog.Get(initialProfileId.Value)
            : Catalog.DefaultProfile;
        if (!selectedProfile.IsAvailable)
        {
            throw new InvalidOperationException(
                $"Workspace profile '{selectedProfile.Id}' is not available.");
        }

        currentProfile = selectedProfile;
    }

    public event EventHandler? CurrentProfileChanged;

    public WorkspaceProfileCatalog Catalog { get; }

    public WorkspaceProfile CurrentProfile => currentProfile;

    public WorkspaceProfile ActiveProfile => currentProfile;

    public WorkspaceProfileId CurrentProfileId => currentProfile.Id;

    public bool TrySwitchTo(WorkspaceProfileId id)
    {
        if (!Catalog.TryGet(id, out WorkspaceProfile profile) || !profile.IsAvailable)
        {
            return false;
        }

        SetCurrentProfile(profile);
        return true;
    }

    public void SwitchTo(WorkspaceProfileId id)
    {
        WorkspaceProfile profile = Catalog.Get(id);
        if (!profile.IsAvailable)
        {
            throw new InvalidOperationException($"Workspace profile '{id}' is not available.");
        }

        SetCurrentProfile(profile);
    }

    public void ResetToDefault() => SwitchTo(Catalog.DefaultProfileId);

    private void SetCurrentProfile(WorkspaceProfile profile)
    {
        if (currentProfile.Id == profile.Id)
        {
            return;
        }

        currentProfile = profile;
        CurrentProfileChanged?.Invoke(this, EventArgs.Empty);
    }
}
