using Avalonia.Controls;
using Avalonia.Media;
using Quantum.Editor.Avalonia.Models;
using Quantum.Editor.Avalonia.Services.Trains;
using Quantum.Track;

namespace Quantum.Editor.Avalonia.Controls;

public partial class TrainSummaryPaneControl : UserControl
{
    public TrainSummaryPaneControl()
    {
        InitializeComponent();
    }

    public void Refresh(TrainConsistEditorSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        TrainConsistDefinition definition = session.CurrentDefinition;
        TrainConsistPresentation presentation = TrainConsistPresentation.Create(definition);

        ValidationStatusText.Text = session.LastAttemptSucceeded
            ? "Valid immutable TrainConsistDefinition"
            : "Last edit rejected: " + session.LastValidationMessage;
        ValidationStatusText.Foreground = new SolidColorBrush(
            Color.Parse(session.LastAttemptSucceeded ? "#9EE493" : "#FF9B8E"));
        TotalLengthText.Text = $"{presentation.ApproximateTotalLength:F3} m";
        InterCarGapText.Text = $"{presentation.InterCarGap:F3} m";
        CarSpacingText.Text = $"{definition.CarSpacing:F3} m";
        CarDimensionsText.Text =
            $"{definition.CarLength:F3} / {definition.CarWidth:F3} / {definition.CarHeight:F3} m";
        BogieSpacingText.Text = $"{definition.BogieSpacing:F3} m";
        RevisionText.Text = session.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture);
        GeometryNoteText.Text = presentation.InterCarGap < 0.0
            ? $"Bodies overlap by {-presentation.InterCarGap:F3} m in this center-spacing layout. " +
              "The backend permits this geometry."
            : string.Empty;
    }
}
