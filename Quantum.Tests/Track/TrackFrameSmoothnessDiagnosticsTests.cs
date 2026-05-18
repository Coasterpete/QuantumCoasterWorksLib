using System;
using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using Xunit;
using ExportTrackFrame = Quantum.Track.TrackFrame;
using SystemMath = System.Math;

namespace Quantum.Tests;

public sealed class TrackFrameSmoothnessDiagnosticsTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void TrackFrameSmoothnessDiagnostics_StraightFrames_ReportZeroDeltas()
    {
        ExportTrackFrame[] frames =
        {
            BuildFrame(0.0, new Vector3d(0.0, 0.0, 0.0), Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ),
            BuildFrame(1.0, new Vector3d(1.0, 0.0, 0.0), Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ),
            BuildFrame(2.0, new Vector3d(2.0, 0.0, 0.0), Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ)
        };
        double[] distances = { 0.0, 1.0, 2.0 };

        TrackFrameSmoothnessReport report = TrackFrameSmoothnessDiagnostics.Analyze(frames, distances);

        Assert.Equal(2, report.IntervalCount);
        AssertNear(0.0, report.TangentAngleDelta.MaxAbsoluteRadians);
        AssertNear(0.0, report.NormalAngleDelta.MaxAbsoluteRadians);
        AssertNear(0.0, report.BinormalAngleDelta.MaxAbsoluteRadians);
        AssertNear(0.0, report.FrameAngleDelta.MaxAbsoluteRadians);
        AssertNear(0.0, report.FrameTwistDelta.MaxAbsoluteRadians);
        AssertNear(0.0, report.CurvatureEstimate.MaxAbsolute);
        AssertNear(0.0, report.CurvatureEstimateDelta.MaxAbsolute);
    }

    [Fact]
    public void TrackFrameSmoothnessDiagnostics_RightAngleTurn_ReportsExpectedTangentAndCurvature()
    {
        Vector3d turnedTangent = Vector3d.UnitZ;
        Vector3d turnedNormal = Vector3d.UnitY;
        Vector3d turnedBinormal = Vector3d.Cross(turnedTangent, turnedNormal);

        ExportTrackFrame[] frames =
        {
            BuildFrame(0.0, new Vector3d(0.0, 0.0, 0.0), Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ),
            BuildFrame(2.0, new Vector3d(2.0, 0.0, 0.0), turnedTangent, turnedNormal, turnedBinormal)
        };

        TrackFrameSmoothnessReport report = TrackFrameSmoothnessDiagnostics.Analyze(frames, new[] { 0.0, 2.0 });

        Assert.Equal(1, report.IntervalCount);
        AssertNear(SystemMath.PI * 0.5, report.TangentAngleDelta.MaxAbsoluteRadians);
        AssertNear(0.0, report.NormalAngleDelta.MaxAbsoluteRadians);
        AssertNear(SystemMath.PI * 0.5, report.BinormalAngleDelta.MaxAbsoluteRadians);
        AssertNear(SystemMath.PI * 0.5, report.FrameAngleDelta.MaxAbsoluteRadians);
        AssertNear(SystemMath.PI * 0.25, report.CurvatureEstimate.MaxAbsolute);
        AssertNear(0.0, report.CurvatureEstimateDelta.MaxAbsolute);
    }

    [Fact]
    public void TrackFrameSmoothnessDiagnostics_RollOnlyChange_ReportsTwistWithoutTangentDelta()
    {
        double quarterTurn = SystemMath.PI * 0.25;
        Vector3d rolledNormal = new Vector3d(0.0, SystemMath.Cos(quarterTurn), SystemMath.Sin(quarterTurn));
        Vector3d rolledBinormal = Vector3d.Cross(Vector3d.UnitX, rolledNormal);

        ExportTrackFrame[] frames =
        {
            BuildFrame(0.0, new Vector3d(0.0, 0.0, 0.0), Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ),
            BuildFrame(1.0, new Vector3d(1.0, 0.0, 0.0), Vector3d.UnitX, rolledNormal, rolledBinormal)
        };

        TrackFrameSmoothnessReport report = TrackFrameSmoothnessDiagnostics.Analyze(frames, new[] { 0.0, 1.0 });

        AssertNear(0.0, report.TangentAngleDelta.MaxAbsoluteRadians);
        AssertNear(quarterTurn, report.NormalAngleDelta.MaxAbsoluteRadians);
        AssertNear(quarterTurn, report.BinormalAngleDelta.MaxAbsoluteRadians);
        AssertNear(quarterTurn, report.FrameTwistDelta.MaxAbsoluteRadians);
    }

    [Fact]
    public void DeterministicDebugTrack_SmoothnessSpikesPersistWithHigherSubSampling()
    {
        TrackDocument document = BuildDeterministicBackendVisualizerTrack(withRoll: true);
        var evaluator = new TrackEvaluator(document);

        double[] baseDistances = TrackFrameDebugGizmoBuilder.BuildUniformFrameDistances(document.TotalLength, 128);
        ExportTrackFrame[] baseFrames = evaluator.EvaluateFramesAtDistances(baseDistances);
        TrackFrameSmoothnessReport baseReport = TrackFrameSmoothnessDiagnostics.Analyze(baseFrames, baseDistances);

        double[] denseDistances = TrackFrameDebugGizmoBuilder.BuildUniformFrameDistances(document.TotalLength, 128, subSamplesPerSegment: 4);
        ExportTrackFrame[] denseFrames = evaluator.EvaluateFramesAtDistances(denseDistances);
        TrackFrameSmoothnessReport denseReport = TrackFrameSmoothnessDiagnostics.Analyze(denseFrames, denseDistances);

        Assert.True(
            baseReport.FrameAngleDelta.MaxAbsoluteDegrees > 2.0,
            $"Expected coarse-frame max delta > 2 deg, got {baseReport.FrameAngleDelta.MaxAbsoluteDegrees:F3}.");
        Assert.True(
            denseReport.FrameAngleDelta.MaxAbsoluteDegrees > 2.0,
            $"Expected dense-frame max delta > 2 deg, got {denseReport.FrameAngleDelta.MaxAbsoluteDegrees:F3}.");
        Assert.True(
            denseReport.FrameAngleDelta.MaxAbsoluteDegrees >= baseReport.FrameAngleDelta.MaxAbsoluteDegrees * 0.5,
            $"Expected dense-frame spike to persist, coarse={baseReport.FrameAngleDelta.MaxAbsoluteDegrees:F3} deg dense={denseReport.FrameAngleDelta.MaxAbsoluteDegrees:F3} deg.");

        Assert.True(baseReport.FrameAngleDelta.MaxAbsoluteDegrees > baseReport.FrameAngleDelta.AverageAbsoluteDegrees * 2.0);
        Assert.True(denseReport.FrameAngleDelta.MaxAbsoluteDegrees > denseReport.FrameAngleDelta.AverageAbsoluteDegrees * 2.0);
    }

    [Fact]
    public void DeterministicDebugTrack_RollStepsIncreaseFrameTwistWhileTangentDeltasStayGeometryDriven()
    {
        TrackDocument rolledDocument = BuildDeterministicBackendVisualizerTrack(withRoll: true);
        TrackDocument unrolledDocument = BuildDeterministicBackendVisualizerTrack(withRoll: false);

        var rolledEvaluator = new TrackEvaluator(rolledDocument);
        var unrolledEvaluator = new TrackEvaluator(unrolledDocument);

        double[] distances = TrackFrameDebugGizmoBuilder.BuildUniformFrameDistances(rolledDocument.TotalLength, 512);
        ExportTrackFrame[] rolledFrames = rolledEvaluator.EvaluateFramesAtDistances(distances);
        ExportTrackFrame[] unrolledFrames = unrolledEvaluator.EvaluateFramesAtDistances(distances);

        TrackFrameSmoothnessReport rolledReport = TrackFrameSmoothnessDiagnostics.Analyze(rolledFrames, distances);
        TrackFrameSmoothnessReport unrolledReport = TrackFrameSmoothnessDiagnostics.Analyze(unrolledFrames, distances);

        double tangentMaxDeltaDifference = SystemMath.Abs(
            rolledReport.TangentAngleDelta.MaxAbsoluteRadians -
            unrolledReport.TangentAngleDelta.MaxAbsoluteRadians);

        Assert.InRange(tangentMaxDeltaDifference, 0.0, 1e-9);
        Assert.True(rolledReport.FrameTwistDelta.MaxAbsoluteDegrees > unrolledReport.FrameTwistDelta.MaxAbsoluteDegrees + 5.0);
        Assert.True(rolledReport.NormalAngleDelta.MaxAbsoluteDegrees > unrolledReport.NormalAngleDelta.MaxAbsoluteDegrees + 5.0);
        Assert.True(unrolledReport.TangentAngleDelta.MaxAbsoluteDegrees > unrolledReport.TangentAngleDelta.AverageAbsoluteDegrees * 3.0);

        TrackFrameSmoothnessInterval rolledWorstTwist = FindWorstInterval(rolledReport, interval => SystemMath.Abs(interval.FrameTwistDeltaRadians));
        TrackFrameSmoothnessInterval unrolledWorstTangent = FindWorstInterval(unrolledReport, interval => interval.TangentAngleDeltaRadians);

        Assert.True(IsNearSegmentBoundary(rolledWorstTwist, toleranceDistance: 3.0));
        Assert.True(IsNearSegmentBoundary(unrolledWorstTangent, toleranceDistance: 3.0));
    }

    [Fact]
    public void TrackFrameSmoothnessDiagnostics_CountMismatch_Throws()
    {
        ExportTrackFrame[] frames =
        {
            BuildFrame(0.0, new Vector3d(0.0, 0.0, 0.0), Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ),
            BuildFrame(1.0, new Vector3d(1.0, 0.0, 0.0), Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ)
        };

        Assert.Throws<ArgumentException>(() => TrackFrameSmoothnessDiagnostics.Analyze(frames, new[] { 0.0 }));
    }

    private static ExportTrackFrame BuildFrame(
        double distance,
        Vector3d position,
        Vector3d tangent,
        Vector3d normal,
        Vector3d binormal)
    {
        return new ExportTrackFrame(distance, position, tangent, normal, binormal);
    }

    private static TrackFrameSmoothnessInterval FindWorstInterval(
        TrackFrameSmoothnessReport report,
        Func<TrackFrameSmoothnessInterval, double> scoreSelector)
    {
        TrackFrameSmoothnessInterval worst = report.Intervals[0];
        double worstScore = scoreSelector(worst);

        for (int i = 1; i < report.Intervals.Count; i++)
        {
            TrackFrameSmoothnessInterval candidate = report.Intervals[i];
            double score = scoreSelector(candidate);
            if (score > worstScore)
            {
                worst = candidate;
                worstScore = score;
            }
        }

        return worst;
    }

    private static bool IsNearSegmentBoundary(TrackFrameSmoothnessInterval interval, double toleranceDistance)
    {
        double midpoint = (interval.StartDistance + interval.EndDistance) * 0.5;
        double[] boundaries = { 52.0, 144.0, 238.0, 314.0 };

        for (int i = 0; i < boundaries.Length; i++)
        {
            if (SystemMath.Abs(midpoint - boundaries[i]) <= toleranceDistance)
            {
                return true;
            }
        }

        return false;
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

    private static void AssertNear(double expected, double actual)
    {
        Assert.InRange(SystemMath.Abs(expected - actual), 0.0, Tolerance);
    }
}
