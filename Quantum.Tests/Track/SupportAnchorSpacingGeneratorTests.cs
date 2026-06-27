using Quantum.Track;

namespace Quantum.Tests;

public sealed class SupportAnchorSpacingGeneratorTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void Generate_WithExactSpacing_ReturnsEvenCandidatesAndIntervals()
    {
        SupportAnchorSpacingResult result = SupportAnchorSpacingGenerator.Generate(
            startDistance: 0.0,
            endDistance: 20.0,
            targetSpacing: 5.0);

        AssertDistances(result, 0.0, 5.0, 10.0, 15.0, 20.0);
        AssertIntervals(result, 5.0, 5.0, 5.0, 5.0);
        AssertDoubleNear(0.0, result.Remainder.StartGap);
        AssertDoubleNear(0.0, result.Remainder.EndGap);
        AssertDoubleNear(0.0, result.Remainder.EndRemainder);
        Assert.False(result.HasWarnings);
    }

    [Fact]
    public void Generate_WithStartOffset_ShiftsFirstCandidateAndPreservesTargetIntervals()
    {
        SupportAnchorSpacingResult result = SupportAnchorSpacingGenerator.Generate(
            startDistance: 10.0,
            endDistance: 24.0,
            targetSpacing: 4.0,
            startOffset: 2.0);

        AssertDistances(result, 12.0, 16.0, 20.0, 24.0);
        AssertIntervals(result, 4.0, 4.0, 4.0);
        AssertDoubleNear(2.0, result.Remainder.StartGap);
        AssertDoubleNear(0.0, result.Remainder.EndGap);
        Assert.False(result.HasWarnings);
    }

    [Fact]
    public void Generate_WithEndRemainder_ReportsEndGapAndWarning()
    {
        SupportAnchorSpacingResult result = SupportAnchorSpacingGenerator.Generate(
            startDistance: 0.0,
            endDistance: 10.0,
            targetSpacing: 3.0);

        AssertDistances(result, 0.0, 3.0, 6.0, 9.0);
        AssertIntervals(result, 3.0, 3.0, 3.0);
        AssertDoubleNear(1.0, result.Remainder.EndGap);
        Assert.Contains(
            result.Warnings,
            warning => warning.Code == SupportAnchorSpacingWarningCode.UnevenEndRemainder &&
                warning.StartDistance == 9.0 &&
                warning.EndDistance == 10.0);
    }

    [Fact]
    public void Generate_WithExcludedZone_RemovesCandidatesAndReportsActualGap()
    {
        var exclusions = new[]
        {
            new SupportAnchorExcludedRange(8.0, 12.0)
        };

        SupportAnchorSpacingResult result = SupportAnchorSpacingGenerator.Generate(
            startDistance: 0.0,
            endDistance: 20.0,
            targetSpacing: 5.0,
            excludedRanges: exclusions);

        AssertDistances(result, 0.0, 5.0, 15.0, 20.0);
        AssertIntervals(result, 5.0, 10.0, 5.0);
        Assert.True(result.Intervals[1].CrossesExcludedRange);
        Assert.Contains(
            result.Warnings,
            warning => warning.Code == SupportAnchorSpacingWarningCode.ExcludedAnchorCandidate &&
                warning.Distance == 10.0 &&
                warning.ExcludedRangeIndex == 0);
        Assert.Contains(
            result.Warnings,
            warning => warning.Code == SupportAnchorSpacingWarningCode.ExcludedGap &&
                warning.StartDistance == 5.0 &&
                warning.EndDistance == 15.0 &&
                warning.ExcludedRangeIndex == 0);
    }

    [Fact]
    public void Generate_WithInvalidTargetSpacing_ReturnsWarningWithoutCandidates()
    {
        SupportAnchorSpacingResult result = SupportAnchorSpacingGenerator.Generate(
            startDistance: 0.0,
            endDistance: 10.0,
            targetSpacing: 0.0);

        Assert.Empty(result.Candidates);
        Assert.Empty(result.CandidateDistances);
        Assert.Empty(result.Intervals);
        AssertDoubleNear(10.0, result.Remainder.TrackSpan);
        Assert.Contains(
            result.Warnings,
            warning => warning.Code == SupportAnchorSpacingWarningCode.InvalidTargetSpacing);
    }

    [Fact]
    public void Generate_WithInvalidSpacingRange_ReturnsWarningWithoutCandidates()
    {
        SupportAnchorSpacingResult result = SupportAnchorSpacingGenerator.Generate(
            startDistance: 5.0,
            endDistance: 5.0,
            targetSpacing: 2.0);

        Assert.Empty(result.Candidates);
        Assert.Contains(
            result.Warnings,
            warning => warning.Code == SupportAnchorSpacingWarningCode.InvalidSpacingRange &&
                warning.StartDistance == 5.0 &&
                warning.EndDistance == 5.0);
    }

    [Fact]
    public void Generate_WithInvalidExcludedRanges_WarnsAndIgnoresThoseRanges()
    {
        var exclusions = new[]
        {
            new SupportAnchorExcludedRange(8.0, 8.0),
            new SupportAnchorExcludedRange(double.NaN, 12.0),
            new SupportAnchorExcludedRange(20.0, 25.0)
        };

        SupportAnchorSpacingResult result = SupportAnchorSpacingGenerator.Generate(
            startDistance: 0.0,
            endDistance: 10.0,
            targetSpacing: 5.0,
            excludedRanges: exclusions);

        AssertDistances(result, 0.0, 5.0, 10.0);
        AssertIntervals(result, 5.0, 5.0);
        Assert.Contains(
            result.Warnings,
            warning => warning.Code == SupportAnchorSpacingWarningCode.InvalidExcludedRange &&
                warning.ExcludedRangeIndex == 0);
        Assert.Contains(
            result.Warnings,
            warning => warning.Code == SupportAnchorSpacingWarningCode.InvalidExcludedRange &&
                warning.ExcludedRangeIndex == 1);
        Assert.DoesNotContain(
            result.Warnings,
            warning => warning.Code == SupportAnchorSpacingWarningCode.ExcludedAnchorCandidate);
    }

    [Fact]
    public void Generate_OutputIsDeterministicForRepeatedRuns()
    {
        var request = new SupportAnchorSpacingRequest(
            startDistance: 0.0,
            endDistance: 30.0,
            targetSpacing: 4.0,
            startOffset: 1.0,
            excludedRanges: new[]
            {
                new SupportAnchorExcludedRange(16.0, 20.0),
                new SupportAnchorExcludedRange(6.0, 13.0)
            });

        SupportAnchorSpacingResult first = SupportAnchorSpacingGenerator.Generate(request);
        SupportAnchorSpacingResult second = SupportAnchorSpacingGenerator.Generate(request);

        Assert.Equal(first.CandidateDistances, second.CandidateDistances);
        Assert.Equal(
            first.Intervals.Select(interval => interval.Length),
            second.Intervals.Select(interval => interval.Length));
        Assert.Equal(
            first.Warnings.Select(warning => warning.Code),
            second.Warnings.Select(warning => warning.Code));
        Assert.Equal(
            first.Warnings.Select(warning => warning.Distance),
            second.Warnings.Select(warning => warning.Distance));
        Assert.Equal(
            first.Warnings.Select(warning => warning.ExcludedRangeIndex),
            second.Warnings.Select(warning => warning.ExcludedRangeIndex));
    }

    private static void AssertDistances(SupportAnchorSpacingResult result, params double[] expectedDistances)
    {
        Assert.Equal(expectedDistances.Length, result.Candidates.Count);
        Assert.Equal(expectedDistances.Length, result.CandidateDistances.Count);

        for (int i = 0; i < expectedDistances.Length; i++)
        {
            Assert.Equal(i, result.Candidates[i].Index);
            AssertDoubleNear(expectedDistances[i], result.Candidates[i].Distance);
            AssertDoubleNear(expectedDistances[i], result.CandidateDistances[i]);
        }
    }

    private static void AssertIntervals(SupportAnchorSpacingResult result, params double[] expectedLengths)
    {
        Assert.Equal(expectedLengths.Length, result.Intervals.Count);

        for (int i = 0; i < expectedLengths.Length; i++)
        {
            Assert.Equal(i, result.Intervals[i].StartCandidateIndex);
            Assert.Equal(i + 1, result.Intervals[i].EndCandidateIndex);
            AssertDoubleNear(expectedLengths[i], result.Intervals[i].Length);
        }
    }

    private static void AssertDoubleNear(double expected, double actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, Tolerance);
    }
}
