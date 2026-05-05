using Quantum.Track;

namespace Quantum.Tests;

public sealed class ForceTargetSamplerTests
{
    [Fact]
    public void ForceTargetSampler_Sample_ReturnsExpectedNormalGFromForceSection()
    {
        var first = new ForceSection(targetNormalG: 2.25, length: 2.0);
        var second = new ForceSection(targetNormalG: 3.5, targetLateralG: -0.15, length: 3.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (first, 2.0),
            (second, 3.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 1.0);

        Assert.Equal(2.25, sampled.TargetNormalG);
    }

    [Fact]
    public void ForceTargetSampler_Sample_BoundaryBetweenIntervals_UsesNextSection()
    {
        var first = new ForceSection(targetNormalG: 2.0, targetLateralG: 0.1, length: 2.0);
        var second = new ForceSection(targetNormalG: 3.0, targetLateralG: -0.2, length: 3.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (first, 2.0),
            (second, 3.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 2.0);

        Assert.Equal(3.0, sampled.TargetNormalG);
        Assert.Equal(-0.2, sampled.TargetLateralG);
        Assert.Equal(0.0, sampled.NormalizedT);
    }

    [Fact]
    public void ForceTargetSampler_Sample_ExactFinalEndpoint_UsesFinalSection()
    {
        var first = new ForceSection(targetNormalG: 2.0, length: 2.0);
        var second = new ForceSection(targetNormalG: 3.0, targetLateralG: 0.05, length: 3.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (first, 2.0),
            (second, 3.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.Equal(3.0, sampled.TargetNormalG);
        Assert.Equal(0.05, sampled.TargetLateralG);
        Assert.Equal(1.0, sampled.NormalizedT);
    }

    [Fact]
    public void ForceTargetSampler_Sample_NormalizedTPassesThroughCorrectly()
    {
        var first = new ForceSection(targetNormalG: 2.0, length: 5.0);
        var second = new ForceSection(targetNormalG: 2.4, targetLateralG: 0.2, length: 3.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (first, 5.0),
            (second, 3.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 6.25);

        Assert.Equal(6.25, sampled.Distance);
        Assert.Equal(1.25 / 3.0, sampled.NormalizedT, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_LinearNormalG_AtStart_UsesStartValue()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Linear,
            startNormalG: 2.0,
            endNormalG: 4.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 0.0);

        Assert.Equal(2.0, sampled.TargetNormalG);
    }

    [Fact]
    public void ForceTargetSampler_Sample_LinearNormalG_AtMidpoint_UsesInterpolatedValue()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Linear,
            startNormalG: 2.0,
            endNormalG: 4.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(3.0, sampled.TargetNormalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_LinearNormalG_AtFinalEndpoint_UsesEndValue()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Linear,
            startNormalG: 2.0,
            endNormalG: 4.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 10.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(4.0, sampled.TargetNormalG.Value, 10);
        Assert.Equal(1.0, sampled.NormalizedT);
    }

    [Fact]
    public void ForceTargetSampler_Sample_LinearLateralG_UsesInterpolatedValue()
    {
        var section = new ForceSection(
            targetNormalG: 3.0,
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Linear,
            startLateralG: 0.2,
            endLateralG: -0.2);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 2.5);

        Assert.True(sampled.TargetLateralG.HasValue);
        Assert.Equal(0.1, sampled.TargetLateralG.Value, 10);
    }

    [Theory]
    [InlineData(-0.0001)]
    [InlineData(5.0001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ForceTargetSampler_Sample_OutOfRangeOrNonFiniteDistance_ThrowsArgumentOutOfRangeException(double distance)
    {
        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (new ForceSection(targetNormalG: 2.0, length: 2.0), 2.0),
            (new ForceSection(targetNormalG: 3.0, length: 3.0), 3.0)
        });

        Assert.Throws<ArgumentOutOfRangeException>(() => ForceTargetSampler.Sample(intervals, distance));
    }
}
