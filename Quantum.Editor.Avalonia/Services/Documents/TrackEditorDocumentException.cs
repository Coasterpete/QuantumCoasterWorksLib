using Quantum.IO.TrackLayout.V2;

namespace Quantum.Editor.Avalonia.Services.Documents;

public sealed class TrackEditorDocumentException : Exception
{
    public TrackEditorDocumentException(
        string message,
        IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> diagnostics,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> Diagnostics { get; }
}
