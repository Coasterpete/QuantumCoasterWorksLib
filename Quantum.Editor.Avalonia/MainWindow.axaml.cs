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
using Quantum.Editor.Avalonia.Services.Plots;
using Quantum.IO.TrackLayout.V2;
using Quantum.Track;

namespace Quantum.Editor.Avalonia;

public partial class MainWindow : Window
{
    private static readonly FilePickerFileType TrackLayoutFileType = new("Quantum Track Layout Package V2")
    {
        Patterns = new[] { "*.json", "*.qcwtrack" },
        MimeTypes = new[] { "application/json" }
    };

    private readonly EditorWorkspace workspace;
    private readonly Dictionary<string, TextBox> inspectorFields = new(StringComparer.Ordinal);
    private readonly Dictionary<EditorSelection, TreeViewItem> outlinerItems = new();
    private bool suppressOutlinerSelection;

    public MainWindow()
        : this(CreateWorkspace())
    {
    }

    public MainWindow(EditorWorkspace workspace)
    {
        this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        InitializeComponent();

        this.workspace.WorkspaceChanged += OnWorkspaceChanged;
        this.workspace.StationCursorChanged += OnStationCursorChanged;
        this.workspace.Viewport.SetActiveViewport(ViewportControl);
        this.workspace.Commands.Register(new EditorCommand(
            EditorCommandIds.FitViewport,
            _ => ViewportControl.FitToTrack()));
        UpdateWorkspaceView();
    }

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

    private void UpdateWorkspaceView()
    {
        TrackEditorDocument? document = workspace.ActiveDocument;
        EditorSelection? selection = workspace.CurrentSelection;
        TrackViewportSnapshot snapshot = workspace.ViewportSnapshot;

        Title = document is null
            ? "Quantum CoasterWorks Editor"
            : $"Quantum CoasterWorks Editor — {document.DisplayName}{(document.IsDirty ? " *" : string.Empty)}";
        DocumentTitleText.Text = document is null
            ? "No active document"
            : document.DisplayName + (document.IsDirty ? " *" : string.Empty);
        DocumentPathText.Text = document?.FilePath ?? "Unsaved Track Layout Package V2";
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

        ViewportControl.Snapshot = snapshot;
        ViewportControl.Selection = selection;
        EngineeringPlotsControl.Snapshot = workspace.EngineeringSnapshot;
        ViewportStatsText.Text = snapshot.Samples.Count == 0
            ? "No compiled track samples"
            : $"{snapshot.TotalLength:F2} m  ·  {snapshot.Samples.Count} frames  ·  max |κ| {snapshot.MaximumAbsoluteCurvature:F5} 1/m  ·  max |bank| {snapshot.MaximumAbsoluteRollDegrees:F1}°";
        ViewportSelectionOverlay.Text = DescribeViewportSelection(selection, snapshot);

        RebuildOutliner();
        RebuildInspector();
        RebuildDiagnostics();
        UpdateStationCursorView();
    }

    private void OnStationCursorChanged(object? sender, EventArgs eventArgs)
    {
        UpdateStationCursorView();
    }

    private void UpdateStationCursorView()
    {
        EngineeringStationCursor? cursor = workspace.StationCursor;
        int sampleIndex = cursor?.SampleIndex ?? -1;
        EngineeringPlotsControl.CursorSampleIndex = sampleIndex;
        ViewportControl.StationCursorSampleIndex = sampleIndex;
        StationReadoutText.Text = cursor.HasValue
            ? $"Station {cursor.Value.Station:F2} m"
            : "Station --";
    }

    private void RebuildOutliner()
    {
        suppressOutlinerSelection = true;
        try
        {
            outlinerItems.Clear();
            var roots = new List<TreeViewItem>();
            foreach (EditorOutlinerNode node in workspace.OutlinerNodes)
            {
                roots.Add(CreateTreeItem(node, 0));
            }

            OutlinerTree.ItemsSource = roots;

            EditorSelection? current = workspace.CurrentSelection;
            EditorSelection? treeSelection = current?.Kind == EditorSelectionKind.Sample && current.SectionIndex >= 0
                ? EditorSelection.Section(current.SectionIndex)
                : current;
            if (treeSelection != null && outlinerItems.TryGetValue(treeSelection, out TreeViewItem? selectedItem))
            {
                OutlinerTree.SelectedItem = selectedItem;
            }
        }
        finally
        {
            suppressOutlinerSelection = false;
        }
    }

