namespace Quantum.Editor.Avalonia.Services.Documents;

public sealed class DocumentService : IDocumentService
{
    public IEditorDocument? ActiveDocument { get; private set; }

    public void SetActiveDocument(IEditorDocument? document)
    {
        ActiveDocument = document;
    }
}
