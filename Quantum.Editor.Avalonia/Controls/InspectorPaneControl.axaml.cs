using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Quantum.Application.Authoring;
using Quantum.Editor.Avalonia.Models;
using Quantum.Editor.Avalonia.Services;
using Quantum.Editor.Avalonia.Services.Authoring;
using Quantum.Editor.Avalonia.Services.Documents;
using Quantum.Editor.Avalonia.Services.Plots;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Editor.Avalonia.Controls;

public partial class InspectorPaneControl : UserControl
{
    private readonly Dictionary<string, TextBox> inspectorFields = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NumericUpDown> numericInspectorFields =
        new(StringComparer.Ordinal);
    private EditorWorkspace? workspace;
    private StraightLengthScrubberControl? lengthScrubber;
    private TextBlock? lengthScrubStatus;
    private string? activeInspectorNodeId;
    private double scrubStartLength;
    private double? lastSubmittedLength;

    public InspectorPaneControl()
    {
        InitializeComponent();
        InspectorTitleText.Text = "Nothing selected";
        AddInspectorNote("Select a section, Math Plot station, or viewport sample.");
    }

    public void Refresh(EditorWorkspace editorWorkspace)
    {
        workspace = editorWorkspace ?? throw new ArgumentNullException(nameof(editorWorkspace));
        if (CanRefreshActiveScrubInPlace(editorWorkspace))
        {
            RefreshActiveScrubPresentation();
            return;
        }

        RebuildInspector();
    }

    private void RebuildInspector()
    {
        EditorWorkspace currentWorkspace = GetWorkspace();
        inspectorFields.Clear();
        numericInspectorFields.Clear();
        lengthScrubber = null;
        lengthScrubStatus = null;
        activeInspectorNodeId = null;
        InspectorFieldsPanel.Children.Clear();

        TrackEditorDocument? document = currentWorkspace.ActiveDocument;
        EditorSelection? selection = currentWorkspace.CurrentSelection;
        if (document?.Graph is null || selection is null)
        {
            InspectorTitleText.Text = "Nothing selected";
            AddInspectorNote("Select a section, Math Plot station, or viewport sample.");
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
                AddInspectorNote("Banking-key editing is outside the M166 authoring scope.");
                break;
            case EditorSelectionKind.ControlPoint:
                InspectorTitleText.Text = "Spatial control point";
                AddInspectorNote("Control-point editing is outside the M166 authoring scope.");
                break;
        }
    }

    private void BuildTrackInspector(TrackEditorDocument document)
    {
        EditorWorkspace currentWorkspace = GetWorkspace();
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
            "Sections",
            currentWorkspace.GraphNodes.Count.ToString(CultureInfo.InvariantCulture),
            editable: false);
        AddInspectorField(
            "length",
            "Compiled length",
            $"{currentWorkspace.ViewportSnapshot.TotalLength:F3} m",
            editable: false);
        HeartlineOffset? heartline = document.AncillaryState?.HeartlineOffset;
        if (heartline.HasValue)
        {
            AddInspectorField(
                "heartlineNormal",
                "Heartline normal (m)",
                heartline.Value.NormalOffsetMeters.ToString("0.######", CultureInfo.InvariantCulture),
                editable: false);
            AddInspectorField(
                "heartlineLateral",
                "Heartline lateral (m)",
                heartline.Value.LateralOffsetMeters.ToString("0.######", CultureInfo.InvariantCulture),
                editable: false);
        }

