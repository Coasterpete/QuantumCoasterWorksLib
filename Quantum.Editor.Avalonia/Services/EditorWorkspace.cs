using System.Globalization;
using Quantum.Application.Authoring;
using Quantum.Editor.Avalonia.Models;
using Quantum.Editor.Avalonia.Services.Authoring;
using Quantum.Editor.Avalonia.Services.Commands;
using Quantum.Editor.Avalonia.Services.Documents;
using Quantum.Editor.Avalonia.Services.Selection;
using Quantum.Editor.Avalonia.Services.UndoRedo;
using Quantum.Editor.Avalonia.Services.Viewport;
using Quantum.IO.TrackLayout.V2;


using Quantum.Track;

using Quantum.Track.Authoring;

namespace Quantum.Editor.Avalonia.Services;

public sealed class EditorWorkspace : IDisposable
{
    private readonly TrackDocumentFileService fileService;
    private readonly TrackSamplingService samplingService;
    private TrackEditorDocument? observedDocument;
    private TrackAuthoringSession? authoringSession;
    private TrackAuthoringEvaluationCoordinator? evaluationCoordinator;
    private StraightLengthEditState? straightLengthEdit;
    private bool applyingSessionCommitToDocument;
    private bool disposed;
    private long straightLengthRawPointerUpdates;
    private long straightLengthAcceptedPreviews;
    private TimeSpan straightLengthFinalCommitWait;

    private TrackAuthoringCompilation? projectedCompilation;

    private IReadOnlyList<EditorGraphNode> graphNodes = Array.Empty<EditorGraphNode>();

    private IReadOnlyList<EditorOutlinerNode> outlinerNodes = Array.Empty<EditorOutlinerNode>();
    private TrackViewportSnapshot viewportSnapshot = TrackViewportSnapshot.Empty;
    private EngineeringSnapshot? engineeringSnapshot;
    private EngineeringStationCursor? stationCursor;
    private int hoveredSectionIndex = -1;
    private long compilationRevision;
    private long snapshotRevision;
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

    public event EventHandler? StationCursorChanged;

    public event EventHandler? SectionHighlightChanged;

    public DocumentService Documents { get; }

    public CommandService Commands { get; }

    public SelectionService Selection { get; }

    public UndoRedoService UndoRedo { get; }

    public ViewportService Viewport { get; }

    public TrackEditorDocument? ActiveDocument => Documents.ActiveDocument as TrackEditorDocument;

    public TrackAuthoringSession? AuthoringSession => authoringSession;

    public TrackAuthoringEvaluationCoordinator? EvaluationCoordinator => evaluationCoordinator;

    public PreparedTrackGraphState? PresentedState => authoringSession?.PresentedState;

    public StraightLengthEditState? StraightLengthEdit => straightLengthEdit;

    public bool IsInteractiveEditActive => straightLengthEdit is not null;

    public EditorSelection? CurrentSelection =>
        Selection.SelectedItems.Count == 0
            ? null
            : Selection.SelectedItems[0] as EditorSelection;

    public IReadOnlyList<EditorGraphNode> GraphNodes => graphNodes;

    /// <summary>
    /// Preserved compatibility projection for non-graph callers. The Avalonia
    /// surface uses <see cref="GraphNodes"/>.
    /// </summary>
    public IReadOnlyList<EditorOutlinerNode> OutlinerNodes => outlinerNodes;

    public TrackViewportSnapshot ViewportSnapshot => viewportSnapshot;

    public EngineeringSnapshot? EngineeringSnapshot => engineeringSnapshot;

    public EngineeringStationCursor? StationCursor => stationCursor;

    /// <summary>
    /// The one effective section highlight shared by every editor surface.
    /// A transient pointer hover takes precedence over the persistent selection.
    /// </summary>
    public int HighlightedSectionIndex => hoveredSectionIndex >= 0
        ? hoveredSectionIndex
        : CurrentSelection?.SectionIndex ?? -1;

    public string StatusMessage => statusMessage;

    public TrackEditorDocument NewDocument()
    {
        CancelStraightLengthEdit();
        TrackEditorDocument document = TrackEditorDocument.CreateEmpty(
            "Untitled Layout",
            "Untitled Layout");
        document.MarkDirty();
        ActivateDocument(document, "Created a new empty track-authoring document.");
        return document;
    }

