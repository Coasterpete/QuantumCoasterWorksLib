namespace Quantum.Editor.Avalonia.Services.Documents;

public interface IDocumentService
{
    event EventHandler? ActiveDocumentChanged;

    IReadOnlyList<IEditorDocument> OpenDocuments { get; }

    IEditorDocument? ActiveDocument { get; }

    void SetActiveDocument(IEditorDocument? document);

    void CloseDocument(IEditorDocument document);
}
