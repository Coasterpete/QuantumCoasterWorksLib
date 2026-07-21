using Quantum.IO.TrackLayout.V2;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Editor.Avalonia.Services.Documents;

public sealed class TrackEditorDocument : IEditorDocument
{
    private TrackAuthoringGraph? graph;
    private TrackLayoutPackageV2GraphAncillaryState? ancillaryState;
    private string? cleanPackageJson;
    private bool explicitlyDirty;

    public TrackEditorDocument(TrackDocument trackDocument, string displayName)
    {
        TrackDocument = trackDocument ?? throw new ArgumentNullException(nameof(trackDocument));
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? throw new ArgumentException("Document display name is required.", nameof(displayName))
            : displayName;
    }

    private TrackEditorDocument(
        TrackAuthoringGraph graph,
        TrackLayoutPackageV2GraphAncillaryState ancillaryState,
        TrackAuthoringGraphCompileResult? graphCompileResult,
        string displayName,
        string? filePath)
        : this(graphCompileResult?.Compilation?.Document ?? new TrackDocument(), displayName)
    {
        this.graph = graph;
        this.ancillaryState = ancillaryState;
        GraphCompileResult = graphCompileResult;
        Compilation = graphCompileResult?.Compilation;
        FilePath = filePath;
        cleanPackageJson = CanSave ? CapturePackageJson() : null;
    }

    public event EventHandler? Changed;

    public string DisplayName { get; private set; }

    public bool IsDirty { get; private set; }

    public TrackDocument TrackDocument { get; private set; }

    /// <summary>
    /// Authoritative editor-facing authoring graph. Section DTOs are produced only
    /// when the compatibility package snapshot is requested.
    /// </summary>
    public TrackAuthoringGraph? Graph => graph;

    public TrackLayoutPackageV2GraphAncillaryState? AncillaryState => ancillaryState;

    public TrackAuthoringGraphCompileResult? GraphCompileResult { get; private set; }

    /// <summary>
    /// Compatibility snapshot generated from the active graph; callers do not receive
    /// mutable editor-owned state.
    /// </summary>
    public TrackLayoutPackageV2Dto? Package =>
        !CanSave
            ? null
            : ExportPackage(graph!, ancillaryState!);

    public TrackAuthoringCompilation? Compilation { get; private set; }

    public string? FilePath { get; private set; }

    public bool CanSave =>
        graph != null &&
        graph.Nodes.Count != 0 &&
        ancillaryState != null &&
        Compilation != null;

    public bool IsEmpty => graph?.Nodes.Count == 0;

    public static TrackEditorDocument CreateEmpty(
        string displayName,
        string? sourceName = null)
    {
        var graph = new TrackAuthoringGraph(
            Array.Empty<TrackAuthoringGraphNode>(),
            Array.Empty<TrackAuthoringGraphEdge>());
        var ancillaryState = new TrackLayoutPackageV2GraphAncillaryState(
            TrackLayoutPackageV2Dto.ContractName,
            TrackLayoutPackageV2Dto.ContractVersion,
            "meters",
            sourceName,
            layoutId: null,
            heartlineOffset: null);
        return new TrackEditorDocument(
            graph,
            ancillaryState,
            graphCompileResult: null,
            displayName,
            filePath: null);
    }

    public static TrackEditorDocument Create(
        TrackLayoutPackageV2Dto package,
        string displayName,
        string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(package);

        TrackLayoutPackageV2GraphImportResult import =
            TrackLayoutPackageV2GraphAdapter.Import(package);
        if (!import.Success || import.Graph is null || import.AncillaryState is null)
        {
            throw CreatePackageImportException(import.Diagnostics);
        }

        TrackAuthoringGraphCompileResult graphCompileResult =
            TrackAuthoringGraphCompiler.Compile(import.Graph);
        if (!graphCompileResult.Success || graphCompileResult.Compilation is null)
        {
            throw new TrackEditorDocumentException(
                FormatGraphDiagnostics(
                    "The imported layout graph could not be compiled",
                    graphCompileResult.Diagnostics),
                import.Diagnostics);
        }

        return new TrackEditorDocument(
            import.Graph,
            import.AncillaryState,
            graphCompileResult,
            displayName,
            filePath);
    }

    public string CapturePackageJson()
    {
        if (!CanSave)
        {
            throw new InvalidOperationException(
                "A non-empty successfully compiled route is required before saving Track Layout Package V2.");
        }

        return SerializePackage(graph!, ancillaryState!);
    }

    internal string CapturePackageJson(TrackAuthoringGraph candidateGraph)
    {
        ArgumentNullException.ThrowIfNull(candidateGraph);
        if (ancillaryState is null)
        {
            throw new InvalidOperationException(
                "This editor document does not have Track Layout Package V2 ancillary state.");
        }

        return SerializePackage(candidateGraph, ancillaryState);
    }

