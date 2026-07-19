using System.Globalization;
using Quantum.Editor.Avalonia.Models;
using Quantum.Editor.Avalonia.Services.Commands;
using Quantum.Editor.Avalonia.Services.Documents;
using Quantum.Editor.Avalonia.Services.Selection;
using Quantum.Editor.Avalonia.Services.UndoRedo;
using Quantum.Editor.Avalonia.Services.Viewport;
using Quantum.IO.TrackLayout.V2;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Editor.Avalonia.Services;

public sealed class EditorWorkspace
{
    private readonly TrackDocumentFileService fileService;
    private readonly TrackSamplingService samplingService;
    private TrackEditorDocument? observedDocument;
    private IReadOnlyList<EditorGraphNode> graphNodes = Array.Empty<EditorGraphNode>();
    private IReadOnlyList<EditorOutlinerNode> outlinerNodes = Array.Empty<EditorOutlinerNode>();
    private TrackViewportSnapshot viewportSnapshot = TrackViewportSnapshot.Empty;
    private string statusMessage = "Ready";

    public EditorWorkspace(
        DocumentService? documents = null,
        CommandService? commands = null,
        SelectionService? selection = null,
        UndoRedoService? undoRedo = null,
        ViewportService? viewport = null,
        TrackDocumentFileService? fileService = null,
        TrackSamplingService? samplingService = null)
    {
        Documents = documents ?? new DocumentService();
        Commands = commands ?? new CommandService();
        Selection = selection ?? new SelectionService();
        UndoRedo = undoRedo ?? new UndoRedoService();
        Viewport = viewport ?? new ViewportService();
        this.fileService = fileService ?? new TrackDocumentFileService();
        this.samplingService = samplingService ?? new TrackSamplingService();

        Documents.ActiveDocumentChanged += OnActiveDocumentChanged;
        Selection.SelectionChanged += OnSelectionChanged;
        UndoRedo.StateChanged += OnUndoRedoStateChanged;
        RegisterCommands();
    }

    public event EventHandler? WorkspaceChanged;

    public DocumentService Documents { get; }

    public CommandService Commands { get; }

    public SelectionService Selection { get; }

    public UndoRedoService UndoRedo { get; }

    public ViewportService Viewport { get; }

    public TrackEditorDocument? ActiveDocument => Documents.ActiveDocument as TrackEditorDocument;

    public EditorSelection? CurrentSelection =>
        Selection.SelectedItems.Count == 0
            ? null
            : Selection.SelectedItems[0] as EditorSelection;

    public IReadOnlyList<EditorGraphNode> GraphNodes => graphNodes;

    /// <summary>
    /// Preserved compatibility projection for non-graph callers. The Avalonia M157
    /// surface uses <see cref="GraphNodes"/>.
    /// </summary>
    public IReadOnlyList<EditorOutlinerNode> OutlinerNodes => outlinerNodes;

    public TrackViewportSnapshot ViewportSnapshot => viewportSnapshot;

    public string StatusMessage => statusMessage;

    public TrackEditorDocument NewDocument()
    {
        TrackEditorDocument document = TrackEditorDocument.Create(
            TrackPackageFactory.CreateShowcasePackage(),
            "Untitled Layout");
        document.MarkDirty();
        ActivateDocument(document, "Created a new graph-authoring document from the showcase layout.");
        return document;
    }

    public TrackEditorDocument OpenDocument(string filePath)
    {
        TrackEditorDocument document = fileService.Open(filePath);
        ActivateDocument(document, $"Opened {document.FilePath}.");
        return document;
    }

    public void SaveDocument(string? filePath = null)
    {
        TrackEditorDocument document = ActiveDocument ??
            throw new InvalidOperationException("There is no active track document to save.");
        fileService.Save(document, filePath);
        SetStatus($"Saved {document.FilePath}.");
    }

    public bool ApplyGraphEdit(
        string description,
        Func<TrackAuthoringGraph, TrackAuthoringGraph> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);

        TrackEditorDocument? document = ActiveDocument;
        TrackAuthoringGraph? beforeGraph = document?.Graph;
        if (document is null || beforeGraph is null)
        {
            SetStatus("The active document does not have an editable authoring graph.");
            return false;
        }

