namespace Quantum.Editor.Avalonia.Models;

public sealed class EditorOutlinerNode
{
    public EditorOutlinerNode(
        string title,
        EditorSelection? selection = null,
        IEnumerable<EditorOutlinerNode>? children = null)
    {
        Title = string.IsNullOrWhiteSpace(title)
            ? throw new ArgumentException("Outliner node title is required.", nameof(title))
            : title;
        Selection = selection;
        Children = children?.ToArray() ?? Array.Empty<EditorOutlinerNode>();
    }

    public string Title { get; }

    public EditorSelection? Selection { get; }

    public IReadOnlyList<EditorOutlinerNode> Children { get; }
}
