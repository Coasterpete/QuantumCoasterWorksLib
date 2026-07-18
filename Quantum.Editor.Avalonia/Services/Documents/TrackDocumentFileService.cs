using System.Text;
using Quantum.IO.TrackLayout.V2;

namespace Quantum.Editor.Avalonia.Services.Documents;

public sealed class TrackDocumentFileService
{
    public TrackEditorDocument Open(string filePath)
    {
        string resolvedPath = ResolvePath(filePath);
        string json = File.ReadAllText(resolvedPath, Encoding.UTF8);
        TrackLayoutPackageV2Dto package = TrackLayoutPackageV2Json.Deserialize(json);

        TrackEditorDocument document = TrackEditorDocument.Create(
            package,
            Path.GetFileNameWithoutExtension(resolvedPath),
            resolvedPath);
        document.MarkClean();
        return document;
    }

    public void Save(TrackEditorDocument document, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        string resolvedPath = ResolvePath(filePath ?? document.FilePath);
        string? directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(resolvedPath, document.CapturePackageJson(), new UTF8Encoding(false));
        document.UpdateFilePath(resolvedPath);
        document.MarkClean();
    }

    private static string ResolvePath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A Track Layout Package V2 file path is required.", nameof(filePath));
        }

        return Path.GetFullPath(filePath);
    }
}
