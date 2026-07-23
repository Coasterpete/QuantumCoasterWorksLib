using Quantum.Editor.Avalonia.Models;
using Quantum.Track;
using Quantum.Track.Authoring;
using System.Diagnostics;

namespace Quantum.Editor.Avalonia.Services.Viewport;

public sealed class TrackSamplingService
{
    private const int MaximumSampleCount = 601;
    private const double TargetSpacing = 1.5;
    private const double RadiansToDegrees = 180.0 / System.Math.PI;

    public int GetSampleCount(double totalLength)
    {
        if (!double.IsFinite(totalLength) || totalLength <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalLength));
        }

        return System.Math.Clamp(
            (int)System.Math.Ceiling(totalLength / TargetSpacing) + 1,
            2,
            MaximumSampleCount);
    }

    public TrackViewportSnapshot CreateViewportSnapshot(EngineeringSnapshot snapshot)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            return CreateViewportSnapshotCore(snapshot);
        }
        finally
        {
            stopwatch.Stop();
            EditorViewportPipelineMeasurement.RecordViewportProjectionBuild(stopwatch.Elapsed);
        }
    }

    private static TrackViewportSnapshot CreateViewportSnapshotCore(EngineeringSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.SampleCount == 0 || snapshot.TotalLength <= 0.0)
        {
            return TrackViewportSnapshot.Empty;
        }

        TrackFrameContinuityReport continuity = TrackFrameContinuityDiagnostics.Analyze(
            snapshot.OrientationFrames,
            snapshot.StationGrid,
            TrackFrameContinuityThresholds.Default);

        var samples = new TrackViewportSample[snapshot.SampleCount];
        double maximumCurvature = 0.0;
        double maximumRoll = 0.0;
        for (int sampleIndex = 0; sampleIndex < snapshot.SampleCount; sampleIndex++)
        {
            EngineeringGeometrySample geometry = snapshot.Geometry[sampleIndex];
            TrackFrame frame = snapshot.OrientationFrames[sampleIndex];
            double curvature = geometry.CurvatureMagnitude ?? 0.0;
            double rollDegrees = snapshot.BankingRollRadians[sampleIndex] * RadiansToDegrees;
            maximumCurvature = System.Math.Max(maximumCurvature, System.Math.Abs(curvature));
            maximumRoll = System.Math.Max(maximumRoll, System.Math.Abs(rollDegrees));

            samples[sampleIndex] = new TrackViewportSample(
                sampleIndex,
                EngineeringSnapshotNavigation.FindSectionIndex(snapshot, sampleIndex),
                geometry.Station,
                geometry.Position,
                geometry.Tangent,
                frame.Normal,
                frame.Binormal,
                curvature,
                rollDegrees);
        }

        string[] diagnostics =
        {
            $"Math Plot snapshot {snapshot.Revision.SnapshotRevision} contains {snapshot.ResolvedSections.Count} sections over {snapshot.TotalLength:F2} m.",
            $"Canonical station grid contains {snapshot.SampleCount} samples at approximately {TargetSpacing:F1} m spacing.",
            $"Maximum |curvature|: {maximumCurvature:F5} 1/m.",
            $"Banking range: {maximumRoll:F2}° maximum absolute roll.",
            continuity.HasDiscontinuities
                ? $"Frame continuity: {continuity.Issues.Count} threshold issue(s)."
                : "Frame continuity: no default-threshold issues."
        };

        return new TrackViewportSnapshot(
            samples,
            snapshot.TotalLength,
            maximumCurvature,
            maximumRoll,
            diagnostics,
            continuity);
    }

}
