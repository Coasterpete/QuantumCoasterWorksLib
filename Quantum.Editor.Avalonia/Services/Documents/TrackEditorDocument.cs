using Quantum.Track;
using Quantum.Track.Authoring;
using Quantum.IO.TrackLayout.V2;

namespace Quantum.Editor.Avalonia.Services.Documents;

public sealed class TrackEditorDocument : IEditorDocument
{
    private TrackLayoutPackageV2Dto? package;

    public TrackEditorDocument(TrackDocument trackDocument, string displayName)
    {
        TrackDocument = trackDocument ?? throw new ArgumentNullException(nameof(trackDocument));
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? throw new ArgumentException("Document display name is required.", nameof(displayName))
            : displayName;
    }

    private TrackEditorDocument(
        TrackLayoutPackageV2Dto package,
        TrackAuthoringCompilation compilation,
        string displayName,
        string? filePath)
        : this(compilation.Document, displayName)
    {
        this.package = package;
        Compilation = compilation;
        FilePath = filePath;
    }

    public event EventHandler? Changed;

    public string DisplayName { get; private set; }

    public bool IsDirty { get; private set; }

    public TrackDocument TrackDocument { get; private set; }

    public TrackLayoutPackageV2Dto? Package => package;

    public TrackAuthoringCompilation? Compilation { get; private set; }

    public string? FilePath { get; private set; }

    public bool CanSave => package != null;

    public static TrackEditorDocument Create(
        TrackLayoutPackageV2Dto package,
        string displayName,
        string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(package);

        TrackLayoutPackageV2Dto packageCopy = ClonePackage(package);
        TrackAuthoringCompilation compilation = CompilePackage(packageCopy);
        return new TrackEditorDocument(packageCopy, compilation, displayName, filePath);
    }

    public string CapturePackageJson()
    {
        if (package is null)
        {
            throw new InvalidOperationException("This editor document does not have a persistable Track Layout Package V2 model.");
        }

        return TrackLayoutPackageV2Json.Serialize(package, indented: true);
    }

    public void ReplacePackageJson(string json, bool markDirty = true)
    {
        ArgumentNullException.ThrowIfNull(json);

        TrackLayoutPackageV2Dto replacement = TrackLayoutPackageV2Json.Deserialize(json);
        TrackAuthoringCompilation compilation = CompilePackage(replacement);

        package = replacement;
        Compilation = compilation;
        TrackDocument = compilation.Document;
        IsDirty = markDirty;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateFilePath(string filePath)
    {
        FilePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("Document file path is required.", nameof(filePath))
            : Path.GetFullPath(filePath);
        DisplayName = Path.GetFileNameWithoutExtension(FilePath);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MarkDirty()
    {
        if (IsDirty)
        {
            return;
        }

        IsDirty = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MarkClean()
    {
        if (!IsDirty)
        {
            return;
        }

        IsDirty = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static TrackLayoutPackageV2Dto ClonePackage(TrackLayoutPackageV2Dto source)
    {
        return TrackLayoutPackageV2Json.Deserialize(
            TrackLayoutPackageV2Json.Serialize(source));
    }

    private static TrackAuthoringCompilation CompilePackage(TrackLayoutPackageV2Dto package)
    {
        TrackLayoutPackageV2ImportResult import = TrackLayoutPackageV2Mapper.Import(package);
        if (!import.Success || import.Definition is null)
        {
            string details = import.Diagnostics.Count == 0
                ? "The Track Layout Package V2 import failed without diagnostics."
                : string.Join(
                    Environment.NewLine,
                    import.Diagnostics.Select(diagnostic =>
                        $"{diagnostic.Code} at {diagnostic.Path}: {diagnostic.Message}"));
            throw new TrackEditorDocumentException(details, import.Diagnostics);
        }

        try
        {
            return TrackAuthoringDocumentBuilder.Compile(import.Definition);
        }
        catch (Exception exception) when (
            exception is ArgumentException ||
            exception is InvalidOperationException ||
            exception is NotSupportedException)
        {
            throw new TrackEditorDocumentException(
                "The imported layout could not be compiled: " + exception.Message,
                import.Diagnostics,
                exception);
        }
    }
}
