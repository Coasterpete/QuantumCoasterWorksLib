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
    public static EditorSelection Track { get; } = new(EditorSelectionKind.Track);

    public static EditorSelection Section(int sectionIndex) =>
        new(EditorSelectionKind.Section, sectionIndex);

    public static EditorSelection BankingKey(int keyIndex) =>
        new(EditorSelectionKind.BankingKey, ElementIndex: keyIndex);

    public static EditorSelection ControlPoint(int sectionIndex, int pointIndex) =>
        new(EditorSelectionKind.ControlPoint, sectionIndex, pointIndex);

    public static EditorSelection Sample(int sampleIndex, int sectionIndex) =>
        new(EditorSelectionKind.Sample, sectionIndex, SampleIndex: sampleIndex);
}