    public void ReplaceGraph(TrackAuthoringGraph candidateGraph)
    {
        ArgumentNullException.ThrowIfNull(candidateGraph);
        if (graph is null || ancillaryState is null)
        {
            throw new InvalidOperationException(
                "This editor document does not have an editable authoring graph.");
        }

        TrackAuthoringGraphRouteResult route =
            TrackAuthoringGraphRouteValidator.Validate(candidateGraph);
        if (!route.Success)
        {
            throw new InvalidOperationException(FormatGraphDiagnostics(
                "The candidate authoring route was rejected",
                route.Diagnostics));
        }

        if (candidateGraph.Nodes.Count == 0)
        {
            bool emptyIsDirty = explicitlyDirty || cleanPackageJson != null;
            CommitEmptyGraph(candidateGraph, emptyIsDirty);
            return;
        }

        TrackAuthoringGraphCompileResult candidateCompilation =
            TrackAuthoringGraphCompiler.Compile(candidateGraph);
        if (!candidateCompilation.Success || candidateCompilation.Compilation is null)
        {
            throw new InvalidOperationException(FormatGraphDiagnostics(
                "The candidate authoring graph was rejected",
                candidateCompilation.Diagnostics));
        }

        string candidatePackageJson = CapturePackageJson(candidateGraph);
        bool candidateIsDirty = explicitlyDirty ||
            cleanPackageJson is null ||
            !string.Equals(cleanPackageJson, candidatePackageJson, StringComparison.Ordinal);
        CommitCompiledGraph(candidateGraph, candidateCompilation, candidateIsDirty);
    }

    public void ReplacePackageJson(string json, bool markDirty = true)
    {
        ArgumentNullException.ThrowIfNull(json);

        TrackLayoutPackageV2Dto replacement = TrackLayoutPackageV2Json.Deserialize(json);
        TrackLayoutPackageV2GraphImportResult import =
            TrackLayoutPackageV2GraphAdapter.Import(replacement);
        if (!import.Success || import.Graph is null || import.AncillaryState is null)
        {
            throw CreatePackageImportException(import.Diagnostics);
        }

        TrackAuthoringGraphCompileResult candidateCompilation =
            TrackAuthoringGraphCompiler.Compile(import.Graph);
        if (!candidateCompilation.Success || candidateCompilation.Compilation is null)
        {
            throw new TrackEditorDocumentException(
                FormatGraphDiagnostics(
                    "The replacement layout graph could not be compiled",
                    candidateCompilation.Diagnostics),
                import.Diagnostics);
        }

        string replacementPackageJson = SerializePackage(import.Graph, import.AncillaryState);
        graph = import.Graph;
        ancillaryState = import.AncillaryState;
        GraphCompileResult = candidateCompilation;
        Compilation = candidateCompilation.Compilation;
        TrackDocument = candidateCompilation.Compilation.Document;

        if (markDirty)
        {
            explicitlyDirty = true;
            IsDirty = true;
        }
        else
        {
            explicitlyDirty = false;
            cleanPackageJson = replacementPackageJson;
            IsDirty = false;
        }

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
        explicitlyDirty = true;
        if (IsDirty)
        {
            return;
        }

        IsDirty = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MarkClean()
    {
        explicitlyDirty = false;
        cleanPackageJson = CanSave ? CapturePackageJson() : null;
        if (!IsDirty)
        {
            return;
        }

        IsDirty = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void CommitCompiledGraph(
        TrackAuthoringGraph candidateGraph,
        TrackAuthoringGraphCompileResult candidateCompilation,
        bool candidateIsDirty)
    {
        graph = candidateGraph;
        GraphCompileResult = candidateCompilation;
        Compilation = candidateCompilation.Compilation;
        TrackDocument = candidateCompilation.Compilation!.Document;
        IsDirty = candidateIsDirty;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void CommitEmptyGraph(
        TrackAuthoringGraph candidateGraph,
        bool candidateIsDirty)
    {
        graph = candidateGraph;
        GraphCompileResult = null;
        Compilation = null;
        TrackDocument = new TrackDocument();
        IsDirty = candidateIsDirty;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string SerializePackage(
        TrackAuthoringGraph sourceGraph,
        TrackLayoutPackageV2GraphAncillaryState sourceAncillaryState)
    {
        return TrackLayoutPackageV2Json.Serialize(
            ExportPackage(sourceGraph, sourceAncillaryState),
            indented: true);
    }

    private static TrackLayoutPackageV2Dto ExportPackage(
        TrackAuthoringGraph sourceGraph,
        TrackLayoutPackageV2GraphAncillaryState sourceAncillaryState)
    {
        TrackLayoutPackageV2GraphExportResult export =
            TrackLayoutPackageV2GraphAdapter.Export(sourceGraph, sourceAncillaryState);
        if (!export.Success || export.Package is null)
        {
            if (export.GraphDiagnostics.Count != 0)
            {
                throw new InvalidOperationException(FormatGraphDiagnostics(
                    "The authoring graph could not be exported",
                    export.GraphDiagnostics));
            }

            string details = export.PackageDiagnostics.Count == 0
                ? "The Track Layout Package V2 export failed without diagnostics."
                : string.Join(
                    Environment.NewLine,
                    export.PackageDiagnostics.Select(diagnostic =>
                        $"{diagnostic.Code} at {diagnostic.Path}: {diagnostic.Message}"));
            throw new TrackEditorDocumentException(details, export.PackageDiagnostics);
        }

        return export.Package;
    }

    private static TrackEditorDocumentException CreatePackageImportException(
        IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> diagnostics)
    {
        string details = diagnostics.Count == 0
            ? "The Track Layout Package V2 import failed without diagnostics."
            : string.Join(
                Environment.NewLine,
                diagnostics.Select(diagnostic =>
                    $"{diagnostic.Code} at {diagnostic.Path}: {diagnostic.Message}"));
        return new TrackEditorDocumentException(details, diagnostics);
    }

    private static string FormatGraphDiagnostics(
        string prefix,
        IReadOnlyList<TrackAuthoringGraphDiagnostic> diagnostics)
    {
        return diagnostics.Count == 0
            ? prefix + " without diagnostics."
            : prefix + ": " + string.Join(
                " ",
                diagnostics.Select(diagnostic =>
                    $"{diagnostic.Code}: {diagnostic.Message}"));
    }
}
