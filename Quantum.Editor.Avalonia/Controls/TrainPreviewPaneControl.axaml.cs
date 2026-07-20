using Avalonia.Controls;
using Quantum.Editor.Avalonia.Models;
using Quantum.Track;

namespace Quantum.Editor.Avalonia.Controls;

public partial class TrainPreviewPaneControl : UserControl
{
    private TrainConsistDefinition? definition;

    public TrainPreviewPaneControl()
    {
        InitializeComponent();
    }

    public TrainConsistDefinition? Definition
    {
        get => definition;
        set
        {
            definition = value;
            PreviewControl.Definition = value;
            PreviewCaptionText.Text = value is null
                ? "No valid consist definition"
                : Describe(TrainConsistPresentation.Create(value));
        }
    }

    private static string Describe(TrainConsistPresentation presentation)
    {
        TrainConsistDefinition value = presentation.Definition;
        return $"{value.CarCount} cars  |  {presentation.ApproximateTotalLength:F2} m overall  |  " +
            $"body {value.CarLength:F2} x {value.CarWidth:F2} m  |  yellow lines: bogie centers";
    }
}
