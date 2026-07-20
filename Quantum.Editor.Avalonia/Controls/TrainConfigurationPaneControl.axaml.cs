using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Quantum.Editor.Avalonia.Services.Trains;
using Quantum.Track;

namespace Quantum.Editor.Avalonia.Controls;

public partial class TrainConfigurationPaneControl : UserControl
{
    private TrainConsistEditorSession? session;

    public TrainConfigurationPaneControl()
    {
        InitializeComponent();
    }

    public TrainConsistEditorSession? Session => session;

    public void Bind(TrainConsistEditorSession editorSession)
    {
        session = editorSession ?? throw new ArgumentNullException(nameof(editorSession));
        LoadDefinition(editorSession.CurrentDefinition);
        UpdateValidationPresentation();
    }

    private void OnApplyClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (session is null)
        {
            return;
        }

        bool applied = session.TryApply(new TrainConsistInput(
            CarCountTextBox.Text ?? string.Empty,
            CarSpacingTextBox.Text ?? string.Empty,
            CarLengthTextBox.Text ?? string.Empty,
            CarWidthTextBox.Text ?? string.Empty,
            CarHeightTextBox.Text ?? string.Empty,
            BogieSpacingTextBox.Text ?? string.Empty));
        if (applied)
        {
            LoadDefinition(session.CurrentDefinition);
        }

        UpdateValidationPresentation();
    }

    private void LoadDefinition(TrainConsistDefinition definition)
    {
        TrainConsistInput input = TrainConsistInput.FromDefinition(definition);
        CarCountTextBox.Text = input.CarCount;
        CarSpacingTextBox.Text = input.CarCenterSpacing;
        CarLengthTextBox.Text = input.CarBodyLength;
        CarWidthTextBox.Text = input.CarBodyWidth;
        CarHeightTextBox.Text = input.CarBodyHeight;
        BogieSpacingTextBox.Text = input.BogieSpacing;
    }

    private void UpdateValidationPresentation()
    {
        bool isValid = session?.LastAttemptSucceeded != false;
        ValidationText.Text = isValid
            ? "Valid consist definition"
            : "Edit rejected: " + session!.LastValidationMessage;
        ValidationText.Foreground = new SolidColorBrush(
            Color.Parse(isValid ? "#9EE493" : "#FF9B8E"));
    }
}
