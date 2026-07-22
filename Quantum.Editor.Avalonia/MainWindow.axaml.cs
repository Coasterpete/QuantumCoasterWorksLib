using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Dock.Avalonia.Controls;
using Quantum.Editor.Avalonia.Controls;
using Quantum.Editor.Avalonia.Models;
using Quantum.Editor.Avalonia.Services;
using Quantum.Editor.Avalonia.Services.Commands;
using Quantum.Editor.Avalonia.Services.Docking;
using Quantum.Editor.Avalonia.Services.Documents;
using Quantum.Editor.Avalonia.Services.Trains;
using Quantum.Editor.Avalonia.Services.Workspaces;
using Quantum.Track.Authoring;

namespace Quantum.Editor.Avalonia;

public partial class MainWindow : Window
{
    private static readonly FilePickerFileType TrackLayoutFileType = new("Quantum Track Layout Package V2")
    {
        Patterns = new[] { "*.json", "*.qcwtrack" },
        MimeTypes = new[] { "application/json" }
    };

    private readonly EditorWorkspace workspace;
    private readonly WorkspaceProfileManager workspaceProfiles;
    private readonly WorkspaceSelectorModel workspaceSelector;
    private readonly IReadOnlyDictionary<string, object> paneContexts;
    private readonly Dictionary<WorkspaceProfileId, EditorDockingAdapter> dockingByWorkspace = new();
    private readonly Dictionary<WorkspaceProfileId, DockControl> dockHostsByWorkspace = new();
    private EditorDockingAdapter docking;
    private DockControl dockHost;
    private bool synchronizingWorkspaceSelector;
    private readonly RoutePaneControl RoutePane;
    private readonly ViewportPaneControl ViewportPane;
    private readonly InspectorPaneControl InspectorPane;
    private readonly MathPlotsPaneControl MathPlotsPane;
    private readonly DiagnosticsPaneControl DiagnosticsPane;
    private readonly TrainConsistEditorSession trainConsistSession;
    private readonly TrainConfigurationPaneControl TrainConfigurationPane;
    private readonly TrainPreviewPaneControl TrainPreviewPane;
    private readonly TrainSummaryPaneControl TrainSummaryPane;
    private TrackEditorDocument? presentedViewportDocument;

    public MainWindow()
        : this(
            CreateWorkspace(),
            new WorkspaceProfileManager(),
            new DockLayoutPersistenceService())
    {
    }

    public MainWindow(EditorWorkspace workspace)
        : this(
            workspace,
            new WorkspaceProfileManager(),
            new DockLayoutPersistenceService())
    {
    }

    public MainWindow(
        EditorWorkspace workspace,
        WorkspaceProfileManager workspaceProfiles)
        : this(workspace, workspaceProfiles, new DockLayoutPersistenceService())
    {
    }

