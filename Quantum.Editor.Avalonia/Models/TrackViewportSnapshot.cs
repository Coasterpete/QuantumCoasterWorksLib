using Quantum.Track;

namespace Quantum.Editor.Avalonia.Models;

public sealed class TrackViewportSnapshot
{
    public static TrackViewportSnapshot Empty { get; } = new(
        Array.Empty<TrackViewportSample>(),
        0.0,
        0.0,
        0.0,
        Array.Empty<string>(),
        null);

    public TrackViewportSnapshot(
        IReadOnlyList<TrackViewportSample> samples,
        double totalLength,
        double maximumAbsoluteCurvature,
        double maximumAbsoluteRollDegrees,
        IReadOnlyList<string> diagnostics,
        TrackFrameContinuityReport? continuityReport)
    {
        Samples = samples ?? throw new ArgumentNullException(nameof(samples));
        TotalLength = totalLength;
        MaximumAbsoluteCurvature = maximumAbsoluteCurvature;
        MaximumAbsoluteRollDegrees = maximumAbsoluteRollDegrees;
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        ContinuityReport = continuityReport;
    }

    public IReadOnlyList<TrackViewportSample> Samples { get; }

    public double TotalLength { get; }

    public double MaximumAbsoluteCurvature { get; }

    public double MaximumAbsoluteRollDegrees { get; }

    public IReadOnlyList<string> Diagnostics { get; }

    public TrackFrameContinuityReport? ContinuityReport { get; }
}
