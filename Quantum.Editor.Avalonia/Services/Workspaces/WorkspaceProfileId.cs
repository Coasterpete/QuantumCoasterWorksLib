namespace Quantum.Editor.Avalonia.Services.Workspaces;

/// <summary>
/// Stable identifier for an editor workspace profile.
/// </summary>
public readonly struct WorkspaceProfileId : IEquatable<WorkspaceProfileId>
{
    private readonly string? value;

    public WorkspaceProfileId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A workspace profile identifier cannot be empty.", nameof(value));
        }

        this.value = value.Trim();
    }

    public static WorkspaceProfileId Track { get; } = new("track");

    public static WorkspaceProfileId Train { get; } = new("train");

    public static WorkspaceProfileId Support { get; } = new("support");

    public static WorkspaceProfileId Terrain { get; } = new("terrain");

    public static WorkspaceProfileId Simulation { get; } = new("simulation");

    public string Value => value ?? string.Empty;

    public bool IsEmpty => string.IsNullOrEmpty(value);

    public bool Equals(WorkspaceProfileId other) =>
        string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is WorkspaceProfileId other && Equals(other);

    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;

    public static bool operator ==(WorkspaceProfileId left, WorkspaceProfileId right) =>
        left.Equals(right);

    public static bool operator !=(WorkspaceProfileId left, WorkspaceProfileId right) =>
        !left.Equals(right);
}
