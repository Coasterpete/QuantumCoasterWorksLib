using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Quantum.Track.Authoring;

namespace Quantum.Editor.Avalonia.Controls;

/// <summary>
/// Presentation-only typed creator for the initial M166 geometric catalog.
/// Backend constructors remain authoritative for validation.
/// </summary>
public sealed class SectionEditorDialog : Window
{
    private readonly ComboBox typeSelector;
    private readonly TextBox idField;
    private readonly NumericUpDown lengthField;
    private readonly NumericUpDown rollField;
    private readonly NumericUpDown radiusField;
    private readonly NumericUpDown startCurvatureField;
    private readonly NumericUpDown endCurvatureField;
    private readonly Control radiusRow;
    private readonly Control startCurvatureRow;
    private readonly Control endCurvatureRow;
    private readonly TextBlock validationText;
    private readonly Func<string, string> suggestId;
    private string lastSuggestedId;

    public SectionEditorDialog(Func<string, string> suggestId)
    {
        this.suggestId = suggestId ?? throw new ArgumentNullException(nameof(suggestId));

        Title = "Add Geometric Section";
        Width = 470;
        Height = 500;
        MinWidth = 420;
        MinHeight = 450;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        SectionChoice[] choices = CreateChoices();
        typeSelector = new ComboBox
        {
            ItemsSource = choices,
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        typeSelector.SelectionChanged += (_, _) => UpdateTypePresentation();

        idField = CreateTextBox();
        lengthField = AuthoringNumericControls.Create(
            "length",
            AuthoringNumericParameterKind.LengthMeters,
            10.0);
        rollField = AuthoringNumericControls.Create(
            "rollDegrees",
            AuthoringNumericParameterKind.RollDegrees,
            0.0);
        radiusField = AuthoringNumericControls.Create(
            "radius",
            AuthoringNumericParameterKind.SignedRadiusMeters,
            25.0);
        startCurvatureField = AuthoringNumericControls.Create(
            "startCurvature",
            AuthoringNumericParameterKind.CurvaturePerMeter,
            0.0);
        endCurvatureField = AuthoringNumericControls.Create(
            "endCurvature",
            AuthoringNumericParameterKind.CurvaturePerMeter,
            0.04);

        lastSuggestedId = suggestId(TrackAuthoringSectionTypeIds.Straight);
        idField.Text = lastSuggestedId;

        var fields = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                CreateFieldRow("Family", ReadOnlyText("Geometry")),
                CreateFieldRow("Section type", typeSelector),
                CreateFieldRow("Section ID", idField),
                CreateFieldRow("Length (m)", lengthField),
                CreateFieldRow("Roll (deg)", rollField)
            }
        };
        radiusRow = CreateFieldRow("Signed radius (m)", radiusField);
        startCurvatureRow = CreateFieldRow("Start curvature (1/m)", startCurvatureField);
        endCurvatureRow = CreateFieldRow("End curvature (1/m)", endCurvatureField);
        fields.Children.Add(radiusRow);
        fields.Children.Add(startCurvatureRow);
        fields.Children.Add(endCurvatureRow);

        validationText = new TextBlock
        {
            Foreground = Brush.Parse("#E98B8B"),
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 44
        };