        AddInspectorNote(document.IsEmpty
            ? "Use Add in the Route pane to create the first geometric section."
            : "Section edits compile atomically through the backend authoring graph.");
    }

    private void BuildGraphNodeInspector(
        TrackEditorDocument document,
        EditorSelection selection)
    {
        EditorWorkspace currentWorkspace = GetWorkspace();
        IReadOnlyList<TrackAuthoringGraphNode> route =
            currentWorkspace.PresentedState?.GraphCompileResult?.OrderedNodes ??
            document.GraphCompileResult!.OrderedNodes;
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

        GeometricSectionDefinition section =
            (GeometricSectionDefinition)node.Section;
        bool supportsParameterEditing =
            section is StraightSectionDefinition ||
            section is ConstantCurvatureSectionDefinition ||
            section is CurvatureTransitionSectionDefinition;
        InspectorTitleText.Text = "Section: " + section.Id;
        AddInspectorField("id", "Section ID", section.Id, editable: false);
        AddInspectorField("kind", "Section type", DescribeSectionKind(section), editable: false);
        if (supportsParameterEditing)
        {
            Control? lengthAdornment = null;
            if (section is StraightSectionDefinition)
            {
                lengthScrubber = CreateLengthScrubber();
                lengthAdornment = lengthScrubber;
                activeInspectorNodeId = node.Id;
            }

            AddInspectorNumericField(
                "sectionLength",
                "Length (m)",
                section.Length,
                AuthoringNumericParameterKind.LengthMeters,
                lengthAdornment);
            if (lengthScrubber is not null)
            {
                lengthScrubStatus = new TextBlock
                {
                    Foreground = Brush.Parse("#7F94A8"),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11
                };
                InspectorFieldsPanel.Children.Add(lengthScrubStatus);
                RefreshActiveScrubPresentation();
            }

            AddInspectorNumericField(
                "rollDegrees",
                "Section roll (deg)",
                section.RollRadians * 180.0 / System.Math.PI,
                AuthoringNumericParameterKind.RollDegrees);
        }
        else
        {
            AddInspectorField(
                "sectionLength",
                "Length (m)",
                section.Length.ToString("0.######", CultureInfo.InvariantCulture),
                editable: false);
            AddInspectorField(
                "rollDegrees",
                "Section roll (deg)",
                (section.RollRadians * 180.0 / System.Math.PI).ToString(
                    "0.###",
                    CultureInfo.InvariantCulture),
                editable: false);
        }

        if (section is ConstantCurvatureSectionDefinition arc)
        {
            AddInspectorNumericField(
                "radius",
                "Signed radius (m)",
                arc.Radius,
                AuthoringNumericParameterKind.SignedRadiusMeters);
        }
        else if (section is CurvatureTransitionSectionDefinition transition)
        {
            AddInspectorNumericField(
                "startCurvature",
                "Start curvature (1/m)",
                transition.StartCurvature,
                AuthoringNumericParameterKind.CurvaturePerMeter);
            AddInspectorNumericField(
                "endCurvature",
                "End curvature (1/m)",
                transition.EndCurvature,
                AuthoringNumericParameterKind.CurvaturePerMeter);
            AddInspectorField(
                "interpolation",
                "Interpolation",
                transition.InterpolationMode.ToString(),
                editable: false);
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
            AddInspectorNote("Spatial control-point editing is intentionally deferred beyond M166.");
        }
        else
        {
            AddInspectorNote("Straight sections use the common length and roll parameters.");
        }

        if (supportsParameterEditing)
        {
            AddInspectorNote(
                "Applying parameters validates and recompiles the complete immutable route.");
            AddApplyButton("Apply section", () => ApplySectionInspector(node.Id));
        }

        int sectionIndex = currentWorkspace.GraphNodes
            .First(candidate => string.Equals(candidate.NodeId, node.Id, StringComparison.Ordinal))
            .RouteIndex;
        int engineeringSampleIndex = FindSectionInspectionSampleIndex(sectionIndex);
        if (engineeringSampleIndex >= 0)
        {
            AddCanonicalEngineeringFields(engineeringSampleIndex, "sectionEngineering", includeSectionIdentity: false);
        }
    }

    private void BuildSampleInspector(int sampleIndex)
    {
        EditorWorkspace currentWorkspace = GetWorkspace();
        if (sampleIndex < 0 || sampleIndex >= currentWorkspace.ViewportSnapshot.Samples.Count)
        {
            InspectorTitleText.Text = "Invalid sample";
            return;
        }

        TrackViewportSample sample = currentWorkspace.ViewportSnapshot.Samples[sampleIndex];
        InspectorTitleText.Text = $"Station sample {sample.SampleIndex}";
        AddCanonicalEngineeringFields(sampleIndex, "sample", includeSectionIdentity: true);
        AddInspectorField("samplePosition", "Position", FormatVector(sample.Position), editable: false);
        AddInspectorField("sampleTangent", "Tangent", FormatVector(sample.Tangent), editable: false);
        AddInspectorField("sampleNormal", "Normal", FormatVector(sample.Normal), editable: false);
        AddInspectorField("sampleBinormal", "Binormal", FormatVector(sample.Binormal), editable: false);
    }

    private int FindSectionInspectionSampleIndex(int sectionIndex)
    {
        EditorWorkspace currentWorkspace = GetWorkspace();
        EngineeringStationCursor? cursor = currentWorkspace.StationCursor;
        if (cursor.HasValue &&
            cursor.Value.SampleIndex >= 0 &&
            cursor.Value.SampleIndex < currentWorkspace.ViewportSnapshot.Samples.Count &&
            currentWorkspace.ViewportSnapshot.Samples[cursor.Value.SampleIndex].SectionIndex == sectionIndex)
        {
            return cursor.Value.SampleIndex;
        }

        for (int sampleIndex = 0; sampleIndex < currentWorkspace.ViewportSnapshot.Samples.Count; sampleIndex++)
        {
            if (currentWorkspace.ViewportSnapshot.Samples[sampleIndex].SectionIndex == sectionIndex)
            {
                return sampleIndex;
            }
        }

        return -1;
    }

    private void AddCanonicalEngineeringFields(
        int sampleIndex,
        string keyPrefix,
        bool includeSectionIdentity)
    {
        EditorWorkspace currentWorkspace = GetWorkspace();
        EngineeringSnapshot? snapshot = currentWorkspace.EngineeringSnapshot;
        if (snapshot is null || sampleIndex < 0 || sampleIndex >= snapshot.SampleCount)
        {
            return;
        }

        TrackViewportSample sample = currentWorkspace.ViewportSnapshot.Samples[sampleIndex];
        TrackAuthoringGraphNode? sectionNode = currentWorkspace.ActiveDocument?.GraphCompileResult?
            .OrderedNodes
            .ElementAtOrDefault(sample.SectionIndex);
        if (includeSectionIdentity)
        {
            AddInspectorField(
                keyPrefix + "Index",
                "Sample index",
                sampleIndex.ToString(CultureInfo.InvariantCulture),
                editable: false);
            AddInspectorField(
                keyPrefix + "SectionId",
                "Section ID",
                sectionNode?.Id ?? (sample.SectionIndex + 1).ToString(CultureInfo.InvariantCulture),
                editable: false);
            AddInspectorField(
                keyPrefix + "SectionType",
                "Section type",
                sectionNode?.Section is GeometricSectionDefinition geometricSection
                    ? DescribeSectionKind(geometricSection)
                    : "Unavailable",
                editable: false);
            AddInspectorField(
                keyPrefix + "SectionLength",
                "Section length",
                sectionNode?.Section is GeometricSectionDefinition sectionDefinition
                    ? $"{sectionDefinition.Length:F3} m"
                    : "Unavailable",
                editable: false);
        }

        AddInspectorField(
            keyPrefix + "Station",
            includeSectionIdentity ? "Station" : "Math Plot station",
            $"{sample.Distance:F3} m",
            editable: false);
        double? elevation = EngineeringPlotProjection.GetValue(
            snapshot,
            EngineeringPlotKind.Elevation,
            sampleIndex);
        double? curvature = EngineeringPlotProjection.GetValue(
            snapshot,
            EngineeringPlotKind.Curvature,
            sampleIndex);
        double? banking = EngineeringPlotProjection.GetValue(
            snapshot,
            EngineeringPlotKind.Roll,
            sampleIndex);
        double? pitch = EngineeringPlotProjection.GetValue(
            snapshot,
            EngineeringPlotKind.Pitch,
            sampleIndex);
        double? yaw = EngineeringPlotProjection.GetValue(
            snapshot,
            EngineeringPlotKind.Yaw,
            sampleIndex);
        AddInspectorField(keyPrefix + "Elevation", "Elevation", FormatPlotValue(elevation, "m"), editable: false);
        AddInspectorField(keyPrefix + "Curvature", "Curvature", FormatPlotValue(curvature, "1/m", 6), editable: false);
        AddInspectorField(
            keyPrefix + "Radius",
            "Radius magnitude",
            curvature.HasValue && curvature.Value > 1e-12
                ? $"{1.0 / curvature.Value:F3} m"
                : curvature.HasValue ? "Infinite (zero curvature)" : "Unavailable",
            editable: false);
        AddInspectorField(keyPrefix + "Banking", "Banking", FormatPlotValue(banking, "deg"), editable: false);
        AddInspectorField(keyPrefix + "Pitch", "Pitch", FormatPlotValue(pitch, "deg"), editable: false);
        AddInspectorField(keyPrefix + "Yaw", "Yaw", FormatPlotValue(yaw, "deg"), editable: false);
    }

    private static string FormatPlotValue(double? value, string unit, int digits = 3)
    {
        return value.HasValue && double.IsFinite(value.Value)
            ? value.Value.ToString("F" + digits, CultureInfo.InvariantCulture) + " " + unit
            : "Unavailable";
    }

    private void AddInspectorField(string key, string label, string value, bool editable = false)
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
            Foreground = Brush.Parse("#8FA5B9"),
            TextWrapping = TextWrapping.Wrap
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

    private void AddInspectorNumericField(
        string key,
        string label,
        double value,
        AuthoringNumericParameterKind kind,
        Control? adornment = null)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(
                adornment is null ? "118,*" : "118,*,Auto"),
            ColumnSpacing = 8
        };
        var labelBlock = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush.Parse("#8FA5B9"),
            TextWrapping = TextWrapping.Wrap
        };
        NumericUpDown field = AuthoringNumericControls.Create(key, kind, value);
        field.Classes.Add("inspectorField");

        Grid.SetColumn(field, 1);
        grid.Children.Add(labelBlock);
        grid.Children.Add(field);
        if (adornment is not null)
        {
            Grid.SetColumn(adornment, 2);
            grid.Children.Add(adornment);
        }

        InspectorFieldsPanel.Children.Add(grid);
        numericInspectorFields[key] = field;
    }

    private StraightLengthScrubberControl CreateLengthScrubber()
    {
        var scrubber = new StraightLengthScrubberControl();
        scrubber.ScrubStarted += OnLengthScrubStarted;
        scrubber.ScrubDelta += OnLengthScrubDelta;
        scrubber.CommitRequested += OnLengthScrubCommitRequested;
        scrubber.CancelRequested += OnLengthScrubCancelRequested;
        return scrubber;
    }

    private void OnLengthScrubStarted(object? sender, EventArgs eventArgs)
    {
        EditorWorkspace currentWorkspace = GetWorkspace();
        if (activeInspectorNodeId is null ||
            !currentWorkspace.BeginStraightLengthEdit(activeInspectorNodeId))
        {
            lengthScrubber?.Cancel();
            return;
        }

        scrubStartLength = currentWorkspace.StraightLengthEdit!.CommittedLength;
        lastSubmittedLength = null;
        SubmitLengthPreview(scrubStartLength);
    }

    private void OnLengthScrubDelta(
        object? sender,
        StraightLengthScrubDeltaEventArgs eventArgs)
    {
        EditorWorkspace currentWorkspace = GetWorkspace();
        currentWorkspace.RecordStraightLengthPointerUpdate();
        double sensitivity = (eventArgs.Modifiers & KeyModifiers.Shift) != 0
            ? 0.01
            : (eventArgs.Modifiers & KeyModifiers.Control) != 0
                ? 1.0
                : 0.1;
        double absoluteLength =
            scrubStartLength + (eventArgs.TotalHorizontalDelta * sensitivity);
        SubmitLengthPreview(absoluteLength);
    }

    private void SubmitLengthPreview(double absoluteLength)
    {
        if (lastSubmittedLength.HasValue &&
            lastSubmittedLength.Value.Equals(absoluteLength))
        {
            return;
        }

        lastSubmittedLength = absoluteLength;
        try
        {
            AuthoringEvaluationSubmission submission =
                GetWorkspace().SubmitStraightLengthEdit(absoluteLength);
            _ = ObserveSubmissionAsync(submission);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException ||
            exception is ObjectDisposedException)
        {
            GetWorkspace().SetStatus("Live edit failed: " + exception.Message);
            lengthScrubber?.Cancel();
        }
    }

    private async Task ObserveSubmissionAsync(AuthoringEvaluationSubmission submission)
    {
        AuthoringEvaluationOutcome outcome = await submission.Completion.ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            EditorWorkspace? currentWorkspace = workspace;
            if (currentWorkspace is not null &&
                currentWorkspace.PublishStraightLengthOutcome(outcome))
            {
                RefreshActiveScrubPresentation();
            }
        });
    }

    private async void OnLengthScrubCommitRequested(object? sender, EventArgs eventArgs)
    {
        try
        {
            await GetWorkspace().CommitStraightLengthEditAsync();
        }
        catch (OperationCanceledException)
        {
            GetWorkspace().CancelStraightLengthEdit();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException ||
            exception is ObjectDisposedException)
        {
            GetWorkspace().CancelStraightLengthEdit();
            GetWorkspace().SetStatus("Live edit commit failed: " + exception.Message);
        }
    }

    private void OnLengthScrubCancelRequested(object? sender, EventArgs eventArgs)
    {
        GetWorkspace().CancelStraightLengthEdit();
    }

    private bool CanRefreshActiveScrubInPlace(EditorWorkspace editorWorkspace)
    {
        StraightLengthEditState? edit = editorWorkspace.StraightLengthEdit;
        return lengthScrubber?.IsScrubbing == true &&
               edit is not null &&
               string.Equals(edit.NodeId, activeInspectorNodeId, StringComparison.Ordinal) &&
               editorWorkspace.CurrentSelection?.Kind == EditorSelectionKind.Section &&
               string.Equals(
                   editorWorkspace.CurrentSelection.NodeId,
                   edit.NodeId,
                   StringComparison.Ordinal);
    }

    private void RefreshActiveScrubPresentation()
    {
        if (lengthScrubber is null ||
            !numericInspectorFields.TryGetValue("sectionLength", out NumericUpDown? field))
        {
            return;
        }

        StraightLengthEditState? edit = workspace?.StraightLengthEdit;
        if (edit is null)
        {
            lengthScrubber.IsInvalid = false;
            if (lengthScrubStatus is not null)
            {
                lengthScrubStatus.Text =
                    "Drag ↔ for live preview. Shift: fine, Ctrl: coarse.";
            }

            return;
        }

        string rawText = edit.RawLength.ToString("0.######", CultureInfo.InvariantCulture);
        field.Text = rawText;
        if (double.IsFinite(edit.RawLength))
        {
            try
            {
                field.Value = (decimal)edit.RawLength;
                field.Text = rawText;
            }
            catch (OverflowException)
            {
                field.Value = null;
            }
        }
        else
        {
            field.Value = null;
        }

        field.BorderBrush = Brush.Parse(edit.IsInvalid ? "#FF6B6B" : "#45657D");
        lengthScrubber.IsInvalid = edit.IsInvalid;
        if (lengthScrubStatus is not null)
        {
            string accepted = edit.AcceptedPreviewLength.HasValue
                ? edit.AcceptedPreviewLength.Value.ToString(
                    "0.######",
                    CultureInfo.InvariantCulture) + " m"
                : "none";
            lengthScrubStatus.Text =
                $"{edit.StatusText}  |  Committed {edit.CommittedLength:0.######} m" +
                $"  |  Accepted {accepted}" +
                (edit.Diagnostics.Count == 0
                    ? string.Empty
                    : Environment.NewLine + string.Join(" ", edit.Diagnostics));
            lengthScrubStatus.Foreground = Brush.Parse(
                edit.IsInvalid ? "#FF8F8F" : "#7F94A8");
        }
    }

    internal StraightLengthScrubberControl? LengthScrubber => lengthScrubber;

    internal string? LengthScrubStatusText => lengthScrubStatus?.Text;

    private void AddInspectorNote(string text)
    {
        InspectorFieldsPanel.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = Brush.Parse("#7F94A8"),
            TextWrapping = TextWrapping.Wrap,
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

    private void ApplySectionInspector(string nodeId)
    {
        EditorWorkspace currentWorkspace = GetWorkspace();
        try
        {
            double length = NumberField("sectionLength");
            double rollRadians = NumberField("rollDegrees") * System.Math.PI / 180.0;
            currentWorkspace.ApplyGraphEdit($"Edit section {nodeId}", graph =>
            {
                TrackAuthoringGraphNode node = graph.Nodes.Single(candidate =>
                    string.Equals(candidate.Id, nodeId, StringComparison.Ordinal));
                TrackAuthoringSectionDefinition replacement = node.Section switch
                {
                    StraightSectionDefinition straight =>
                        new StraightSectionDefinition(
                            straight.Id,
                            length,
                            rollRadians),
                    ConstantCurvatureSectionDefinition arc =>
                        new ConstantCurvatureSectionDefinition(
                            arc.Id,
                            length,
                            NumberField("radius"),
                            rollRadians),
                    CurvatureTransitionSectionDefinition transition =>
                        new CurvatureTransitionSectionDefinition(
                            transition.Id,
                            length,
                            NumberField("startCurvature"),
                            NumberField("endCurvature"),
                            transition.InterpolationMode,
                            rollRadians),
                    _ => throw new NotSupportedException(
                        $"Section type '{node.TypeId}' does not have an M166 Inspector editor.")
                };
                return TrackAuthoringGraphOperations.Replace(
                    graph,
                    nodeId,
                    replacement);
            });
        }
        catch (Exception exception) when (exception is FormatException || exception is OverflowException)
        {
            currentWorkspace.SetStatus("Edit rejected: " + exception.Message);
        }
    }

    private double NumberField(string key)
    {
        return numericInspectorFields.TryGetValue(key, out NumericUpDown? field)
            ? AuthoringNumericControls.ReadFiniteDouble(field, key)
            : throw new InvalidOperationException(
                $"Inspector numeric field '{key}' is unavailable.");
    }

    private EditorWorkspace GetWorkspace()
    {
        return workspace ?? throw new InvalidOperationException("The Inspector pane has not been initialized.");
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

    private static string FormatVector(Quantum.Math.Vector3d vector)
    {
        return $"({vector.X:F4}, {vector.Y:F4}, {vector.Z:F4})";
    }
}
