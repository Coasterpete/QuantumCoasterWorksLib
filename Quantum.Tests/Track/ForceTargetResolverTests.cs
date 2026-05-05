using Quantum.Track;

namespace Quantum.Tests;

public sealed class ForceTargetResolverTests
{
    [Fact]
    public void ForceTargetResolver_Resolve_CreatesExpectedCumulativeIntervals()
    {
        var lift = new ForceSection(targetNormalG: 2.5, length: 5.0);
        var crest = new ForceSection(targetNormalG: 1.2, targetLateralG: 0.1, length: 3.0);
        var drop = new ForceSection(targetNormalG: 3.8, targetLateralG: -0.2, length: 2.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (lift, 5.0),
            (crest, 3.0),
            (drop, 2.0)
        });

        Assert.Equal(3, intervals.Count);

        Assert.Same(lift, intervals[0].Section);
        Assert.Equal(0.0, intervals[0].StartDistance);
        Assert.Equal(5.0, intervals[0].EndDistance);

        Assert.Same(crest, intervals[1].Section);
        Assert.Equal(5.0, intervals[1].StartDistance);
        Assert.Equal(8.0, intervals[1].EndDistance);

        Assert.Same(drop, intervals[2].Section);
        Assert.Equal(8.0, intervals[2].StartDistance);
        Assert.Equal(10.0, intervals[2].EndDistance);
    }

    [Fact]
    public void ForceTargetResolver_Lookup_BoundaryBetweenIntervals_ReturnsNextSection()
    {
        var first = new ForceSection(targetNormalG: 2.0, length: 2.0);
        var second = new ForceSection(targetNormalG: 3.0, length: 3.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (first, 2.0),
            (second, 3.0)
        });

        ForceTargetSnapshot snapshot = ForceTargetResolver.Lookup(intervals, 2.0);

        Assert.Same(second, snapshot.ResolvedSection);
        Assert.Equal(0.0, snapshot.LocalDistance);
        Assert.Equal(0.0, snapshot.NormalizedT);
    }

    [Fact]
    public void ForceTargetResolver_Lookup_ExactFinalEndpoint_ReturnsLastSectionWithNormalizedTOne()
    {
        var first = new ForceSection(targetNormalG: 2.0, length: 2.0);
        var second = new ForceSection(targetNormalG: 3.0, length: 3.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (first, 2.0),
            (second, 3.0)
        });

        ForceTargetSnapshot snapshot = ForceTargetResolver.Lookup(intervals, 5.0);

        Assert.Same(second, snapshot.ResolvedSection);
        Assert.Equal(2.0, snapshot.StartDistance);
        Assert.Equal(5.0, snapshot.EndDistance);
        Assert.Equal(3.0, snapshot.LocalDistance);
        Assert.Equal(1.0, snapshot.NormalizedT);
    }

    [Fact]
    public void ForceTargetResolver_Lookup_ReturnsExpectedLocalDistance()
    {
        var first = new ForceSection(targetNormalG: 2.0, length: 5.0);
        var second = new ForceSection(targetNormalG: 2.4, targetLateralG: 0.2, length: 3.0);

        ForceTargetSnapshot snapshot = ForceTargetResolver.Lookup(
            new[]
            {
                (first, 5.0),
                (second, 3.0)
            },
            6.25);

        Assert.Same(second, snapshot.ResolvedSection);
        Assert.Equal(5.0, snapshot.StartDistance);
        Assert.Equal(8.0, snapshot.EndDistance);
        Assert.Equal(1.25, snapshot.LocalDistance, 10);
        Assert.Equal(1.25 / 3.0, snapshot.NormalizedT, 10);
    }

    [Theory]
    [InlineData(-0.0001)]
    [InlineData(5.0001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ForceTargetResolver_Lookup_OutOfRangeOrNonFiniteDistance_ThrowsArgumentOutOfRangeException(double distance)
    {
        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (new ForceSection(targetNormalG: 2.0, length: 2.0), 2.0),
            (new ForceSection(targetNormalG: 3.0, length: 3.0), 3.0)
        });

        Assert.Throws<ArgumentOutOfRangeException>(() => ForceTargetResolver.Lookup(intervals, distance));
    }
}