        var addButton = new Button
        {
            Content = "Add and Compile",
            MinWidth = 135
        };
        addButton.Click += (_, _) => TryAccept();
        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 90
        };
        cancelButton.Click += (_, _) => Close(null);

        Content = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children =
            {
                new ScrollViewer
                {
                    Content = new StackPanel
                    {
                        Margin = new global::Avalonia.Thickness(20),
                        Spacing = 14,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "Create a typed backend section definition. " +
                                       "The complete route is validated and compiled before it is committed.",
                                Foreground = Brush.Parse("#8FA5B9"),
                                TextWrapping = TextWrapping.Wrap
                            },
                            fields,
                            validationText
                        }
                    }
                },
                CreateButtonBar(cancelButton, addButton)
            }
        };

        UpdateTypePresentation();
    }

    private SectionChoice SelectedChoice =>
        typeSelector.SelectedItem as SectionChoice ??
        throw new InvalidOperationException("A section type must be selected.");

    private void UpdateTypePresentation()
    {
        if (typeSelector.SelectedItem is not SectionChoice choice)
        {
            return;
        }

        bool arc = choice.TypeId == TrackAuthoringSectionTypeIds.ConstantCurvature;
        bool transition = choice.TypeId == TrackAuthoringSectionTypeIds.CurvatureTransition;
        radiusRow.IsVisible = arc;
        startCurvatureRow.IsVisible = transition;
        endCurvatureRow.IsVisible = transition;

        string currentId = idField.Text ?? string.Empty;
        string replacementSuggestion = suggestId(choice.TypeId);
        if (string.IsNullOrWhiteSpace(currentId) ||
            string.Equals(currentId, lastSuggestedId, StringComparison.Ordinal))
        {
            idField.Text = replacementSuggestion;
        }

        lastSuggestedId = replacementSuggestion;
        validationText.Text = string.Empty;
    }

    private void TryAccept()
    {
        try
        {
            string id = idField.Text ?? string.Empty;
            double length = Number(lengthField, "length");
            double rollRadians = Number(rollField, "roll") * System.Math.PI / 180.0;
            TrackAuthoringSectionDefinition section = SelectedChoice.TypeId switch
            {
                TrackAuthoringSectionTypeIds.Straight =>
                    new StraightSectionDefinition(id, length, rollRadians),
                TrackAuthoringSectionTypeIds.ConstantCurvature =>
                    new ConstantCurvatureSectionDefinition(
                        id,
                        length,
                        Number(radiusField, "signed radius"),
                        rollRadians),
                TrackAuthoringSectionTypeIds.CurvatureTransition =>
                    new CurvatureTransitionSectionDefinition(
                        id,
                        length,
                        Number(startCurvatureField, "start curvature"),
                        Number(endCurvatureField, "end curvature"),
                        CurvatureTransitionInterpolationMode.Linear,
                        rollRadians),
                _ => throw new NotSupportedException(
                    $"Section type '{SelectedChoice.TypeId}' is not in the M166 creation catalog.")
            };

            Close(section);
        }
        catch (Exception exception) when (
            exception is ArgumentException ||
            exception is InvalidOperationException ||
            exception is NotSupportedException ||
            exception is FormatException ||
            exception is OverflowException)
        {
            validationText.Text = exception.Message.Replace(Environment.NewLine, " ");
        }
    }

    private static SectionChoice[] CreateChoices()
    {
        IReadOnlyList<string> authorableTypeIds = new[]
        {
            TrackAuthoringSectionTypeIds.Straight,
            TrackAuthoringSectionTypeIds.ConstantCurvature,
            TrackAuthoringSectionTypeIds.CurvatureTransition
        };
        return TrackAuthoringSectionCatalog.Types
            .Where(type =>
                type.Family == TrackAuthoringSectionFamily.Geometry &&
                authorableTypeIds.Contains(type.TypeId))
            .Select(type => new SectionChoice(type.TypeId, DisplayName(type.TypeId)))
            .ToArray();
    }

    private static string DisplayName(string typeId)
    {
        return typeId switch
        {
            TrackAuthoringSectionTypeIds.Straight => "Straight",
            TrackAuthoringSectionTypeIds.ConstantCurvature => "Constant Curvature",
            TrackAuthoringSectionTypeIds.CurvatureTransition => "Curvature Transition",
            _ => typeId
        };
    }

    private static Grid CreateFieldRow(string label, Control field)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("145,*"),
            ColumnSpacing = 10
        };
        row.Children.Add(new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush.Parse("#8FA5B9"),
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(field, 1);
        row.Children.Add(field);
        return row;
    }

    private static Border CreateButtonBar(Button cancelButton, Button addButton)
    {
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancelButton, addButton }
        };
        var border = new Border
        {
            Padding = new global::Avalonia.Thickness(20, 12),
            BorderBrush = Brush.Parse("#2B3948"),
            BorderThickness = new global::Avalonia.Thickness(0, 1, 0, 0),
            Child = buttons
        };
        Grid.SetRow(border, 1);
        return border;
    }

    private static TextBox CreateTextBox(string text = "")
    {
        return new TextBox { Text = text, MinHeight = 30 };
    }

    private static TextBox ReadOnlyText(string text)
    {
        return new TextBox { Text = text, IsReadOnly = true, MinHeight = 30 };
    }

    private static double Number(NumericUpDown field, string label)
    {
        return AuthoringNumericControls.ReadFiniteDouble(field, label);
    }

    private sealed class SectionChoice
    {
        public SectionChoice(string typeId, string displayName)
        {
            TypeId = typeId;
            DisplayName = displayName;
        }

        public string TypeId { get; }

        public string DisplayName { get; }

        public override string ToString() => DisplayName;
    }
}
