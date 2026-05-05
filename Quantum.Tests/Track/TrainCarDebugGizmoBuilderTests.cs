using System;
using System.Collections.Generic;
using Quantum.Math;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class TrainCarDebugGizmoBuilderTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void BuildWireBox_ProducesTwelveSegments()
    {
        TrainCarTransform car = CreateCarTransform(
            position: new Vector3d(1.0, 2.0, 3.0),
            tangent: Vector3d.UnitX,
            normal: Vector3d.UnitY,
            binormal: Vector3d.UnitZ);

        DebugLineSegment[] segments = TrainCarDebugGizmoBuilder.BuildWireBox(
            car,
            length: 4.0,
            width: 2.0,
            height: 1.0);

        Assert.Equal(12, segments.Length);
    }

    [Theory]
    [InlineData(0.0, 1.0, 1.0)]
    [InlineData(-1.0, 1.0, 1.0)]
    [InlineData(double.NaN, 1.0, 1.0)]
    [InlineData(double.PositiveInfinity, 1.0, 1.0)]
    [InlineData(1.0, 0.0, 1.0)]
    [InlineData(1.0, -1.0, 1.0)]
    [InlineData(1.0, double.NaN, 1.0)]
    [InlineData(1.0, double.NegativeInfinity, 1.0)]
    [InlineData(1.0, 1.0, 0.0)]
    [InlineData(1.0, 1.0, -1.0)]
    [InlineData(1.0, 1.0, double.NaN)]
    [InlineData(1.0, 1.0, double.PositiveInfinity)]
    public void BuildWireBox_RejectsInvalidDimensions(double length, double width, double height)
    {
        TrainCarTransform car = CreateCarTransform(
            position: Vector3d.Zero,
            tangent: Vector3d.UnitX,
            normal: Vector3d.UnitY,
            binormal: Vector3d.UnitZ);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrainCarDebugGizmoBuilder.BuildWireBox(car, length, width, height));
    }

    [Fact]
    public void BuildWireBox_OutputIsFinite()
    {
        TrainCarTransform car = CreateCarTransform(
            position: new Vector3d(-10.5, 2.25, 7.75),
            tangent: new Vector3d(0.6, 0.8, 0.0),
            normal: new Vector3d(-0.8, 0.6, 0.0),
            binormal: Vector3d.UnitZ);

        DebugLineSegment[] segments = TrainCarDebugGizmoBuilder.BuildWireBox(
            car,
            length: 3.5,
            width: 1.8,
            height: 2.2);

        foreach (DebugLineSegment segment in segments)
        {
            AssertFinite(segment.Start);
            AssertFinite(segment.End);
        }
    }

    [Fact]
    public void BuildWireBox_BoxIsCenteredOnCarFrame()
    {
        const double length = 6.0;
        const double width = 4.0;
        const double height = 2.0;

        TrainCarTransform car = CreateCarTransform(
            position: new Vector3d(3.5, -7.0, 1.25),
            tangent: Vector3d.UnitX,
            normal: Vector3d.UnitY,
            binormal: Vector3d.UnitZ);

        DebugLineSegment[] segments = TrainCarDebugGizmoBuilder.BuildWireBox(car, length, width, height);

        double minTangent = double.PositiveInfinity;
        double maxTangent = double.NegativeInfinity;
        double minNormal = double.PositiveInfinity;
        double maxNormal = double.NegativeInfinity;
        double minBinormal = double.PositiveInfinity;
        double maxBinormal = double.NegativeInfinity;

        foreach (DebugLineSegment segment in segments)
        {
            UpdateExtents(segment.Start);
            UpdateExtents(segment.End);
        }

        AssertDoubleNear(-(length * 0.5), minTangent);
        AssertDoubleNear(length * 0.5, maxTangent);
        AssertDoubleNear(-(height * 0.5), minNormal);
        AssertDoubleNear(height * 0.5, maxNormal);
        AssertDoubleNear(-(width * 0.5), minBinormal);
        AssertDoubleNear(width * 0.5, maxBinormal);

        void UpdateExtents(Vector3d point)
        {
            Vector3d offset = point - car.Frame.Position;

            double tangentProjection = Vector3d.Dot(offset, car.Frame.Tangent);
            double normalProjection = Vector3d.Dot(offset, car.Frame.Normal);
            double binormalProjection = Vector3d.Dot(offset, car.Frame.Binormal);

            minTangent = System.Math.Min(minTangent, tangentProjection);
            maxTangent = System.Math.Max(maxTangent, tangentProjection);
            minNormal = System.Math.Min(minNormal, normalProjection);
            maxNormal = System.Math.Max(maxNormal, normalProjection);
            minBinormal = System.Math.Min(minBinormal, binormalProjection);
            maxBinormal = System.Math.Max(maxBinormal, binormalProjection);
        }
    }

    [Fact]
    public void BuildWireBox_OrientationFollowsTrackFrameBasis()
    {
        const double length = 7.0;
        const double width = 5.0;
        const double height = 3.0;

        TrainCarTransform car = CreateCarTransform(
            position: new Vector3d(0.5, 1.0, -2.0),
            tangent: Vector3d.UnitX,
            normal: Vector3d.UnitY,
            binormal: Vector3d.UnitZ);

        DebugLineSegment[] segments = TrainCarDebugGizmoBuilder.BuildWireBox(car, length, width, height);

        int tangentCount = 0;
        int normalCount = 0;
        int binormalCount = 0;

        foreach (DebugLineSegment segment in segments)
        {
            Vector3d delta = segment.End - segment.Start;

            switch (segment.AxisType)
            {
                case TrackFrameAxisType.Tangent:
                    tangentCount++;
                    AssertDoubleNear(length, Vector3d.Dot(delta, car.Frame.Tangent));
                    AssertDoubleNear(0.0, Vector3d.Dot(delta, car.Frame.Normal));
                    AssertDoubleNear(0.0, Vector3d.Dot(delta, car.Frame.Binormal));
                    break;

                case TrackFrameAxisType.Normal:
                    normalCount++;
                    AssertDoubleNear(0.0, Vector3d.Dot(delta, car.Frame.Tangent));
                    AssertDoubleNear(height, Vector3d.Dot(delta, car.Frame.Normal));
                    AssertDoubleNear(0.0, Vector3d.Dot(delta, car.Frame.Binormal));
                    break;

                case TrackFrameAxisType.Binormal:
                    binormalCount++;
                    AssertDoubleNear(0.0, Vector3d.Dot(delta, car.Frame.Tangent));
                    AssertDoubleNear(0.0, Vector3d.Dot(delta, car.Frame.Normal));
                    AssertDoubleNear(width, Vector3d.Dot(delta, car.Frame.Binormal));
                    break;

                default:
                    throw new InvalidOperationException($"Unexpected axis type: {segment.AxisType}");
            }
        }

        Assert.Equal(4, tangentCount);
        Assert.Equal(4, normalCount);
        Assert.Equal(4, binormalCount);
    }

    [Fact]
    public void BuildWireBoxes_ProducesSegmentsForEachCar()
    {
        var cars = new List<TrainCarTransform>
        {
            CreateCarTransform(
                position: new Vector3d(0.0, 0.0, 0.0),
                tangent: Vector3d.UnitX,
                normal: Vector3d.UnitY,
                binormal: Vector3d.UnitZ),
            CreateCarTransform(
                position: new Vector3d(10.0, 1.0, -2.0),
                tangent: Vector3d.UnitX,
                normal: Vector3d.UnitY,
                binormal: Vector3d.UnitZ)
        };

        DebugLineSegment[] segments = TrainCarDebugGizmoBuilder.BuildWireBoxes(
            cars,
            length: 4.0,
            width: 2.0,
            height: 1.0);

        Assert.Equal(24, segments.Length);
    }

    [Fact]
    public void BuildWireBoxes_WithNullCars_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TrainCarDebugGizmoBuilder.BuildWireBoxes(
            null!,
            length: 4.0,
            width: 2.0,
            height: 1.0));
    }

    private static TrainCarTransform CreateCarTransform(
        Vector3d position,
        Vector3d tangent,
        Vector3d normal,
        Vector3d binormal)
    {
        var frame = new ExportTrackFrame(position, tangent, normal, binormal);
        return new TrainCarTransform(
            carIndex: 0,
            distance: 0.0,
            frame: frame,
            matrix: frame.ToMatrix4x4());
    }

    private static void AssertFinite(Vector3d vector)
    {
        Assert.False(double.IsNaN(vector.X) || double.IsNaN(vector.Y) || double.IsNaN(vector.Z));
        Assert.False(double.IsInfinity(vector.X) || double.IsInfinity(vector.Y) || double.IsInfinity(vector.Z));
    }

    private static void AssertDoubleNear(double expected, double actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, Tolerance);
    }
}
