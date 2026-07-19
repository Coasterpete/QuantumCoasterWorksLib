namespace Quantum.Editor.Avalonia.Controls;

public sealed class SectionPointerChangedEventArgs : EventArgs
{
    public SectionPointerChangedEventArgs(int? sectionIndex)
    {
        SectionIndex = sectionIndex;
    }

    public int? SectionIndex { get; }
}
