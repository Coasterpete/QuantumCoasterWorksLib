namespace Quantum.Editor.Avalonia.Services.Docking;

/// <summary>
/// Frontend-only metadata for one dockable editor pane.
/// </summary>
public sealed class DockPaneRegistration
{
    public DockPaneRegistration(string id, string title, bool canClose = true)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A dock pane identifier cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("A dock pane title cannot be empty.", nameof(title));
        }

        Id = id.Trim();
        Title = title.Trim();
        CanClose = canClose;
    }

    public string Id { get; }

    public string Title { get; }

    public bool CanClose { get; }
}
