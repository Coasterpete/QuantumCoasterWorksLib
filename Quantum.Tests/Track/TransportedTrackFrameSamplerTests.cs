using Quantum.Debug;
using Quantum.Math;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;
using SystemMath = System.Math;

namespace Quantum.Tests;

public sealed class TransportedTrackFrameSamplerTests
{
    private const double DistanceTolerance = 1e-7;
    private const double AxisTolerance = 1e-6;

    public static IEnumerable<object[]> FixtureData
    {
        get
        {
            foreach (DiagnosticTrackFixture fixture in DiagnosticTrackFixtures.All())
            {
                yield return new object[] { fixture };
            }
        }
    }

    [Fact]
    public void TransportedTrackFrameSampler_EmptyDistances_ReturnsEmptyFrames()
    {
        DiagnosticTrackFixture fixture = DiagnosticTrackFixtures.StraightHorizontal();

        ExportTrackFrame[] frames = TransportedTrackFrameSampler.SampleFramesAtDistances(
            fixture.Document,
            Array.Empty<double>());

        Assert.Empty(frames);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void TransportedTrackFrameSampler_NonFiniteDistance_Throws(double distance)
    {
        DiagnosticTrackFixture fixture = DiagnosticTrackFixtures.StraightHorizontal();

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => TransportedTrackFrameSampler.SampleFramesAtDistances(
                fixture.Document,
                new[] { 0.0, distance }));

        Assert.Equal("distances", exception.ParamName);
        Assert.Contains("must be finite", exception.Message);
    }

    [Fact]
    public void TransportedTrackFrameSampler_DescendingDistances_Throws()
    {
        DiagnosticTrackFixture fixture = DiagnosticTrackFixtures.StraightHorizontal();

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => TransportedTrackFrameSampler.SampleFramesAtDistances(
                fixture.Document,
                new[] { 0.0, 4.0, 3.0 }));

