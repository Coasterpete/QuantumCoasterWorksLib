namespace Quantum.Editor.Avalonia.Services.Documents;

public interface IEditorDocument
{
    string DisplayName { get; }

    bool IsDirty { get; }
}