    public TrackEditorDocument OpenDocument(string filePath)
    {
        CancelStraightLengthEdit();
        TrackEditorDocument document = fileService.Open(filePath);
        ActivateDocument(document, $"Opened {document.FilePath}.");
        return document;
    }

    public void SaveDocument(string? filePath = null)
    {
        ThrowIfInteractiveEditActive("Save is disabled while a live edit is active.");
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

        if (IsInteractiveEditActive)
        {
            SetStatus("Source edits are disabled while a live edit is active.");
            return false;
        }

        TrackEditorDocument? document = ActiveDocument;
        if (document?.Graph is null)
        {
            SetStatus("The active document does not have an editable authoring graph.");
            return false;
        }

        PreparedTrackGraphState beforeState = document.CaptureGraphState();
        TrackAuthoringGraph beforeGraph = beforeState.Graph;

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

        TrackAuthoringGraphRouteResult route =
            TrackAuthoringGraphRouteValidator.Validate(candidateGraph);
        if (!route.Success)
        {
            SetStatus("Edit rejected: " + FormatGraphDiagnostics(route.Diagnostics));
            return false;
        }

        TrackAuthoringGraphCompileResult? candidateCompilation = null;
        if (candidateGraph.Nodes.Count != 0)
        {
            candidateCompilation =
                TrackAuthoringGraphCompiler.Compile(candidateGraph);
            if (!candidateCompilation.Success || candidateCompilation.Compilation is null)
            {
                SetStatus("Edit rejected: " + FormatGraphDiagnostics(candidateCompilation.Diagnostics));
                return false;
            }
        }

        try
        {
            PreparedTrackGraphState afterState = document.PrepareGraphState(
                candidateGraph,
                candidateCompilation);
            if (beforeState.CanonicalPackageJson is not null &&
                afterState.CanonicalPackageJson is not null)
            {
                if (string.Equals(
                    beforeState.CanonicalPackageJson,
                    afterState.CanonicalPackageJson,
                    StringComparison.Ordinal))
                {
                    SetStatus("No graph values changed.");
                    return false;
                }
            }

            UndoRedo.Execute(new TrackGraphSnapshotOperation(
                description,
                document,
                beforeState,
                afterState));
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

    public bool AddSection(TrackAuthoringSectionDefinition section)
    {
        ArgumentNullException.ThrowIfNull(section);
        bool applied = ApplyGraphEdit(
            $"Add section {section.Id}",
            graph => TrackAuthoringGraphOperations.Append(graph, section));
        if (applied)
        {
            SelectGraphNode(section.Id);
        }

        return applied;
    }

    public bool InsertSectionBefore(
        string anchorNodeId,
        TrackAuthoringSectionDefinition section)
    {
        ArgumentNullException.ThrowIfNull(section);
        bool applied = ApplyGraphEdit(
            $"Insert section {section.Id} before {anchorNodeId}",
            graph => TrackAuthoringGraphOperations.InsertBefore(
                graph,
                anchorNodeId,
                section));
        if (applied)
        {
            SelectGraphNode(section.Id);
        }

        return applied;
    }

    public bool InsertSectionAfter(
        string anchorNodeId,
        TrackAuthoringSectionDefinition section)
    {
        ArgumentNullException.ThrowIfNull(section);
        bool applied = ApplyGraphEdit(
            $"Insert section {section.Id} after {anchorNodeId}",
            graph => TrackAuthoringGraphOperations.InsertAfter(
                graph,
                anchorNodeId,
                section));
        if (applied)
        {
            SelectGraphNode(section.Id);
        }

        return applied;
    }

    public bool DeleteSection(string nodeId)
    {
        TrackAuthoringGraph? graph = ActiveDocument?.Graph;
        if (graph is null)
        {
            SetStatus("The active document does not have an editable authoring graph.");
            return false;
        }

        TrackAuthoringGraphRouteResult route =
            TrackAuthoringGraphRouteValidator.Validate(graph);
        int deletedIndex = route.OrderedNodes
            .Select((node, index) => (node, index))
            .Where(item => string.Equals(item.node.Id, nodeId, StringComparison.Ordinal))
            .Select(item => item.index)
            .DefaultIfEmpty(-1)
            .Single();

        bool applied = ApplyGraphEdit(
            $"Delete section {nodeId}",
            candidate => TrackAuthoringGraphOperations.Delete(candidate, nodeId));
        if (!applied)
        {
            return false;
        }

        IReadOnlyList<EditorGraphNode> remaining = GraphNodes;
        if (remaining.Count == 0)
        {
            Select(EditorSelection.Track);
        }
        else
        {
            int fallbackIndex = System.Math.Min(deletedIndex, remaining.Count - 1);
            Select(remaining[fallbackIndex].Selection);
        }

        return true;
    }

    public bool MoveSectionUp(string nodeId)
    {
        int index = FindGraphNodeIndex(nodeId);
        if (index <= 0)
        {
            SetStatus(index < 0
                ? $"Section {nodeId} was not found."
                : $"Section {nodeId} is already first.");
            return false;
        }

        string previousNodeId = GraphNodes[index - 1].NodeId;
        bool applied = ApplyGraphEdit(
            $"Move section {nodeId} up",
            graph => TrackAuthoringGraphOperations.MoveBefore(
                graph,
                nodeId,
                previousNodeId));
        if (applied)
        {
            SelectGraphNode(nodeId);
        }

        return applied;
    }

    public bool MoveSectionDown(string nodeId)
    {
        int index = FindGraphNodeIndex(nodeId);
        if (index < 0 || index >= GraphNodes.Count - 1)
        {
            SetStatus(index < 0
                ? $"Section {nodeId} was not found."
                : $"Section {nodeId} is already last.");
            return false;
        }

        string nextNodeId = GraphNodes[index + 1].NodeId;
        bool applied = ApplyGraphEdit(
            $"Move section {nodeId} down",
            graph => TrackAuthoringGraphOperations.MoveAfter(
                graph,
                nodeId,
                nextNodeId));
        if (applied)
        {
            SelectGraphNode(nodeId);
        }

        return applied;
    }

    /// <summary>
    /// Compatibility bridge for existing callers. Package edits are re-imported as a
    /// candidate graph and enter history only as immutable graph snapshots. This path does
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
                SetStatus("Edit rejected: package compatibility edits cannot change metadata or heartline state.");
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
        if (IsInteractiveEditActive && !MatchesActiveStraightSelection(selection))
        {
            SetStatus("Selection changes are disabled while a live edit is active.");
            return;
        }

        if (selection is null)
        {
            Selection.Clear();
        }
        else
        {
            Selection.SetSelection(new object[] { selection });
        }
    }

    public void SetStationCursor(int sampleIndex)
    {
        EngineeringSnapshot? snapshot = engineeringSnapshot;
        if (snapshot is null || sampleIndex < 0 || sampleIndex >= snapshot.SampleCount)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleIndex));
        }

        var replacement = new EngineeringStationCursor(
            sampleIndex,
            snapshot.StationGrid[sampleIndex]);
        if (stationCursor == replacement)
        {
            return;
        }

        stationCursor = replacement;
        StationCursorChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectStationSample(int sampleIndex)
    {
        if (sampleIndex < 0 || sampleIndex >= viewportSnapshot.Samples.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleIndex));
        }

        TrackViewportSample sample = viewportSnapshot.Samples[sampleIndex];
        string? nodeId = graphNodes
            .FirstOrDefault(node => node.RouteIndex == sample.SectionIndex)
            ?.NodeId;

        SetStationCursor(sampleIndex);
        Select(EditorSelection.Sample(sampleIndex, sample.SectionIndex, nodeId));
        SetStatus(
            $"Selected Math Plot sample {sample.SampleIndex} at station {sample.Distance:F2} m.");
    }

    public void SelectStationAt(double station)
    {
        EngineeringSnapshot snapshot = engineeringSnapshot ??
            throw new InvalidOperationException("There is no Math Plot snapshot to select.");
        int sampleIndex = EngineeringSnapshotNavigation.FindNearestSampleIndex(snapshot, station);
        if (sampleIndex < 0)
        {
            throw new InvalidOperationException("The Math Plot snapshot has no canonical samples.");
        }

        SelectStationSample(sampleIndex);
    }

    public void SetHoveredSection(int? sectionIndex)
    {
        int replacement = sectionIndex ?? -1;
        if (replacement < -1 || replacement >= graphNodes.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(sectionIndex));
        }

        if (hoveredSectionIndex == replacement)
        {
            return;
        }

        hoveredSectionIndex = replacement;
        SectionHighlightChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool UndoLast()
    {
        if (IsInteractiveEditActive)
        {
            SetStatus("Undo is disabled while a live edit is active.");
            return false;
        }

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
        if (IsInteractiveEditActive)
        {
            SetStatus("Redo is disabled while a live edit is active.");
            return false;
        }

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

    public bool BeginStraightLengthEdit(string nodeId)
    {
        ThrowIfDisposed();
        if (straightLengthEdit is not null)
        {
            return false;
        }

        TrackAuthoringSession session = authoringSession ??
            throw new InvalidOperationException("The active document has no authoring session.");
        TrackAuthoringGraphNode node = session.CommittedState.SourceGraph.Nodes
            .Single(candidate => string.Equals(candidate.Id, nodeId, StringComparison.Ordinal));
        StraightSectionDefinition straight = node.Section as StraightSectionDefinition ??
            throw new InvalidOperationException(
                $"Graph node '{nodeId}' is not a straight section.");
        InteractiveAuthoringTransaction transaction = session.BeginTransaction(
            nodeId,
            "StraightSectionDefinition.Length",
            $"Edit straight length {nodeId}");
        straightLengthEdit = new StraightLengthEditState(
            transaction.Revision,
            transaction.BeforeState,
            nodeId,
            straight.Length,
            straight.Length,
            straight.Length,
            StraightLengthEditStatus.Ready,
            Array.Empty<string>(),
            RawPointerUpdates: 0,
            AcceptedPreviews: 0,
            FinalCommitWait: TimeSpan.Zero);
        straightLengthRawPointerUpdates = 0;
        straightLengthAcceptedPreviews = 0;
        straightLengthFinalCommitWait = TimeSpan.Zero;
        SetStatus($"Editing straight length {nodeId}.");
        return true;
    }

    public void RecordStraightLengthPointerUpdate()
    {
        if (straightLengthEdit is not null)
        {
            straightLengthRawPointerUpdates++;
            straightLengthEdit = straightLengthEdit with
            {
                RawPointerUpdates = straightLengthEdit.RawPointerUpdates + 1
            };
        }
    }

    public AuthoringEvaluationSubmission SubmitStraightLengthEdit(double absoluteLength)
    {
        ThrowIfDisposed();
        StraightLengthEditState edit = straightLengthEdit ??
            throw new InvalidOperationException("There is no active straight-length edit.");
        TrackAuthoringEvaluationCoordinator coordinator = evaluationCoordinator ??
            throw new InvalidOperationException("The active document has no evaluation coordinator.");

        AuthoringEvaluationSubmission submission = coordinator.SubmitProvisionalEdit(
            edit.TransactionRevision,
            new SetStraightLengthCandidateOperation(edit.NodeId, absoluteLength));
        bool rawInvalid = !double.IsFinite(absoluteLength) || absoluteLength <= 0.0;
        straightLengthEdit = edit with
        {
            RawLength = absoluteLength,
            Status = rawInvalid
                ? StraightLengthEditStatus.Invalid
                : StraightLengthEditStatus.Evaluating,
            Diagnostics = rawInvalid
                ? new[] { "Section length must be finite and greater than zero." }
                : Array.Empty<string>()
        };
        SetStatus(straightLengthEdit.StatusText);
        return submission;
    }

    /// <summary>
    /// Called by the Avalonia dispatcher after an immutable completion is awaited.
    /// The outcome never supplies presentation directly; the session is re-read and
    /// remains the sole freshness authority.
    /// </summary>
    public bool PublishStraightLengthOutcome(AuthoringEvaluationOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        StraightLengthEditState? edit = straightLengthEdit;
        TrackAuthoringSession? session = authoringSession;
        if (edit is null || session is null ||
            outcome.Request.TransactionRevision != edit.TransactionRevision)
        {
            return false;
        }

        InteractiveAuthoringTransaction? transaction = session.ActiveTransaction;
        if (transaction is null || transaction.Revision != edit.TransactionRevision ||
            transaction.NewestProvisionalRevision != outcome.Request.ProvisionalEditRevision)
        {
            return false;
        }

        if (outcome.Status == AuthoringEvaluationOutcomeStatus.Accepted &&
            outcome.Candidate?.PreparedState is not null &&
            ReferenceEquals(session.PresentedState, outcome.Candidate.PreparedState))
        {
            PreparedTrackGraphState presented = session.PresentedState;
            double acceptedLength = GetStraightLength(presented.Graph, edit.NodeId);
            straightLengthEdit = edit with
            {
                AcceptedPreviewLength = acceptedLength,
                Status = StraightLengthEditStatus.Accepted,
                Diagnostics = Array.Empty<string>(),
                AcceptedPreviews = edit.AcceptedPreviews + 1
            };
            straightLengthAcceptedPreviews++;
            ProjectPreparedState(presented);
            SetStatus($"Live preview: {acceptedLength:0.######} m.");
            return true;
        }

        if (outcome.Status == AuthoringEvaluationOutcomeStatus.Rejected)
        {
            IReadOnlyList<string> diagnostics = BuildOutcomeDiagnostics(outcome);
            straightLengthEdit = edit with
            {
                Status = StraightLengthEditStatus.Invalid,
                Diagnostics = diagnostics
            };
            SetStatus("Last valid preview — current value is invalid. " +
                string.Join(" ", diagnostics));
            return true;
        }

        if (outcome.Status == AuthoringEvaluationOutcomeStatus.Faulted)
        {
            string message = outcome.Fault?.Message ?? "Preview evaluation faulted.";
            straightLengthEdit = edit with
            {
                Status = StraightLengthEditStatus.Invalid,
                Diagnostics = new[] { message }
            };
            SetStatus("Last valid preview — current value is invalid. " + message);
            return true;
        }

        return false;
    }

    public async Task<AuthoringScheduledCommitResult?> CommitStraightLengthEditAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        StraightLengthEditState? edit = straightLengthEdit;
        TrackAuthoringEvaluationCoordinator? coordinator = evaluationCoordinator;
        if (edit is null || coordinator is null)
        {
            return null;
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        AuthoringScheduledCommitResult result = await coordinator.CommitLatestAsync(
            edit.TransactionRevision,
            cancellationToken);
        stopwatch.Stop();
        if (!ReferenceEquals(edit, straightLengthEdit) &&
            straightLengthEdit?.TransactionRevision != edit.TransactionRevision)
        {
            return result;
        }

        edit = straightLengthEdit ?? edit;
        straightLengthFinalCommitWait = stopwatch.Elapsed;
        straightLengthEdit = edit with { FinalCommitWait = stopwatch.Elapsed };
        if (result.Succeeded && result.CommitResult?.CommittedState is not null)
        {
            AuthoringCommitResult commit = result.CommitResult;
            PreparedTrackGraphState afterState = commit.CommittedState.PreparedState;
            if (commit.Changed)
            {
                TrackEditorDocument document = ActiveDocument ??
                    throw new InvalidOperationException("The edited document is no longer active.");
                applyingSessionCommitToDocument = true;
                try
                {
                    UndoRedo.Execute(new TrackGraphSnapshotOperation(
                        $"Edit straight length {edit.NodeId}",
                        document,
                        edit.BeforeState,
                        afterState));
                }
                finally
                {
                    applyingSessionCommitToDocument = false;
                }
            }

            ProjectPreparedState(afterState);
            straightLengthEdit = null;
            SetStatus(commit.Changed
                ? $"Committed straight length {edit.NodeId}."
                : "No graph values changed.");
            return result;
        }

        coordinator.CancelTransaction(edit.TransactionRevision);
        ProjectPreparedState(authoringSession!.CommittedState.PreparedState);
        straightLengthEdit = null;
        SetStatus("Straight length was not committed; the committed value was restored.");
        return result;
    }

    public bool CancelStraightLengthEdit()
    {
        StraightLengthEditState? edit = straightLengthEdit;
        if (edit is null)
        {
            return false;
        }

        bool cancelled = evaluationCoordinator?.CancelTransaction(edit.TransactionRevision) == true;
        PreparedTrackGraphState? committed = authoringSession?.CommittedState.PreparedState;
        straightLengthEdit = null;
        if (committed is not null && ReferenceEquals(observedDocument, ActiveDocument))
        {
            ProjectPreparedState(committed);
        }

        SetStatus("Live straight-length edit cancelled.");
        return cancelled;
    }

    public StraightLengthInteractionMetrics CaptureStraightLengthMetrics()
    {
        EvaluationSchedulerSnapshot scheduler = evaluationCoordinator?.CaptureSnapshot() ??
            throw new InvalidOperationException("The active document has no evaluation coordinator.");
        StraightLengthEditState? edit = straightLengthEdit;
        return new StraightLengthInteractionMetrics(
            straightLengthRawPointerUpdates,
            scheduler.Submitted,
            scheduler.Coalesced,
            scheduler.Started,
            straightLengthAcceptedPreviews,
            scheduler.Stale,
            scheduler.SubmitToPresentTime,
            straightLengthFinalCommitWait,
            scheduler.CompilerInvocationCount);
    }

    private void ActivateDocument(TrackEditorDocument document, string message)
    {
        CancelStraightLengthEdit();
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
            _ => NewDocument(),
            _ => !IsInteractiveEditActive));
        Commands.Register(new EditorCommand(
            EditorCommandIds.OpenDocument,
            parameter => OpenDocument((string)parameter!),
            parameter => !IsInteractiveEditActive &&
                         parameter is string path && !string.IsNullOrWhiteSpace(path)));
        Commands.Register(new EditorCommand(
            EditorCommandIds.SaveDocument,
            parameter => SaveDocument(parameter as string),
            _ => !IsInteractiveEditActive && ActiveDocument?.CanSave == true));
        Commands.Register(new EditorCommand(
            EditorCommandIds.SaveDocumentAs,
            parameter => SaveDocument((string)parameter!),
            parameter => !IsInteractiveEditActive &&
                         ActiveDocument?.CanSave == true &&
                         parameter is string path &&
                         !string.IsNullOrWhiteSpace(path)));
        Commands.Register(new EditorCommand(
            EditorCommandIds.Undo,
            _ => UndoLast(),
            _ => !IsInteractiveEditActive && UndoRedo.CanUndo));
        Commands.Register(new EditorCommand(
            EditorCommandIds.Redo,
            _ => RedoLast(),
            _ => !IsInteractiveEditActive && UndoRedo.CanRedo));
        Commands.Register(new EditorCommand(
            EditorCommandIds.Select,
            parameter => Select((EditorSelection?)parameter),
            parameter => !IsInteractiveEditActive && parameter is EditorSelection));
    }

    private void OnActiveDocumentChanged(object? sender, EventArgs eventArgs)
    {
        CancelStraightLengthEdit();
        if (observedDocument != null)
        {
            observedDocument.Changed -= OnDocumentChanged;
        }

        evaluationCoordinator?.Dispose();
        evaluationCoordinator = null;
        authoringSession = null;
        observedDocument = ActiveDocument;
        if (observedDocument != null)
        {
            observedDocument.Changed += OnDocumentChanged;
            if (observedDocument.Graph is not null)
            {
                authoringSession = new TrackAuthoringSession(
                    observedDocument.CaptureGraphState(),
                    markClean: !observedDocument.IsDirty);
                evaluationCoordinator = new TrackAuthoringEvaluationCoordinator(
                    authoringSession,
                    AuthoringEvaluationSchedulerOptions.SerializedBackground);
            }
        }

        projectedCompilation = null;
        stationCursor = null;
        RefreshDocumentProjection();
    }

    private void OnDocumentChanged(object? sender, EventArgs eventArgs)
    {
        TrackEditorDocument? document = ActiveDocument;
        if (!applyingSessionCommitToDocument &&
            straightLengthEdit is null &&
            document?.Graph is not null &&
            authoringSession is not null)
        {
            PreparedTrackGraphState documentState = document.CaptureGraphState();
            PreparedTrackGraphState sessionState = authoringSession.CommittedState.PreparedState;
            if (!ReferenceEquals(documentState.Graph, sessionState.Graph) ||
                !ReferenceEquals(
                    documentState.GraphCompileResult,
                    sessionState.GraphCompileResult))
            {
                authoringSession.ReplaceSessionState(
                    documentState,
                    markClean: !document.IsDirty);
            }
            else if (!document.IsDirty)
            {
                authoringSession.MarkClean();
            }
        }

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
        PreparedTrackGraphState? state = authoringSession?.PresentedState;
        if (document?.Graph is null || state is null)
        {
            graphNodes = Array.Empty<EditorGraphNode>();
            outlinerNodes = Array.Empty<EditorOutlinerNode>();
            viewportSnapshot = TrackViewportSnapshot.Empty;
            engineeringSnapshot = null;
            stationCursor = null;
            hoveredSectionIndex = -1;
            projectedCompilation = null;
        }
        else
        {
            ProjectPreparedState(state, notify: false);
        }

        NormalizeSelectionAfterProjection();
        WorkspaceChanged?.Invoke(this, EventArgs.Empty);
    }

    private void NormalizeSelectionAfterProjection()
    {
        EditorSelection? selection = CurrentSelection;
        if (selection?.Kind == EditorSelectionKind.Sample && stationCursor.HasValue)
        {
            int sampleIndex = stationCursor.Value.SampleIndex;
            if (sampleIndex >= 0 && sampleIndex < viewportSnapshot.Samples.Count)
            {
                TrackViewportSample sample = viewportSnapshot.Samples[sampleIndex];
                string? nodeId = graphNodes
                    .FirstOrDefault(node => node.RouteIndex == sample.SectionIndex)
                    ?.NodeId;
                EditorSelection replacement = EditorSelection.Sample(
                    sampleIndex,
                    sample.SectionIndex,
                    nodeId);
                if (replacement != selection)
                {
                    Selection.SetSelection(new object[] { replacement });
                }
            }

            return;
        }

        if (selection?.Kind != EditorSelectionKind.Section || selection.NodeId is null)
        {
            return;
        }

        EditorGraphNode? currentNode = GraphNodes.FirstOrDefault(node =>
            string.Equals(node.NodeId, selection.NodeId, StringComparison.Ordinal));
        if (currentNode is not null)
        {
            if (selection.SectionIndex != currentNode.RouteIndex)
            {
                Selection.SetSelection(new object[] { currentNode.Selection });
            }

            return;
        }

        Select(EditorSelection.Track);
    }

    private void ProjectPreparedState(
        PreparedTrackGraphState state,
        bool notify = true)
    {
        ArgumentNullException.ThrowIfNull(state);
        TrackEditorDocument? document = ActiveDocument;
        EngineeringStationCursor? previousCursor = stationCursor;

        if (state.GraphCompileResult?.Compilation is not TrackAuthoringCompilation compilation)
        {
            graphNodes = Array.Empty<EditorGraphNode>();
            outlinerNodes = Array.Empty<EditorOutlinerNode>();
            viewportSnapshot = TrackViewportSnapshot.Empty;
            engineeringSnapshot = null;
            stationCursor = null;
            hoveredSectionIndex = -1;
            projectedCompilation = null;
        }
        else
        {
            graphNodes = BuildGraphNodes(state.GraphCompileResult.OrderedNodes);
            outlinerNodes = document is null
                ? Array.Empty<EditorOutlinerNode>()
                : BuildOutliner(document, state);
            if (!ReferenceEquals(projectedCompilation, compilation))
            {
                double? previousStation = stationCursor?.Station;
                projectedCompilation = compilation;
                compilationRevision++;
                snapshotRevision++;
                engineeringSnapshot = EngineeringSnapshotBuilder.Build(
                    compilation,
                    new EngineeringSnapshotRequest(
                        compilationRevision,
                        snapshotRevision,
                        samplingService.GetSampleCount(compilation.TotalLength)));
                viewportSnapshot = samplingService.CreateViewportSnapshot(engineeringSnapshot);
                double remappedStation = previousStation.HasValue
                    ? System.Math.Clamp(previousStation.Value, 0.0, engineeringSnapshot.TotalLength)
                    : 0.0;
                int remappedIndex = EngineeringSnapshotNavigation.FindNearestSampleIndex(
                    engineeringSnapshot,
                    remappedStation);
                stationCursor = remappedIndex < 0
                    ? null
                    : new EngineeringStationCursor(
                        remappedIndex,
                        engineeringSnapshot.StationGrid[remappedIndex]);
            }

            if (hoveredSectionIndex >= graphNodes.Count)
            {
                hoveredSectionIndex = -1;
            }
        }

        NormalizeSelectionAfterProjection();
        if (!notify)
        {
            return;
        }

        if (previousCursor != stationCursor)
        {
            StationCursorChanged?.Invoke(this, EventArgs.Empty);
        }

        WorkspaceChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool MatchesActiveStraightSelection(EditorSelection? selection)
    {
        return straightLengthEdit is not null &&
               selection?.Kind == EditorSelectionKind.Section &&
               string.Equals(
                   selection.NodeId,
                   straightLengthEdit.NodeId,
                   StringComparison.Ordinal);
    }

    private static double GetStraightLength(TrackAuthoringGraph graph, string nodeId)
    {
        TrackAuthoringGraphNode node = graph.Nodes.Single(candidate =>
            string.Equals(candidate.Id, nodeId, StringComparison.Ordinal));
        return (node.Section as StraightSectionDefinition ??
            throw new InvalidOperationException(
                $"Graph node '{nodeId}' is not a straight section.")).Length;
    }

    private static IReadOnlyList<string> BuildOutcomeDiagnostics(
        AuthoringEvaluationOutcome outcome)
    {
        var diagnostics = new List<string>();
        if (outcome.Candidate is not null)
        {
            diagnostics.AddRange(outcome.Candidate.GraphDiagnostics.Select(diagnostic =>
                $"{diagnostic.Code}: {diagnostic.Message}"));
        }

        diagnostics.AddRange(outcome.Diagnostics.Select(diagnostic =>
            $"{diagnostic.Code}: {diagnostic.Message}"));
        if (outcome.Fault is not null)
        {
            diagnostics.Add($"{outcome.Fault.Phase}: {outcome.Fault.Message}");
        }

        return diagnostics.Count == 0
            ? new[] { "The provisional straight length is invalid." }
            : diagnostics.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void ThrowIfInteractiveEditActive(string message)
    {
        if (IsInteractiveEditActive)
        {
            throw new InvalidOperationException(message);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        CancelStraightLengthEdit();
        Documents.ActiveDocumentChanged -= OnActiveDocumentChanged;
        Selection.SelectionChanged -= OnSelectionChanged;
        UndoRedo.StateChanged -= OnUndoRedoStateChanged;
        if (observedDocument is not null)
        {
            observedDocument.Changed -= OnDocumentChanged;
        }

        evaluationCoordinator?.Dispose();
        evaluationCoordinator = null;
        authoringSession = null;
        observedDocument = null;
        disposed = true;
    }

    private static IReadOnlyList<EditorGraphNode> BuildGraphNodes(
        IReadOnlyList<TrackAuthoringGraphNode> orderedNodes)
    {
        var result = new EditorGraphNode[orderedNodes.Count];
        for (int routeIndex = 0; routeIndex < orderedNodes.Count; routeIndex++)
        {
            GeometricSectionDefinition section =
                (GeometricSectionDefinition)orderedNodes[routeIndex].Section;
            result[routeIndex] = new EditorGraphNode(
                section.Id,
                routeIndex,
                DescribeSectionKind(section),
                DescribeSectionSummary(section));
        }

        return result;
    }

    private void SelectGraphNode(string nodeId)
    {
        int index = FindGraphNodeIndex(nodeId);
        if (index >= 0)
        {
            Select(GraphNodes[index].Selection);
        }
    }

    private int FindGraphNodeIndex(string nodeId)
    {
        for (int i = 0; i < GraphNodes.Count; i++)
        {
            if (string.Equals(GraphNodes[i].NodeId, nodeId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static IReadOnlyList<EditorOutlinerNode> BuildOutliner(
        TrackEditorDocument document,
        PreparedTrackGraphState state)
    {
        IReadOnlyList<TrackAuthoringGraphNode> route =
            state.GraphCompileResult?.OrderedNodes ?? Array.Empty<TrackAuthoringGraphNode>();
        var sectionNodes = new List<EditorOutlinerNode>(route.Count);
        for (int sectionIndex = 0; sectionIndex < route.Count; sectionIndex++)
        {
            GeometricSectionDefinition section =
                (GeometricSectionDefinition)route[sectionIndex].Section;
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
            state.Graph.Banking?.Keys ?? Array.Empty<BankingProfileKey>();
        for (int keyIndex = 0; keyIndex < bankingKeys.Count; keyIndex++)
        {
            BankingProfileKey key = bankingKeys[keyIndex];
            bankingNodes.Add(new EditorOutlinerNode(
                $"Key {keyIndex} | {key.Distance:F1} m | {key.RollRadians * 180.0 / System.Math.PI:F1} deg",
                EditorSelection.BankingKey(keyIndex)));
        }

        HeartlineOffset? heartline = state.AncillaryState.HeartlineOffset;
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