        Assert.Equal("distances", exception.ParamName);
        Assert.Contains("non-decreasing", exception.Message);
    }

    [Fact]
    public void TransportedTrackFrameSampler_DuplicateDistances_PreserveOutputsAndOrder()
    {
        DiagnosticTrackFixture fixture = DiagnosticTrackFixtures.CrestHill();
        double distance = fixture.Document.TotalLength * 0.5;

        ExportTrackFrame[] frames = TransportedTrackFrameSampler.SampleFramesAtDistances(
            fixture.Document,
            new[] { distance, distance, distance });

        Assert.Equal(3, frames.Length);
        AssertFrameNear(frames[0], frames[1]);
        AssertFrameNear(frames[1], frames[2]);
    }

    [Theory]
    [MemberData(nameof(FixtureData))]
    public void TransportedTrackFrameSampler_DiagnosticFixtures_ReturnFiniteOrthonormalFrames(
        DiagnosticTrackFixture fixture)
    {
        ExportTrackFrame[] frames = TransportedTrackFrameSampler.SampleFramesAtDistances(
            fixture.Document,
            fixture.SampleDistances);

        Assert.Equal(fixture.SampleDistances.Count, frames.Length);
        for (int i = 0; i < frames.Length; i++)
        {
            AssertFiniteOrthonormalFrame(fixture.Name, i, frames[i]);
        }
    }

    [Theory]
    [InlineData(DiagnosticTrackFixtures.NearVerticalTangentSequenceName)]
    [InlineData(DiagnosticTrackFixtures.CrestHillName)]
    public void TransportedTrackFrameSampler_PriorityFixtures_WorkWithExistingDiagnostics(
        string fixtureName)
    {
        DiagnosticTrackFixture fixture = GetFixture(fixtureName);
        ExportTrackFrame[] frames = TransportedTrackFrameSampler.SampleFramesAtDistances(
            fixture.Document,
            fixture.SampleDistances);

        TrackFrameSmoothnessReport smoothnessReport = TrackFrameSmoothnessDiagnostics.Analyze(
            frames,
            fixture.SampleDistances);
        TrackFrameContinuityReport continuityReport = TrackFrameContinuityDiagnostics.Analyze(
            frames,
            fixture.SampleDistances,
            TrackFrameContinuityThresholds.UniformDegrees(181.0));

        Assert.Equal(fixture.SampleDistances.Count - 1, smoothnessReport.IntervalCount);
        Assert.Equal(fixture.SampleDistances.Count - 1, continuityReport.IntervalCount);
        Assert.False(continuityReport.HasDiscontinuities, continuityReport.ToDiagnosticString());
    }

    [Theory]
    [MemberData(nameof(FixtureData))]
    public void TransportedTrackFrameSampler_PreservesExistingEvaluatorPositionsTangentsAndDistances(
        DiagnosticTrackFixture fixture)
    {
        var evaluator = new TrackEvaluator(fixture.Document);
        ExportTrackFrame[] scalarFrames = evaluator.EvaluateFramesAtDistances(fixture.SampleDistances);
        ExportTrackFrame[] transportedFrames = TransportedTrackFrameSampler.SampleFramesAtDistances(
            fixture.Document,
            fixture.SampleDistances);

        Assert.Equal(scalarFrames.Length, transportedFrames.Length);
        for (int i = 0; i < scalarFrames.Length; i++)
        {
            AssertNear(scalarFrames[i].Distance, transportedFrames[i].Distance, DistanceTolerance);
            AssertVectorNear(scalarFrames[i].Position, transportedFrames[i].Position, AxisTolerance);
            AssertVectorNear(scalarFrames[i].Tangent, transportedFrames[i].Tangent, AxisTolerance);
        }
    }

    [Fact]
    public void TransportedTrackFrameSampler_FirstFrameMatchesCurrentEvaluatorSeedAfterRoll()
    {
        DiagnosticTrackFixture fixture = DiagnosticTrackFixtures.SimpleBankedTurn();
        var evaluator = new TrackEvaluator(fixture.Document);
        double[] distances =
        {
            fixture.Document.TotalLength * 0.25,
            fixture.Document.TotalLength * 0.5,
            fixture.Document.TotalLength * 0.75
        };

        ExportTrackFrame expectedFirstFrame = evaluator.EvaluateFrameAtDistance(distances[0]);
        ExportTrackFrame[] transportedFrames = TransportedTrackFrameSampler.SampleFramesAtDistances(
            fixture.Document,
            distances);

        AssertFrameNear(expectedFirstFrame, transportedFrames[0]);
    }

    [Fact]
    public void TransportedTrackFrameSampler_QuarterLoopLike_MatchesCanonicalEvaluatorFrames()
    {
        DiagnosticTrackFixture fixture = DiagnosticTrackFixtures.QuarterLoopLike();
        var evaluator = new TrackEvaluator(fixture.Document);
        ExportTrackFrame[] canonicalFrames = evaluator.EvaluateFramesAtDistances(fixture.SampleDistances);
        ExportTrackFrame[] transportedFrames = TransportedTrackFrameSampler.SampleFramesAtDistances(
            fixture.Document,
            fixture.SampleDistances);

        Assert.Equal(canonicalFrames.Length, transportedFrames.Length);
        for (int i = 0; i < canonicalFrames.Length; i++)
        {
            AssertFrameNear(canonicalFrames[i], transportedFrames[i]);
        }
    }

    private static DiagnosticTrackFixture GetFixture(string fixtureName)
    {
        return DiagnosticTrackFixtures.All().Single(fixture => fixture.Name == fixtureName);
    }

    private static void AssertFiniteOrthonormalFrame(
        string fixtureName,
        int sampleIndex,
        ExportTrackFrame frame)
    {
        Assert.True(IsFinite(frame.Distance), $"{fixtureName} frame {sampleIndex} distance should be finite.");
        Assert.True(IsFinite(frame.Position), $"{fixtureName} frame {sampleIndex} position should be finite.");
        Assert.True(IsFinite(frame.Tangent), $"{fixtureName} frame {sampleIndex} tangent should be finite.");
        Assert.True(IsFinite(frame.Normal), $"{fixtureName} frame {sampleIndex} normal should be finite.");
        Assert.True(IsFinite(frame.Binormal), $"{fixtureName} frame {sampleIndex} binormal should be finite.");

        AssertNear(1.0, frame.Tangent.Length, AxisTolerance);
        AssertNear(1.0, frame.Normal.Length, AxisTolerance);
        AssertNear(1.0, frame.Binormal.Length, AxisTolerance);
        AssertNear(0.0, Vector3d.Dot(frame.Tangent, frame.Normal), AxisTolerance);
        AssertNear(0.0, Vector3d.Dot(frame.Tangent, frame.Binormal), AxisTolerance);
        AssertNear(0.0, Vector3d.Dot(frame.Normal, frame.Binormal), AxisTolerance);

        Vector3d expectedBinormal = Vector3d.Cross(frame.Tangent, frame.Normal).Normalized();
        Assert.True(
            Vector3d.Dot(expectedBinormal, frame.Binormal) > 1.0 - AxisTolerance,
            $"{fixtureName} frame {sampleIndex} should preserve the TrackFrame handedness convention.");
    }

    private static void AssertFrameNear(ExportTrackFrame expected, ExportTrackFrame actual)
    {
        AssertNear(expected.Distance, actual.Distance, DistanceTolerance);
        AssertVectorNear(expected.Position, actual.Position, AxisTolerance);
        AssertVectorNear(expected.Tangent, actual.Tangent, AxisTolerance);
        AssertVectorNear(expected.Normal, actual.Normal, AxisTolerance);
        AssertVectorNear(expected.Binormal, actual.Binormal, AxisTolerance);
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual, double tolerance)
    {
        AssertNear(expected.X, actual.X, tolerance);
        AssertNear(expected.Y, actual.Y, tolerance);
        AssertNear(expected.Z, actual.Z, tolerance);
    }

    private static bool IsFinite(Vector3d value)
    {
        return IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);
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
