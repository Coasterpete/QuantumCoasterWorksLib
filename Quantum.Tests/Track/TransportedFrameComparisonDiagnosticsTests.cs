using Quantum.Track;
using SystemMath = System.Math;

namespace Quantum.Tests;

public sealed class TransportedFrameComparisonDiagnosticsTests
{
    private const double DistanceTolerance = 1e-7;
    private const double AngleTolerance = 1e-6;

    public static IEnumerable<object[]> ComparisonFixtureData
    {
        get
        {
            yield return new object[] { GetFixture(DiagnosticTrackFixtures.NearVerticalTangentSequenceName) };
            yield return new object[] { GetFixture(DiagnosticTrackFixtures.CrestHillName) };
            yield return new object[] { GetFixture(DiagnosticTrackFixtures.ConstantRadiusTurnName) };
            yield return new object[] { GetFixture(DiagnosticTrackFixtures.SimpleBankedTurnName) };
            yield return new object[] { GetFixture(DiagnosticTrackFixtures.QuarterLoopLikeName) };
        }
    }

    [Theory]
    [MemberData(nameof(ComparisonFixtureData))]
    public void Compare_RequestedDiagnosticFixtures_ReturnsFiniteMatchingStationReport(
        DiagnosticTrackFixture fixture)
    {
        TransportedFrameComparisonReport report = TransportedFrameComparisonDiagnostics.Compare(
            fixture.Document,
            fixture.SampleDistances,
            TrackFrameContinuityThresholds.UniformDegrees(181.0));

        Assert.Equal(fixture.SampleDistances.Count, report.SampleCount);
        Assert.Equal(fixture.SampleDistances.Count - 1, report.StatelessSmoothnessReport.IntervalCount);
        Assert.Equal(fixture.SampleDistances.Count - 1, report.TransportedSmoothnessReport.IntervalCount);
        Assert.Equal(fixture.SampleDistances.Count - 1, report.StatelessContinuityReport.IntervalCount);
        Assert.Equal(fixture.SampleDistances.Count - 1, report.TransportedContinuityReport.IntervalCount);
        Assert.False(report.StatelessContinuityReport.HasDiscontinuities, report.StatelessContinuityReport.ToDiagnosticString());
        Assert.False(report.TransportedContinuityReport.HasDiscontinuities, report.TransportedContinuityReport.ToDiagnosticString());

        for (int i = 0; i < report.Samples.Count; i++)
        {
            TransportedFrameComparisonSample sample = report.Samples[i];
            Assert.Equal(i, sample.SampleIndex);
            AssertNear(fixture.SampleDistances[i], sample.Distance, DistanceTolerance);
            AssertFiniteSample(fixture.Name, sample);
        }

        AssertFiniteSummary(fixture.Name, report.TangentAngleDelta);
        AssertFiniteSummary(fixture.Name, report.NormalAngleDelta);
        AssertFiniteSummary(fixture.Name, report.BinormalAngleDelta);
        AssertFiniteSummary(fixture.Name, report.FrameAngleDelta);
        AssertFiniteSummary(fixture.Name, report.RollAngleDelta);
        AssertFiniteSummary(fixture.Name, report.MatrixOrientationAngleDelta);
    }

    [Theory]
    [InlineData(DiagnosticTrackFixtures.ConstantRadiusTurnName)]
    [InlineData(DiagnosticTrackFixtures.SimpleBankedTurnName)]
    public void Compare_HorizontalTurnFixtures_PreserveStatelessFrameOrientation(
        string fixtureName)
    {
        DiagnosticTrackFixture fixture = GetFixture(fixtureName);

        TransportedFrameComparisonReport report = TransportedFrameComparisonDiagnostics.Compare(
            fixture.Document,
            fixture.SampleDistances,
            TrackFrameContinuityThresholds.UniformDegrees(181.0));

        Assert.InRange(report.TangentAngleDelta.MaxAbsoluteRadians, 0.0, AngleTolerance);
        Assert.InRange(report.NormalAngleDelta.MaxAbsoluteRadians, 0.0, AngleTolerance);
        Assert.InRange(report.BinormalAngleDelta.MaxAbsoluteRadians, 0.0, AngleTolerance);
        Assert.InRange(report.FrameAngleDelta.MaxAbsoluteRadians, 0.0, AngleTolerance);
        Assert.InRange(report.RollAngleDelta.MaxAbsoluteRadians, 0.0, AngleTolerance);
        Assert.InRange(report.MatrixOrientationAngleDelta.MaxAbsoluteRadians, 0.0, AngleTolerance);
    }

