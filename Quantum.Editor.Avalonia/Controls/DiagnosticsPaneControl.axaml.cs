using Avalonia.Controls;
using Quantum.Editor.Avalonia.Models;
using Quantum.Track;

namespace Quantum.Editor.Avalonia.Controls;

public partial class DiagnosticsPaneControl : UserControl
{
    private TrackViewportSnapshot snapshot = TrackViewportSnapshot.Empty;

    public DiagnosticsPaneControl()
    {
        InitializeComponent();
        UpdatePresentation();
    }

    public TrackViewportSnapshot Snapshot
    {
        get => snapshot;
        set
        {
            snapshot = value ?? TrackViewportSnapshot.Empty;
            UpdatePresentation();
        }
    }

    private void UpdatePresentation()
    {
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
}
