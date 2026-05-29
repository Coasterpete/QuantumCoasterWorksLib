using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;
using SystemMath = System.Math;

namespace Quantum.Tests;

public sealed class TrackFrameContinuityDiagnosticsTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void SmoothSyntheticCenterline_HasNoContinuityDiscontinuities()
    {
        ExportTrackFrame[] frames = BuildSmoothYawRollFrames(
            sampleCount: 33,
            totalDistance: 48.0,
            totalYawRadians: DegreesToRadians(60.0),
            totalRollRadians: DegreesToRadians(20.0));
        TrackFrameContinuityThresholds thresholds = TrackFrameContinuityThresholds.UniformDegrees(8.0);

        TrackFrameContinuityReport report = TrackFrameContinuityDiagnostics.Analyze(frames, thresholds);

        Assert.False(report.HasDiscontinuities, report.ToDiagnosticString());
        Assert.Empty(report.Issues);
        Assert.Equal(frames.Length - 1, report.IntervalCount);
        Assert.InRange(report.MatrixOrientationAngleDelta.MaxAbsoluteDegrees, 0.0, 8.0);
    }

    [Fact]
    public void PersistentNormalFlip_DetectsNormalBinormalRollAndMatrixDiscontinuities()
    {
        ExportTrackFrame[] frames =
        {
            BuildFrame(0.0, Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ),
            BuildFrame(1.0, Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ),
            BuildFrame(2.0, Vector3d.UnitX, Vector3d.UnitY * -1.0, Vector3d.UnitZ * -1.0),
            BuildFrame(3.0, Vector3d.UnitX, Vector3d.UnitY * -1.0, Vector3d.UnitZ * -1.0)
        };
        TrackFrameContinuityThresholds thresholds = TrackFrameContinuityThresholds.UniformDegrees(20.0);

        TrackFrameContinuityReport report = TrackFrameContinuityDiagnostics.Analyze(frames, thresholds);

        Assert.True(report.HasDiscontinuities);
        AssertIssue(report, TrackFrameContinuityIssueKind.Normal, startSampleIndex: 1, expectedDegrees: 180.0);
        AssertIssue(report, TrackFrameContinuityIssueKind.Binormal, startSampleIndex: 1, expectedDegrees: 180.0);
        AssertIssue(report, TrackFrameContinuityIssueKind.Roll, startSampleIndex: 1, expectedDegrees: 180.0);
        AssertIssue(report, TrackFrameContinuityIssueKind.MatrixOrientation, startSampleIndex: 1, expectedDegrees: 180.0);
        Assert.DoesNotContain(report.Issues, issue => issue.Kind == TrackFrameContinuityIssueKind.Tangent);
    }

    [Fact]
    public void PersistentTangentFlip_DetectsTangentAndMatrixDiscontinuities()
    {
        ExportTrackFrame[] frames =
        {
            BuildFrame(0.0, Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ),
            BuildFrame(1.0, Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ),
            BuildFrame(2.0, Vector3d.UnitX * -1.0, Vector3d.UnitY, Vector3d.UnitZ * -1.0),
            BuildFrame(3.0, Vector3d.UnitX * -1.0, Vector3d.UnitY, Vector3d.UnitZ * -1.0)
        };
        TrackFrameContinuityThresholds thresholds = TrackFrameContinuityThresholds.UniformDegrees(20.0);

        TrackFrameContinuityReport report = TrackFrameContinuityDiagnostics.Analyze(frames, thresholds);

        AssertIssue(report, TrackFrameContinuityIssueKind.Tangent, startSampleIndex: 1, expectedDegrees: 180.0);
        AssertIssue(report, TrackFrameContinuityIssueKind.Binormal, startSampleIndex: 1, expectedDegrees: 180.0);
        AssertIssue(report, TrackFrameContinuityIssueKind.MatrixOrientation, startSampleIndex: 1, expectedDegrees: 180.0);
        Assert.DoesNotContain(report.Issues, issue => issue.Kind == TrackFrameContinuityIssueKind.Normal);
        Assert.DoesNotContain(report.Issues, issue => issue.Kind == TrackFrameContinuityIssueKind.Roll);
    }

    [Fact]
    public void RollStep_DetectsRollDiscontinuityWhenAxisThresholdsAreLoose()
    {
        double rollRadians = DegreesToRadians(90.0);
        Vector3d rolledNormal = RotateAroundAxis(Vector3d.UnitY, Vector3d.UnitX, rollRadians);
        Vector3d rolledBinormal = RotateAroundAxis(Vector3d.UnitZ, Vector3d.UnitX, rollRadians);
        ExportTrackFrame[] frames =
        {
            BuildFrame(0.0, Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ),
            BuildFrame(1.0, Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ),
            BuildFrame(2.0, Vector3d.UnitX, rolledNormal, rolledBinormal),
            BuildFrame(3.0, Vector3d.UnitX, rolledNormal, rolledBinormal)
        };
        var thresholds = TrackFrameContinuityThresholds.FromDegrees(
            tangentAngleDegrees: 180.0,
            normalAngleDegrees: 180.0,
            binormalAngleDegrees: 180.0,
            rollAngleDegrees: 10.0,
            matrixOrientationAngleDegrees: 180.0);

        TrackFrameContinuityReport report = TrackFrameContinuityDiagnostics.Analyze(frames, thresholds);

        TrackFrameContinuityIssue issue = Assert.Single(report.Issues);
        Assert.Equal(TrackFrameContinuityIssueKind.Roll, issue.Kind);
        Assert.Equal(1, issue.Interval.StartSampleIndex);
        AssertNear(90.0, issue.ActualAngleDegrees);
    }

    [Fact]
    public void MatrixOrientationThreshold_DetectsCombinedRotationEvenWhenAxisDeltasAreBelowAxisThresholds()
    {
        double rotationRadians = DegreesToRadians(40.0);
        Vector3d axis = Normalize(new Vector3d(1.0, 1.0, 1.0));
        ExportTrackFrame[] frames =
        {
            BuildFrame(0.0, Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ),
            BuildFrame(
                1.0,
                RotateAroundAxis(Vector3d.UnitX, axis, rotationRadians),
                RotateAroundAxis(Vector3d.UnitY, axis, rotationRadians),
                RotateAroundAxis(Vector3d.UnitZ, axis, rotationRadians))
        };
        var thresholds = TrackFrameContinuityThresholds.FromDegrees(
            tangentAngleDegrees: 35.0,
            normalAngleDegrees: 35.0,
            binormalAngleDegrees: 35.0,
            rollAngleDegrees: 180.0,
            matrixOrientationAngleDegrees: 35.0);

        TrackFrameContinuityReport report = TrackFrameContinuityDiagnostics.Analyze(frames, thresholds);

        TrackFrameContinuityIssue issue = Assert.Single(report.Issues);
        Assert.Equal(TrackFrameContinuityIssueKind.MatrixOrientation, issue.Kind);
        AssertNear(40.0, issue.ActualAngleDegrees);
        Assert.InRange(report.SmoothnessReport.TangentAngleDelta.MaxAbsoluteDegrees, 0.0, 35.0);
        Assert.InRange(report.SmoothnessReport.NormalAngleDelta.MaxAbsoluteDegrees, 0.0, 35.0);
        Assert.InRange(report.SmoothnessReport.BinormalAngleDelta.MaxAbsoluteDegrees, 0.0, 35.0);
    }

    [Fact]
    public void AnalyzeSampledCenterline_EvaluatesBoundTrackDeterministically()
    {
        var document = new TrackDocument(new[]
        {
            new StraightSegment(
                length: 12.0,
                id: "straight",
                spline: new LineCurve(Vector3d.Zero, new Vector3d(12.0, 0.0, 0.0)),
                rollRadians: 0.0)
        });
        var evaluator = new TrackEvaluator(document);
        double[] distances = TrackFrameDebugGizmoBuilder.BuildUniformFrameDistances(document.TotalLength, 5);

        TrackFrameContinuityReport report = TrackFrameContinuityDiagnostics.AnalyzeSampledCenterline(
            evaluator,
            distances,
            TrackFrameContinuityThresholds.UniformDegrees(1.0));

        Assert.False(report.HasDiscontinuities, report.ToDiagnosticString());
        Assert.Equal(4, report.IntervalCount);
        Assert.Contains("discontinuities=0", report.ToDiagnosticString());
    }

    [Fact]
    public void Analyze_WithCountMismatch_Throws()
    {
        ExportTrackFrame[] frames =
        {
            BuildFrame(0.0, Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ),
            BuildFrame(1.0, Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ)
        };

        Assert.Throws<ArgumentException>(() => TrackFrameContinuityDiagnostics.Analyze(
            frames,
            new[] { 0.0 },
            TrackFrameContinuityThresholds.Default));
    }

    private static ExportTrackFrame BuildFrame(
        double distance,
        Vector3d tangent,
        Vector3d normal,
        Vector3d binormal)
    {
        return new ExportTrackFrame(
            distance,
            new Vector3d(distance, 0.0, 0.0),
            tangent,
            normal,
            binormal);
    }

    private static ExportTrackFrame[] BuildSmoothYawRollFrames(
        int sampleCount,
        double totalDistance,
        double totalYawRadians,
        double totalRollRadians)
    {
        var frames = new ExportTrackFrame[sampleCount];
        double radius = totalDistance / totalYawRadians;

        for (int i = 0; i < sampleCount; i++)
        {
            double t = (double)i / (sampleCount - 1);
            double yaw = totalYawRadians * t;
            double roll = totalRollRadians * t;
            double distance = totalDistance * t;
            Vector3d tangent = Normalize(new Vector3d(SystemMath.Cos(yaw), 0.0, SystemMath.Sin(yaw)));
            Vector3d position = new Vector3d(
                radius * SystemMath.Sin(yaw),
                0.0,
                radius * (1.0 - SystemMath.Cos(yaw)));
            Vector3d baseNormal = Vector3d.UnitY;
            Vector3d baseBinormal = Normalize(Vector3d.Cross(tangent, baseNormal));
            Vector3d normal = RotateAroundAxis(baseNormal, tangent, roll);
            Vector3d binormal = RotateAroundAxis(baseBinormal, tangent, roll);

            frames[i] = new ExportTrackFrame(distance, position, tangent, normal, binormal);
        }

        return frames;
    }

    private static void AssertIssue(
        TrackFrameContinuityReport report,
        TrackFrameContinuityIssueKind kind,
        int startSampleIndex,
        double expectedDegrees)
    {
        for (int i = 0; i < report.Issues.Count; i++)
        {
            TrackFrameContinuityIssue issue = report.Issues[i];
            if (issue.Kind == kind && issue.Interval.StartSampleIndex == startSampleIndex)
            {
                AssertNear(expectedDegrees, issue.ActualAngleDegrees);
                return;
            }
        }

        throw new Xunit.Sdk.XunitException(
            $"Expected {kind} issue at sample {startSampleIndex}. Report: {report.ToDiagnosticString()}");
    }

    private static Vector3d RotateAroundAxis(Vector3d vector, Vector3d axis, double angle)
    {
        Vector3d normalizedAxis = Normalize(axis);
        double cos = SystemMath.Cos(angle);
        double sin = SystemMath.Sin(angle);

        Vector3d scaledVector = vector * cos;
        Vector3d crossTerm = Vector3d.Cross(normalizedAxis, vector) * sin;
        Vector3d projectionTerm = normalizedAxis * (Vector3d.Dot(normalizedAxis, vector) * (1.0 - cos));
        return scaledVector + crossTerm + projectionTerm;
    }

    private static Vector3d Normalize(Vector3d vector)
    {
        double length = vector.Length;
        if (length <= 1e-9)
        {
            throw new InvalidOperationException("Test fixture vector cannot be normalized.");
        }

        return vector / length;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * (SystemMath.PI / 180.0);
    }

    private static void AssertNear(double expected, double actual)
    {
        Assert.InRange(SystemMath.Abs(expected - actual), 0.0, Tolerance);
    }
}