    private TreeViewItem CreateTreeItem(EditorOutlinerNode node, int depth)
    {
        var item = new TreeViewItem
        {
            Header = node.Title,
            Tag = node,
            IsExpanded = depth < 2
        };

        if (node.Selection != null)
        {
            outlinerItems[node.Selection] = item;
        }

        if (node.Children.Count != 0)
        {
            item.ItemsSource = node.Children.Select(child => CreateTreeItem(child, depth + 1)).ToArray();
        }

        return item;
    }

    private void RebuildInspector()
    {
        inspectorFields.Clear();
        InspectorFieldsPanel.Children.Clear();

        TrackEditorDocument? document = workspace.ActiveDocument;
        TrackLayoutPackageV2Dto? package = document?.Package;
        EditorSelection? selection = workspace.CurrentSelection;
        if (document is null || package is null || selection is null)
        {
            InspectorTitleText.Text = "Nothing selected";
            AddInspectorNote("Select the track, a section, banking key, control point, or viewport sample.");
            return;
        }

        switch (selection.Kind)
        {
            case EditorSelectionKind.Track:
                InspectorTitleText.Text = "Track document";
                AddInspectorField("sourceName", "Source name", package.Metadata.SourceName ?? string.Empty);
                AddInspectorField("layoutId", "Layout ID", package.Metadata.LayoutId ?? string.Empty);
                AddInspectorField("units", "Units", package.Metadata.Units, editable: false);
                AddInspectorField("sections", "Sections", package.Sections.Length.ToString(CultureInfo.InvariantCulture), editable: false);
                AddInspectorField("length", "Compiled length", $"{workspace.ViewportSnapshot.TotalLength:F3} m", editable: false);
                if (package.Heartline != null)
                {
                    AddInspectorField("heartlineNormal", "Heartline normal", package.Heartline.NormalOffset.ToString("G17", CultureInfo.InvariantCulture));
                    AddInspectorField("heartlineLateral", "Heartline lateral", package.Heartline.LateralOffset.ToString("G17", CultureInfo.InvariantCulture));
                }
                AddApplyButton("Apply track properties", ApplyTrackInspector);
                break;

            case EditorSelectionKind.Section:
                BuildSectionInspector(package, selection.SectionIndex);
                break;

            case EditorSelectionKind.BankingKey:
                BuildBankingInspector(package, selection.ElementIndex);
                break;

            case EditorSelectionKind.ControlPoint:
                BuildControlPointInspector(package, selection.SectionIndex, selection.ElementIndex);
                break;

            case EditorSelectionKind.Sample:
                BuildSampleInspector(selection.SampleIndex);
                break;
        }
    }

    private void BuildSectionInspector(TrackLayoutPackageV2Dto package, int sectionIndex)
    {
        if (sectionIndex < 0 || sectionIndex >= package.Sections.Length)
        {
            InspectorTitleText.Text = "Invalid section";
            return;
        }

        TrackLayoutSectionV2Dto section = package.Sections[sectionIndex];
        InspectorTitleText.Text = $"Section {sectionIndex + 1}: {section.Id}";
        AddInspectorField("kind", "Kind", section.Kind, editable: false);
        AddInspectorField("id", "ID", section.Id);
        AddInspectorField("sectionLength", "Length (m)", section.Length.ToString("G17", CultureInfo.InvariantCulture));
        AddInspectorField("rollDegrees", "Section roll (°)", (section.RollRadians * 180.0 / System.Math.PI).ToString("G17", CultureInfo.InvariantCulture));

        if (section.Radius.HasValue)
        {
            AddInspectorField("radius", "Signed radius (m)", section.Radius.Value.ToString("G17", CultureInfo.InvariantCulture));
        }

        if (section.StartCurvature.HasValue)
        {
            AddInspectorField("startCurvature", "Start curvature", section.StartCurvature.Value.ToString("G17", CultureInfo.InvariantCulture));
        }

        if (section.EndCurvature.HasValue)
        {
            AddInspectorField("endCurvature", "End curvature", section.EndCurvature.Value.ToString("G17", CultureInfo.InvariantCulture));
        }

        if (section.Degree.HasValue)
        {
            AddInspectorField("degree", "NURBS degree", section.Degree.Value.ToString(CultureInfo.InvariantCulture), editable: false);
        }

        AddApplyButton("Apply section edit", () => ApplySectionInspector(sectionIndex));
    }

