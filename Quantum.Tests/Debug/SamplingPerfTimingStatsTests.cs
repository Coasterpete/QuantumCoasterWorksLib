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

    [Fact]
    public void ComputeRelativeSpeedupAgainst_ReturnsFactorGreaterThanOneWhenFasterThanBaseline()
    {
        SamplingPerfTimingStats benchmark = SamplingPerfTimingStats.Compute(new[] { 2.0, 2.0 });
        SamplingPerfTimingStats baseline = SamplingPerfTimingStats.Compute(new[] { 4.0, 4.0 });

        double factor = benchmark.ComputeRelativeSpeedupAgainst(baseline);

        Assert.Equal(2.0, factor);
    }

    [Fact]
    public void ComputeRelativeSpeedupAgainst_ReturnsFactorLessThanOneWhenSlowerThanBaseline()
    {
        SamplingPerfTimingStats benchmark = SamplingPerfTimingStats.Compute(new[] { 6.0, 6.0 });
        SamplingPerfTimingStats baseline = SamplingPerfTimingStats.Compute(new[] { 3.0, 3.0 });

        double factor = benchmark.ComputeRelativeSpeedupAgainst(baseline);

        Assert.Equal(0.5, factor);
    }
}
