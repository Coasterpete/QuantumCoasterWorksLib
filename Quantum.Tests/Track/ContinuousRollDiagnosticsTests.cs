using Quantum.Track;
using SystemMath = System.Math;

namespace Quantum.Tests;

public sealed class ContinuousRollDiagnosticsTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void AnalyzeRollRadians_FlatRoll_HasZeroDeltasAndRates()
    {
        ContinuousRollDiagnosticsReport report = ContinuousRollDiagnostics.AnalyzeRollRadians(
            new[] { 0.0, 10.0, 20.0 },
            new[] { ToRadians(5.0), ToRadians(5.0), ToRadians(5.0) });

        Assert.Equal(3, report.Summary.SampleCount);
        Assert.Equal(2, report.Summary.IntervalCount);
        Assert.False(report.HasWarnings);
        AssertNear(0.0, report.Summary.MaxAbsoluteRollDeltaRadians);
        AssertNear(0.0, report.Summary.AverageAbsoluteRollDeltaRadians);
        AssertNear(0.0, report.Summary.MaxAbsoluteRollRateRadPerMeter);
        AssertNear(0.0, report.Summary.AverageAbsoluteRollRateRadPerMeter);

        for (int i = 0; i < report.Intervals.Count; i++)
        {
            AssertNear(0.0, report.Intervals[i].RollDeltaRadians);
            AssertNear(0.0, report.Intervals[i].RollRateRadPerMeter);
            Assert.False(report.Intervals[i].UsedWrapAround);
        }
    }

    [Fact]
    public void AnalyzeRollRadians_LinearRollRamp_ComputesDeltaAndRate()
    {
        ContinuousRollDiagnosticsReport report = ContinuousRollDiagnostics.AnalyzeRollRadians(
            new[] { 0.0, 10.0, 20.0, 30.0 },
            new[] { ToRadians(0.0), ToRadians(10.0), ToRadians(20.0), ToRadians(30.0) });

        Assert.False(report.HasWarnings);
        AssertNear(ToRadians(10.0), report.Summary.MaxAbsoluteRollDeltaRadians);
        AssertNear(ToRadians(10.0), report.Summary.AverageAbsoluteRollDeltaRadians);
        AssertNear(ToRadians(1.0), report.Summary.MaxAbsoluteRollRateRadPerMeter);
        AssertNear(ToRadians(1.0), report.Summary.AverageAbsoluteRollRateRadPerMeter);

        for (int i = 0; i < report.Intervals.Count; i++)
        {
            AssertNear(ToRadians(10.0), report.Intervals[i].RollDeltaRadians);
            AssertNear(ToRadians(1.0), report.Intervals[i].RollRateRadPerMeter);
        }
    }

    [Fact]
    public void AnalyzeRollRadians_Wrapped360Transition_Treats359To1AsSmallDelta()
    {
        ContinuousRollDiagnosticsReport wrappedReport = ContinuousRollDiagnostics.AnalyzeRollRadians(
            new[] { 0.0, 1.0 },
            new[] { ToRadians(359.0), ToRadians(1.0) });

        ContinuousRollDiagnosticsInterval wrappedInterval = Assert.Single(wrappedReport.Intervals);
        Assert.True(wrappedInterval.UsedWrapAround);
        AssertNear(ToRadians(-358.0), wrappedInterval.RawRollDeltaRadians);
        AssertNear(ToRadians(2.0), wrappedInterval.RollDeltaRadians);
        AssertNear(ToRadians(2.0), wrappedReport.Summary.MaxAbsoluteRollDeltaRadians);
        AssertNear(361.0, wrappedReport.Samples[1].ContinuousRollDegrees);
        Assert.False(wrappedReport.HasWarnings);

        ContinuousRollDiagnosticsReport noWrapReport = ContinuousRollDiagnostics.AnalyzeRollRadians(
            new[] { 0.0, 1.0 },
            new[] { ToRadians(359.0), ToRadians(1.0) },
            ContinuousRollDiagnosticsOptions.NoWrap);

        ContinuousRollDiagnosticsInterval noWrapInterval = Assert.Single(noWrapReport.Intervals);
        Assert.False(noWrapInterval.UsedWrapAround);
        AssertNear(ToRadians(-358.0), noWrapInterval.RollDeltaRadians);
        Assert.Contains(
            noWrapReport.Warnings,
            warning => warning.Kind == ContinuousRollWarningKind.RollDelta);
    }

    [Fact]
    public void AnalyzeRollRadians_OscillatingBanking_ComputesAbsoluteAverageRate()
    {
        ContinuousRollDiagnosticsReport report = ContinuousRollDiagnostics.AnalyzeRollRadians(
            new[] { 0.0, 10.0, 20.0, 30.0, 40.0 },
            new[]
            {
                ToRadians(0.0),
                ToRadians(15.0),
                ToRadians(-15.0),
                ToRadians(15.0),
                ToRadians(-15.0)
            });

        Assert.False(report.HasWarnings);
        AssertNear(ToRadians(30.0), report.Summary.MaxAbsoluteRollDeltaRadians);
        AssertNear(ToRadians(26.25), report.Summary.AverageAbsoluteRollDeltaRadians);
        AssertNear(ToRadians(3.0), report.Summary.MaxAbsoluteRollRateRadPerMeter);
        AssertNear(ToRadians(2.625), report.Summary.AverageAbsoluteRollRateRadPerMeter);
    }

    [Fact]
    public void AnalyzeRollRadians_DiscontinuousJump_EmitsContinuityWarning()
    {
        ContinuousRollDiagnosticsReport report = ContinuousRollDiagnostics.AnalyzeRollRadians(
            new[] { 0.0, 10.0, 20.0 },
            new[] { ToRadians(0.0), ToRadians(10.0), ToRadians(120.0) });

        ContinuousRollWarning warning = Assert.Single(report.Warnings);
        Assert.Equal(ContinuousRollWarningKind.RollDelta, warning.Kind);
        Assert.Equal(1, warning.Interval.StartSampleIndex);
        Assert.Equal(2, warning.Interval.EndSampleIndex);
        AssertNear(ToRadians(110.0), warning.ActualValue);
        AssertNear(ToRadians(45.0), warning.ThresholdValue);
        AssertNear(ToRadians(110.0), report.Summary.MaxAbsoluteRollDeltaRadians);
    }

    [Fact]
    public void AnalyzeBankingProfile_SamplesProfileRollValues()
    {
        var profile = new BankingProfile(new[]
        {
            new BankingProfileKey(0.0, ToRadians(0.0), BankingProfileInterpolationMode.Linear),
            new BankingProfileKey(10.0, ToRadians(10.0), BankingProfileInterpolationMode.Linear),
            new BankingProfileKey(20.0, ToRadians(20.0), BankingProfileInterpolationMode.Constant)
        });

        ContinuousRollDiagnosticsReport report = ContinuousRollDiagnostics.AnalyzeBankingProfile(
            profile,
            new[] { 0.0, 10.0, 20.0 });

        Assert.False(report.HasWarnings);
        AssertNear(ToRadians(10.0), report.Intervals[0].RollDeltaRadians);
        AssertNear(ToRadians(10.0), report.Intervals[1].RollDeltaRadians);
        AssertNear(20.0, report.Samples[2].RollDegrees);
    }

    private static double ToRadians(double degrees)
    {
        return degrees * SystemMath.PI / 180.0;
    }

    private static void AssertNear(double expected, double actual)
    {
        Assert.InRange(SystemMath.Abs(expected - actual), 0.0, Tolerance);
    }
}
