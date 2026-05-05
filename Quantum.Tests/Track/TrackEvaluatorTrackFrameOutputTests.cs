using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using Xunit;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class TrackEvaluatorTrackFrameOutputTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void TrackEvaluator_EvaluateFrameAtDistance_ReturnsValidFrame()
    {
        IParamCurve spline = new CubicBezierCurve(
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(3.0, 1.0, 0.5),
            new Vector3d(7.0, 4.0, 2.0),
            new Vector3d(10.0, 6.0, 4.0));
        var document = new TrackDocument(new TrackSegment[]
        {
            new CurvedSegment(length: 10.0, spline: spline, rollRadians: 0.4)
        });
        var evaluator = new TrackEvaluator(document);

        ExportTrackFrame frame = evaluator.EvaluateFrameAtDistance(4.2);

        AssertFiniteVector(frame.Position);
        AssertFiniteVector(frame.Tangent);
        AssertFiniteVector(frame.Normal);
        AssertFiniteVector(frame.Binormal);
        Assert.True(frame.Tangent.Length > 0.0);
        Assert.True(frame.Normal.Length > 0.0);
        Assert.True(frame.Binormal.Length > 0.0);
    }

    [Fact]
    public void TrackEvaluator_EvaluateFrameAtDistance_ReturnsOrthonormalFrame()
    {
        IParamCurve spline = new CubicBezierCurve(
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(4.0, 2.0, 0.0),
            new Vector3d(7.0, 6.0, 2.0),
            new Vector3d(10.0, 8.0, 3.0));
        var document = new TrackDocument(new TrackSegment[]
        {
            new CurvedSegment(length: 12.0, spline: spline, rollRadians: 0.7)
        });
        var evaluator = new TrackEvaluator(document);

        ExportTrackFrame frame = evaluator.EvaluateFrameAtDistance(5.5);

        AssertOrthonormal(frame);
    }

    [Fact]
    public void TrackEvaluator_EvaluateFrameAtDistance_PositionMatchesEvaluateAtDistance()
    {
        IParamCurve spline = new LineCurve(
            new Vector3d(2.0, -1.0, 5.0),
            new Vector3d(12.0, 3.0, 9.0));
        var document = new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(length: 10.0, spline: spline)
        });
        var evaluator = new TrackEvaluator(document);
        double distance = 3.75;

        TrackEvaluationPoint point = evaluator.EvaluateAtDistance(document, distance);
        ExportTrackFrame frame = evaluator.EvaluateFrameAtDistance(distance);
        Vector3d expectedPosition = spline.Evaluate(point.LocalT);

        AssertVectorNear(frame.Position, expectedPosition);
    }

    [Fact]
    public void TrackEvaluator_EvaluateFrameAtDistance_FrameContainsNoNaNsOrInfinities()
    {
        IParamCurve spline = new CubicBezierCurve(
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(2.0, 5.0, 1.0),
            new Vector3d(6.0, 7.0, 3.0),
            new Vector3d(10.0, 9.0, 4.0));
        var document = new TrackDocument(new TrackSegment[]
        {
            new CurvedSegment(length: 10.0, spline: spline),
            new StraightSegment(length: 5.0, rollRadians: 0.25)
        });
        var evaluator = new TrackEvaluator(document);

        ExportTrackFrame frameA = evaluator.EvaluateFrameAtDistance(2.0);
        ExportTrackFrame frameB = evaluator.EvaluateFrameAtDistance(12.0);

        AssertFiniteVector(frameA.Position);
        AssertFiniteVector(frameA.Tangent);
        AssertFiniteVector(frameA.Normal);
        AssertFiniteVector(frameA.Binormal);
        AssertFiniteVector(frameB.Position);
        AssertFiniteVector(frameB.Tangent);
        AssertFiniteVector(frameB.Normal);
        AssertFiniteVector(frameB.Binormal);
    }

    private static void AssertOrthonormal(ExportTrackFrame frame)
    {
        Assert.InRange(System.Math.Abs(frame.Tangent.Length - 1.0), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(frame.Normal.Length - 1.0), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(frame.Binormal.Length - 1.0), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(Vector3d.Dot(frame.Tangent, frame.Normal)), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(Vector3d.Dot(frame.Tangent, frame.Binormal)), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(Vector3d.Dot(frame.Normal, frame.Binormal)), 0.0, Tolerance);

        Vector3d cross = Vector3d.Cross(frame.Tangent, frame.Normal);
        AssertVectorNear(cross, frame.Binormal);
    }

    private static void AssertFiniteVector(Vector3d vector)
    {
        Assert.False(double.IsNaN(vector.X) || double.IsNaN(vector.Y) || double.IsNaN(vector.Z));
        Assert.False(double.IsInfinity(vector.X) || double.IsInfinity(vector.Y) || double.IsInfinity(vector.Z));
    }

    private static void AssertVectorNear(Vector3d actual, Vector3d expected)
    {
        Assert.InRange(System.Math.Abs(expected.X - actual.X), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(expected.Y - actual.Y), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(expected.Z - actual.Z), 0.0, Tolerance);
    }
}
