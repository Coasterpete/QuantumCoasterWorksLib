using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Quantum.Editor.Avalonia.Models;
using Quantum.Editor.Avalonia.Services;
using Quantum.Editor.Avalonia.Services.Documents;
using Quantum.Editor.Avalonia.Services.Plots;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Editor.Avalonia.Controls;

public partial class InspectorPaneControl : UserControl
{
    private readonly Dictionary<string, TextBox> inspectorFields = new(StringComparer.Ordinal);
    private EditorWorkspace? workspace;

    public InspectorPaneControl()
    {
        InitializeComponent();
        InspectorTitleText.Text = "Nothing selected";
        AddInspectorNote("Select a section, Math Plot station, or viewport sample.");
    }

    public void Refresh(EditorWorkspace editorWorkspace)
    {
        workspace = editorWorkspace ?? throw new ArgumentNullException(nameof(editorWorkspace));
        RebuildInspector();
    }

    private void RebuildInspector()
    {
        EditorWorkspace currentWorkspace = GetWorkspace();
        inspectorFields.Clear();
        InspectorFieldsPanel.Children.Clear();

        TrackEditorDocument? document = currentWorkspace.ActiveDocument;
        EditorSelection? selection = currentWorkspace.CurrentSelection;
        if (document?.Graph is null || document.GraphCompileResult is null || selection is null)
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
        EditorWorkspace currentWorkspace = GetWorkspace();
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
        InspectorTitleText.Text = "Section: " + section.Id;
        AddInspectorField("id", "Section ID", section.Id, editable: false);
        AddInspectorField("kind", "Section type", DescribeSectionKind(section), editable: false);
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
                sectionNode is null ? "Unavailable" : DescribeSectionKind(sectionNode.Section),
                editable: false);
            AddInspectorField(
                keyPrefix + "SectionLength",
                "Section length",
                sectionNode is null ? "Unavailable" : $"{sectionNode.Section.Length:F3} m",
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

    private void ApplyRadiusInspector(string nodeId)
    {
        EditorWorkspace currentWorkspace = GetWorkspace();
        try
        {
            double radius = NumberField("radius");
            currentWorkspace.ApplyGraphEdit($"Edit {nodeId} radius", graph =>
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
            currentWorkspace.SetStatus("Edit rejected: " + exception.Message);
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
