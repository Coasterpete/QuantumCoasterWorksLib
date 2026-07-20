namespace Quantum.Editor.Avalonia.Services.Workspaces;

/// <summary>
/// Testable selector projection over the workspace profile registry.
/// </summary>
public sealed class WorkspaceSelectorModel
{
    private readonly WorkspaceProfileManager profiles;

    public WorkspaceSelectorModel(WorkspaceProfileManager profiles)
    {
        this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        Items = profiles.Catalog.VisibleProfiles
            .Select(profile => new WorkspaceSelectorItem(profile))
            .ToArray();
    }

    public IReadOnlyList<WorkspaceSelectorItem> Items { get; }

    public WorkspaceSelectorItem SelectedItem =>
        Items.Single(item => item.Id == profiles.CurrentProfileId);

    public bool TryActivate(WorkspaceProfileId id) => profiles.TrySwitchTo(id);
}

public sealed class WorkspaceSelectorItem
{
    internal WorkspaceSelectorItem(WorkspaceProfile profile)
    {
        Id = profile.Id;
        DisplayName = profile.DisplayName;
        IsEnabled = profile.IsAvailable;
        DisplayText = IsEnabled
            ? DisplayName
            : DisplayName + " (Coming Soon)";
    }

    public WorkspaceProfileId Id { get; }

    public string DisplayName { get; }

    public string DisplayText { get; }

    public bool IsEnabled { get; }
}
