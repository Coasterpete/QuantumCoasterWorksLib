using Quantum.Editor.Avalonia.Models;
using Quantum.Editor.Avalonia.Services.Documents;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Editor.Avalonia.Services.Viewport;

public sealed class TrackSamplingService
{
    private const int MaximumSampleCount = 601;
    private const double TargetSpacing = 1.5;
    private const double RadiansToDegrees = 180.0 / System.Math.PI;

    public TrackViewportSnapshot Sample(TrackEditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        TrackAuthoringCompilation? compilation = document.Compilation;
        if (compilation is null || compilation.TotalLength <= 0.0)
        {
            return TrackViewportSnapshot.Empty;
        }

        double[] distances = BuildDistances(compilation.TotalLength);
        var evaluator = new TrackEvaluator(compilation.Runtime);
        TrackFrame[] frames = BankingProfileSampler.SampleFramesAtDistances(
            evaluator,
            compilation.BankingProfile,
            distances);
        TrackFrameSmoothnessReport smoothness = TrackFrameSmoothnessDiagnostics.Analyze(
            frames,
            distances);
        TrackFrameContinuityReport continuity = TrackFrameContinuityDiagnostics.Analyze(
            frames,
            distances,
            TrackFrameContinuityThresholds.Default);

        var samples = new TrackViewportSample[frames.Length];
        double maximumRoll = 0.0;

        for (int index = 0; index < frames.Length; index++)
        {
            double curvature = ResolveCurvature(index, smoothness);
            double rollDegrees = BankingProfileSampler.SampleRollRadians(
                compilation.BankingProfile,
                distances[index]) * RadiansToDegrees;
            maximumRoll = System.Math.Max(maximumRoll, System.Math.Abs(rollDegrees));

            TrackFrame frame = frames[index];
            samples[index] = new TrackViewportSample(
                index,
                ResolveSectionIndex(compilation.ResolvedSections, distances[index]),
                distances[index],
                frame.Position,
                frame.Tangent,
                frame.Normal,
                frame.Binormal,
                curvature,
                rollDegrees);
        }

        string[] diagnostics =
        {
            $"Compiled {compilation.ResolvedSections.Count} sections over {compilation.TotalLength:F2} m.",
            $"Sampled {samples.Length} transported frames at approximately {TargetSpacing:F1} m spacing.",
            $"Maximum |curvature|: {smoothness.CurvatureEstimate.MaxAbsolute:F5} 1/m.",
            $"Banking range: {maximumRoll:F2}° maximum absolute roll.",
            continuity.HasDiscontinuities
                ? $"Frame continuity: {continuity.Issues.Count} threshold issue(s)."
                : "Frame continuity: no default-threshold issues."
        };

        return new TrackViewportSnapshot(
            samples,
            compilation.TotalLength,
            smoothness.CurvatureEstimate.MaxAbsolute,
            maximumRoll,
            diagnostics,
            continuity);
    }

    private static double[] BuildDistances(double totalLength)
    {
        int sampleCount = System.Math.Clamp(
            (int)System.Math.Ceiling(totalLength / TargetSpacing) + 1,
            2,
            MaximumSampleCount);
        var distances = new double[sampleCount];
        for (int index = 0; index < sampleCount; index++)
        {
            distances[index] = totalLength * index / (sampleCount - 1);
        }

        return distances;
    }

    private static double ResolveCurvature(int sampleIndex, TrackFrameSmoothnessReport smoothness)
    {
        if (smoothness.IntervalCount == 0)
        {
            return 0.0;
        }

        int intervalIndex = System.Math.Min(sampleIndex, smoothness.IntervalCount - 1);
        return smoothness.Intervals[intervalIndex].CurvatureEstimate;
    }

    private static int ResolveSectionIndex(
        IReadOnlyList<ResolvedSectionInterval<GeometricSectionDefinition>> sections,
        double distance)
    {
        for (int index = 0; index < sections.Count; index++)
        {
            if (sections[index].Contains(distance))
            {
                return index;
            }
        }

        return sections.Count - 1;
    }
}
