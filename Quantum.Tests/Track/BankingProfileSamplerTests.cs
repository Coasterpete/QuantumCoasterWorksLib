using Quantum.Debug;
using Quantum.Math;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;
using SystemMath = System.Math;

namespace Quantum.Tests;

public sealed class BankingProfileSamplerTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void SampleRollRadians_BoundarySamples_ClampToNearestKey()
    {
        BankingProfile profile = CreateProfile(
            new BankingProfileKey(10.0, 0.25, BankingProfileInterpolationMode.Linear),
            new BankingProfileKey(20.0, 1.25, BankingProfileInterpolationMode.Linear));

        AssertNear(0.25, BankingProfileSampler.SampleRollRadians(profile, 5.0));
        AssertNear(0.25, BankingProfileSampler.SampleRollRadians(profile, 10.0));
        AssertNear(1.25, BankingProfileSampler.SampleRollRadians(profile, 20.0));
        AssertNear(1.25, BankingProfileSampler.SampleRollRadians(profile, 25.0));
    }

    [Fact]
    public void SampleRollRadians_ExactInteriorKey_ReturnsInteriorKeyValue()
    {
        BankingProfile profile = CreateProfile(
            new BankingProfileKey(0.0, 0.0, BankingProfileInterpolationMode.Constant),
            new BankingProfileKey(10.0, 0.75, BankingProfileInterpolationMode.Linear),
            new BankingProfileKey(20.0, 1.5, BankingProfileInterpolationMode.Linear));

        AssertNear(0.75, BankingProfileSampler.SampleRollRadians(profile, 10.0));
    }

    [Fact]
    public void SampleRollRadians_ConstantInterpolation_HoldsLeftKey()
    {
        BankingProfile profile = CreateProfile(
            new BankingProfileKey(0.0, 1.0, BankingProfileInterpolationMode.Constant),
            new BankingProfileKey(10.0, 3.0, BankingProfileInterpolationMode.Linear));

        AssertNear(1.0, BankingProfileSampler.SampleRollRadians(profile, 5.0));
    }

    [Fact]
    public void SampleRollRadians_LinearInterpolation_InterpolatesUnwrappedRadians()
    {
        BankingProfile profile = CreateProfile(
            new BankingProfileKey(0.0, 1.0, BankingProfileInterpolationMode.Linear),
            new BankingProfileKey(10.0, 5.0, BankingProfileInterpolationMode.Linear));

        AssertNear(2.0, BankingProfileSampler.SampleRollRadians(profile, 2.5));
        AssertNear(3.0, BankingProfileSampler.SampleRollRadians(profile, 5.0));
        AssertNear(4.0, BankingProfileSampler.SampleRollRadians(profile, 7.5));
    }

    [Fact]
    public void SampleRollRadians_SmoothStepInterpolation_EasesBetweenKeys()
    {
        BankingProfile profile = CreateProfile(
            new BankingProfileKey(0.0, 0.0, BankingProfileInterpolationMode.SmoothStep),
            new BankingProfileKey(10.0, 10.0, BankingProfileInterpolationMode.Linear));

        AssertNear(1.5625, BankingProfileSampler.SampleRollRadians(profile, 2.5));
        AssertNear(5.0, BankingProfileSampler.SampleRollRadians(profile, 5.0));
        AssertNear(8.4375, BankingProfileSampler.SampleRollRadians(profile, 7.5));
    }

    [Fact]
    public void SampleRollRadians_ValuesGreaterThanTwoPi_AreNotWrapped()
    {
        double start = (2.0 * SystemMath.PI) + 0.5;
        double end = (4.0 * SystemMath.PI) + 1.5;
        BankingProfile profile = CreateProfile(
            new BankingProfileKey(0.0, start, BankingProfileInterpolationMode.Linear),
            new BankingProfileKey(10.0, end, BankingProfileInterpolationMode.Linear));

        AssertNear(start, BankingProfileSampler.SampleRollRadians(profile, 0.0));
        AssertNear((start + end) * 0.5, BankingProfileSampler.SampleRollRadians(profile, 5.0));
        AssertNear(end, BankingProfileSampler.SampleRollRadians(profile, 10.0));
    }

    [Fact]
    public void Diagnostics_SamplesRollMetadataAndSummaryMetrics()
    {
        BankingProfileFixture fixture = BankingProfileFixtures.RollHoldWithMultipleKeys();

        BankingProfileDiagnosticsReport report = BankingProfileDiagnostics.Sample(
            fixture.Profile,
            fixture.SampleDistances);

        Assert.Equal(fixture.SampleDistances.Count, report.Summary.SampleCount);
        AssertNear(-15.0 * SystemMath.PI / 180.0, report.Summary.MinRollRadians);
        AssertNear(30.0 * SystemMath.PI / 180.0, report.Summary.MaxRollRadians);
        Assert.True(report.Summary.MaxAbsoluteRollSlopeRadPerMeter > 0.0);

        BankingProfileDiagnosticsSample constantSample = report.Samples[3];
        Assert.Equal(3, constantSample.SampleIndex);
        AssertNear(30.0, constantSample.Distance);
        AssertNear(30.0 * SystemMath.PI / 180.0, constantSample.RollRadians);
        Assert.Equal(BankingProfileInterpolationMode.Constant, constantSample.InterpolationMode);
        Assert.Equal(BankingProfileSampleSourceKind.KeyInterval, constantSample.SourceKind);
        Assert.Equal(1, constantSample.SourceStartKeyIndex);
        Assert.Equal(2, constantSample.SourceEndKeyIndex);
        AssertNear(20.0, constantSample.SourceStartDistance);
        AssertNear(50.0, constantSample.SourceEndDistance);
        Assert.True(constantSample.ApproximateRollSlopeRadPerMeter.HasValue);

        BankingProfileDiagnosticsSample linearSample = report.Samples[1];
        AssertNear(10.0, linearSample.Distance);
        AssertNear(15.0 * SystemMath.PI / 180.0, linearSample.RollRadians);
        AssertNear(15.0, linearSample.RollDegrees);
        Assert.Equal(BankingProfileInterpolationMode.Linear, linearSample.InterpolationMode);
        AssertNear(1.5 * SystemMath.PI / 180.0, linearSample.ApproximateRollSlopeRadPerMeter!.Value);
    }

    [Fact]
    public void Diagnostics_DescendingDistances_Throw()
    {
        BankingProfile profile = CreateProfile(new BankingProfileKey(0.0, 0.0));

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            BankingProfileDiagnostics.Sample(profile, new[] { 1.0, 0.0 }));

        Assert.Equal("distances", exception.ParamName);
    }

    [Fact]
    public void SampleFramesAtDistances_PositiveRoll_RotatesNormalTowardPositiveBinormal()
    {
        var document = new TrackDocument(new[]
        {
            new StraightSegment(length: 10.0)
        });
        BankingProfile profile = CreateProfile(
            new BankingProfileKey(0.0, SystemMath.PI * 0.5, BankingProfileInterpolationMode.Constant),
            new BankingProfileKey(10.0, SystemMath.PI * 0.5, BankingProfileInterpolationMode.Constant));

        ExportTrackFrame[] frames = BankingProfileSampler.SampleFramesAtDistances(
            document,
            profile,
            new[] { 5.0 });

        Assert.Single(frames);
        AssertVectorNear(Vector3d.UnitX, frames[0].Tangent);
        AssertVectorNear(Vector3d.UnitZ, frames[0].Normal);
        AssertVectorNear(new Vector3d(0.0, -1.0, 0.0), frames[0].Binormal);
    }

    [Fact]
    public void BankingProfile_NullKeys_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new BankingProfile(null!));
    }

    [Fact]
    public void BankingProfile_EmptyKeys_Throws()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => new BankingProfile(Array.Empty<BankingProfileKey>()));

        Assert.Equal("keys", exception.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void BankingProfile_NonFiniteKeyDistance_Throws(double distance)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateProfile(new BankingProfileKey(distance, 0.0)));

        Assert.Equal("keys", exception.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void BankingProfile_NonFiniteRoll_Throws(double rollRadians)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateProfile(new BankingProfileKey(0.0, rollRadians)));

        Assert.Equal("keys", exception.ParamName);
    }

    [Fact]
    public void BankingProfile_DuplicateDistances_Throw()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => CreateProfile(
                new BankingProfileKey(0.0, 0.0),
                new BankingProfileKey(0.0, 1.0)));

        Assert.Equal("keys", exception.ParamName);
    }

    [Fact]
    public void BankingProfile_DescendingDistances_Throw()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => CreateProfile(
                new BankingProfileKey(2.0, 0.0),
                new BankingProfileKey(1.0, 1.0)));

        Assert.Equal("keys", exception.ParamName);
    }

    [Fact]
    public void BankingProfile_InvalidInterpolationMode_Throws()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateProfile(new BankingProfileKey(
                0.0,
                0.0,
                (BankingProfileInterpolationMode)99)));

        Assert.Equal("keys", exception.ParamName);
    }

    [Fact]
    public void SampleRollRadians_NonFiniteDistance_Throws()
    {
        BankingProfile profile = CreateProfile(new BankingProfileKey(0.0, 0.0));

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => BankingProfileSampler.SampleRollRadians(profile, double.NaN));

        Assert.Equal("distance", exception.ParamName);
    }

    [Fact]
    public void ConstantProfile_ReproducesSimpleBankedTurnSegmentRollFrames()
    {
        DiagnosticTrackFixture fixture = DiagnosticTrackFixtures.SimpleBankedTurn();
        double rollRadians = fixture.Document.Segments[0].RollRadians;
        BankingProfile profile = CreateConstantProfile(fixture.Document.TotalLength, rollRadians);

        var evaluator = new TrackEvaluator(fixture.Document);
        ExportTrackFrame[] expectedFrames = evaluator.EvaluateFramesAtDistances(fixture.SampleDistances);
        ExportTrackFrame[] actualFrames = BankingProfileSampler.SampleFramesAtDistances(
            fixture.Document,
            profile,
            fixture.SampleDistances);

        AssertFramesNear(expectedFrames, actualFrames);
    }

    [Fact]
    public void ConstantProfile_ReproducesMultiSegmentTrackSegmentRollFrames()
    {
        var document = new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(length: 10.0, rollRadians: 0.0),
            new StraightSegment(length: 10.0, rollRadians: 0.6)
        });
        double[] distances = { 0.0, 5.0, 10.0, 15.0, 20.0 };
        BankingProfile profile = CreateProfile(
            new BankingProfileKey(0.0, 0.0, BankingProfileInterpolationMode.Constant),
            new BankingProfileKey(10.0, 0.6, BankingProfileInterpolationMode.Constant),
            new BankingProfileKey(20.0, 0.6, BankingProfileInterpolationMode.Constant));

        var evaluator = new TrackEvaluator(document);
        ExportTrackFrame[] expectedFrames = evaluator.EvaluateFramesAtDistances(distances);
        ExportTrackFrame[] actualFrames = BankingProfileSampler.SampleFramesAtDistances(
            document,
            profile,
            distances);

        AssertFramesNear(expectedFrames, actualFrames);
    }

    private static BankingProfile CreateProfile(params BankingProfileKey[] keys)
    {
        return new BankingProfile(keys);
    }

    private static BankingProfile CreateConstantProfile(double totalLength, double rollRadians)
    {
        return CreateProfile(
            new BankingProfileKey(0.0, rollRadians, BankingProfileInterpolationMode.Constant),
            new BankingProfileKey(totalLength, rollRadians, BankingProfileInterpolationMode.Constant));
    }

    private static void AssertFramesNear(
        IReadOnlyList<ExportTrackFrame> expectedFrames,
        IReadOnlyList<ExportTrackFrame> actualFrames)
    {
        Assert.Equal(expectedFrames.Count, actualFrames.Count);
        for (int i = 0; i < expectedFrames.Count; i++)
        {
            AssertFrameNear(expectedFrames[i], actualFrames[i]);
        }
    }

    private static void AssertFrameNear(ExportTrackFrame expected, ExportTrackFrame actual)
    {
        AssertNear(expected.Distance, actual.Distance);
        AssertVectorNear(expected.Position, actual.Position);
        AssertVectorNear(expected.Tangent, actual.Tangent);
        AssertVectorNear(expected.Normal, actual.Normal);
        AssertVectorNear(expected.Binormal, actual.Binormal);
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual)
    {
        AssertNear(expected.X, actual.X);
        AssertNear(expected.Y, actual.Y);
        AssertNear(expected.Z, actual.Z);
    }

    private static void AssertNear(double expected, double actual)
    {
        Assert.InRange(SystemMath.Abs(expected - actual), 0.0, Tolerance);
    }
}
