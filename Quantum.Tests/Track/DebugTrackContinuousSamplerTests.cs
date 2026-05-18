using System.Collections.Generic;
using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class DebugTrackContinuousSamplerTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void SampleContinuousFrames_PreservesSampleCountAndFiniteBasis()
    {
        TrackDocument document = BuildDeterministicBackendVisualizerTrack(withRoll: true);
        var evaluator = new TrackEvaluator(document);
        double[] distances = TrackFrameDebugGizmoBuilder.BuildUniformFrameDistances(document.TotalLength, 192, subSamplesPerSegment: 2);

        ExportTrackFrame[] frames = DebugTrackContinuousSampler.SampleContinuousFrames(
            document,
            evaluator,
            distances,
            controlPointSampleCount: 32,
            arcLengthSampleCount: 512,
            rollBlendDistance: 8.0);

        Assert.Equal(distances.Length, frames.Length);

        for (int i = 0; i < frames.Length; i++)
        {
            ExportTrackFrame frame = frames[i];
            AssertFinite(frame.Position);
            AssertFinite(frame.Tangent);
            AssertFinite(frame.Normal);
            AssertFinite(frame.Binormal);

            Assert.InRange(System.Math.Abs(frame.Tangent.Length - 1.0), 0.0, Tolerance);
            Assert.InRange(System.Math.Abs(frame.Normal.Length - 1.0), 0.0, Tolerance);
            Assert.InRange(System.Math.Abs(frame.Binormal.Length - 1.0), 0.0, Tolerance);
            Assert.InRange(System.Math.Abs(Vector3d.Dot(frame.Tangent, frame.Normal)), 0.0, 1e-5);
            Assert.InRange(System.Math.Abs(Vector3d.Dot(frame.Tangent, frame.Binormal)), 0.0, 1e-5);
            Assert.InRange(System.Math.Abs(Vector3d.Dot(frame.Normal, frame.Binormal)), 0.0, 1e-5);
        }
    }

    [Fact]
    public void SampleContinuousFrames_ReducesFrameAndCurvatureDeltaSpikesAgainstSegmentedBaseline()
    {
        TrackDocument document = BuildDeterministicBackendVisualizerTrack(withRoll: true);
        var evaluator = new TrackEvaluator(document);
        double[] distances = TrackFrameDebugGizmoBuilder.BuildUniformFrameDistances(document.TotalLength, 512);

        ExportTrackFrame[] segmentedFrames = evaluator.EvaluateFramesAtDistances(distances);
        ExportTrackFrame[] smoothFrames = DebugTrackContinuousSampler.SampleContinuousFrames(
            document,
            evaluator,
            distances,
            controlPointSampleCount: 32,
            arcLengthSampleCount: 768,
            rollBlendDistance: 8.0);

        TrackFrameSmoothnessReport segmentedReport = TrackFrameSmoothnessDiagnostics.Analyze(segmentedFrames, distances);
        TrackFrameSmoothnessReport smoothReport = TrackFrameSmoothnessDiagnostics.Analyze(smoothFrames, distances);

        Assert.True(
            smoothReport.FrameAngleDelta.MaxAbsoluteDegrees < segmentedReport.FrameAngleDelta.MaxAbsoluteDegrees,
            $"Expected smooth frame max delta to improve. segmented={segmentedReport.FrameAngleDelta.MaxAbsoluteDegrees:F3} smooth={smoothReport.FrameAngleDelta.MaxAbsoluteDegrees:F3}");
        Assert.True(
            smoothReport.TangentAngleDelta.MaxAbsoluteDegrees < segmentedReport.TangentAngleDelta.MaxAbsoluteDegrees,
            $"Expected smooth tangent max delta to improve. segmented={segmentedReport.TangentAngleDelta.MaxAbsoluteDegrees:F3} smooth={smoothReport.TangentAngleDelta.MaxAbsoluteDegrees:F3}");
        Assert.True(
            smoothReport.CurvatureEstimateDelta.MaxAbsolute < segmentedReport.CurvatureEstimateDelta.MaxAbsolute,
            $"Expected smooth |dCurvature| max to improve. segmented={segmentedReport.CurvatureEstimateDelta.MaxAbsolute:F5} smooth={smoothReport.CurvatureEstimateDelta.MaxAbsolute:F5}");
    }

    [Fact]
    public void SampleContinuousFrames_RollBlendSoftensTwistDeltasNearBoundaries()
    {
        TrackDocument document = BuildDeterministicBackendVisualizerTrack(withRoll: true);
        var evaluator = new TrackEvaluator(document);
        double[] distances = TrackFrameDebugGizmoBuilder.BuildUniformFrameDistances(document.TotalLength, 512);

        ExportTrackFrame[] noBlendFrames = DebugTrackContinuousSampler.SampleContinuousFrames(
            document,
            evaluator,
            distances,
            controlPointSampleCount: 32,
            arcLengthSampleCount: 768,
            rollBlendDistance: 0.0);
        ExportTrackFrame[] blendFrames = DebugTrackContinuousSampler.SampleContinuousFrames(
            document,
            evaluator,
            distances,
            controlPointSampleCount: 32,
            arcLengthSampleCount: 768,
            rollBlendDistance: 8.0);

        TrackFrameSmoothnessReport noBlendReport = TrackFrameSmoothnessDiagnostics.Analyze(noBlendFrames, distances);
        TrackFrameSmoothnessReport blendReport = TrackFrameSmoothnessDiagnostics.Analyze(blendFrames, distances);

        Assert.True(
            blendReport.FrameTwistDelta.MaxAbsoluteDegrees < noBlendReport.FrameTwistDelta.MaxAbsoluteDegrees,
            $"Expected eased roll blending to reduce max twist delta. noBlend={noBlendReport.FrameTwistDelta.MaxAbsoluteDegrees:F3} blend={blendReport.FrameTwistDelta.MaxAbsoluteDegrees:F3}");
    }

    private static TrackDocument BuildDeterministicBackendVisualizerTrack(bool withRoll)
    {
        double c1Roll = withRoll ? 0.18 : 0.0;
        double c2Roll = withRoll ? 0.34 : 0.0;
        double c3Roll = withRoll ? 0.16 : 0.0;
        double s4Roll = withRoll ? 0.06 : 0.0;

        TrackSegment[] segments =
        {
            new StraightSegment(
                length: 52.0,
                id: "s0",
                spline: new LineCurve(
                    new Vector3d(0.0, 0.0, 0.0),
                    new Vector3d(52.0, 6.0, 0.0)),
                rollRadians: 0.0),
            new CurvedSegment(
                length: 92.0,
                id: "c1",
                spline: new CubicBezierCurve(
                    new Vector3d(52.0, 6.0, 0.0),
                    new Vector3d(80.0, 10.0, 3.0),
                    new Vector3d(105.0, 31.0, 45.0),
                    new Vector3d(122.0, 34.0, 66.0)),
                rollRadians: c1Roll),
            new CurvedSegment(
                length: 94.0,
                id: "c2",
                spline: new CubicBezierCurve(
                    new Vector3d(122.0, 34.0, 66.0),
                    new Vector3d(139.0, 37.0, 87.0),
                    new Vector3d(157.0, 28.0, 36.0),
                    new Vector3d(176.0, 24.0, 22.0)),
                rollRadians: c2Roll),
            new CurvedSegment(
                length: 76.0,
                id: "c3",
                spline: new CubicBezierCurve(
                    new Vector3d(176.0, 24.0, 22.0),
                    new Vector3d(195.0, 20.0, 8.0),
                    new Vector3d(220.0, 12.0, -8.0),
                    new Vector3d(244.0, 10.0, -6.0)),
                rollRadians: c3Roll),
            new StraightSegment(
                length: 54.0,
                id: "s4",
                spline: new LineCurve(
                    new Vector3d(244.0, 10.0, -6.0),
                    new Vector3d(298.0, 8.0, -6.0)),
                rollRadians: s4Roll)
        };

        return new TrackDocument(segments);
    }

    private static void AssertFinite(Vector3d value)
    {
        Assert.False(double.IsNaN(value.X) || double.IsInfinity(value.X));
        Assert.False(double.IsNaN(value.Y) || double.IsInfinity(value.Y));
        Assert.False(double.IsNaN(value.Z) || double.IsInfinity(value.Z));
    }
}
