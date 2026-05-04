using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using Xunit;

namespace Quantum.Tests;

public sealed class TrackEvaluatorFrameTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void TrackEvaluator_EvaluateFrame_WithSpline_ReturnsOrthonormalFrame()
    {
        var evaluator = new TrackEvaluator();
        IParamCurve spline = new CubicBezierCurve(
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(4.0, 2.0, 0.0),
            new Vector3d(7.0, 6.0, 2.0),
            new Vector3d(10.0, 8.0, 3.0));
        var document = new TrackDocument(new TrackSegment[]
        {
            new CurvedSegment(length: 12.0, spline: spline)
        });

        TrackFrame frame = evaluator.EvaluateFrame(document, new TrackPosition(segmentIndex: 0, localT: 0.4));

        AssertUnit(frame.Tangent);
        AssertUnit(frame.Normal);
        AssertUnit(frame.Binormal);

        Assert.InRange(System.Math.Abs(Vector3d.Dot(frame.Tangent, frame.Normal)), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(Vector3d.Dot(frame.Tangent, frame.Binormal)), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(Vector3d.Dot(frame.Normal, frame.Binormal)), 0.0, Tolerance);

        Vector3d cross = Vector3d.Cross(frame.Tangent, frame.Normal);
        AssertVectorNear(cross, frame.Binormal);
    }

    [Fact]
    public void TrackEvaluator_EvaluateFrame_WithSpline_TangentMatchesCurveDirection()
    {
        var evaluator = new TrackEvaluator();
        IParamCurve spline = new LineCurve(
            new Vector3d(2.0, 1.0, -4.0),
            new Vector3d(2.0, 1.0, 6.0));
        var document = new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(length: 10.0, spline: spline)
        });

        TrackFrame frame = evaluator.EvaluateFrame(document, new TrackPosition(segmentIndex: 0, localT: 0.25));

        AssertVectorNear(frame.Tangent, Vector3d.UnitZ);
    }

    [Fact]
    public void TrackEvaluator_EvaluateFrame_WithCurvedSpline_FrameChangesAlongCurve()
    {
        var evaluator = new TrackEvaluator();
        IParamCurve spline = new CubicBezierCurve(
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(5.0, 0.0, 0.0),
            new Vector3d(5.0, 5.0, 0.0),
            new Vector3d(0.0, 5.0, 0.0));
        var document = new TrackDocument(new TrackSegment[]
        {
            new CurvedSegment(length: 10.0, spline: spline)
        });

        TrackFrame start = evaluator.EvaluateFrame(document, new TrackPosition(segmentIndex: 0, localT: 0.1));
        TrackFrame end = evaluator.EvaluateFrame(document, new TrackPosition(segmentIndex: 0, localT: 0.9));

        double tangentDot = Vector3d.Dot(start.Tangent, end.Tangent);

        Assert.True(tangentDot < -0.5);
        Assert.True(end.Position.Y > start.Position.Y);
    }

    [Fact]
    public void TrackEvaluator_EvaluateFrame_WithoutSpline_UsesFallbackAxesAndPosition()
    {
        var evaluator = new TrackEvaluator();
        var document = new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(length: 10.0),
            new CurvedSegment(length: 5.0)
        });

        TrackFrame frame = evaluator.EvaluateFrame(document, new TrackPosition(segmentIndex: 1, localT: 0.4));

        AssertVectorNear(frame.Tangent, Vector3d.UnitX);
        AssertVectorNear(frame.Normal, Vector3d.UnitY);
        AssertVectorNear(frame.Binormal, Vector3d.UnitZ);

        AssertDoubleNear(12.0, frame.Position.X);
        AssertDoubleNear(0.0, frame.Position.Y);
        AssertDoubleNear(0.0, frame.Position.Z);
    }

    [Fact]
    public void TrackEvaluator_EvaluateFrameAtDistance_ConsistentWithEvaluateAtAndEvaluateAtDistance()
    {
        var evaluator = new TrackEvaluator();
        var first = new StraightSegment(length: 10.0, id: "s-01");
        var second = new CurvedSegment(length: 5.0, id: "c-01");
        var document = new TrackDocument(new TrackSegment[] { first, second });
        double distance = 12.0;

        TrackEvaluationPoint fromDistance = evaluator.EvaluateAtDistance(document, distance);
        TrackEvaluationPoint fromPosition = evaluator.EvaluateAt(document, new TrackPosition(segmentIndex: 1, localT: 0.4));
        TrackFrame frameFromDistance = evaluator.EvaluateFrameAtDistance(document, distance);
        TrackFrame frameFromPosition = evaluator.EvaluateFrame(document, new TrackPosition(segmentIndex: 1, localT: fromDistance.LocalT));

        Assert.Same(fromPosition.Segment, fromDistance.Segment);
        AssertDoubleNear(fromPosition.LocalT, fromDistance.LocalT);
        AssertDoubleNear(frameFromPosition.S, frameFromDistance.S);
        AssertVectorNear(frameFromPosition.Position, frameFromDistance.Position);
        AssertVectorNear(frameFromPosition.Tangent, frameFromDistance.Tangent);
        AssertVectorNear(frameFromPosition.Normal, frameFromDistance.Normal);
        AssertVectorNear(frameFromPosition.Binormal, frameFromDistance.Binormal);
    }

    [Fact]
    public void TrackEvaluator_EvaluateFrameAtDistance_WithCurvedSpline_FrameChangesWithDistance()
    {
        var evaluator = new TrackEvaluator();
        IParamCurve spline = new CubicBezierCurve(
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(5.0, 0.0, 0.0),
            new Vector3d(5.0, 5.0, 0.0),
            new Vector3d(0.0, 5.0, 0.0));
        var document = new TrackDocument(new TrackSegment[]
        {
            new CurvedSegment(length: 10.0, spline: spline)
        });

        TrackFrame start = evaluator.EvaluateFrameAtDistance(document, 1.0);
        TrackFrame end = evaluator.EvaluateFrameAtDistance(document, 9.0);

        double tangentDot = Vector3d.Dot(start.Tangent, end.Tangent);

        Assert.True(tangentDot < -0.5);
        Assert.True(end.Position.Y > start.Position.Y);
    }

    [Fact]
    public void TrackEvaluator_EvaluateFrameAtDistance_EmptyDocument_ThrowsArgumentOutOfRangeException()
    {
        var evaluator = new TrackEvaluator();
        var document = new TrackDocument();

        Assert.Throws<ArgumentOutOfRangeException>(() => evaluator.EvaluateFrameAtDistance(document, 0.0));
    }

    private static void AssertUnit(Vector3d vector)
    {
        Assert.InRange(System.Math.Abs(vector.Length - 1.0), 0.0, Tolerance);
    }

    private static void AssertVectorNear(Vector3d actual, Vector3d expected)
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
