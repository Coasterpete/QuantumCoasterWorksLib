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
using Quantum.Track;
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
    private readonly Dictionary<string, TextBox> inspectorFields = new(StringComparer.Ordinal);

    public MainWindow()
        : this(CreateWorkspace())
    {
    }

    public MainWindow(EditorWorkspace workspace)
    {
        this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        InitializeComponent();

        this.workspace.WorkspaceChanged += OnWorkspaceChanged;
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
            : $"Quantum CoasterWorks Editor - {document.DisplayName}{(document.IsDirty ? " *" : string.Empty)}";
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
        ViewportStatsText.Text = snapshot.Samples.Count == 0
            ? "No compiled track samples"
            : $"{snapshot.TotalLength:F2} m | {snapshot.Samples.Count} frames | " +
              $"max |k| {snapshot.MaximumAbsoluteCurvature:F5} 1/m | " +
              $"max |bank| {snapshot.MaximumAbsoluteRollDegrees:F1} deg";
        ViewportSelectionOverlay.Text = DescribeViewportSelection(selection, snapshot);

        RebuildGraphPanel();
        RebuildInspector();
        RebuildDiagnostics();
    }

    private void RebuildGraphPanel()
    {
        GraphNodesPanel.Children.Clear();
        EditorSelection? selection = workspace.CurrentSelection;
        string? selectedNodeId = selection?.NodeId;
        if (selectedNodeId is null && selection?.SectionIndex >= 0)
        {
            selectedNodeId = workspace.GraphNodes
                .FirstOrDefault(node => node.RouteIndex == selection.SectionIndex)
                ?.NodeId;
        }

        for (int index = 0; index < workspace.GraphNodes.Count; index++)
        {
            EditorGraphNode node = workspace.GraphNodes[index];
            bool selected = string.Equals(selectedNodeId, node.NodeId, StringComparison.Ordinal);
            var header = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                ColumnSpacing = 8
            };
            header.Children.Add(new TextBlock
            {
                Text = (node.RouteIndex + 1).ToString("D2", CultureInfo.InvariantCulture),
                Foreground = global::Avalonia.Media.Brush.Parse("#6FA9D3"),
                FontFamily = new global::Avalonia.Media.FontFamily("Consolas"),
                VerticalAlignment = VerticalAlignment.Center
            });
            var title = new TextBlock
            {
                Text = node.NodeId,
                FontSize = 14,
                FontWeight = global::Avalonia.Media.FontWeight.SemiBold,
                TextTrimming = global::Avalonia.Media.TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(title, 1);
            header.Children.Add(title);
            var kind = new TextBlock
            {
                Text = node.SectionKind,
                Foreground = global::Avalonia.Media.Brush.Parse("#8FA5B9"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(kind, 2);
            header.Children.Add(kind);

            var content = new StackPanel
            {
                Spacing = 5,
                Children =
                {
                    header,
                    new TextBlock
                    {
                        Text = node.Summary,
                        Foreground = global::Avalonia.Media.Brush.Parse("#8FA5B9"),
                        FontSize = 11,
                        TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
                    }
                }
            };
            var button = new Button
            {
                Tag = node,
                Content = content,
                Background = global::Avalonia.Media.Brush.Parse(selected ? "#203B50" : "#18232E"),
                BorderBrush = global::Avalonia.Media.Brush.Parse(selected ? "#59B5E8" : "#34495C")
            };
            button.Classes.Add("graphNode");
            button.Click += OnGraphNodeClick;
            GraphNodesPanel.Children.Add(button);

            if (index + 1 < workspace.GraphNodes.Count)
            {
                var connector = new Grid
                {
                    Height = 28,
                    IsHitTestVisible = false
                };
                connector.Children.Add(new Border
                {
                    Width = 2,
                    Height = 22,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Background = global::Avalonia.Media.Brush.Parse("#3E718F")
                });
                connector.Children.Add(new TextBlock
                {
                    Text = "\u25BC",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Foreground = global::Avalonia.Media.Brush.Parse("#59B5E8"),
                    FontSize = 10
                });
                GraphNodesPanel.Children.Add(connector);
            }
        }
    }

    private void RebuildInspector()
    {
        inspectorFields.Clear();
        InspectorFieldsPanel.Children.Clear();

        TrackEditorDocument? document = workspace.ActiveDocument;
        EditorSelection? selection = workspace.CurrentSelection;
        if (document?.Graph is null || document.GraphCompileResult is null || selection is null)
        {
            InspectorTitleText.Text = "Nothing selected";
            AddInspectorNote("Select a graph node or viewport sample.");
            return;
        }

        switch (selection.Kind)
        {
            case EditorSelectionKind.Track:
                BuildTrackInspector(document);
                break;
            case EditorSelectionKind.Section:
                BuildGraphNodeInspector(document, selection);
                break;
            case EditorSelectionKind.Sample:
                BuildSampleInspector(selection.SampleIndex);
                break;
            case EditorSelectionKind.BankingKey:
                InspectorTitleText.Text = "Banking key";
                AddInspectorNote("Banking-key editing is outside the M157 graph-authoring vertical slice.");
                break;
            case EditorSelectionKind.ControlPoint:
                InspectorTitleText.Text = "Spatial control point";
                AddInspectorNote("Control-point editing is outside the M157 graph-authoring vertical slice.");
                break;
        }
    }

    private void BuildTrackInspector(TrackEditorDocument document)
    {
        InspectorTitleText.Text = "Track document";
        AddInspectorField(
            "sourceName",
            "Source name",
            document.AncillaryState?.SourceName ?? string.Empty,
            editable: false);
        AddInspectorField(
            "layoutId",
            "Layout ID",
            document.AncillaryState?.LayoutId ?? string.Empty,
            editable: false);
        AddInspectorField(
            "units",
            "Units",
            document.AncillaryState?.Units ?? string.Empty,
            editable: false);
        AddInspectorField(
            "sections",
            "Graph nodes",
            workspace.GraphNodes.Count.ToString(CultureInfo.InvariantCulture),
            editable: false);
        AddInspectorField(
            "length",
            "Compiled length",
            $"{workspace.ViewportSnapshot.TotalLength:F3} m",
            editable: false);
        HeartlineOffset? heartline = document.AncillaryState?.HeartlineOffset;
        if (heartline.HasValue)
        {
            AddInspectorField(
                "heartlineNormal",
                "Heartline normal",
                heartline.Value.NormalOffsetMeters.ToString("G17", CultureInfo.InvariantCulture),
                editable: false);
            AddInspectorField(
                "heartlineLateral",
                "Heartline lateral",
                heartline.Value.LateralOffsetMeters.ToString("G17", CultureInfo.InvariantCulture),
                editable: false);
        }

        AddInspectorNote(
            "M157 edits section nodes through the authoring graph. Document metadata remains read-only.");
    }

    private void BuildGraphNodeInspector(
        TrackEditorDocument document,
        EditorSelection selection)
    {
        IReadOnlyList<TrackAuthoringGraphNode> route = document.GraphCompileResult!.OrderedNodes;
        TrackAuthoringGraphNode? node = selection.NodeId is null
            ? null
            : route.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, selection.NodeId, StringComparison.Ordinal));
        if (node is null && selection.SectionIndex >= 0 && selection.SectionIndex < route.Count)
        {
            node = route[selection.SectionIndex];
        }

        if (node is null)
        {
            InspectorTitleText.Text = "Invalid graph node";
            return;
        }

        GeometricSectionDefinition section = node.Section;
        InspectorTitleText.Text = "Graph node: " + section.Id;
        AddInspectorField("id", "Node ID", section.Id, editable: false);
        AddInspectorField("kind", "Section kind", DescribeSectionKind(section), editable: false);
        AddInspectorField(
            "sectionLength",
            "Length (m)",
            section.Length.ToString("G17", CultureInfo.InvariantCulture),
            editable: false);
        AddInspectorField(
            "rollDegrees",
            "Section roll (deg)",
            (section.RollRadians * 180.0 / System.Math.PI).ToString("G17", CultureInfo.InvariantCulture),
            editable: false);

        if (section is ConstantCurvatureSectionDefinition arc)
        {
            AddInspectorField(
                "radius",
                "Signed radius (m)",
                arc.Radius.ToString("G17", CultureInfo.InvariantCulture));
            AddInspectorNote(
                "Changing signed radius recompiles the complete graph through the existing backend pipeline.");
            AddApplyButton("Apply radius", () => ApplyRadiusInspector(node.Id));
        }
        else if (section is CurvatureTransitionSectionDefinition transition)
        {
            AddInspectorField(
                "startCurvature",
                "Start curvature",
                transition.StartCurvature.ToString("G17", CultureInfo.InvariantCulture),
                editable: false);
            AddInspectorField(
                "endCurvature",
                "End curvature",
                transition.EndCurvature.ToString("G17", CultureInfo.InvariantCulture),
                editable: false);
            AddInspectorField(
                "interpolation",
                "Interpolation",
                transition.InterpolationMode.ToString(),
                editable: false);
            AddInspectorNote("Transition editing is intentionally deferred beyond M157.");
        }
        else if (section is SpatialSectionDefinition spatial)
        {
            AddInspectorField(
                "degree",
                "NURBS degree",
                spatial.Degree.ToString(CultureInfo.InvariantCulture),
                editable: false);
            AddInspectorField(
                "controlPoints",
                "Control points",
                spatial.ControlPoints.Count.ToString(CultureInfo.InvariantCulture),
                editable: false);
            AddInspectorNote("Spatial control-point editing is intentionally deferred beyond M157.");
        }
        else
        {
            AddInspectorNote(
                "Straight-node length editing is deferred because authored banking shares the total station domain.");
        }
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
        AddInspectorField(
            "sampleSection",
            "Graph node",
            ResolveGraphNodeId(sample.SectionIndex) ?? (sample.SectionIndex + 1).ToString(CultureInfo.InvariantCulture),
            editable: false);
        AddInspectorField("samplePosition", "Position", FormatVector(sample.Position), editable: false);
        AddInspectorField("sampleTangent", "Tangent", FormatVector(sample.Tangent), editable: false);
        AddInspectorField("sampleNormal", "Normal", FormatVector(sample.Normal), editable: false);
        AddInspectorField("sampleBinormal", "Binormal", FormatVector(sample.Binormal), editable: false);
        AddInspectorField("sampleCurvature", "Curvature", $"{sample.Curvature:F6} 1/m", editable: false);
        AddInspectorField("sampleRoll", "Banking", $"{sample.RollDegrees:F3} deg", editable: false);
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
                    $"{issue.Kind}: {issue.Interval.StartDistance:F2}-{issue.Interval.EndDistance:F2} m, " +
                    $"{issue.ActualAngleDegrees:F3} deg > {issue.ThresholdAngleDegrees:F3} deg.");
            }
        }

        DiagnosticsList.ItemsSource = lines;
        DiagnosticSummaryText.Text = continuity is null
            ? "No compiled diagnostics"
            : $"{continuity.IntervalCount} intervals | {continuity.Issues.Count} issues";
    }

    private void ApplyRadiusInspector(string nodeId)
    {
        try
        {
            double radius = NumberField("radius");
            workspace.ApplyGraphEdit($"Edit {nodeId} radius", graph =>
            {
                TrackAuthoringGraphNode node = graph.Nodes.Single(candidate =>
                    string.Equals(candidate.Id, nodeId, StringComparison.Ordinal));
                ConstantCurvatureSectionDefinition arc =
                    node.Section as ConstantCurvatureSectionDefinition ??
                    throw new InvalidOperationException(
                        $"Graph node ID '{nodeId}' is not a constant-curvature section.");
                var replacement = new ConstantCurvatureSectionDefinition(
                    arc.Id,
                    arc.Length,
                    radius,
                    arc.RollRadians);
                return graph.WithSection(nodeId, replacement);
            });
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

    private void OnViewportSampleSelected(object? sender, ViewportSampleSelectedEventArgs eventArgs)
    {
        TrackViewportSample sample = eventArgs.Sample;
        string? nodeId = ResolveGraphNodeId(sample.SectionIndex);
        workspace.Select(EditorSelection.Sample(sample.SampleIndex, sample.SectionIndex, nodeId));
        workspace.SetStatus(
            $"Selected frame sample {sample.SampleIndex} at station {sample.Distance:F2} m.");
    }

    private void OnGraphNodeClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: EditorGraphNode node })
        {
            workspace.Select(node.Selection);
            workspace.SetStatus("Selected graph node " + node.NodeId + ".");
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

    private string? ResolveGraphNodeId(int sectionIndex)
    {
        return workspace.GraphNodes
            .FirstOrDefault(node => node.RouteIndex == sectionIndex)
            ?.NodeId;
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

    private static string DescribeSelection(EditorSelection? selection)
    {
        return selection?.Kind switch
        {
            EditorSelectionKind.Track => "Track selected",
            EditorSelectionKind.Section =>
                "Graph node " + (selection.NodeId ?? (selection.SectionIndex + 1).ToString(CultureInfo.InvariantCulture)) +
                " selected",
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
            return $"s {sample.Distance:F2} m\nk {sample.Curvature:F5} 1/m\nbank {sample.RollDegrees:F2} deg";
        }

        if (selection?.SectionIndex >= 0)
        {
            string label = selection.NodeId ?? $"Section {selection.SectionIndex + 1}";
            return label + "\nClick a frame sample for station diagnostics";
        }

        return "Track graph workspace\nSelect a graph node to inspect it";
    }

    private static string FormatVector(Quantum.Math.Vector3d vector)
    {
        return $"({vector.X:F4}, {vector.Y:F4}, {vector.Z:F4})";
    }
}