    private void BuildBankingInspector(TrackLayoutPackageV2Dto package, int keyIndex)
    {
        TrackBankingKeyV2Dto[] keys = package.Banking?.Keys ?? Array.Empty<TrackBankingKeyV2Dto>();
        if (keyIndex < 0 || keyIndex >= keys.Length)
        {
            InspectorTitleText.Text = "Invalid banking key";
            return;
        }

        TrackBankingKeyV2Dto key = keys[keyIndex];
        InspectorTitleText.Text = $"Banking key {keyIndex}";
        AddInspectorField("bankDistance", "Distance (m)", key.Distance.ToString("G17", CultureInfo.InvariantCulture));
        AddInspectorField("bankRollDegrees", "Roll (°)", (key.RollRadians * 180.0 / System.Math.PI).ToString("G17", CultureInfo.InvariantCulture));
        AddInspectorField("bankInterpolation", "Interpolation", key.InterpolationToNext);
        AddApplyButton("Apply banking edit", () => ApplyBankingInspector(keyIndex));
    }

    private void BuildControlPointInspector(TrackLayoutPackageV2Dto package, int sectionIndex, int pointIndex)
    {
        if (sectionIndex < 0 || sectionIndex >= package.Sections.Length)
        {
            InspectorTitleText.Text = "Invalid control point";
            return;
        }

        TrackLayoutVector3dV2Dto[] points = package.Sections[sectionIndex].ControlPoints ?? Array.Empty<TrackLayoutVector3dV2Dto>();
        if (pointIndex < 0 || pointIndex >= points.Length)
        {
            InspectorTitleText.Text = "Invalid control point";
            return;
        }

        TrackLayoutVector3dV2Dto point = points[pointIndex];
        InspectorTitleText.Text = $"Control point {pointIndex}";
        AddInspectorField("pointX", "Local X", point.X.ToString("G17", CultureInfo.InvariantCulture));
        AddInspectorField("pointY", "Local Y", point.Y.ToString("G17", CultureInfo.InvariantCulture));
        AddInspectorField("pointZ", "Local Z", point.Z.ToString("G17", CultureInfo.InvariantCulture));
        AddInspectorNote("Spatial edits are revalidated and recompiled. Invalid start/tangent or declared-length changes are rejected.");
        AddApplyButton("Apply control point edit", () => ApplyControlPointInspector(sectionIndex, pointIndex));
    }

    private void BuildSampleInspector(int sampleIndex)
    {
        if (sampleIndex < 0 || sampleIndex >= workspace.ViewportSnapshot.Samples.Count)
        {
            InspectorTitleText.Text = "Invalid sample";
            return;
        }

        TrackViewportSample sample = workspace.ViewportSnapshot.Samples[sampleIndex];
        InspectorTitleText.Text = $"Frame sample {sample.SampleIndex}";
        AddInspectorField("sampleDistance", "Station", $"{sample.Distance:F3} m", editable: false);
        AddInspectorField("sampleSection", "Section", (sample.SectionIndex + 1).ToString(CultureInfo.InvariantCulture), editable: false);
        AddInspectorField("samplePosition", "Position", FormatVector(sample.Position), editable: false);
        AddInspectorField("sampleTangent", "Tangent", FormatVector(sample.Tangent), editable: false);
        AddInspectorField("sampleNormal", "Normal", FormatVector(sample.Normal), editable: false);
        AddInspectorField("sampleBinormal", "Binormal", FormatVector(sample.Binormal), editable: false);
        AddInspectorField("sampleCurvature", "Curvature", $"{sample.Curvature:F6} 1/m", editable: false);
        AddInspectorField("sampleRoll", "Banking", $"{sample.RollDegrees:F3}°", editable: false);
    }

