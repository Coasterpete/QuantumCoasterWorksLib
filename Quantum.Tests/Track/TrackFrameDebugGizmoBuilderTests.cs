using System;
using Quantum.Math;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class TrackFrameDebugGizmoBuilderTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void BuildAxes_ProducesThreeAxisSegments()
    {
        var frame = new ExportTrackFrame(
            position: new Vector3d(1.0, 2.0, 3.0),
            tangent: Vector3d.UnitX,
            normal: Vector3d.UnitY,
            binormal: Vector3d.UnitZ);

        DebugLineSegment[] axes = TrackFrameDebugGizmoBuilder.BuildAxes(frame, axisLength: 2.0);

        Assert.Equal(3, axes.Length);
        Assert.Equal(TrackFrameAxisType.Tangent, axes[0].AxisType);
        Assert.Equal(TrackFrameAxisType.Normal, axes[1].AxisType);
        Assert.Equal(TrackFrameAxisType.Binormal, axes[2].AxisType);
    }

    [Fact]
    public void BuildAxes_AxisEndpointsMatchPositionPlusDirectionTimesLength()
    {
        var frame = new ExportTrackFrame(
            position: new Vector3d(10.0, -4.0, 7.0),
            tangent: Vector3d.UnitX,
            normal: Vector3d.UnitY,
            binormal: Vector3d.UnitZ);
        double axisLength = 3.5;

        DebugLineSegment[] axes = TrackFrameDebugGizmoBuilder.BuildAxes(frame, axisLength);

        AssertVectorNear(frame.Position, axes[0].Start);
        AssertVectorNear(frame.Position + (frame.Tangent * axisLength), axes[0].End);
        AssertVectorNear(frame.Position, axes[1].Start);
        AssertVectorNear(frame.Position + (frame.Normal * axisLength), axes[1].End);
        AssertVectorNear(frame.Position, axes[2].Start);
        AssertVectorNear(frame.Position + (frame.Binormal * axisLength), axes[2].End);
    }

    [Fact]
    public void BuildAxes_RejectsInvalidAxisLength()
    {
        var frame = new ExportTrackFrame(
            position: Vector3d.Zero,
            tangent: Vector3d.UnitX,
            normal: Vector3d.UnitY,
            binormal: Vector3d.UnitZ);

        Assert.Throws<ArgumentOutOfRangeException>(() => TrackFrameDebugGizmoBuilder.BuildAxes(frame, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrackFrameDebugGizmoBuilder.BuildAxes(frame, -1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrackFrameDebugGizmoBuilder.BuildAxes(frame, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrackFrameDebugGizmoBuilder.BuildAxes(frame, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrackFrameDebugGizmoBuilder.BuildAxes(frame, double.NegativeInfinity));
    }

    [Fact]
    public void BuildAxes_OutputIsFinite()
    {
        var frame = new ExportTrackFrame(
            position: new Vector3d(-3.0, 5.0, 1.5),
            tangent: new Vector3d(1.0, 1.0, 0.0),
            normal: new Vector3d(-1.0, 1.0, 0.0),
            binormal: new Vector3d(0.0, 0.0, 1.0));

        DebugLineSegment[] axes = TrackFrameDebugGizmoBuilder.BuildAxes(frame, axisLength: 1.25);

        foreach (DebugLineSegment axis in axes)
        {
            AssertFinite(axis.Start);
            AssertFinite(axis.End);
        }
    }

    [Fact]
    public void BuildAxesAtDistance_UsesEvaluatorFrameAtDistance()
    {
        var document = new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(length: 10.0)
        });
        var evaluator = new TrackEvaluator(document);

        DebugLineSegment[] axes = TrackFrameDebugGizmoBuilder.BuildAxesAtDistance(evaluator, distance: 4.0, axisLength: 2.0);

        Assert.Equal(3, axes.Length);
        AssertVectorNear(new Vector3d(4.0, 0.0, 0.0), axes[0].Start);
        AssertVectorNear(new Vector3d(6.0, 0.0, 0.0), axes[0].End);
        AssertVectorNear(new Vector3d(4.0, 2.0, 0.0), axes[1].End);
        AssertVectorNear(new Vector3d(4.0, 0.0, 2.0), axes[2].End);
    }

    [Fact]
    public void BuildAxesAtDistance_WithNullEvaluator_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TrackFrameDebugGizmoBuilder.BuildAxesAtDistance(null!, distance: 1.0, axisLength: 1.0));
    }

    private static void AssertFinite(Vector3d vector)
    {
        Assert.False(double.IsNaN(vector.X) || double.IsNaN(vector.Y) || double.IsNaN(vector.Z));
        Assert.False(double.IsInfinity(vector.X) || double.IsInfinity(vector.Y) || double.IsInfinity(vector.Z));
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual)
    {
        Assert.InRange(System.Math.Abs(expected.X - actual.X), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(expected.Y - actual.Y), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(expected.Z - actual.Z), 0.0, Tolerance);
    }
}
