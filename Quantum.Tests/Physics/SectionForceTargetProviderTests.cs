using Quantum.Physics;
using Quantum.Track;

namespace Quantum.Tests;

public sealed class SectionForceTargetProviderTests
{
    [Fact]
    public void SectionForceTargetProvider_Sample_ReturnsSameValuesAsForceTargetSampler()
    {
        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (new ForceSection(targetNormalG: 2.0, targetLateralG: 0.1, length: 2.0), 2.0),
            (new ForceSection(targetNormalG: 3.0, targetLateralG: -0.2, length: 3.0), 3.0)
        });

        var provider = new SectionForceTargetProvider(intervals);

        SampledForceTarget expected = ForceTargetSampler.Sample(intervals, 1.5);
        SampledForceTarget actual = provider.Sample(1.5);

        Assert.Equal(expected.Distance, actual.Distance);
        Assert.Equal(expected.NormalizedT, actual.NormalizedT);
        Assert.Equal(expected.TargetNormalG, actual.TargetNormalG);
        Assert.Equal(expected.TargetLateralG, actual.TargetLateralG);
        Assert.Equal(expected.TargetLongitudinalG, actual.TargetLongitudinalG);
        Assert.Equal(expected.TargetRollRateDegPerSec, actual.TargetRollRateDegPerSec);
    }

    [Fact]
    public void SectionForceTargetProvider_Sample_BoundaryBetweenIntervals_UsesNextSection()
    {
        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (new ForceSection(targetNormalG: 2.0, targetLateralG: 0.1, length: 2.0), 2.0),
            (new ForceSection(targetNormalG: 3.0, targetLateralG: -0.2, length: 3.0), 3.0)
        });

        var provider = new SectionForceTargetProvider(intervals);

        SampledForceTarget sampled = provider.Sample(2.0);

        Assert.Equal(3.0, sampled.TargetNormalG);
        Assert.Equal(-0.2, sampled.TargetLateralG);
        Assert.Equal(0.0, sampled.NormalizedT);
    }

    [Fact]
    public void SectionForceTargetProvider_Sample_ExactFinalEndpoint_UsesFinalSection()
    {
        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (new ForceSection(targetNormalG: 2.0, length: 2.0), 2.0),
            (new ForceSection(targetNormalG: 3.0, targetLateralG: 0.05, length: 3.0), 3.0)
        });

        var provider = new SectionForceTargetProvider(intervals);

        SampledForceTarget sampled = provider.Sample(5.0);

        Assert.Equal(3.0, sampled.TargetNormalG);
        Assert.Equal(0.05, sampled.TargetLateralG);
        Assert.Equal(1.0, sampled.NormalizedT);
    }

    [Theory]
    [InlineData(-0.0001)]
    [InlineData(5.0001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void SectionForceTargetProvider_Sample_OutOfRangeOrNonFiniteDistance_ThrowsArgumentOutOfRangeException(double distance)
    {
        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (new ForceSection(targetNormalG: 2.0, length: 2.0), 2.0),
            (new ForceSection(targetNormalG: 3.0, length: 3.0), 3.0)
        });

        var provider = new SectionForceTargetProvider(intervals);

        Assert.Throws<ArgumentOutOfRangeException>(() => provider.Sample(distance));
    }

    [Fact]
    public void SectionForceTargetProvider_TryGetForceTargets_ReturnsCompatibleForceTargets()
    {
        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (new ForceSection(targetNormalG: 2.0, targetLateralG: -0.25, length: 3.0), 3.0)
        });

        var provider = new SectionForceTargetProvider(intervals);

        bool returned = provider.TryGetForceTargets(1.25, out ForceTargets targets);

        Assert.True(returned);
        Assert.Equal(2.0, targets.NormalG);
        Assert.Equal(-0.25, targets.LateralG);
        Assert.Equal(0.0, targets.RollRateDegPerSec);
    }

    [Fact]
    public void SectionForceTargetProvider_TryGetForceTargets_UsesRollRateChannelWhenPresent()
    {
        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (
                new ForceSection(
                    targetNormalG: 2.0,
                    targetLateralG: -0.25,
                    length: 3.0,
                    rollRateChannel: new KeyframedForceEasingFunction(new System.Collections.Generic.List<(double t, double value)>
                    {
                        (0.0, 6.0),
                        (1.0, 6.0)
                    })),
                3.0)
        });

        var provider = new SectionForceTargetProvider(intervals);

        bool returned = provider.TryGetForceTargets(1.25, out ForceTargets targets);

        Assert.True(returned);
        Assert.Equal(2.0, targets.NormalG);
        Assert.Equal(-0.25, targets.LateralG);
        Assert.Equal(6.0, targets.RollRateDegPerSec);
    }
}
