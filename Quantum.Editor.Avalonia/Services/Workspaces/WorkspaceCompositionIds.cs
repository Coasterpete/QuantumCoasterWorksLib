namespace Quantum.Editor.Avalonia.Services.Workspaces;

/// <summary>
/// Pane identifiers understood by the current workbench composition root.
/// </summary>
public static class WorkspacePaneIds
{
    public const string Route = "route";
    public const string Viewport = "viewport";
    public const string Inspector = "inspector";
    public const string MathPlots = "math-plots";
    public const string Diagnostics = "diagnostics";
}

/// <summary>
/// Command group identifiers understood by the current workbench shell.
/// </summary>
public static class WorkspaceCommandGroupIds
{
    public const string File = "file";
    public const string Edit = "edit";
    public const string View = "view";
}

/// <summary>
/// Overlay identifiers whose initial state can be supplied by a workspace profile.
/// </summary>
public static class WorkspaceOverlayIds
{
    public const string TransportedFrames = "transported-frames";
}
