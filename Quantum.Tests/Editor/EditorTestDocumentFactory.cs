using Quantum.Editor.Avalonia.Models;
using Quantum.Editor.Avalonia.Services;
using Quantum.Editor.Avalonia.Services.Documents;

namespace Quantum.Tests;

internal static class EditorTestDocumentFactory
{
    public static TrackEditorDocument ActivateShowcase(
        EditorWorkspace workspace,
        bool markDirty = false)
    {
        var document = TrackEditorDocument.Create(
            TrackPackageFactory.CreateShowcasePackage(),
            "Showcase Layout");
        if (markDirty)
        {
            document.MarkDirty();
        }

        workspace.Documents.SetActiveDocument(document);
        workspace.Select(EditorSelection.Track);
        return document;
    }
}
