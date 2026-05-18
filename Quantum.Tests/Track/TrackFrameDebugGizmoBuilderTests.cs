using System;
using System.Collections.Generic;
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

    [Fact]
    public void BuildUniformFrameDistances_RejectsInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrackFrameDebugGizmoBuilder.BuildUniformFrameDistances(double.NaN, sampleCount: 8, subSamplesPerSegment: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrackFrameDebugGizmoBuilder.BuildUniformFrameDistances(double.PositiveInfinity, sampleCount: 8, subSamplesPerSegment: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrackFrameDebugGizmoBuilder.BuildUniformFrameDistances(-1.0, sampleCount: 8, subSamplesPerSegment: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrackFrameDebugGizmoBuilder.BuildUniformFrameDistances(10.0, sampleCount: 1, subSamplesPerSegment: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrackFrameDebugGizmoBuilder.BuildUniformFrameDistances(10.0, sampleCount: 8, subSamplesPerSegment: 0));
    }

    [Fact]
    public void BuildUniformFrameDistances_WithNoSubSampling_ReturnsUniformRequestedSampleCount()
    {
        double[] distances = TrackFrameDebugGizmoBuilder.BuildUniformFrameDistances(
            totalLength: 10.0,
            sampleCount: 3,
            subSamplesPerSegment: 1);

        Assert.Equal(3, distances.Length);
        Assert.InRange(System.Math.Abs(distances[0] - 0.0), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(distances[1] - 5.0), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(distances[2] - 10.0), 0.0, Tolerance);
    }

    [Fact]
    public void BuildUniformFrameDistances_WithSubSampling_InsertsIntermediateSamples()
    {
        double[] distances = TrackFrameDebugGizmoBuilder.BuildUniformFrameDistances(
            totalLength: 10.0,
            sampleCount: 3,
            subSamplesPerSegment: 2);

        Assert.Equal(5, distances.Length);
        Assert.InRange(System.Math.Abs(distances[0] - 0.0), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(distances[1] - 2.5), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(distances[2] - 5.0), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(distances[3] - 7.5), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(distances[4] - 10.0), 0.0, Tolerance);
    }

    [Fact]
    public void BuildUniformFrameDistances_WithZeroLength_ReturnsAllZeroDistances()
    {
        double[] distances = TrackFrameDebugGizmoBuilder.BuildUniformFrameDistances(
            totalLength: 0.0,
            sampleCount: 4,
            subSamplesPerSegment: 3);

        Assert.Equal(10, distances.Length);
        for (int i = 0; i < distances.Length; i++)
        {
            Assert.InRange(System.Math.Abs(distances[i]), 0.0, Tolerance);
        }
    }

    [Fact]
    public void BuildRailCrossTies_RejectsInvalidInputs()
    {
        var frames = new List<ExportTrackFrame>
        {
            new(
                distance: 0.0,
                position: Vector3d.Zero,
                tangent: Vector3d.UnitX,
                normal: Vector3d.UnitY,
                binormal: Vector3d.UnitZ)
        };

        Assert.Throws<ArgumentNullException>(() => TrackFrameDebugGizmoBuilder.BuildRailCrossTies(null!, trackGauge: 1.435, spacingInterval: 0.6));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrackFrameDebugGizmoBuilder.BuildRailCrossTies(frames, trackGauge: 0.0, spacingInterval: 0.6));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrackFrameDebugGizmoBuilder.BuildRailCrossTies(frames, trackGauge: -1.0, spacingInterval: 0.6));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrackFrameDebugGizmoBuilder.BuildRailCrossTies(frames, trackGauge: double.NaN, spacingInterval: 0.6));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrackFrameDebugGizmoBuilder.BuildRailCrossTies(frames, trackGauge: 1.435, spacingInterval: 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrackFrameDebugGizmoBuilder.BuildRailCrossTies(frames, trackGauge: 1.435, spacingInterval: double.PositiveInfinity));
    }

    [Fact]
    public void BuildRailCrossTies_SamplesAtConfiguredDistanceIntervals()
    {
        var frames = new List<ExportTrackFrame>
        {
            CreateFrame(distance: 0.0, x: 0.0),
            CreateFrame(distance: 4.0, x: 4.0),
            CreateFrame(distance: 10.0, x: 10.0)
        };

        DebugLineSegment[] ties = TrackFrameDebugGizmoBuilder.BuildRailCrossTies(
            frames,
            trackGauge: 2.0,
            spacingInterval: 2.5);

        Assert.Equal(5, ties.Length);

        AssertVectorNear(new Vector3d(0.0, 0.0, -1.0), ties[0].Start);
        AssertVectorNear(new Vector3d(0.0, 0.0, 1.0), ties[0].End);

        AssertVectorNear(new Vector3d(2.5, 0.0, -1.0), ties[1].Start);
        AssertVectorNear(new Vector3d(2.5, 0.0, 1.0), ties[1].End);

        AssertVectorNear(new Vector3d(5.0, 0.0, -1.0), ties[2].Start);
        AssertVectorNear(new Vector3d(5.0, 0.0, 1.0), ties[2].End);

        AssertVectorNear(new Vector3d(7.5, 0.0, -1.0), ties[3].Start);
        AssertVectorNear(new Vector3d(7.5, 0.0, 1.0), ties[3].End);

        AssertVectorNear(new Vector3d(10.0, 0.0, -1.0), ties[4].Start);
        AssertVectorNear(new Vector3d(10.0, 0.0, 1.0), ties[4].End);
    }

    [Fact]
    public void BuildRailCrossTies_UsesBinormalAxisType()
    {
        var frames = new List<ExportTrackFrame>
        {
            CreateFrame(distance: 0.0, x: 0.0),
            CreateFrame(distance: 3.0, x: 3.0)
        };

        DebugLineSegment[] ties = TrackFrameDebugGizmoBuilder.BuildRailCrossTies(
            frames,
            trackGauge: 1.5,
            spacingInterval: 1.5);

        Assert.NotEmpty(ties);
        foreach (DebugLineSegment tie in ties)
        {
            Assert.Equal(TrackFrameAxisType.Binormal, tie.AxisType);
        }
    }

    [Fact]
    public void BuildRailCrossTies_WithAdjacentBinormalSignFlip_PreservesContinuousLeftRightPairing()
    {
        var frames = new List<ExportTrackFrame>
        {
            CreateFrame(distance: 0.0, x: 0.0),
            new(
                distance: 10.0,
                position: new Vector3d(10.0, 0.0, 0.0),
                tangent: Vector3d.UnitX,
                normal: Vector3d.UnitY * -1.0,
                binormal: Vector3d.UnitZ * -1.0)
        };

        DebugLineSegment[] ties = TrackFrameDebugGizmoBuilder.BuildRailCrossTies(
            frames,
            trackGauge: 2.0,
            spacingInterval: 5.0);

        Assert.Equal(3, ties.Length);

        AssertVectorNear(new Vector3d(0.0, 0.0, -1.0), ties[0].Start);
        AssertVectorNear(new Vector3d(0.0, 0.0, 1.0), ties[0].End);

        AssertVectorNear(new Vector3d(5.0, 0.0, -1.0), ties[1].Start);
        AssertVectorNear(new Vector3d(5.0, 0.0, 1.0), ties[1].End);

        AssertVectorNear(new Vector3d(10.0, 0.0, -1.0), ties[2].Start);
        AssertVectorNear(new Vector3d(10.0, 0.0, 1.0), ties[2].End);
    }

    [Fact]
    public void BuildRailCrossTies_WithDegenerateIntermediateBinormal_UsesPreviousLateralForContinuity()
    {
        var frames = new List<ExportTrackFrame>
        {
            CreateFrame(distance: 0.0, x: 0.0),
            new(
                distance: 5.0,
                position: new Vector3d(5.0, 0.0, 0.0),
                tangent: Vector3d.UnitX,
                normal: Vector3d.UnitY,
                binormal: Vector3d.Zero),
            CreateFrame(distance: 10.0, x: 10.0)
        };

        DebugLineSegment[] ties = TrackFrameDebugGizmoBuilder.BuildRailCrossTies(
            frames,
            trackGauge: 2.0,
            spacingInterval: 2.5);

        Assert.Equal(5, ties.Length);

        AssertVectorNear(new Vector3d(0.0, 0.0, -1.0), ties[0].Start);
        AssertVectorNear(new Vector3d(0.0, 0.0, 1.0), ties[0].End);

        AssertVectorNear(new Vector3d(2.5, 0.0, -1.0), ties[1].Start);
        AssertVectorNear(new Vector3d(2.5, 0.0, 1.0), ties[1].End);

        AssertVectorNear(new Vector3d(5.0, 0.0, -1.0), ties[2].Start);
        AssertVectorNear(new Vector3d(5.0, 0.0, 1.0), ties[2].End);

        AssertVectorNear(new Vector3d(7.5, 0.0, -1.0), ties[3].Start);
        AssertVectorNear(new Vector3d(7.5, 0.0, 1.0), ties[3].End);

        AssertVectorNear(new Vector3d(10.0, 0.0, -1.0), ties[4].Start);
        AssertVectorNear(new Vector3d(10.0, 0.0, 1.0), ties[4].End);
    }

    [Fact]
    public void BuildBankingRibbon_RejectsInvalidInputs()
    {
        var frames = new List<ExportTrackFrame>
        {
            CreateFrame(distance: 0.0, x: 0.0)
        };

        Assert.Throws<ArgumentNullException>(() => TrackFrameDebugGizmoBuilder.BuildBankingRibbon(null!, halfWidth: 0.8, normalOffset: 0.2));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrackFrameDebugGizmoBuilder.BuildBankingRibbon(frames, halfWidth: 0.0, normalOffset: 0.2));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrackFrameDebugGizmoBuilder.BuildBankingRibbon(frames, halfWidth: -1.0, normalOffset: 0.2));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrackFrameDebugGizmoBuilder.BuildBankingRibbon(frames, halfWidth: double.NaN, normalOffset: 0.2));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrackFrameDebugGizmoBuilder.BuildBankingRibbon(frames, halfWidth: 0.8, normalOffset: double.PositiveInfinity));
    }

    [Fact]
    public void BuildBankingRibbon_WithNoFrames_ReturnsEmpty()
    {
        DebugLineSegment[] segments = TrackFrameDebugGizmoBuilder.BuildBankingRibbon(
            Array.Empty<ExportTrackFrame>(),
            halfWidth: 1.0,
            normalOffset: 0.5);

        Assert.Empty(segments);
    }

    [Fact]
    public void BuildBankingRibbon_UsesNormalOffsetAndBinormalWidth()
    {
        var frames = new List<ExportTrackFrame>
        {
            CreateFrame(distance: 0.0, x: 0.0),
            CreateFrame(distance: 2.0, x: 2.0)
        };

        DebugLineSegment[] segments = TrackFrameDebugGizmoBuilder.BuildBankingRibbon(
            frames,
            halfWidth: 1.5,
            normalOffset: 0.75);

        Assert.Equal(6, segments.Length);

        Assert.Equal(TrackFrameAxisType.Normal, segments[0].AxisType);
        AssertVectorNear(new Vector3d(0.0, 0.0, 0.0), segments[0].Start);
        AssertVectorNear(new Vector3d(0.0, 0.75, 0.0), segments[0].End);

        Assert.Equal(TrackFrameAxisType.Binormal, segments[1].AxisType);
        AssertVectorNear(new Vector3d(0.0, 0.75, -1.5), segments[1].Start);
        AssertVectorNear(new Vector3d(0.0, 0.75, 1.5), segments[1].End);

        Assert.Equal(TrackFrameAxisType.Normal, segments[2].AxisType);
        AssertVectorNear(new Vector3d(2.0, 0.0, 0.0), segments[2].Start);
        AssertVectorNear(new Vector3d(2.0, 0.75, 0.0), segments[2].End);

        Assert.Equal(TrackFrameAxisType.Binormal, segments[3].AxisType);
        AssertVectorNear(new Vector3d(0.0, 0.75, -1.5), segments[3].Start);
        AssertVectorNear(new Vector3d(2.0, 0.75, -1.5), segments[3].End);

        Assert.Equal(TrackFrameAxisType.Binormal, segments[4].AxisType);
        AssertVectorNear(new Vector3d(0.0, 0.75, 1.5), segments[4].Start);
        AssertVectorNear(new Vector3d(2.0, 0.75, 1.5), segments[4].End);

        Assert.Equal(TrackFrameAxisType.Binormal, segments[5].AxisType);
        AssertVectorNear(new Vector3d(2.0, 0.75, -1.5), segments[5].Start);
        AssertVectorNear(new Vector3d(2.0, 0.75, 1.5), segments[5].End);
    }

    [Fact]
    public void BuildBankingRibbon_WithDegenerateNormalAndBinormal_UsesFallbackAxes()
    {
        var frames = new List<ExportTrackFrame>
        {
            new(
                distance: 0.0,
                position: new Vector3d(5.0, 0.0, -2.0),
                tangent: Vector3d.UnitX,
                normal: Vector3d.Zero,
                binormal: Vector3d.Zero)
        };

        DebugLineSegment[] segments = TrackFrameDebugGizmoBuilder.BuildBankingRibbon(
            frames,
            halfWidth: 2.0,
            normalOffset: 1.0);

        Assert.Equal(2, segments.Length);
        AssertVectorNear(new Vector3d(5.0, 0.0, -2.0), segments[0].Start);
        AssertVectorNear(new Vector3d(5.0, 1.0, -2.0), segments[0].End);
        AssertVectorNear(new Vector3d(3.0, 1.0, -2.0), segments[1].Start);
        AssertVectorNear(new Vector3d(7.0, 1.0, -2.0), segments[1].End);
    }

    [Fact]
    public void BuildBankingRibbon_WithAdjacentNormalAndBinormalSignFlip_KeepsRibbonEdgesContinuous()
    {
        var frames = new List<ExportTrackFrame>
        {
            CreateFrame(distance: 0.0, x: 0.0),
            new(
                distance: 2.0,
                position: new Vector3d(2.0, 0.0, 0.0),
                tangent: Vector3d.UnitX,
                normal: Vector3d.UnitY * -1.0,
                binormal: Vector3d.UnitZ * -1.0)
        };

        DebugLineSegment[] segments = TrackFrameDebugGizmoBuilder.BuildBankingRibbon(
            frames,
            halfWidth: 1.0,
            normalOffset: 0.5);

        Assert.Equal(6, segments.Length);

        AssertVectorNear(new Vector3d(0.0, 0.5, -1.0), segments[1].Start);
        AssertVectorNear(new Vector3d(0.0, 0.5, 1.0), segments[1].End);

        AssertVectorNear(new Vector3d(2.0, 0.5, 0.0), segments[2].End);

        AssertVectorNear(new Vector3d(0.0, 0.5, -1.0), segments[3].Start);
        AssertVectorNear(new Vector3d(2.0, 0.5, -1.0), segments[3].End);

        AssertVectorNear(new Vector3d(0.0, 0.5, 1.0), segments[4].Start);
        AssertVectorNear(new Vector3d(2.0, 0.5, 1.0), segments[4].End);
    }

    private static ExportTrackFrame CreateFrame(double distance, double x)
    {
        return new ExportTrackFrame(
            distance: distance,
            position: new Vector3d(x, 0.0, 0.0),
            tangent: Vector3d.UnitX,
            normal: Vector3d.UnitY,
            binormal: Vector3d.UnitZ);
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

