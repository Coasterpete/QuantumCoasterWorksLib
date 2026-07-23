namespace Quantum.Editor.Avalonia.Services.Authoring;

/// <summary>
/// Nearest-rank latency percentiles for observational live-edit measurements.
/// Empty samples produce a zero-count, zero-duration summary.
/// </summary>
public sealed record LatencyPercentileSummary(
    int SampleCount,
    TimeSpan P50,
    TimeSpan P95,
    TimeSpan P99);

public static class LatencyPercentiles
{
    public static LatencyPercentileSummary Calculate(IEnumerable<TimeSpan> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        TimeSpan[] ordered = samples.OrderBy(sample => sample).ToArray();
        if (ordered.Length == 0)
        {
            return new LatencyPercentileSummary(
                0,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero);
        }

        if (ordered[0] < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(samples),
                "Latency samples cannot be negative.");
        }

        return new LatencyPercentileSummary(
            ordered.Length,
            NearestRank(ordered, 0.50),
            NearestRank(ordered, 0.95),
            NearestRank(ordered, 0.99));
    }

    private static TimeSpan NearestRank(TimeSpan[] ordered, double percentile)
    {
        int index = System.Math.Max(
            0,
            (int)System.Math.Ceiling(percentile * ordered.Length) - 1);
        return ordered[index];
    }
}
