using Quantum.Track;

namespace Quantum.Editor.Avalonia.Services.Documents;

public sealed class TrackEditorDocument : IEditorDocument
{
    public TrackEditorDocument(TrackDocument trackDocument, string displayName)
    {
        TrackDocument = trackDocument ?? throw new ArgumentNullException(nameof(trackDocument));
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? throw new ArgumentException("Document display name is required.", nameof(displayName))
            : displayName;
    }

    public string DisplayName { get; }

    public bool IsDirty { get; private set; }

    public TrackDocument TrackDocument { get; }

    public void MarkDirty()
    {
        IsDirty = true;
    }

    public void MarkClean()
    {
        IsDirty = false;
    }
}
