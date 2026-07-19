using Quantum.Editor.Avalonia.Models;
using Quantum.Editor.Avalonia.Services.Commands;
using Quantum.Editor.Avalonia.Services.Documents;
using Quantum.Editor.Avalonia.Services.Selection;
using Quantum.Editor.Avalonia.Services.UndoRedo;
using Quantum.Editor.Avalonia.Services.Viewport;
using Quantum.IO.TrackLayout.V2;
using Quantum.Track.Authoring;

namespace Quantum.Editor.Avalonia.Services;

public sealed class EditorWorkspace
{
    private readonly TrackDocumentFileService fileService;
    private readonly TrackSamplingService samplingService;
    private TrackEditorDocument? observedDocument;
    private TrackAuthoringCompilation? projectedCompilation;
    private IReadOnlyList<EditorOutlinerNode> outlinerNodes = Array.Empty<EditorOutlinerNode>();
    private TrackViewportSnapshot viewportSnapshot = TrackViewportSnapshot.Empty;
    private EngineeringSnapshot? engineeringSnapshot;
    private EngineeringStationCursor? stationCursor;
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

    public IReadOnlyList<EditorOutlinerNode> OutlinerNodes => outlinerNodes;

    public TrackViewportSnapshot ViewportSnapshot => viewportSnapshot;

    public EngineeringSnapshot? EngineeringSnapshot => engineeringSnapshot;

    public EngineeringStationCursor? StationCursor => stationCursor;

    public string StatusMessage => statusMessage;

    public TrackEditorDocument NewDocument()
    {
        TrackEditorDocument document = TrackEditorDocument.Create(
            TrackPackageFactory.CreateShowcasePackage(),
            "Untitled Layout");
        document.MarkDirty();
        ActivateDocument(document, "Created a new Track Layout Package V2 document.");
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

    public bool ApplyPackageEdit(
        string description,
        Action<TrackLayoutPackageV2Dto> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);

        TrackEditorDocument? document = ActiveDocument;
        if (document?.Package is null)
        {
            SetStatus("The active document cannot be edited as a Track Layout Package V2 document.");
            return false;
        }

        string beforeJson = document.CapturePackageJson();
        TrackLayoutPackageV2Dto candidate = TrackLayoutPackageV2Json.Deserialize(beforeJson);
        edit(candidate);
        string afterJson = TrackLayoutPackageV2Json.Serialize(candidate, indented: true);

        if (string.Equals(beforeJson, afterJson, StringComparison.Ordinal))
        {
            SetStatus("No editor values changed.");
            return false;
        }

        try
        {
            UndoRedo.Execute(new TrackPackageSnapshotOperation(
                description,
                document,
                beforeJson,
                afterJson));
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
            parameter => ActiveDocument?.CanSave == true && parameter is string path && !string.IsNullOrWhiteSpace(path)));
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
        if (document?.Package is null)
        {
            outlinerNodes = Array.Empty<EditorOutlinerNode>();
            viewportSnapshot = TrackViewportSnapshot.Empty;
            engineeringSnapshot = null;
            stationCursor = null;
            projectedCompilation = null;
        }
        else
        {
            outlinerNodes = BuildOutliner(document);
            TrackAuthoringCompilation compilation = document.Compilation!;
            if (!ReferenceEquals(projectedCompilation, compilation))
            {
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
                stationCursor = new EngineeringStationCursor(
                    0,
                    engineeringSnapshot.StationGrid[0]);
            }
        }

        WorkspaceChanged?.Invoke(this, EventArgs.Empty);
    }

    private static IReadOnlyList<EditorOutlinerNode> BuildOutliner(TrackEditorDocument document)
    {
        TrackLayoutPackageV2Dto package = document.Package!;
        var sectionNodes = new List<EditorOutlinerNode>(package.Sections.Length);
        for (int sectionIndex = 0; sectionIndex < package.Sections.Length; sectionIndex++)
        {
            TrackLayoutSectionV2Dto section = package.Sections[sectionIndex];
            var controlPointNodes = new List<EditorOutlinerNode>();
            if (section.ControlPoints != null)
            {
                for (int pointIndex = 0; pointIndex < section.ControlPoints.Length; pointIndex++)
                {
                    controlPointNodes.Add(new EditorOutlinerNode(
                        $"Control point {pointIndex}",
                        EditorSelection.ControlPoint(sectionIndex, pointIndex)));
                }
            }

            sectionNodes.Add(new EditorOutlinerNode(
                $"{sectionIndex + 1}. {section.Id}  ·  {section.Kind}  ·  {section.Length:F1} m",
                EditorSelection.Section(sectionIndex),
                controlPointNodes));
        }

        var bankingNodes = new List<EditorOutlinerNode>();
        TrackBankingKeyV2Dto[] bankingKeys = package.Banking?.Keys ?? Array.Empty<TrackBankingKeyV2Dto>();
        for (int keyIndex = 0; keyIndex < bankingKeys.Length; keyIndex++)
        {
            TrackBankingKeyV2Dto key = bankingKeys[keyIndex];
            bankingNodes.Add(new EditorOutlinerNode(
                $"Key {keyIndex}  ·  {key.Distance:F1} m  ·  {key.RollRadians * 180.0 / System.Math.PI:F1}°",
                EditorSelection.BankingKey(keyIndex)));
        }

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
                        package.Heartline is null
                            ? "Heartline: none"
                            : $"Heartline: N {package.Heartline.NormalOffset:F2} m / B {package.Heartline.LateralOffset:F2} m")
                })
        };
    }
}
