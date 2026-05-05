using System;
using System.Collections.Generic;
using System.Numerics;
using Quantum.Math;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class TrainCarTransformProviderTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void GetCarTransforms_ReturnsRequestedCarCount()
    {
        TrackDocument document = BuildStraightTrack(length: 20.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);

        IReadOnlyList<TrainCarTransform> cars = provider.GetCarTransforms(
            leadDistance: 12.0,
            carSpacing: 2.0,
            carCount: 4);

        Assert.Equal(4, cars.Count);
    }

    [Fact]
    public void GetCarTransforms_UsesExpectedSpacingDistances()
    {
        TrackDocument document = BuildStraightTrack(length: 20.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);

        IReadOnlyList<TrainCarTransform> cars = provider.GetCarTransforms(
            leadDistance: 10.0,
            carSpacing: 2.5,
            carCount: 4);

        AssertDoubleNear(10.0, cars[0].Distance);
        AssertDoubleNear(7.5, cars[1].Distance);
        AssertDoubleNear(5.0, cars[2].Distance);
        AssertDoubleNear(2.5, cars[3].Distance);
    }

    [Fact]
    public void GetCarTransforms_ProducesFiniteMatrices()
    {
        TrackDocument document = BuildSplineTrack(length: 15.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);

        IReadOnlyList<TrainCarTransform> cars = provider.GetCarTransforms(
            leadDistance: 12.0,
            carSpacing: 1.75,
            carCount: 5);

        for (int i = 0; i < cars.Count; i++)
        {
            AssertFiniteMatrix(cars[i].Matrix);
        }
    }

    [Fact]
    public void GetCarTransforms_FirstCarFrameMatchesLeadFrame()
    {
        TrackDocument document = BuildSplineTrack(length: 20.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        const double leadDistance = 8.25;

        ExportTrackFrame expectedLeadFrame = evaluator.EvaluateFrameAtDistance(leadDistance);
        IReadOnlyList<TrainCarTransform> cars = provider.GetCarTransforms(
            leadDistance,
            carSpacing: 1.5,
            carCount: 3);

        ExportTrackFrame actualLeadFrame = cars[0].Frame;

        AssertVectorNear(expectedLeadFrame.Position, actualLeadFrame.Position);
        AssertVectorNear(expectedLeadFrame.Tangent, actualLeadFrame.Tangent);
        AssertVectorNear(expectedLeadFrame.Normal, actualLeadFrame.Normal);
        AssertVectorNear(expectedLeadFrame.Binormal, actualLeadFrame.Binormal);
    }

    [Fact]
    public void GetCarTransforms_WhenAnyCarDistanceIsOutOfRange_ThrowsWithClearMessage()
    {
        TrackDocument document = BuildStraightTrack(length: 6.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => provider.GetCarTransforms(
            leadDistance: 1.0,
            carSpacing: 2.0,
            carCount: 2));

        Assert.Contains("car 1", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("out of range", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(6.1)]
    public void GetCarTransforms_WhenLeadDistanceIsOutOfRange_ThrowsWithClearMessage(double invalidLeadDistance)
    {
        TrackDocument document = BuildStraightTrack(length: 6.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => provider.GetCarTransforms(
            leadDistance: invalidLeadDistance,
            carSpacing: 1.0,
            carCount: 1));

        Assert.Contains("lead car distance", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("out of range", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static TrackDocument BuildStraightTrack(double length)
    {
        return new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(length: length)
        });
    }

    private static TrackDocument BuildSplineTrack(double length)
    {
        return new TrackDocument(new TrackSegment[]
        {
            new CurvedSegment(
                length: length,
                spline: new Quantum.Splines.CubicBezierCurve(
                    new Vector3d(0.0, 0.0, 0.0),
                    new Vector3d(4.0, 1.0, 0.0),
                    new Vector3d(9.0, 3.0, 2.0),
                    new Vector3d(14.0, 6.0, 3.0)),
                rollRadians: 0.3)
        });
    }

    private static void AssertFiniteMatrix(Matrix4x4 matrix)
    {
        AssertFinite(matrix.M11);
        AssertFinite(matrix.M12);
        AssertFinite(matrix.M13);
        AssertFinite(matrix.M14);
        AssertFinite(matrix.M21);
        AssertFinite(matrix.M22);
        AssertFinite(matrix.M23);
        AssertFinite(matrix.M24);
        AssertFinite(matrix.M31);
        AssertFinite(matrix.M32);
        AssertFinite(matrix.M33);
        AssertFinite(matrix.M34);
        AssertFinite(matrix.M41);
        AssertFinite(matrix.M42);
        AssertFinite(matrix.M43);
        AssertFinite(matrix.M44);
    }

    private static void AssertFinite(float value)
    {
        Assert.False(float.IsNaN(value));
        Assert.False(float.IsInfinity(value));
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual)
    {
        AssertDoubleNear(expected.X, actual.X);
        AssertDoubleNear(expected.Y, actual.Y);
        AssertDoubleNear(expected.Z, actual.Z);
    }

    private static void AssertDoubleNear(double expected, double actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, Tolerance);
    }
}
