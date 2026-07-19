using System.Collections.ObjectModel;

namespace Quantum.Editor.Avalonia.Services.Workspaces;

/// <summary>
/// Immutable editor-composition metadata for one workspace.
/// </summary>
public sealed class WorkspaceProfile
{
    private readonly HashSet<string> availablePaneSet;
    private readonly HashSet<string> defaultVisiblePaneSet;
    private readonly HashSet<string> commandGroupSet;

    public WorkspaceProfile(
        WorkspaceProfileId id,
        string displayName,
        string? icon = null,
        IEnumerable<string>? availablePanes = null,
        IEnumerable<string>? defaultVisiblePanes = null,
        IEnumerable<string>? commandGroups = null,
        IReadOnlyDictionary<string, bool>? overlayDefaults = null,
        bool isAvailable = true,
        bool isVisible = true)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("A workspace profile identifier cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("A workspace display name cannot be empty.", nameof(displayName));
        }

        Id = id;
        DisplayName = displayName.Trim();
        Icon = icon;
        IsAvailable = isAvailable;
        IsVisible = isVisible;

        string[] panes = CopyDistinctIdentifiers(availablePanes, nameof(availablePanes));
        string[] visiblePanes = CopyDistinctIdentifiers(defaultVisiblePanes, nameof(defaultVisiblePanes));
        string[] groups = CopyDistinctIdentifiers(commandGroups, nameof(commandGroups));
        availablePaneSet = new HashSet<string>(panes, StringComparer.Ordinal);
        defaultVisiblePaneSet = new HashSet<string>(visiblePanes, StringComparer.Ordinal);
        commandGroupSet = new HashSet<string>(groups, StringComparer.Ordinal);

        string? unavailableDefault = visiblePanes.FirstOrDefault(pane => !availablePaneSet.Contains(pane));
        if (unavailableDefault != null)
        {
            throw new ArgumentException(
                $"Default-visible pane '{unavailableDefault}' is not an available pane.",
                nameof(defaultVisiblePanes));
        }

        AvailablePanes = Array.AsReadOnly(panes);
        DefaultVisiblePanes = Array.AsReadOnly(visiblePanes);
        CommandGroups = Array.AsReadOnly(groups);
        DefaultPaneVisibility = new ReadOnlyDictionary<string, bool>(
            panes.ToDictionary(pane => pane, defaultVisiblePaneSet.Contains, StringComparer.Ordinal));
        OverlayDefaults = CopyOverlayDefaults(overlayDefaults);
    }

    public WorkspaceProfileId Id { get; }

    public string DisplayName { get; }

    public string? Icon { get; }

    /// <summary>
    /// Whether the profile contains enough functionality to be activated.
    /// </summary>
    public bool IsAvailable { get; }

    /// <summary>
    /// Whether the profile should be offered by a future workspace switcher.
    /// </summary>
    public bool IsVisible { get; }

    public IReadOnlyList<string> AvailablePanes { get; }

    public IReadOnlyList<string> DefaultVisiblePanes { get; }

    public IReadOnlyDictionary<string, bool> DefaultPaneVisibility { get; }

    public IReadOnlyList<string> CommandGroups { get; }

    public IReadOnlyDictionary<string, bool> OverlayDefaults { get; }

    public bool HasPane(string paneId) =>
        !string.IsNullOrWhiteSpace(paneId) && availablePaneSet.Contains(paneId);

    public bool IsPaneVisibleByDefault(string paneId) =>
        !string.IsNullOrWhiteSpace(paneId) && defaultVisiblePaneSet.Contains(paneId);

    public bool HasCommandGroup(string commandGroupId) =>
        !string.IsNullOrWhiteSpace(commandGroupId) && commandGroupSet.Contains(commandGroupId);

    public bool IsOverlayEnabledByDefault(string overlayId) =>
        !string.IsNullOrWhiteSpace(overlayId) &&
        OverlayDefaults.TryGetValue(overlayId, out bool isEnabled) &&
        isEnabled;

    private static string[] CopyDistinctIdentifiers(IEnumerable<string>? values, string parameterName)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string? value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Composition identifiers cannot be empty.", parameterName);
            }

            string trimmed = value.Trim();
            if (seen.Add(trimmed))
            {
                result.Add(trimmed);
            }
        }

        return result.ToArray();
    }

    private static IReadOnlyDictionary<string, bool> CopyOverlayDefaults(
        IReadOnlyDictionary<string, bool>? overlayDefaults)
    {
        var result = new Dictionary<string, bool>(StringComparer.Ordinal);
        if (overlayDefaults != null)
        {
            foreach ((string key, bool value) in overlayDefaults)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new ArgumentException(
                        "Overlay identifiers cannot be empty.",
                        nameof(overlayDefaults));
                }

                result.Add(key.Trim(), value);
            }
        }

        return new ReadOnlyDictionary<string, bool>(result);
    }
}
