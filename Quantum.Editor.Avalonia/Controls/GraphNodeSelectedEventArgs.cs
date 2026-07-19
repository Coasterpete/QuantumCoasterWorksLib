using Quantum.Editor.Avalonia.Models;

namespace Quantum.Editor.Avalonia.Controls;

public sealed class GraphNodeSelectedEventArgs : EventArgs
{
    public GraphNodeSelectedEventArgs(EditorGraphNode node)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
    }

    public EditorGraphNode Node { get; }
}
