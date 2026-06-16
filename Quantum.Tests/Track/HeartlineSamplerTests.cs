using System;
using System.Collections.Generic;
using System.Numerics;
using Quantum.Math;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class HeartlineSamplerTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void HeartlineOffset_AcceptsFiniteOffsets()
    {
        var offset = new HeartlineOffset(1.25, -0.5);
        var normalOnly = new HeartlineOffset(2.0);

        AssertDoubleNear(1.25, offset.NormalOffsetMeters);
        AssertDoubleNear(-0.5, offset.LateralOffsetMeters);
        AssertDoubleNear(2.0, normalOnly.NormalOffsetMeters);
        AssertDoubleNear(0.0, normalOnly.LateralOffsetMeters);
        AssertDoubleNear(0.0, HeartlineOffset.Zero.NormalOffsetMeters);
        AssertDoubleNear(0.0, HeartlineOffset.Zero.LateralOffsetMeters);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void HeartlineOffset_NonFiniteNormalOffset_Throws(double normalOffsetMeters)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new HeartlineOffset(normalOffsetMeters));

        Assert.Equal("normalOffsetMeters", exception.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void HeartlineOffset_NonFiniteLateralOffset_Throws(double lateralOffsetMeters)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new HeartlineOffset(0.0, lateralOffsetMeters));

        Assert.Equal("lateralOffsetMeters", exception.ParamName);
    }

    [Fact]
    public void ZeroOffset_MatchesTrackEvaluatorCenterlinePositionsAndAxes()
    {
        TrackDocument document = BuildStraightTrack(length: 10.0);
        var evaluator = new TrackEvaluator(document);
        double[] distances = { -2.0, 0.0, 2.5, 10.0, 12.0 };

        HeartlineFrame[] heartlineFrames = HeartlineSampler.SampleAtDistances(
            evaluator,
            HeartlineOffset.Zero,
            distances);
        ExportTrackFrame[] centerlineFrames = evaluator.EvaluateFramesAtDistances(distances);

        Assert.Equal(centerlineFrames.Length, heartlineFrames.Length);
        for (int i = 0; i < centerlineFrames.Length; i++)
        {
            AssertHeartlineFrameMatchesCenterline(centerlineFrames[i], heartlineFrames[i]);
        }

        HeartlineFrame scalar = HeartlineSampler.SampleAtDistance(
            evaluator,
            HeartlineOffset.Zero,
            distance: 4.0);
        ExportTrackFrame expectedScalar = evaluator.EvaluateFrameAtDistance(4.0);
        AssertHeartlineFrameMatchesCenterline(expectedScalar, scalar);
    }

    [Fact]
    public void UnbankedStraightTrack_PositiveOffsets_MapToNormalAndBinormal()
    {
        TrackDocument document = BuildStraightTrack(length: 10.0);
        var evaluator = new TrackEvaluator(document);
        var offset = new HeartlineOffset(normalOffsetMeters: 2.0, lateralOffsetMeters: 0.75);

        HeartlineFrame frame = HeartlineSampler.SampleAtDistance(evaluator, offset, distance: 4.0);

        AssertVectorNear(new Vector3d(4.0, 0.0, 0.0), frame.CenterlinePosition);
        AssertVectorNear(new Vector3d(4.0, 2.0, 0.75), frame.Position);
        AssertVectorNear(Vector3d.UnitX, frame.Tangent);
        AssertVectorNear(Vector3d.UnitY, frame.Normal);
        AssertVectorNear(Vector3d.UnitZ, frame.Binormal);
    }

    [Fact]
    public void ProfileBankedStraightTrack_PositiveRoll_RotatesNormalOffsetTowardPositiveBinormal()
    {
        TrackDocument document = BuildStraightTrack(length: 10.0);
        var evaluator = new TrackEvaluator(document);
        BankingProfile profile = CreateConstantProfile(document.TotalLength, System.Math.PI * 0.5);
        var offset = new HeartlineOffset(normalOffsetMeters: 1.5);

        HeartlineFrame[] frames = HeartlineSampler.SampleAtDistances(
            evaluator,
            profile,
            offset,
            new[] { 5.0 });

        HeartlineFrame frame = Assert.Single(frames);
        AssertVectorNear(new Vector3d(5.0, 0.0, 0.0), frame.CenterlinePosition);
        AssertVectorNear(new Vector3d(5.0, 0.0, 1.5), frame.Position);
        AssertVectorNear(Vector3d.UnitX, frame.Tangent);
        AssertVectorNear(Vector3d.UnitZ, frame.Normal);
        AssertVectorNear(new Vector3d(0.0, -1.0, 0.0), frame.Binormal);
    }

    [Fact]
    public void ExplicitBankingProfile_AffectsHeartlineOnlyThroughOptInSampler()
    {
        TrackDocument document = BuildStraightTrack(length: 10.0);
        var evaluator = new TrackEvaluator(document);
        BankingProfile profile = CreateConstantProfile(document.TotalLength, System.Math.PI * 0.5);
        var offset = new HeartlineOffset(normalOffsetMeters: 1.0);

        HeartlineFrame defaultFrame = HeartlineSampler.SampleAtDistance(
            evaluator,
            offset,
            distance: 5.0);
        HeartlineFrame profileFrame = Assert.Single(HeartlineSampler.SampleAtDistances(
            evaluator,
            profile,
            offset,
            new[] { 5.0 }));
        ExportTrackFrame evaluatorFrame = evaluator.EvaluateFrameAtDistance(5.0);

        AssertVectorNear(new Vector3d(5.0, 1.0, 0.0), defaultFrame.Position);
        AssertVectorNear(new Vector3d(5.0, 0.0, 1.0), profileFrame.Position);
        AssertVectorNear(Vector3d.UnitY, evaluatorFrame.Normal);
        AssertVectorNear(Vector3d.UnitZ, evaluatorFrame.Binormal);
    }

    [Fact]
    public void HeartlineSampling_DoesNotChangeDefaultTrainBehavior()
    {
        TrackDocument document = BuildStraightTrackWithRoll(
            length: 30.0,
            rollRadians: System.Math.PI * 0.5);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        BankingProfile profile = CreateConstantProfile(document.TotalLength, rollRadians: 0.0);

        IReadOnlyList<TrainCarTransform> before = provider.EvaluateCarTransforms(
            leadDistance: 12.0,
            carSpacing: 2.0,
            carCount: 3);

        _ = HeartlineSampler.SampleAtDistance(
            evaluator,
            new HeartlineOffset(normalOffsetMeters: 1.0),
            distance: 12.0);
        _ = HeartlineSampler.SampleAtDistances(
            evaluator,
            profile,
            new HeartlineOffset(normalOffsetMeters: 1.0),
            new[] { 12.0, 10.0 });

        IReadOnlyList<TrainCarTransform> after = provider.EvaluateCarTransforms(
            leadDistance: 12.0,
            carSpacing: 2.0,
            carCount: 3);

        Assert.Equal(before.Count, after.Count);
        for (int i = 0; i < before.Count; i++)
        {
            AssertTrainCarTransformNear(before[i], after[i]);
        }

        AssertVectorNear(Vector3d.UnitZ, after[0].Frame.Normal);
        AssertVectorNear(new Vector3d(0.0, -1.0, 0.0), after[0].Frame.Binormal);
    }

    [Fact]
    public void BatchSampling_PreservesInputOrder()
    {
        TrackDocument document = BuildStraightTrack(length: 10.0);
        var evaluator = new TrackEvaluator(document);
        double[] distances = { 8.0, 2.0, 5.0 };

        HeartlineFrame[] frames = HeartlineSampler.SampleAtDistances(
            evaluator,
            new HeartlineOffset(normalOffsetMeters: 0.5),
            distances);

        Assert.Equal(distances.Length, frames.Length);
        AssertVectorNear(new Vector3d(8.0, 0.5, 0.0), frames[0].Position);
        AssertVectorNear(new Vector3d(2.0, 0.5, 0.0), frames[1].Position);
        AssertVectorNear(new Vector3d(5.0, 0.5, 0.0), frames[2].Position);
    }

    [Fact]
    public void EmptyBatch_ReturnsEmptyArray()
    {
        TrackDocument document = BuildStraightTrack(length: 10.0);
        var evaluator = new TrackEvaluator(document);
        BankingProfile profile = CreateConstantProfile(document.TotalLength, rollRadians: 0.0);

        HeartlineFrame[] defaultFrames = HeartlineSampler.SampleAtDistances(
            evaluator,
            HeartlineOffset.Zero,
            Array.Empty<double>());
        HeartlineFrame[] profileFrames = HeartlineSampler.SampleAtDistances(
            evaluator,
            profile,
            HeartlineOffset.Zero,
            Array.Empty<double>());

        Assert.Empty(defaultFrames);
        Assert.Empty(profileFrames);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteScalarDistance_MatchesEvaluatorBehavior(double distance)
    {
        TrackDocument document = BuildStraightTrack(length: 10.0);
        var evaluator = new TrackEvaluator(document);

        ArgumentOutOfRangeException evaluatorException = Assert.Throws<ArgumentOutOfRangeException>(
            () => evaluator.EvaluateFrameAtDistance(distance));
        ArgumentOutOfRangeException heartlineException = Assert.Throws<ArgumentOutOfRangeException>(
            () => HeartlineSampler.SampleAtDistance(evaluator, HeartlineOffset.Zero, distance));

        Assert.Equal(evaluatorException.ParamName, heartlineException.ParamName);
        Assert.Contains("Distance must be finite.", heartlineException.Message);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteBatchDistance_MatchesEvaluatorBehavior(double distance)
    {
        TrackDocument document = BuildStraightTrack(length: 10.0);
        var evaluator = new TrackEvaluator(document);
        BankingProfile profile = CreateConstantProfile(document.TotalLength, rollRadians: 0.0);

        ArgumentOutOfRangeException defaultEvaluatorException = Assert.Throws<ArgumentOutOfRangeException>(
            () => evaluator.EvaluateFramesAtDistances(new[] { distance }));
        ArgumentOutOfRangeException defaultHeartlineException = Assert.Throws<ArgumentOutOfRangeException>(
            () => HeartlineSampler.SampleAtDistances(evaluator, HeartlineOffset.Zero, new[] { distance }));
        ArgumentOutOfRangeException profileEvaluatorException = Assert.Throws<ArgumentOutOfRangeException>(
            () => BankingProfileSampler.SampleFramesAtDistances(evaluator, profile, new[] { distance }));
        ArgumentOutOfRangeException profileHeartlineException = Assert.Throws<ArgumentOutOfRangeException>(
            () => HeartlineSampler.SampleAtDistances(evaluator, profile, HeartlineOffset.Zero, new[] { distance }));

        Assert.Equal(defaultEvaluatorException.ParamName, defaultHeartlineException.ParamName);
        Assert.Equal(profileEvaluatorException.ParamName, profileHeartlineException.ParamName);
        Assert.Contains("Distance must be finite.", defaultHeartlineException.Message);
        Assert.Contains("Distance must be finite.", profileHeartlineException.Message);
    }

    [Fact]
    public void ToMatrix4x4_MatchesTrackFrameMatrixConvention()
    {
        TrackDocument document = BuildStraightTrack(length: 10.0);
        var evaluator = new TrackEvaluator(document);
        HeartlineFrame heartlineFrame = HeartlineSampler.SampleAtDistance(
            evaluator,
            new HeartlineOffset(normalOffsetMeters: 1.0, lateralOffsetMeters: 2.0),
            distance: 3.0);
        Matrix4x4 expected = TrackFrame.CreateFromFrame(
            heartlineFrame.Position,
            heartlineFrame.Tangent,
            heartlineFrame.Normal,
            heartlineFrame.Binormal);

        Matrix4x4 actual = heartlineFrame.ToMatrix4x4();

        AssertMatrixNear(expected, actual);
    }

    private static TrackDocument BuildStraightTrack(double length)
    {
        return new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(length: length)
        });
    }

    private static TrackDocument BuildStraightTrackWithRoll(double length, double rollRadians)
    {
        return new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(length: length, rollRadians: rollRadians)
        });
    }

    private static BankingProfile CreateConstantProfile(double totalLength, double rollRadians)
    {
        return new BankingProfile(new[]
        {
            new BankingProfileKey(0.0, rollRadians, BankingProfileInterpolationMode.Constant),
            new BankingProfileKey(totalLength, rollRadians, BankingProfileInterpolationMode.Constant)
        });
    }

    private static void AssertHeartlineFrameMatchesCenterline(
        ExportTrackFrame expected,
        HeartlineFrame actual)
    {
        AssertDoubleNear(expected.Distance, actual.Distance);
        AssertVectorNear(expected.Position, actual.CenterlinePosition);
        AssertVectorNear(expected.Position, actual.Position);
        AssertVectorNear(expected.Tangent, actual.Tangent);
        AssertVectorNear(expected.Normal, actual.Normal);
        AssertVectorNear(expected.Binormal, actual.Binormal);
    }

    private static void AssertTrainCarTransformNear(TrainCarTransform expected, TrainCarTransform actual)
    {
        Assert.Equal(expected.CarIndex, actual.CarIndex);
        AssertDoubleNear(expected.Distance, actual.Distance);
        AssertTrackFrameNear(expected.Frame, actual.Frame);
        AssertMatrixNear(expected.Matrix, actual.Matrix);
    }

    private static void AssertTrackFrameNear(ExportTrackFrame expected, ExportTrackFrame actual)
    {
        AssertDoubleNear(expected.Distance, actual.Distance);
        AssertVectorNear(expected.Position, actual.Position);
        AssertVectorNear(expected.Tangent, actual.Tangent);
        AssertVectorNear(expected.Normal, actual.Normal);
        AssertVectorNear(expected.Binormal, actual.Binormal);
    }

    private static void AssertMatrixNear(Matrix4x4 expected, Matrix4x4 actual)
    {
        AssertFloatNear(expected.M11, actual.M11);
        AssertFloatNear(expected.M12, actual.M12);
        AssertFloatNear(expected.M13, actual.M13);
        AssertFloatNear(expected.M14, actual.M14);
        AssertFloatNear(expected.M21, actual.M21);
        AssertFloatNear(expected.M22, actual.M22);
        AssertFloatNear(expected.M23, actual.M23);
        AssertFloatNear(expected.M24, actual.M24);
        AssertFloatNear(expected.M31, actual.M31);
        AssertFloatNear(expected.M32, actual.M32);
        AssertFloatNear(expected.M33, actual.M33);
        AssertFloatNear(expected.M34, actual.M34);
        AssertFloatNear(expected.M41, actual.M41);
        AssertFloatNear(expected.M42, actual.M42);
        AssertFloatNear(expected.M43, actual.M43);
        AssertFloatNear(expected.M44, actual.M44);
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual)
    {
        AssertDoubleNear(expected.X, actual.X);
        AssertDoubleNear(expected.Y, actual.Y);
        AssertDoubleNear(expected.Z, actual.Z);
    }

    private static void AssertFloatNear(float expected, float actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0f, 1e-6f);
    }

    private static void AssertDoubleNear(double expected, double actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, Tolerance);
    }
}
