namespace Quantum.Editor.Avalonia.Services.Documents;

public interface IDocumentService
{
    IEditorDocument? ActiveDocument { get; }

    void SetActiveDocument(IEditorDocument? document);
}
