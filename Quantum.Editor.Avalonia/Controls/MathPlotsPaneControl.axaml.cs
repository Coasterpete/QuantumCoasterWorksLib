using Avalonia.Controls;
using Avalonia.Interactivity;
using Quantum.Editor.Avalonia.Services;
using Quantum.Editor.Avalonia.Services.Plots;
using Quantum.Track.Authoring;

namespace Quantum.Editor.Avalonia.Controls;

public partial class MathPlotsPaneControl : UserControl
{
    public MathPlotsPaneControl()
    {
        InitializeComponent();
    }

    public event EventHandler<EngineeringStationChangedEventArgs>? StationChanged;

    public event EventHandler<EngineeringStationChangedEventArgs>? StationSelected;

    public event EventHandler<SectionPointerChangedEventArgs>? SectionPointerChanged;

    public EngineeringSnapshot? Snapshot
    {
        get => EngineeringPlotsControl.Snapshot;
        set => EngineeringPlotsControl.Snapshot = value;
    }

    public int CursorSampleIndex
    {
        get => EngineeringPlotsControl.CursorSampleIndex;
        set => EngineeringPlotsControl.CursorSampleIndex = value;
    }

    public int HighlightedSectionIndex
    {
        get => EngineeringPlotsControl.HighlightedSectionIndex;
        set => EngineeringPlotsControl.HighlightedSectionIndex = value;
    }

    public string StationReadout
    {
        get => StationReadoutText.Text ?? string.Empty;
        set => StationReadoutText.Text = value ?? string.Empty;
    }

    public EngineeringPlotKind EnabledPlots => EngineeringPlotsControl.EnabledPlots;

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

    private void OnStationChanged(object? sender, EngineeringStationChangedEventArgs eventArgs)
    {
        StationChanged?.Invoke(this, eventArgs);
    }

    private void OnStationSelected(object? sender, EngineeringStationChangedEventArgs eventArgs)
    {
        StationSelected?.Invoke(this, eventArgs);
    }

    private void OnSectionPointerChanged(object? sender, SectionPointerChangedEventArgs eventArgs)
    {
        SectionPointerChanged?.Invoke(this, eventArgs);
    }
}