        TrackAuthoringGraph candidateGraph;
        try
        {
            candidateGraph = edit(beforeGraph) ??
                throw new InvalidOperationException("A graph edit cannot return null.");
        }
        catch (Exception exception) when (
            exception is ArgumentException ||
            exception is InvalidOperationException ||
            exception is NotSupportedException)
        {
            SetStatus("Edit rejected: " + exception.Message.Replace(Environment.NewLine, " "));
            return false;
        }

        if (ReferenceEquals(beforeGraph, candidateGraph))
        {
            SetStatus("No graph values changed.");
            return false;
        }

        TrackAuthoringGraphCompileResult candidateCompilation =
            TrackAuthoringGraphCompiler.Compile(candidateGraph);
        if (!candidateCompilation.Success || candidateCompilation.Compilation is null)
        {
            SetStatus("Edit rejected: " + FormatGraphDiagnostics(candidateCompilation.Diagnostics));
            return false;
        }

        try
        {
            string beforeJson = document.CapturePackageJson();
            string afterJson = document.CapturePackageJson(candidateGraph);
            if (string.Equals(beforeJson, afterJson, StringComparison.Ordinal))
            {
                SetStatus("No graph values changed.");
                return false;
            }

            UndoRedo.Execute(new TrackGraphSnapshotOperation(
                description,
                document,
                beforeGraph,
                candidateGraph));
            SetStatus(description + ".");
            return true;
        }
        catch (Exception exception) when (
            exception is TrackEditorDocumentException ||
            exception is ArgumentException ||
            exception is InvalidOperationException)
        {
            SetStatus("Edit rejected: " + exception.Message.Replace(Environment.NewLine, " "));
            return false;
        }
    }

    /// <summary>
    /// Compatibility bridge for existing callers. Package edits are re-imported as a
    /// candidate graph and enter history only as immutable graph snapshots. M157 does
    /// not permit this bridge to change non-graph ancillary state.
    /// </summary>
    public bool ApplyPackageEdit(
        string description,
        Action<TrackLayoutPackageV2Dto> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);

        TrackEditorDocument? document = ActiveDocument;
        TrackLayoutPackageV2Dto? package = document?.Package;
        if (document is null || package is null || document.AncillaryState is null)
        {
            SetStatus("The active document cannot be edited through the V2 compatibility adapter.");
            return false;
        }

        try
        {
            edit(package);
            TrackLayoutPackageV2GraphImportResult import =
                TrackLayoutPackageV2GraphAdapter.Import(package);
            if (!import.Success || import.Graph is null || import.AncillaryState is null)
            {
                SetStatus("Edit rejected: " + FormatPackageDiagnostics(import.Diagnostics));
                return false;
            }

            if (!AncillaryStateEquals(document.AncillaryState, import.AncillaryState))
            {
                SetStatus("Edit rejected: M157 package compatibility edits cannot change metadata or heartline state.");
                return false;
            }

            return ApplyGraphEdit(description, _ => import.Graph);
        }
        catch (Exception exception) when (
            exception is ArgumentException ||
            exception is InvalidOperationException ||
            exception is NotSupportedException)
        {
            SetStatus("Edit rejected: " + exception.Message.Replace(Environment.NewLine, " "));
            return false;
        }
    }

    public void Select(EditorSelection? selection)
    {
        if (selection is null)
        {
            Selection.Clear();
        }
        else
        {
            Selection.SetSelection(new object[] { selection });
        }
    }

    public bool UndoLast()
    {
        string? description = UndoRedo.UndoDescription;
        bool result = UndoRedo.Undo();
        if (result)
        {
            SetStatus("Undid " + description + ".");
        }

        return result;
    }

    public bool RedoLast()
    {
        string? description = UndoRedo.RedoDescription;
        bool result = UndoRedo.Redo();
        if (result)
        {
            SetStatus("Redid " + description + ".");
        }

        return result;
    }

    public void SetStatus(string message)
    {
        statusMessage = string.IsNullOrWhiteSpace(message) ? "Ready" : message;
        WorkspaceChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ActivateDocument(TrackEditorDocument document, string message)
    {
        TrackEditorDocument? previousDocument = ActiveDocument;
        UndoRedo.Clear();
        if (previousDocument != null)
        {
            Documents.CloseDocument(previousDocument);
        }

        Documents.SetActiveDocument(document);
        Select(EditorSelection.Track);
        SetStatus(message);
    }

    private void RegisterCommands()
    {
        Commands.Register(new EditorCommand(
            EditorCommandIds.NewDocument,
            _ => NewDocument()));
        Commands.Register(new EditorCommand(
            EditorCommandIds.OpenDocument,
            parameter => OpenDocument((string)parameter!),
            parameter => parameter is string path && !string.IsNullOrWhiteSpace(path)));
        Commands.Register(new EditorCommand(
            EditorCommandIds.SaveDocument,
            parameter => SaveDocument(parameter as string),
            _ => ActiveDocument?.CanSave == true));
        Commands.Register(new EditorCommand(
            EditorCommandIds.SaveDocumentAs,
            parameter => SaveDocument((string)parameter!),
            parameter => ActiveDocument?.CanSave == true &&
                         parameter is string path &&
                         !string.IsNullOrWhiteSpace(path)));
        Commands.Register(new EditorCommand(
            EditorCommandIds.Undo,
            _ => UndoLast(),
            _ => UndoRedo.CanUndo));
        Commands.Register(new EditorCommand(
            EditorCommandIds.Redo,
            _ => RedoLast(),
            _ => UndoRedo.CanRedo));
        Commands.Register(new EditorCommand(
            EditorCommandIds.Select,
            parameter => Select((EditorSelection?)parameter),
            parameter => parameter is EditorSelection));
    }

    private void OnActiveDocumentChanged(object? sender, EventArgs eventArgs)
    {
        if (observedDocument != null)
        {
            observedDocument.Changed -= OnDocumentChanged;
        }

        observedDocument = ActiveDocument;
        if (observedDocument != null)
        {
            observedDocument.Changed += OnDocumentChanged;
        }

        RefreshDocumentProjection();
    }

    private void OnDocumentChanged(object? sender, EventArgs eventArgs)
    {
        RefreshDocumentProjection();
    }

    private void OnSelectionChanged(object? sender, EventArgs eventArgs)
    {
        WorkspaceChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnUndoRedoStateChanged(object? sender, EventArgs eventArgs)
    {
        WorkspaceChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshDocumentProjection()
    {
        TrackEditorDocument? document = ActiveDocument;
        if (document?.Graph is null || document.GraphCompileResult is null)
        {
            graphNodes = Array.Empty<EditorGraphNode>();
            outlinerNodes = Array.Empty<EditorOutlinerNode>();
            viewportSnapshot = TrackViewportSnapshot.Empty;
        }
        else
        {
            graphNodes = BuildGraphNodes(document.GraphCompileResult.OrderedNodes);
            outlinerNodes = BuildOutliner(document);
            viewportSnapshot = samplingService.Sample(document);
        }

        WorkspaceChanged?.Invoke(this, EventArgs.Empty);
    }

    private static IReadOnlyList<EditorGraphNode> BuildGraphNodes(
        IReadOnlyList<TrackAuthoringGraphNode> orderedNodes)
    {
        var result = new EditorGraphNode[orderedNodes.Count];
        for (int routeIndex = 0; routeIndex < orderedNodes.Count; routeIndex++)
        {
            GeometricSectionDefinition section = orderedNodes[routeIndex].Section;
            result[routeIndex] = new EditorGraphNode(
                section.Id,
                routeIndex,
                DescribeSectionKind(section),
                DescribeSectionSummary(section));
        }

        return result;
    }

    private static IReadOnlyList<EditorOutlinerNode> BuildOutliner(TrackEditorDocument document)
    {
        IReadOnlyList<TrackAuthoringGraphNode> route = document.GraphCompileResult!.OrderedNodes;
        var sectionNodes = new List<EditorOutlinerNode>(route.Count);
        for (int sectionIndex = 0; sectionIndex < route.Count; sectionIndex++)
        {
            GeometricSectionDefinition section = route[sectionIndex].Section;
            var controlPointNodes = new List<EditorOutlinerNode>();
            if (section is SpatialSectionDefinition spatial)
            {
                for (int pointIndex = 0; pointIndex < spatial.ControlPoints.Count; pointIndex++)
                {
                    controlPointNodes.Add(new EditorOutlinerNode(
                        $"Control point {pointIndex}",
                        EditorSelection.ControlPoint(sectionIndex, pointIndex)));
                }
            }

            sectionNodes.Add(new EditorOutlinerNode(
                $"{sectionIndex + 1}. {section.Id} | {DescribeSectionKind(section)} | {section.Length:F1} m",
                EditorSelection.GraphNode(section.Id, sectionIndex),
                controlPointNodes));
        }

        var bankingNodes = new List<EditorOutlinerNode>();
        IReadOnlyList<BankingProfileKey> bankingKeys =
            document.Graph!.Banking?.Keys ?? Array.Empty<BankingProfileKey>();
        for (int keyIndex = 0; keyIndex < bankingKeys.Count; keyIndex++)
        {
            BankingProfileKey key = bankingKeys[keyIndex];
            bankingNodes.Add(new EditorOutlinerNode(
                $"Key {keyIndex} | {key.Distance:F1} m | {key.RollRadians * 180.0 / System.Math.PI:F1} deg",
                EditorSelection.BankingKey(keyIndex)));
        }

        HeartlineOffset? heartline = document.AncillaryState?.HeartlineOffset;
        return new[]
        {
            new EditorOutlinerNode(
                document.DisplayName,
                EditorSelection.Track,
                new[]
                {
                    new EditorOutlinerNode($"Sections ({sectionNodes.Count})", children: sectionNodes),
                    new EditorOutlinerNode($"Banking ({bankingNodes.Count} keys)", children: bankingNodes),
                    new EditorOutlinerNode(
                        !heartline.HasValue
                            ? "Heartline: none"
                            : $"Heartline: N {heartline.Value.NormalOffsetMeters:F2} m / " +
                              $"B {heartline.Value.LateralOffsetMeters:F2} m")
                })
        };
    }

    private static string DescribeSectionKind(GeometricSectionDefinition section)
    {
        return section switch
        {
            StraightSectionDefinition => "Straight",
            ConstantCurvatureSectionDefinition => "Constant Curvature",
            CurvatureTransitionSectionDefinition => "Curvature Transition",
            SpatialSectionDefinition => "Spatial",
            _ => section.GetType().Name
        };
    }

    private static string DescribeSectionSummary(GeometricSectionDefinition section)
    {
        string length = section.Length.ToString("F1", CultureInfo.InvariantCulture);
        return section switch
        {
            ConstantCurvatureSectionDefinition arc =>
                $"Length {length} m | Radius {arc.Radius.ToString("F2", CultureInfo.InvariantCulture)} m",
            CurvatureTransitionSectionDefinition transition =>
                $"Length {length} m | k {transition.StartCurvature.ToString("F4", CultureInfo.InvariantCulture)}" +
                $" -> {transition.EndCurvature.ToString("F4", CultureInfo.InvariantCulture)} 1/m",
            SpatialSectionDefinition spatial =>
                $"Length {length} m | Degree {spatial.Degree} | {spatial.ControlPoints.Count} points",
            _ => $"Length {length} m"
        };
    }

    private static bool AncillaryStateEquals(
        TrackLayoutPackageV2GraphAncillaryState first,
        TrackLayoutPackageV2GraphAncillaryState second)
    {
        return string.Equals(first.Contract, second.Contract, StringComparison.Ordinal) &&
               first.Version == second.Version &&
               string.Equals(first.Units, second.Units, StringComparison.Ordinal) &&
               string.Equals(first.SourceName, second.SourceName, StringComparison.Ordinal) &&
               string.Equals(first.LayoutId, second.LayoutId, StringComparison.Ordinal) &&
               HeartlineEquals(first.HeartlineOffset, second.HeartlineOffset);
    }

    private static bool HeartlineEquals(HeartlineOffset? first, HeartlineOffset? second)
    {
        if (!first.HasValue || !second.HasValue)
        {
            return first.HasValue == second.HasValue;
        }

        return first.Value.NormalOffsetMeters == second.Value.NormalOffsetMeters &&
               first.Value.LateralOffsetMeters == second.Value.LateralOffsetMeters;
    }

    private static string FormatGraphDiagnostics(
        IReadOnlyList<TrackAuthoringGraphDiagnostic> diagnostics)
    {
        return diagnostics.Count == 0
            ? "Graph compilation failed without diagnostics."
            : string.Join(
                " ",
                diagnostics.Select(diagnostic =>
                    $"{diagnostic.Code}: {diagnostic.Message}"));
    }

    private static string FormatPackageDiagnostics(
        IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> diagnostics)
    {
        return diagnostics.Count == 0
            ? "Package import failed without diagnostics."
            : string.Join(
                " ",
                diagnostics.Select(diagnostic =>
                    $"{diagnostic.Code} at {diagnostic.Path}: {diagnostic.Message}"));
    }
}
