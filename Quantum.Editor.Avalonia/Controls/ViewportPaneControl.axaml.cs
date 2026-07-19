using Avalonia.Controls;
using Quantum.Editor.Avalonia.Models;
using Quantum.Editor.Avalonia.Services.Viewport;

namespace Quantum.Editor.Avalonia.Controls;

public partial class ViewportPaneControl : UserControl, IViewportSurface
{
    private TrackViewportSnapshot snapshot = TrackViewportSnapshot.Empty;
    private EditorSelection? selection;

    public ViewportPaneControl()
    {
        InitializeComponent();
        UpdatePresentation();
    }

    public event EventHandler<ViewportSampleSelectedEventArgs>? SampleSelected;

    public event EventHandler<SectionPointerChangedEventArgs>? SectionPointerChanged;

    string IViewportSurface.Name => "Track technical viewport";

    public TrackViewportSnapshot Snapshot
    {
        get => snapshot;
        set
        {
            snapshot = value ?? TrackViewportSnapshot.Empty;
            ViewportControl.Snapshot = snapshot;
            UpdatePresentation();
        }
    }

    public EditorSelection? Selection
    {
        get => selection;
        set
        {
            selection = value;
            ViewportControl.Selection = value;
            UpdatePresentation();
        }
    }

    public int StationCursorSampleIndex
    {
        get => ViewportControl.StationCursorSampleIndex;
        set => ViewportControl.StationCursorSampleIndex = value;
    }

    public int HighlightedSectionIndex
    {
        get => ViewportControl.HighlightedSectionIndex;
        set => ViewportControl.HighlightedSectionIndex = value;
    }

    public TrackViewportProjection Projection
    {
        get => ViewportControl.Projection;
        set => ViewportControl.Projection = value;
    }

    public bool ShowFrames
    {
        get => ViewportControl.ShowFrames;
        set => ViewportControl.ShowFrames = value;
    }

    public void FitToTrack()
    {
        ViewportControl.FitToTrack();
    }

    private void UpdatePresentation()
    {
        ViewportStatsText.Text = snapshot.Samples.Count == 0
            ? "No compiled track samples"
            : $"{snapshot.TotalLength:F2} m | {snapshot.Samples.Count} frames | " +
              $"max |k| {snapshot.MaximumAbsoluteCurvature:F5} 1/m | " +
              $"max |bank| {snapshot.MaximumAbsoluteRollDegrees:F1} deg";
        ViewportSelectionOverlay.Text = DescribeSelection(selection, snapshot);
    }

    private void OnSampleSelected(object? sender, ViewportSampleSelectedEventArgs eventArgs)
    {
        SampleSelected?.Invoke(this, eventArgs);
    }

    private void OnSectionPointerChanged(object? sender, SectionPointerChangedEventArgs eventArgs)
    {
        SectionPointerChanged?.Invoke(this, eventArgs);
    }

    private static string DescribeSelection(
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
            return label + "\nClick a Math Plot or viewport sample for station diagnostics";
        }

        return "Track workspace\nSelect a section or use the Math Plots";
    }
}