    [Fact]
    public void Compare_QuarterLoopLike_ReportsTransportedReductionForReferenceUpJump()
    {
        DiagnosticTrackFixture fixture = DiagnosticTrackFixtures.QuarterLoopLike();

        TransportedFrameComparisonReport report = TransportedFrameComparisonDiagnostics.Compare(
            fixture.Document,
            fixture.SampleDistances,
            TrackFrameContinuityThresholds.UniformDegrees(181.0));

        Assert.True(
            report.TransportedSmoothnessReport.NormalAngleDelta.MaxAbsoluteRadians <
            report.StatelessSmoothnessReport.NormalAngleDelta.MaxAbsoluteRadians,
            $"Expected transported normal max {report.TransportedSmoothnessReport.NormalAngleDelta.MaxAbsoluteDegrees:F3} deg to be below stateless max {report.StatelessSmoothnessReport.NormalAngleDelta.MaxAbsoluteDegrees:F3} deg.");
        Assert.True(
            report.TransportedSmoothnessReport.BinormalAngleDelta.MaxAbsoluteRadians <
            report.StatelessSmoothnessReport.BinormalAngleDelta.MaxAbsoluteRadians,
            $"Expected transported binormal max {report.TransportedSmoothnessReport.BinormalAngleDelta.MaxAbsoluteDegrees:F3} deg to be below stateless max {report.StatelessSmoothnessReport.BinormalAngleDelta.MaxAbsoluteDegrees:F3} deg.");
    }

    [Fact]
    public void Compare_EmptyDistances_ReturnsEmptyReport()
    {
        DiagnosticTrackFixture fixture = DiagnosticTrackFixtures.StraightHorizontal();

        TransportedFrameComparisonReport report = TransportedFrameComparisonDiagnostics.Compare(
            fixture.Document,
            Array.Empty<double>(),
            TrackFrameContinuityThresholds.Default);

        Assert.Empty(report.Samples);
        Assert.Equal(0, report.SampleCount);
        Assert.Equal(0, report.StatelessSmoothnessReport.IntervalCount);
        Assert.Equal(0, report.TransportedSmoothnessReport.IntervalCount);
        Assert.Equal(0, report.StatelessContinuityReport.IntervalCount);
        Assert.Equal(0, report.TransportedContinuityReport.IntervalCount);
        AssertNear(0.0, report.MatrixOrientationAngleDelta.MaxAbsoluteRadians, AngleTolerance);
    }

    private static DiagnosticTrackFixture GetFixture(string fixtureName)
    {
        return DiagnosticTrackFixtures.All().Single(fixture => fixture.Name == fixtureName);
    }

    private static void AssertFiniteSample(string fixtureName, TransportedFrameComparisonSample sample)
    {
        Assert.True(IsFinite(sample.Distance), $"{fixtureName} sample {sample.SampleIndex} distance should be finite.");
        AssertFiniteAngle(fixtureName, sample.SampleIndex, nameof(sample.TangentAngleDeltaRadians), sample.TangentAngleDeltaRadians);
        AssertFiniteAngle(fixtureName, sample.SampleIndex, nameof(sample.NormalAngleDeltaRadians), sample.NormalAngleDeltaRadians);
        AssertFiniteAngle(fixtureName, sample.SampleIndex, nameof(sample.BinormalAngleDeltaRadians), sample.BinormalAngleDeltaRadians);
        AssertFiniteAngle(fixtureName, sample.SampleIndex, nameof(sample.FrameAngleDeltaRadians), sample.FrameAngleDeltaRadians);
        AssertFiniteAngle(fixtureName, sample.SampleIndex, nameof(sample.AbsoluteRollAngleDeltaRadians), sample.AbsoluteRollAngleDeltaRadians);
        AssertFiniteAngle(fixtureName, sample.SampleIndex, nameof(sample.MatrixOrientationAngleDeltaRadians), sample.MatrixOrientationAngleDeltaRadians);
    }

    private static void AssertFiniteAngle(string fixtureName, int sampleIndex, string label, double value)
    {
        Assert.True(IsFinite(value), $"{fixtureName} sample {sampleIndex} {label} should be finite.");
        Assert.InRange(value, 0.0, SystemMath.PI);
    }

    private static void AssertFiniteSummary(string fixtureName, TrackFrameSmoothnessMetricSummary summary)
    {
        Assert.True(IsFinite(summary.MaxAbsoluteRadians), $"{fixtureName} max angle should be finite.");
        Assert.True(IsFinite(summary.AverageAbsoluteRadians), $"{fixtureName} average angle should be finite.");
        Assert.InRange(summary.MaxAbsoluteRadians, 0.0, SystemMath.PI);
        Assert.InRange(summary.AverageAbsoluteRadians, 0.0, SystemMath.PI);
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static void AssertNear(double expected, double actual, double tolerance)
    {
        Assert.InRange(SystemMath.Abs(expected - actual), 0.0, tolerance);
    }
}
