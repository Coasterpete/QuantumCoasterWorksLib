namespace Quantum.Editor.Avalonia.Models;

public sealed class EditorGraphNode
{
    public EditorGraphNode(
        string nodeId,
        int routeIndex,
        string sectionKind,
        string summary)
    {
        NodeId = string.IsNullOrWhiteSpace(nodeId)
            ? throw new ArgumentException("Graph node ID is required.", nameof(nodeId))
            : nodeId;
        RouteIndex = routeIndex >= 0
            ? routeIndex
            : throw new ArgumentOutOfRangeException(nameof(routeIndex));
        SectionKind = string.IsNullOrWhiteSpace(sectionKind)
            ? throw new ArgumentException("Section kind is required.", nameof(sectionKind))
            : sectionKind;
        Summary = summary ?? string.Empty;
    }

    public string NodeId { get; }

    public int RouteIndex { get; }

    public string SectionKind { get; }

    public string Summary { get; }

    public EditorSelection Selection => EditorSelection.GraphNode(NodeId, RouteIndex);
}
