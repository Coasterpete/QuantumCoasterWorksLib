namespace Quantum.Editor.Avalonia.Services.Docking;

/// <summary>
/// Describes whether a persisted frontend docking layout was available and readable.
/// </summary>
public enum DockLayoutLoadStatus
{
    Missing,
    Restored,
    Failed
}
