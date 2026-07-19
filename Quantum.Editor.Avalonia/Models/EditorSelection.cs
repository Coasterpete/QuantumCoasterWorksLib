namespace Quantum.Editor.Avalonia.Models;

public enum EditorSelectionKind
{
    Track,
    Section,
    BankingKey,
    ControlPoint,
    Sample
}

public sealed record EditorSelection(
    EditorSelectionKind Kind,
    int SectionIndex = -1,
    int ElementIndex = -1,
    int SampleIndex = -1)
{
    public string? NodeId { get; init; }

    public static EditorSelection Track { get; } = new(EditorSelectionKind.Track);

    public static EditorSelection Section(int sectionIndex) =>
        new(EditorSelectionKind.Section, sectionIndex);

    public static EditorSelection GraphNode(string nodeId, int sectionIndex) =>
        new(EditorSelectionKind.Section, sectionIndex)
        {
            NodeId = string.IsNullOrWhiteSpace(nodeId)
                ? throw new ArgumentException("Graph node ID is required.", nameof(nodeId))
                : nodeId
        };

    public static EditorSelection BankingKey(int keyIndex) =>
        new(EditorSelectionKind.BankingKey, ElementIndex: keyIndex);

    public static EditorSelection ControlPoint(int sectionIndex, int pointIndex) =>
        new(EditorSelectionKind.ControlPoint, sectionIndex, pointIndex);

    public static EditorSelection Sample(
        int sampleIndex,
        int sectionIndex,
        string? nodeId = null) =>
        new(EditorSelectionKind.Sample, sectionIndex, SampleIndex: sampleIndex)
        {
            NodeId = nodeId
        };
}
