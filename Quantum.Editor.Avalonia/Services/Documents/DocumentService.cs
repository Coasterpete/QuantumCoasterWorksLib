namespace Quantum.Editor.Avalonia.Services.Documents;

public sealed class DocumentService : IDocumentService
{
    private readonly List<IEditorDocument> openDocuments = new();

    public event EventHandler? ActiveDocumentChanged;

    public IReadOnlyList<IEditorDocument> OpenDocuments => openDocuments;

    public IEditorDocument? ActiveDocument { get; private set; }

    public void SetActiveDocument(IEditorDocument? document)
    {
        if (document != null && !openDocuments.Contains(document))
        {
            openDocuments.Add(document);
        }

        if (ReferenceEquals(ActiveDocument, document))
        {
            return;
        }

        ActiveDocument = document;
        ActiveDocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void CloseDocument(IEditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (!openDocuments.Remove(document))
        {
            return;
        }

        if (ReferenceEquals(ActiveDocument, document))
        {
            ActiveDocument = openDocuments.Count == 0 ? null : openDocuments[^1];
            ActiveDocumentChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