    private void AddInspectorField(string key, string label, string value, bool editable = true)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("118,*"),
            ColumnSpacing = 8
        };
        var labelBlock = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = global::Avalonia.Media.Brush.Parse("#8FA5B9"),
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
        };
        var field = new TextBox
        {
            Text = value,
            IsReadOnly = !editable,
            Classes = { "inspectorField" }
        };
        Grid.SetColumn(field, 1);
        grid.Children.Add(labelBlock);
        grid.Children.Add(field);
        InspectorFieldsPanel.Children.Add(grid);
        inspectorFields[key] = field;
    }

    private void AddInspectorNote(string text)
    {
        InspectorFieldsPanel.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = global::Avalonia.Media.Brush.Parse("#7F94A8"),
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            Margin = new global::Avalonia.Thickness(0, 4)
        });
    }

    private void AddApplyButton(string label, Action apply)
    {
        var button = new Button
        {
            Content = label,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new global::Avalonia.Thickness(0, 6, 0, 0)
        };
        button.Click += (_, _) => apply();
        InspectorFieldsPanel.Children.Add(button);
    }

    private void RebuildDiagnostics()
    {
        TrackViewportSnapshot snapshot = workspace.ViewportSnapshot;
        var lines = new List<string>(snapshot.Diagnostics);
        TrackFrameContinuityReport? continuity = snapshot.ContinuityReport;
        if (continuity != null)
        {
            foreach (TrackFrameContinuityIssue issue in continuity.Issues.Take(12))
            {
                lines.Add(
                    $"{issue.Kind}: {issue.Interval.StartDistance:F2}–{issue.Interval.EndDistance:F2} m, " +
                    $"{issue.ActualAngleDegrees:F3}° > {issue.ThresholdAngleDegrees:F3}°." );
            }
        }

        DiagnosticsList.ItemsSource = lines;
        DiagnosticSummaryText.Text = continuity is null
            ? "No compiled diagnostics"
            : $"{continuity.IntervalCount} intervals · {continuity.Issues.Count} issues";
    }

    private void ApplyTrackInspector()
    {
        TryApplyInspectorEdit("Edit track properties", package =>
        {
            package.Metadata.SourceName = Field("sourceName");
            package.Metadata.LayoutId = Field("layoutId");
            if (package.Heartline != null)
            {
                package.Heartline.NormalOffset = NumberField("heartlineNormal");
                package.Heartline.LateralOffset = NumberField("heartlineLateral");
            }
        });
    }

    private void ApplySectionInspector(int sectionIndex)
    {
        TryApplyInspectorEdit("Edit section", package =>
        {
            TrackLayoutSectionV2Dto section = package.Sections[sectionIndex];
            double previousLength = section.Length;
            double newLength = NumberField("sectionLength");
            section.Id = Field("id");
            section.Length = newLength;
            section.RollRadians = NumberField("rollDegrees") * System.Math.PI / 180.0;
            if (section.Radius.HasValue)
            {
                section.Radius = NumberField("radius");
            }
            if (section.StartCurvature.HasValue)
            {
                section.StartCurvature = NumberField("startCurvature");
            }
            if (section.EndCurvature.HasValue)
            {
                section.EndCurvature = NumberField("endCurvature");
            }

            TrackBankingKeyV2Dto[] keys = package.Banking?.Keys ?? Array.Empty<TrackBankingKeyV2Dto>();
            if (keys.Length != 0)
            {
                keys[^1].Distance += newLength - previousLength;
            }
        });
    }

    private void ApplyBankingInspector(int keyIndex)
    {
        TryApplyInspectorEdit("Edit banking key", package =>
        {
            TrackBankingKeyV2Dto key = package.Banking!.Keys[keyIndex];
            key.Distance = NumberField("bankDistance");
            key.RollRadians = NumberField("bankRollDegrees") * System.Math.PI / 180.0;
            key.InterpolationToNext = Field("bankInterpolation");
        });
    }

    private void ApplyControlPointInspector(int sectionIndex, int pointIndex)
    {
        TryApplyInspectorEdit("Edit spatial control point", package =>
        {
            TrackLayoutVector3dV2Dto point = package.Sections[sectionIndex].ControlPoints![pointIndex];
            point.X = NumberField("pointX");
            point.Y = NumberField("pointY");
            point.Z = NumberField("pointZ");
        });
    }

    private void TryApplyInspectorEdit(string description, Action<TrackLayoutPackageV2Dto> edit)
    {
        try
        {
            workspace.ApplyPackageEdit(description, edit);
        }
        catch (Exception exception) when (exception is FormatException || exception is OverflowException)
        {
            workspace.SetStatus("Edit rejected: " + exception.Message);
        }
    }

    private string Field(string key)
    {
        return inspectorFields.TryGetValue(key, out TextBox? field)
            ? field.Text ?? string.Empty
            : throw new InvalidOperationException($"Inspector field '{key}' is unavailable.");
    }

    private double NumberField(string key)
    {
        string value = Field(key);
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) ||
            double.IsNaN(number) ||
            double.IsInfinity(number))
        {
            throw new FormatException($"'{value}' is not a finite invariant-culture number for {key}.");
        }

        return number;
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
        if (ViewportControl is null || ProjectionComboBox is null)
        {
            return;
        }

        ViewportControl.Projection = ProjectionComboBox.SelectedIndex switch
        {
            1 => TrackViewportProjection.Top,
            2 => TrackViewportProjection.Side,
            _ => TrackViewportProjection.Isometric
        };
    }

    private void OnShowFramesChanged(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewportControl != null && ShowFramesCheckBox != null)
        {
            ViewportControl.ShowFrames = ShowFramesCheckBox.IsChecked == true;
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

    private void OnPlotVisibilityChanged(object? sender, RoutedEventArgs eventArgs)
    {
        if (EngineeringPlotsControl is null)
        {
            return;
        }

        EngineeringPlotsControl.SetPlotEnabled(
            EngineeringPlotKind.Elevation,
            ElevationPlotCheckBox?.IsChecked == true);
        EngineeringPlotsControl.SetPlotEnabled(
            EngineeringPlotKind.Curvature,
            CurvaturePlotCheckBox?.IsChecked == true);
        EngineeringPlotsControl.SetPlotEnabled(
            EngineeringPlotKind.Roll,
            RollPlotCheckBox?.IsChecked == true);
        EngineeringPlotsControl.SetPlotEnabled(
            EngineeringPlotKind.Pitch,
            PitchPlotCheckBox?.IsChecked == true);
        EngineeringPlotsControl.SetPlotEnabled(
            EngineeringPlotKind.Yaw,
            YawPlotCheckBox?.IsChecked == true);
    }

    private void OnEngineeringPlotStationChanged(
        object? sender,
        EngineeringStationChangedEventArgs eventArgs)
    {
        workspace.SetStationCursor(eventArgs.SampleIndex);
    }

    private void OnViewportSampleSelected(object? sender, ViewportSampleSelectedEventArgs eventArgs)
    {
        TrackViewportSample sample = eventArgs.Sample;
        workspace.SetStationCursor(sample.SampleIndex);
        workspace.Select(EditorSelection.Sample(sample.SampleIndex, sample.SectionIndex));
        workspace.SetStatus($"Selected frame sample {sample.SampleIndex} at station {sample.Distance:F2} m.");
    }

    private void OnOutlinerSelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (suppressOutlinerSelection || OutlinerTree.SelectedItem is not TreeViewItem item)
        {
            return;
        }

        if (item.Tag is EditorOutlinerNode { Selection: not null } node)
        {
            workspace.Select(node.Selection);
            workspace.SetStatus("Selected " + node.Title + ".");
        }
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

    private static string DescribeSelection(EditorSelection? selection)
    {
        return selection?.Kind switch
        {
            EditorSelectionKind.Track => "Track selected",
            EditorSelectionKind.Section => $"Section {selection.SectionIndex + 1} selected",
            EditorSelectionKind.BankingKey => $"Banking key {selection.ElementIndex} selected",
            EditorSelectionKind.ControlPoint => $"Control point {selection.ElementIndex} selected",
            EditorSelectionKind.Sample => $"Frame sample {selection.SampleIndex} selected",
            _ => "No selection"
        };
    }

    private static string DescribeViewportSelection(
        EditorSelection? selection,
        TrackViewportSnapshot snapshot)
    {
        if (selection?.Kind == EditorSelectionKind.Sample &&
            selection.SampleIndex >= 0 &&
            selection.SampleIndex < snapshot.Samples.Count)
        {
            TrackViewportSample sample = snapshot.Samples[selection.SampleIndex];
            return $"s {sample.Distance:F2} m\nκ {sample.Curvature:F5} 1/m\nbank {sample.RollDegrees:F2}°";
        }

        if (selection?.SectionIndex >= 0)
        {
            return $"Section {selection.SectionIndex + 1}\nClick a frame sample for station diagnostics";
        }

        return "Track workspace\nClick a frame sample for station diagnostics";
    }

    private static string FormatVector(Quantum.Math.Vector3d vector)
    {
        return $"({vector.X:F4}, {vector.Y:F4}, {vector.Z:F4})";
    }
}
