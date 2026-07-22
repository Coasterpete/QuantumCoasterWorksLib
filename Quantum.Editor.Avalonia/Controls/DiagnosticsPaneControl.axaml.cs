using Avalonia.Controls;
using Quantum.Editor.Avalonia.Models;
using Quantum.Track;

namespace Quantum.Editor.Avalonia.Controls;

public partial class DiagnosticsPaneControl : UserControl
{
    private TrackViewportSnapshot snapshot = TrackViewportSnapshot.Empty;
    private IReadOnlyList<string> liveAuthoringDiagnostics = Array.Empty<string>();

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

    public IReadOnlyList<string> LiveAuthoringDiagnostics
    {
        get => liveAuthoringDiagnostics;
        set
        {
            liveAuthoringDiagnostics = value ?? Array.Empty<string>();
            UpdatePresentation();
        }
    }

    private void UpdatePresentation()
    {
        var lines = new List<string>(snapshot.Diagnostics);
        lines.AddRange(liveAuthoringDiagnostics.Select(diagnostic =>
            "Live edit: " + diagnostic));
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
        DiagnosticSummaryText.Text = liveAuthoringDiagnostics.Count != 0
            ? $"{liveAuthoringDiagnostics.Count} live authoring diagnostic(s)"
            : continuity is null
            ? "No compiled diagnostics"
            : $"{continuity.IntervalCount} intervals | {continuity.Issues.Count} issues";
    }
}