    public MainWindow(
        EditorWorkspace workspace,
        WorkspaceProfileManager workspaceProfiles,
        DockLayoutPersistenceService layoutPersistence)
    {
        this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        this.workspaceProfiles = workspaceProfiles ??
            throw new ArgumentNullException(nameof(workspaceProfiles));
        ArgumentNullException.ThrowIfNull(layoutPersistence);
        InitializeComponent();

        RoutePane = new RoutePaneControl();
        ViewportPane = new ViewportPaneControl();
        InspectorPane = new InspectorPaneControl();
        MathPlotsPane = new MathPlotsPaneControl();
        DiagnosticsPane = new DiagnosticsPaneControl();
        trainConsistSession = new TrainConsistEditorSession();
        TrainConfigurationPane = new TrainConfigurationPaneControl();
        TrainPreviewPane = new TrainPreviewPaneControl();
        TrainSummaryPane = new TrainSummaryPaneControl();
        TrainConfigurationPane.Bind(trainConsistSession);
        trainConsistSession.StateChanged += OnTrainConsistStateChanged;
        WirePaneInteractions();

        paneContexts = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [WorkspacePaneIds.Route] = RoutePane,
            [WorkspacePaneIds.Viewport] = ViewportPane,
            [WorkspacePaneIds.Inspector] = InspectorPane,
            [WorkspacePaneIds.MathPlots] = MathPlotsPane,
            [WorkspacePaneIds.Diagnostics] = DiagnosticsPane,
            [WorkspacePaneIds.TrainConfiguration] = TrainConfigurationPane,
            [WorkspacePaneIds.TrainPreview] = TrainPreviewPane,
            [WorkspacePaneIds.TrainSummary] = TrainSummaryPane
        };
        workspaceSelector = new WorkspaceSelectorModel(this.workspaceProfiles);
        docking = new EditorDockingAdapter(
            this.workspaceProfiles.CurrentProfile.Composition,
            ResolvePaneContexts(this.workspaceProfiles.CurrentProfile.Composition),
            layoutPersistence);
        dockingByWorkspace.Add(this.workspaceProfiles.CurrentProfileId, docking);
        dockHost = CreateDockHost(docking);
        dockHostsByWorkspace.Add(this.workspaceProfiles.CurrentProfileId, dockHost);
        DockHostRegion.Content = dockHost;
        InitializeWorkspaceSelector();
        PopulatePaneMenu();

