using Quantum.Track;

namespace Quantum.Tests;

public sealed class SectionResolverTests
{
    [Fact]
    public void SectionResolver_Resolve_CreatesExpectedCumulativeIntervals()
    {
        IReadOnlyList<ResolvedSectionInterval<string>> intervals = SectionResolver.Resolve(new[]
        {
            ("lift", 5.0),
            ("crest", 3.0),
            ("drop", 2.0)
        });

        Assert.Equal(3, intervals.Count);

        Assert.Equal("lift", intervals[0].Section);
        Assert.Equal(0.0, intervals[0].StartDistance);
        Assert.Equal(5.0, intervals[0].EndDistance);
        Assert.Equal(5.0, intervals[0].Length);

        Assert.Equal("crest", intervals[1].Section);
        Assert.Equal(5.0, intervals[1].StartDistance);
        Assert.Equal(8.0, intervals[1].EndDistance);
        Assert.Equal(3.0, intervals[1].Length);

        Assert.Equal("drop", intervals[2].Section);
        Assert.Equal(8.0, intervals[2].StartDistance);
        Assert.Equal(10.0, intervals[2].EndDistance);
        Assert.Equal(2.0, intervals[2].Length);
    }

    [Fact]
    public void ResolvedSectionInterval_Contains_UsesStableBoundaries()
    {
        IReadOnlyList<ResolvedSectionInterval<string>> intervals = SectionResolver.Resolve(new[]
        {
            ("a", 2.0),
            ("b", 3.0)
        });

        Assert.True(intervals[0].Contains(0.0));
        Assert.True(intervals[0].Contains(1.999999999999));
        Assert.False(intervals[0].Contains(2.0));

        Assert.True(intervals[1].Contains(2.0));
        Assert.True(intervals[1].Contains(4.999999999999));
        Assert.True(intervals[1].Contains(5.0));
    }

    [Fact]
    public void ResolvedSectionInterval_NullSection_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            new ResolvedSectionInterval<string>(null!, 0.0, 1.0));

        Assert.Equal("section", exception.ParamName);
    }

    [Fact]
    public void SectionResolver_Lookup_BoundaryBetweenIntervals_ReturnsNextInterval()
    {
        IReadOnlyList<ResolvedSectionInterval<string>> intervals = SectionResolver.Resolve(new[]
        {
            ("a", 2.0),
            ("b", 3.0)
        });

        ResolvedSectionInterval<string> atBoundary = SectionResolver.Lookup(intervals, 2.0);
        Assert.Equal("b", atBoundary.Section);
    }

    [Fact]
    public void SectionResolver_Lookup_ExactFinalEndpoint_ReturnsLastInterval()
    {
        IReadOnlyList<ResolvedSectionInterval<string>> intervals = SectionResolver.Resolve(new[]
        {
            ("a", 2.0),
            ("b", 3.0)
        });

        ResolvedSectionInterval<string> atEnd = SectionResolver.Lookup(intervals, 5.0);
        Assert.Equal("b", atEnd.Section);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void SectionResolver_Resolve_InvalidLengths_ThrowsArgumentOutOfRangeException(double invalidLength)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SectionResolver.Resolve(new[] { ("bad", invalidLength) }));
    }

    [Fact]
    public void SectionResolver_Resolve_ZeroLength_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SectionResolver.Resolve(new[] { ("zero", 0.0) }));
    }

    [Theory]
    [InlineData(-0.0001)]
    [InlineData(5.0001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void SectionResolver_Lookup_OutOfRangeOrNonFiniteDistance_ThrowsArgumentOutOfRangeException(double distance)
    {
        IReadOnlyList<ResolvedSectionInterval<string>> intervals = SectionResolver.Resolve(new[]
        {
            ("a", 2.0),
            ("b", 3.0)
        });

        Assert.Throws<ArgumentOutOfRangeException>(() => SectionResolver.Lookup(intervals, distance));
    }

    [Fact]
    public void SectionResolver_Lookup_IsStableAroundBoundaries()
    {
        IReadOnlyList<ResolvedSectionInterval<string>> intervals = SectionResolver.Resolve(new[]
        {
            ("a", 2.0),
            ("b", 3.0),
            ("c", 4.0)
        });

        const double epsilon = 1e-12;
        Assert.Equal("a", SectionResolver.Lookup(intervals, 2.0 - epsilon).Section);
        Assert.Equal("b", SectionResolver.Lookup(intervals, 2.0).Section);
        Assert.Equal("b", SectionResolver.Lookup(intervals, 2.0 + epsilon).Section);

        Assert.Equal("b", SectionResolver.Lookup(intervals, 5.0 - epsilon).Section);
        Assert.Equal("c", SectionResolver.Lookup(intervals, 5.0).Section);
        Assert.Equal("c", SectionResolver.Lookup(intervals, 5.0 + epsilon).Section);

        Assert.Equal("c", SectionResolver.Lookup(intervals, 9.0 - epsilon).Section);
        Assert.Equal("c", SectionResolver.Lookup(intervals, 9.0).Section);
    }
}
