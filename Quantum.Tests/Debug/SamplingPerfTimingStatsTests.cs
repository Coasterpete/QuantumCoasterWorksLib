using Quantum.Debug;

namespace Quantum.Tests;

public sealed class SamplingPerfTimingStatsTests
{
    [Fact]
    public void Compute_ReturnsMeanMinAndMax()
    {
        SamplingPerfTimingStats stats = SamplingPerfTimingStats.Compute(new[] { 4.0, 2.0, 10.0, 8.0 });

        Assert.Equal(6.0, stats.MeanMilliseconds);
        Assert.Equal(2.0, stats.MinMilliseconds);
        Assert.Equal(10.0, stats.MaxMilliseconds);
    }

    [Fact]
    public void ComputeThroughputOperationsPerSecond_UsesMeanMilliseconds()
    {
        SamplingPerfTimingStats stats = SamplingPerfTimingStats.Compute(new[] { 2.0, 4.0 });

        double throughput = stats.ComputeThroughputOperationsPerSecond(120);

        Assert.Equal(40000.0, throughput);
    }
}