        this.workspace.WorkspaceChanged += OnWorkspaceChanged;
        this.workspace.StationCursorChanged += OnStationCursorChanged;
        this.workspace.SectionHighlightChanged += OnSectionHighlightChanged;
        this.workspaceProfiles.CurrentProfileChanged += OnCurrentProfileChanged;
        this.workspace.Viewport.SetActiveViewport(ViewportPane);
        this.workspace.Commands.Register(new EditorCommand(
            EditorCommandIds.FitViewport,
            _ => ViewportPane.FitToTrack()));
        Closing += OnWindowClosing;
        ApplyWorkspaceProfile(
            this.workspaceProfiles.CurrentProfile,
            applyPaneVisibilityDefaults: !docking.RestoredSavedLayout);
        UpdateWorkspaceView();
    }

    public WorkspaceProfileManager WorkspaceProfiles => workspaceProfiles;

    public WorkspaceSelectorModel WorkspaceSelector => workspaceSelector;

    public EditorDockingAdapter Docking => docking;

    public TrainConsistEditorSession TrainConsistSession => trainConsistSession;

    private static EditorWorkspace CreateWorkspace()
    {
        var result = new EditorWorkspace();
        result.NewDocument();
        return result;
    }

    private void WirePaneInteractions()
    {
        RoutePane.NodeSelected += OnGraphNodeSelected;
        RoutePane.AddSectionRequested += OnAddSectionRequested;
        RoutePane.InsertBeforeRequested += OnInsertBeforeRequested;
        RoutePane.InsertAfterRequested += OnInsertAfterRequested;
        RoutePane.DeleteSectionRequested += OnDeleteSectionRequested;
        RoutePane.MoveUpRequested += OnMoveUpRequested;
        RoutePane.MoveDownRequested += OnMoveDownRequested;
        RoutePane.SectionPointerChanged += OnSectionPointerChanged;
        ViewportPane.SectionPointerChanged += OnSectionPointerChanged;
        ViewportPane.SampleSelected += OnViewportSampleSelected;
        MathPlotsPane.SectionPointerChanged += OnSectionPointerChanged;
        MathPlotsPane.StationChanged += OnEngineeringPlotStationChanged;
        MathPlotsPane.StationSelected += OnEngineeringPlotStationSelected;
    }

    private void OnWorkspaceChanged(object? sender, EventArgs eventArgs)
    {
        UpdateWorkspaceView();
    }

    private void OnCurrentProfileChanged(object? sender, EventArgs eventArgs)
    {
        ActivateWorkspaceComposition(workspaceProfiles.CurrentProfile);
        ApplyWorkspaceProfile(
            workspaceProfiles.CurrentProfile,
            applyPaneVisibilityDefaults: !docking.RestoredSavedLayout);
        SynchronizeWorkspaceSelector();
        UpdateWorkspaceView();
    }

    private void ActivateWorkspaceComposition(WorkspaceProfile profile)
    {
        docking.TrySaveLayout();
        if (!dockingByWorkspace.TryGetValue(profile.Id, out EditorDockingAdapter? nextDocking))
        {
            nextDocking = new EditorDockingAdapter(
                profile.Composition,
                ResolvePaneContexts(profile.Composition),
                new DockLayoutPersistenceService(
                    DockLayoutPersistenceService.GetDefaultLayoutFilePath(profile.Id)));
            dockingByWorkspace.Add(profile.Id, nextDocking);
            dockHostsByWorkspace.Add(profile.Id, CreateDockHost(nextDocking));
        }

        docking = nextDocking;
        dockHost = dockHostsByWorkspace[profile.Id];
        DockHostRegion.Content = dockHost;
        PopulatePaneMenu();
    }

    private static DockControl CreateDockHost(EditorDockingAdapter adapter)
    {
        return new DockControl
        {
            InitializeFactory = false,
            InitializeLayout = false,
            Factory = adapter.Factory,
            Layout = adapter.Layout
        };
    }

    private IReadOnlyDictionary<string, object> ResolvePaneContexts(
        WorkspaceComposition composition)
    {
        return composition.Panes.Panes.ToDictionary(
            pane => pane.Id,
            pane => paneContexts.TryGetValue(pane.Id, out object? context)
                ? context
                : throw new InvalidOperationException(
                    $"Workspace pane '{pane.Id}' has no registered frontend content."),
            StringComparer.Ordinal);
    }

    private void InitializeWorkspaceSelector()
    {
        WorkspaceSelectorComboBox.ItemsSource = workspaceSelector.Items
            .Select(item => new ComboBoxItem
            {
                Content = item.DisplayText,
                IsEnabled = item.IsEnabled,
                Tag = item.Id
            })
            .ToArray();
        SynchronizeWorkspaceSelector();
    }

    private void SynchronizeWorkspaceSelector()
    {
        synchronizingWorkspaceSelector = true;
        WorkspaceSelectorComboBox.SelectedItem =
            WorkspaceSelectorComboBox.Items
                .OfType<ComboBoxItem>()
                .Single(item => item.Tag is WorkspaceProfileId id &&
                    id == workspaceProfiles.CurrentProfileId);
        synchronizingWorkspaceSelector = false;
    }

    private void PopulatePaneMenu()
    {
        PanesMenu.ItemsSource = docking.Registry.Panes
            .Select(pane =>
            {
                var menuItem = new MenuItem
                {
                    Header = pane.Title,
                    Tag = pane.Id
                };
                menuItem.Click += OnShowPaneClick;
                return menuItem;
            })
            .ToArray();
    }

    private void ApplyWorkspaceProfile(
        WorkspaceProfile profile,
        bool applyPaneVisibilityDefaults)
    {
        if (applyPaneVisibilityDefaults)
        {
            foreach (DockPaneRegistration pane in docking.Registry.Panes)
            {
                docking.SetPaneVisible(
                    pane.Id,
                    profile.IsPaneVisibleByDefault(pane.Id));
            }
        }

        bool showFileCommands = profile.HasCommandGroup(WorkspaceCommandGroupIds.File);
        bool showEditCommands = profile.HasCommandGroup(WorkspaceCommandGroupIds.Edit);
        bool showViewCommands = profile.HasCommandGroup(WorkspaceCommandGroupIds.View);
        bool showLayoutCommands = profile.HasCommandGroup(WorkspaceCommandGroupIds.Layout);
        FileMenu.IsVisible = true;
        EditMenu.IsVisible = showEditCommands;
        ViewMenu.IsVisible = showViewCommands || showLayoutCommands;
        NewTrackMenuItem.IsVisible = showFileCommands;
        OpenTrackMenuItem.IsVisible = showFileCommands;
        SaveTrackMenuItem.IsVisible = showFileCommands;
        SaveTrackAsMenuItem.IsVisible = showFileCommands;
        TrackFileOpenSeparator.IsVisible = showFileCommands;
        TrackFileExitSeparator.IsVisible = showFileCommands;
        FitTrackMenuItem.IsVisible = showViewCommands;
        TransportedFramesMenuItem.IsVisible = showViewCommands;
        MathPlotsMenuItem.IsVisible = showViewCommands;
        TrackViewSeparator.IsVisible = showViewCommands && showLayoutCommands;
        PanesMenu.IsVisible = showLayoutCommands;
        LayoutSeparator.IsVisible = showLayoutCommands;
        ResetLayoutMenuItem.IsVisible = showLayoutCommands;
        NewButton.IsVisible = showFileCommands;
        OpenButton.IsVisible = showFileCommands;
        SaveButton.IsVisible = showFileCommands;
        UndoButton.IsVisible = showEditCommands;
        RedoButton.IsVisible = showEditCommands;
        FitButton.IsVisible = showViewCommands;
        ShowFramesCheckBox.IsVisible = showViewCommands;
        ProjectionComboBox.IsVisible = showViewCommands;

        ShowFramesCheckBox.IsChecked = profile.IsOverlayEnabledByDefault(
            WorkspaceOverlayIds.TransportedFrames);
    }

    private void UpdateWorkspaceView()
    {
        TrackEditorDocument? document = workspace.ActiveDocument;
        EditorSelection? selection = workspace.CurrentSelection;
        TrackViewportSnapshot snapshot = workspace.ViewportSnapshot;

        bool trackActive = workspaceProfiles.CurrentProfileId == WorkspaceProfileId.Track;
        Title = trackActive
            ? document is null
                ? "Quantum CoasterWorks Editor"
                : $"Quantum CoasterWorks Editor - {document.DisplayName}{(document.IsDirty ? " *" : string.Empty)}"
            : "Quantum CoasterWorks Editor - " + workspaceProfiles.CurrentProfile.DisplayName;
        RoutePane.DocumentTitle = document is null
            ? "No active document"
            : document.DisplayName + (document.IsDirty ? " *" : string.Empty);
        RoutePane.DocumentPath = document?.FilePath ?? "Unsaved Track Layout Package V2";
        RoutePane.GraphNodes = workspace.GraphNodes;
        RoutePane.Selection = selection;
        bool interactionActive = workspace.IsInteractiveEditActive;
        bool canSaveTrack = document?.CanSave == true && !interactionActive;
        NewButton.IsEnabled = !interactionActive;
        OpenButton.IsEnabled = !interactionActive;
        NewTrackMenuItem.IsEnabled = !interactionActive;
        OpenTrackMenuItem.IsEnabled = !interactionActive;
        SaveButton.IsEnabled = canSaveTrack;
        SaveTrackMenuItem.IsEnabled = canSaveTrack;
        SaveTrackAsMenuItem.IsEnabled = canSaveTrack;

        if (trackActive)
        {
            DocumentStateText.Text = document is null
                ? "NO DOCUMENT"
                : document.IsDirty ? "MODIFIED" : "SAVED";
            StatusMessageText.Text = workspace.StatusMessage;
            SelectionText.Text = DescribeSelection(selection);
        }
        else if (workspaceProfiles.CurrentProfileId == WorkspaceProfileId.Train)
        {
            DocumentStateText.Text = "VALID CONSIST";
            StatusMessageText.Text = trainConsistSession.LastAttemptSucceeded
                ? "Train definition is valid and backed by Quantum.Track."
                : "Train edit rejected: " + trainConsistSession.LastValidationMessage;
            SelectionText.Text = $"{trainConsistSession.CurrentDefinition.CarCount} car consist";
        }

        UndoButton.IsEnabled = !interactionActive && workspace.UndoRedo.CanUndo;
        RedoButton.IsEnabled = !interactionActive && workspace.UndoRedo.CanRedo;
        UndoButton.Content = workspace.UndoRedo.UndoDescription is null
            ? "Undo"
            : "Undo " + workspace.UndoRedo.UndoDescription;
        RedoButton.Content = workspace.UndoRedo.RedoDescription is null
            ? "Redo"
            : "Redo " + workspace.UndoRedo.RedoDescription;
        UndoMenuItem.IsEnabled = !interactionActive && workspace.UndoRedo.CanUndo;
        RedoMenuItem.IsEnabled = !interactionActive && workspace.UndoRedo.CanRedo;

        RoutePane.SourceEditingEnabled = !interactionActive;
        if (!ReferenceEquals(presentedViewportDocument, document))
        {
            presentedViewportDocument = document;
            ViewportPane.BeginDocumentPresentation();
        }
        ViewportPane.Snapshot = snapshot;
        ViewportPane.Selection = selection;
        MathPlotsPane.Snapshot = workspace.EngineeringSnapshot;
        InspectorPane.Refresh(workspace);
        DiagnosticsPane.Snapshot = snapshot;
        DiagnosticsPane.LiveAuthoringDiagnostics =
            workspace.StraightLengthEdit?.Diagnostics ?? Array.Empty<string>();
        RefreshTrainWorkspaceView();
        UpdateStationCursorView();
        UpdateSectionHighlightView();
    }

    private void OnTrainConsistStateChanged(object? sender, EventArgs eventArgs)
    {
        RefreshTrainWorkspaceView();
        if (workspaceProfiles.CurrentProfileId == WorkspaceProfileId.Train)
        {
            UpdateWorkspaceView();
        }
    }

    private void RefreshTrainWorkspaceView()
    {
        TrainPreviewPane.Definition = trainConsistSession.CurrentDefinition;
        TrainSummaryPane.Refresh(trainConsistSession);
    }

    private void OnStationCursorChanged(object? sender, EventArgs eventArgs)
    {
        UpdateStationCursorView();
    }

    private void UpdateStationCursorView()
    {
        EngineeringStationCursor? cursor = workspace.StationCursor;
        int sampleIndex = cursor?.SampleIndex ?? -1;
        MathPlotsPane.CursorSampleIndex = sampleIndex;
        ViewportPane.StationCursorSampleIndex = sampleIndex;
        MathPlotsPane.StationReadout = cursor.HasValue
            ? DescribeStationCursor(cursor.Value)
            : "Station --";
    }

    private string DescribeStationCursor(EngineeringStationCursor cursor)
    {
        TrackViewportSample? sample = cursor.SampleIndex >= 0 &&
            cursor.SampleIndex < workspace.ViewportSnapshot.Samples.Count
                ? workspace.ViewportSnapshot.Samples[cursor.SampleIndex]
                : null;
        string sectionId = sample.HasValue
            ? ResolveGraphNodeId(sample.Value.SectionIndex) ?? "--"
            : "--";
        return $"Station {cursor.Station:F2} m  |  Sample {cursor.SampleIndex}  |  Section {sectionId}";
    }

    private void OnSectionHighlightChanged(object? sender, EventArgs eventArgs)
    {
        UpdateSectionHighlightView();
    }

    private void UpdateSectionHighlightView()
    {
        int highlightedSectionIndex = workspace.HighlightedSectionIndex;
        MathPlotsPane.HighlightedSectionIndex = highlightedSectionIndex;
        ViewportPane.HighlightedSectionIndex = highlightedSectionIndex;
        RoutePane.HighlightedSectionIndex = highlightedSectionIndex;
    }

    private async void OnNewDocumentClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (await ConfirmDiscardChangesAsync("create a new track"))
        {
            workspace.Commands.Execute(EditorCommandIds.NewDocument);
        }
    }

    private async void OnOpenDocumentClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (!await ConfirmDiscardChangesAsync("open another track"))
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open Track Layout Package V2",
                AllowMultiple = false,
                FileTypeFilter = new[] { TrackLayoutFileType }
            });
        if (files.Count == 0)
        {
            return;
        }

        try
        {
            workspace.Commands.Execute(EditorCommandIds.OpenDocument, files[0].Path.LocalPath);
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is System.Text.Json.JsonException ||
            exception is TrackEditorDocumentException)
        {
            workspace.SetStatus("Open failed: " + exception.Message.Replace(Environment.NewLine, " "));
        }
    }

    private async void OnSaveDocumentClick(object? sender, RoutedEventArgs eventArgs)
    {
        TrackEditorDocument? document = workspace.ActiveDocument;
        if (document is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(document.FilePath))
        {
            await SaveDocumentAsAsync();
            return;
        }

        TrySave(document.FilePath);
    }

    private async void OnSaveDocumentAsClick(object? sender, RoutedEventArgs eventArgs)
    {
        await SaveDocumentAsAsync();
    }

    private async Task SaveDocumentAsAsync()
    {
        TrackEditorDocument? document = workspace.ActiveDocument;
        if (document is null)
        {
            return;
        }

        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Save Track Layout Package V2",
                SuggestedFileName = document.DisplayName + ".qcwtrack.json",
                DefaultExtension = "json",
                FileTypeChoices = new[] { TrackLayoutFileType }
            });
        if (file != null)
        {
            TrySave(file.Path.LocalPath, saveAs: true);
        }
    }

    private void TrySave(string filePath, bool saveAs = false)
    {
        try
        {
            workspace.Commands.Execute(
                saveAs ? EditorCommandIds.SaveDocumentAs : EditorCommandIds.SaveDocument,
                filePath);
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            workspace.SetStatus("Save failed: " + exception.Message);
        }
    }

    private void OnUndoClick(object? sender, RoutedEventArgs eventArgs)
    {
        workspace.Commands.Execute(EditorCommandIds.Undo);
    }

    private void OnRedoClick(object? sender, RoutedEventArgs eventArgs)
    {
        workspace.Commands.Execute(EditorCommandIds.Redo);
    }

    private void OnFitViewportClick(object? sender, RoutedEventArgs eventArgs)
    {
        workspace.Commands.Execute(EditorCommandIds.FitViewport);
    }

    private void OnExitClick(object? sender, RoutedEventArgs eventArgs)
    {
        Close();
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs eventArgs)
    {
        docking.TrySaveLayout();
        workspace.Dispose();
    }

    private void OnProjectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (ViewportPane is null || ProjectionComboBox is null)
        {
            return;
        }

        ViewportPane.Projection = ProjectionComboBox.SelectedIndex switch
        {
            1 => TrackViewportProjection.Top,
            2 => TrackViewportProjection.Side,
            _ => TrackViewportProjection.Isometric
        };
    }

    private void OnWorkspaceSelectorChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (synchronizingWorkspaceSelector)
        {
            return;
        }

        if (WorkspaceSelectorComboBox.SelectedItem is ComboBoxItem
            {
                Tag: WorkspaceProfileId profileId
            } && workspaceSelector.TryActivate(profileId))
        {
            return;
        }

        SynchronizeWorkspaceSelector();
    }

    private void OnShowFramesChanged(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewportPane != null && ShowFramesCheckBox != null)
        {
            ViewportPane.ShowFrames = ShowFramesCheckBox.IsChecked == true;
        }
    }

    private void OnMenuShowFramesClick(object? sender, RoutedEventArgs eventArgs)
    {
        ShowFramesCheckBox.IsChecked = ShowFramesCheckBox.IsChecked != true;
    }

    private void OnShowEngineeringPlotsClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (docking.Registry.Contains(WorkspacePaneIds.MathPlots))
        {
            docking.ShowPane(WorkspacePaneIds.MathPlots);
        }
    }

    private void OnShowPaneClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is MenuItem { Tag: string paneId })
        {
            docking.ShowPane(paneId);
        }
    }

    private void OnResetLayoutClick(object? sender, RoutedEventArgs eventArgs)
    {
        dockHost.Layout = docking.ResetLayout();
        workspace.SetStatus(
            $"Docking layout reset to the default {workspaceProfiles.CurrentProfile.DisplayName} workspace.");
    }

    private void OnEngineeringPlotStationChanged(
        object? sender,
        EngineeringStationChangedEventArgs eventArgs)
    {
        workspace.SetStationCursor(eventArgs.SampleIndex);
    }

    private void OnEngineeringPlotStationSelected(
        object? sender,
        EngineeringStationChangedEventArgs eventArgs)
    {
        workspace.SelectStationAt(eventArgs.Station);
    }

    private void OnSectionPointerChanged(
        object? sender,
        SectionPointerChangedEventArgs eventArgs)
    {
        workspace.SetHoveredSection(eventArgs.SectionIndex);
    }

    private void OnViewportSampleSelected(object? sender, ViewportSampleSelectedEventArgs eventArgs)
    {
        TrackViewportSample sample = eventArgs.Sample;
        workspace.SelectStationSample(sample.SampleIndex);
        workspace.SetStatus(
            $"Selected viewport sample {sample.SampleIndex} at station {sample.Distance:F2} m.");
    }

    private void OnGraphNodeSelected(object? sender, GraphNodeSelectedEventArgs eventArgs)
    {
        EditorGraphNode node = eventArgs.Node;
        workspace.Select(node.Selection);
        workspace.SetStatus("Selected section " + node.NodeId + ".");
    }

    private async void OnAddSectionRequested(object? sender, EventArgs eventArgs)
    {
        TrackAuthoringSectionDefinition? section = await ShowSectionEditorAsync();
        if (section != null)
        {
            workspace.AddSection(section);
        }
    }

    private async void OnInsertBeforeRequested(
        object? sender,
        GraphNodeSelectedEventArgs eventArgs)
    {
        TrackAuthoringSectionDefinition? section = await ShowSectionEditorAsync();
        if (section != null)
        {
            workspace.InsertSectionBefore(eventArgs.Node.NodeId, section);
        }
    }

    private async void OnInsertAfterRequested(
        object? sender,
        GraphNodeSelectedEventArgs eventArgs)
    {
        TrackAuthoringSectionDefinition? section = await ShowSectionEditorAsync();
        if (section != null)
        {
            workspace.InsertSectionAfter(eventArgs.Node.NodeId, section);
        }
    }

    private void OnDeleteSectionRequested(
        object? sender,
        GraphNodeSelectedEventArgs eventArgs)
    {
        workspace.DeleteSection(eventArgs.Node.NodeId);
    }

    private void OnMoveUpRequested(
        object? sender,
        GraphNodeSelectedEventArgs eventArgs)
    {
        workspace.MoveSectionUp(eventArgs.Node.NodeId);
    }

    private void OnMoveDownRequested(
        object? sender,
        GraphNodeSelectedEventArgs eventArgs)
    {
        workspace.MoveSectionDown(eventArgs.Node.NodeId);
    }

    private Task<TrackAuthoringSectionDefinition?> ShowSectionEditorAsync()
    {
        var dialog = new SectionEditorDialog(SuggestSectionId);
        return dialog.ShowDialog<TrackAuthoringSectionDefinition?>(this);
    }

    private string SuggestSectionId(string typeId)
    {
        string prefix = typeId switch
        {
            TrackAuthoringSectionTypeIds.Straight => "straight",
            TrackAuthoringSectionTypeIds.ConstantCurvature => "curve",
            TrackAuthoringSectionTypeIds.CurvatureTransition => "transition",
            _ => "section"
        };
        var existingIds = new HashSet<string>(
            workspace.GraphNodes.Select(node => node.NodeId),
            StringComparer.Ordinal);
        if (!existingIds.Contains(prefix))
        {
            return prefix;
        }

        int suffix = 2;
        while (existingIds.Contains(prefix + "-" + suffix.ToString(CultureInfo.InvariantCulture)))
        {
            suffix++;
        }

        return prefix + "-" + suffix.ToString(CultureInfo.InvariantCulture);
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        bool control = eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool shift = eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift);
        WorkspaceProfile profile = workspaceProfiles.CurrentProfile;
        bool trackFileCommands = profile.HasCommandGroup(WorkspaceCommandGroupIds.File);
        bool trackEditCommands = profile.HasCommandGroup(WorkspaceCommandGroupIds.Edit);
        bool trackViewCommands = profile.HasCommandGroup(WorkspaceCommandGroupIds.View);

        if (trackFileCommands && control && eventArgs.Key == Key.N)
        {
            OnNewDocumentClick(sender, new RoutedEventArgs());
        }
        else if (trackFileCommands && control && eventArgs.Key == Key.O)
        {
            OnOpenDocumentClick(sender, new RoutedEventArgs());
        }
        else if (trackFileCommands && control && eventArgs.Key == Key.S && shift)
        {
            await SaveDocumentAsAsync();
        }
        else if (trackFileCommands && control && eventArgs.Key == Key.S)
        {
            OnSaveDocumentClick(sender, new RoutedEventArgs());
        }
        else if (trackEditCommands && control && eventArgs.Key == Key.Z)
        {
            workspace.Commands.Execute(EditorCommandIds.Undo);
        }
        else if (trackEditCommands && control && eventArgs.Key == Key.Y)
        {
            workspace.Commands.Execute(EditorCommandIds.Redo);
        }
        else if (trackViewCommands && !control && eventArgs.Key == Key.F)
        {
            workspace.Commands.Execute(EditorCommandIds.FitViewport);
        }
        else
        {
            return;
        }

        eventArgs.Handled = true;
    }

    private async Task<bool> ConfirmDiscardChangesAsync(string action)
    {
        TrackEditorDocument? document = workspace.ActiveDocument;
        if (document?.IsDirty != true)
        {
            return true;
        }

        var dialog = new Window
        {
            Title = "Unsaved changes",
            Width = 430,
            Height = 180,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var discardButton = new Button { Content = "Discard changes", MinWidth = 125 };
        var cancelButton = new Button { Content = "Cancel", MinWidth = 90 };
        discardButton.Click += (_, _) => dialog.Close(true);
        cancelButton.Click += (_, _) => dialog.Close(false);
        dialog.Content = new StackPanel
        {
            Margin = new global::Avalonia.Thickness(20),
            Spacing = 18,
            Children =
            {
                new TextBlock
                {
                    Text = $"{document.DisplayName} has unsaved changes. Discard them and {action}?",
                    TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancelButton, discardButton }
                }
            }
        };

        return await dialog.ShowDialog<bool>(this);
    }

    private string? ResolveGraphNodeId(int sectionIndex)
    {
        return workspace.GraphNodes
            .FirstOrDefault(node => node.RouteIndex == sectionIndex)
            ?.NodeId;
    }

    private static string DescribeSelection(EditorSelection? selection)
    {
        return selection?.Kind switch
        {
            EditorSelectionKind.Track => "Track selected",
            EditorSelectionKind.Section =>
                "Section " + (selection.NodeId ?? (selection.SectionIndex + 1).ToString(CultureInfo.InvariantCulture)) +
                " selected",
            EditorSelectionKind.BankingKey => $"Banking key {selection.ElementIndex} selected",
            EditorSelectionKind.ControlPoint => $"Control point {selection.ElementIndex} selected",
            EditorSelectionKind.Sample => $"Station sample {selection.SampleIndex} selected",
            _ => "No selection"
        };
    }
}
