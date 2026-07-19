using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Quantum.Editor.Avalonia.Controls;
using Quantum.Editor.Avalonia.Models;
using Quantum.Editor.Avalonia.Services;
using Quantum.Editor.Avalonia.Services.Commands;
using Quantum.Editor.Avalonia.Services.Documents;
using Quantum.Editor.Avalonia.Services.Workspaces;

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

    public MainWindow()
        : this(CreateWorkspace(), new WorkspaceProfileManager())
    {
    }

    public MainWindow(EditorWorkspace workspace)
        : this(workspace, new WorkspaceProfileManager())
    {
    }

    public MainWindow(
        EditorWorkspace workspace,
        WorkspaceProfileManager workspaceProfiles)
    {
        this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        this.workspaceProfiles = workspaceProfiles ??
            throw new ArgumentNullException(nameof(workspaceProfiles));
        InitializeComponent();

        this.workspace.WorkspaceChanged += OnWorkspaceChanged;
        this.workspace.StationCursorChanged += OnStationCursorChanged;
        this.workspace.SectionHighlightChanged += OnSectionHighlightChanged;
        this.workspaceProfiles.CurrentProfileChanged += OnCurrentProfileChanged;
        this.workspace.Viewport.SetActiveViewport(ViewportPane);
        this.workspace.Commands.Register(new EditorCommand(
            EditorCommandIds.FitViewport,
            _ => ViewportPane.FitToTrack()));
        ApplyWorkspaceProfile(this.workspaceProfiles.CurrentProfile);
        UpdateWorkspaceView();
    }

    public WorkspaceProfileManager WorkspaceProfiles => workspaceProfiles;

    private static EditorWorkspace CreateWorkspace()
    {
        var result = new EditorWorkspace();
        result.NewDocument();
        return result;
    }

    private void OnWorkspaceChanged(object? sender, EventArgs eventArgs)
    {
        UpdateWorkspaceView();
    }

    private void OnCurrentProfileChanged(object? sender, EventArgs eventArgs)
    {
        ApplyWorkspaceProfile(workspaceProfiles.CurrentProfile);
        UpdateWorkspaceView();
    }

    private void ApplyWorkspaceProfile(WorkspaceProfile profile)
    {
        bool showRoute = profile.IsPaneVisibleByDefault(WorkspacePaneIds.Route);
        bool showViewport = profile.IsPaneVisibleByDefault(WorkspacePaneIds.Viewport);
        bool showInspector = profile.IsPaneVisibleByDefault(WorkspacePaneIds.Inspector);
        bool showMathPlots = profile.IsPaneVisibleByDefault(WorkspacePaneIds.MathPlots);
        bool showDiagnostics = profile.IsPaneVisibleByDefault(WorkspacePaneIds.Diagnostics);
        bool showBottomPanel = showMathPlots || showDiagnostics;

        RoutePane.IsVisible = showRoute;
        ViewportPane.IsVisible = showViewport;
        InspectorPane.IsVisible = showInspector;
        RouteSplitter.IsVisible = showRoute && showViewport;
        InspectorSplitter.IsVisible = showViewport && showInspector;
        MathPlotsTab.IsVisible = showMathPlots;
        DiagnosticsTab.IsVisible = showDiagnostics;
        BottomPanelRegion.IsVisible = showBottomPanel;
        BottomPanelSplitter.IsVisible = showBottomPanel;
        if (showBottomPanel &&
            BottomWorkspaceTabs.SelectedItem is Control selectedTab &&
            !selectedTab.IsVisible)
        {
            BottomWorkspaceTabs.SelectedItem = showMathPlots ? MathPlotsTab : DiagnosticsTab;
        }

        ContentPaneGrid.ColumnDefinitions[0].Width = new GridLength(showRoute ? 320 : 0);
        ContentPaneGrid.ColumnDefinitions[1].Width = new GridLength(showRoute && showViewport ? 5 : 0);
        ContentPaneGrid.ColumnDefinitions[2].Width = showViewport
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
        ContentPaneGrid.ColumnDefinitions[3].Width = new GridLength(showViewport && showInspector ? 5 : 0);
        ContentPaneGrid.ColumnDefinitions[4].Width = new GridLength(showInspector ? 340 : 0);
        WorkbenchGrid.RowDefinitions[3].Height = new GridLength(showBottomPanel ? 5 : 0);
        WorkbenchGrid.RowDefinitions[4].Height = new GridLength(showBottomPanel ? 300 : 0);

        bool showFileCommands = profile.HasCommandGroup(WorkspaceCommandGroupIds.File);
        bool showEditCommands = profile.HasCommandGroup(WorkspaceCommandGroupIds.Edit);
        bool showViewCommands = profile.HasCommandGroup(WorkspaceCommandGroupIds.View);
        FileMenu.IsVisible = showFileCommands;
        EditMenu.IsVisible = showEditCommands;
        ViewMenu.IsVisible = showViewCommands;
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

        Title = document is null
            ? "Quantum CoasterWorks Editor"
            : $"Quantum CoasterWorks Editor - {document.DisplayName}{(document.IsDirty ? " *" : string.Empty)}";
        RoutePane.DocumentTitle = document is null
            ? "No active document"
            : document.DisplayName + (document.IsDirty ? " *" : string.Empty);
        RoutePane.DocumentPath = document?.FilePath ?? "Unsaved Track Layout Package V2";
        RoutePane.GraphNodes = workspace.GraphNodes;
        RoutePane.Selection = selection;

        DocumentStateText.Text = document is null
            ? "NO DOCUMENT"
            : document.IsDirty ? "MODIFIED" : "SAVED";
        StatusMessageText.Text = workspace.StatusMessage;
        SelectionText.Text = DescribeSelection(selection);

        UndoButton.IsEnabled = workspace.UndoRedo.CanUndo;
        RedoButton.IsEnabled = workspace.UndoRedo.CanRedo;
        UndoButton.Content = workspace.UndoRedo.UndoDescription is null
            ? "Undo"
            : "Undo " + workspace.UndoRedo.UndoDescription;
        RedoButton.Content = workspace.UndoRedo.RedoDescription is null
            ? "Redo"
            : "Redo " + workspace.UndoRedo.RedoDescription;
        UndoMenuItem.IsEnabled = workspace.UndoRedo.CanUndo;
        RedoMenuItem.IsEnabled = workspace.UndoRedo.CanRedo;

        ViewportPane.Snapshot = snapshot;
        ViewportPane.Selection = selection;
        MathPlotsPane.Snapshot = workspace.EngineeringSnapshot;
        InspectorPane.Refresh(workspace);
        DiagnosticsPane.Snapshot = snapshot;
        UpdateStationCursorView();
        UpdateSectionHighlightView();
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
        BottomWorkspaceTabs.SelectedIndex = 0;
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

    private async void OnWindowKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        bool control = eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool shift = eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (control && eventArgs.Key == Key.N)
        {
            OnNewDocumentClick(sender, new RoutedEventArgs());
        }
        else if (control && eventArgs.Key == Key.O)
        {
            OnOpenDocumentClick(sender, new RoutedEventArgs());
        }
        else if (control && eventArgs.Key == Key.S && shift)
        {
            await SaveDocumentAsAsync();
        }
        else if (control && eventArgs.Key == Key.S)
        {
            OnSaveDocumentClick(sender, new RoutedEventArgs());
        }
        else if (control && eventArgs.Key == Key.Z)
        {
            workspace.Commands.Execute(EditorCommandIds.Undo);
        }
        else if (control && eventArgs.Key == Key.Y)
        {
            workspace.Commands.Execute(EditorCommandIds.Redo);
        }
        else if (!control && eventArgs.Key == Key.F)
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
