using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using Xunit;

namespace Quantum.Tests;

public sealed class TrackEvaluatorSplineTransformTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void TrackEvaluator_EvaluateTransform_WithStraightSpline_ProducesLinearMotion()
    {
        var evaluator = new TrackEvaluator();
        IParamCurve spline = new LineCurve(
            new Vector3d(2.0, 1.0, -1.0),
            new Vector3d(8.0, 1.0, -1.0));
        var document = new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(length: 6.0, spline: spline)
        });

        Transform3d first = evaluator.EvaluateTransform(document, new TrackPosition(segmentIndex: 0, localT: 0.25));
        Transform3d second = evaluator.EvaluateTransform(document, new TrackPosition(segmentIndex: 0, localT: 0.75));

        AssertDoubleNear(3.5, first.Position.X);
        AssertDoubleNear(1.0, first.Position.Y);
        AssertDoubleNear(-1.0, first.Position.Z);

        AssertDoubleNear(6.5, second.Position.X);
        AssertDoubleNear(1.0, second.Position.Y);
        AssertDoubleNear(-1.0, second.Position.Z);
    }

    [Fact]
    public void TrackEvaluator_EvaluateTransform_WithSpline_AlignsForwardAxisToTangent()
    {
        var evaluator = new TrackEvaluator();
        IParamCurve spline = new LineCurve(
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(0.0, 0.0, 10.0));
        var document = new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(length: 10.0, spline: spline)
        });

        Transform3d transform = evaluator.EvaluateTransform(document, new TrackPosition(segmentIndex: 0, localT: 0.4));
        Vector3d forward = transform.TransformDirection(Vector3d.UnitX);

        AssertVectorNear(forward, Vector3d.UnitZ);
    }

    [Fact]
    public void TrackEvaluator_EvaluateTransform_WithSpline_ChangesWithLocalT()
    {
        var evaluator = new TrackEvaluator();
        IParamCurve spline = new LineCurve(
            new Vector3d(1.0, 2.0, 3.0),
            new Vector3d(11.0, 2.0, 3.0));
        var document = new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(length: 10.0, spline: spline)
        });

        Transform3d start = evaluator.EvaluateTransform(document, new TrackPosition(segmentIndex: 0, localT: 0.1));
        Transform3d end = evaluator.EvaluateTransform(document, new TrackPosition(segmentIndex: 0, localT: 0.9));

        Assert.True(end.Position.X > start.Position.X);
        AssertDoubleNear(start.Position.Y, end.Position.Y);
        AssertDoubleNear(start.Position.Z, end.Position.Z);
    }

    [Fact]
    public void TrackEvaluator_EvaluateTransform_WithoutSpline_UsesFallbackBehavior()
    {
        var evaluator = new TrackEvaluator();
        var document = new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(length: 10.0),
            new CurvedSegment(length: 5.0)
        });

        Transform3d transform = evaluator.EvaluateTransform(document, new TrackPosition(segmentIndex: 1, localT: 0.4));

        AssertDoubleNear(12.0, transform.Position.X);
        AssertDoubleNear(0.0, transform.Position.Y);
        AssertDoubleNear(0.0, transform.Position.Z);
        AssertDoubleNear(1.0, transform.Rotation.M00);
        AssertDoubleNear(1.0, transform.Rotation.M11);
        AssertDoubleNear(1.0, transform.Rotation.M22);
    }

    [Fact]
    public void TrackEvaluator_EvaluateTransformAtDistance_ConsistentWithEvaluateAtAndEvaluateAtDistance()
    {
        var evaluator = new TrackEvaluator();
        var first = new StraightSegment(length: 10.0, id: "s-01");
        var second = new CurvedSegment(length: 5.0, id: "c-01");
        var document = new TrackDocument(new TrackSegment[] { first, second });
        double distance = 12.0;

        TrackEvaluationPoint fromDistance = evaluator.EvaluateAtDistance(document, distance);
        TrackEvaluationPoint fromPosition = evaluator.EvaluateAt(document, new TrackPosition(segmentIndex: 1, localT: 0.4));
        Transform3d transformFromDistance = evaluator.EvaluateTransformAtDistance(document, distance);
        Transform3d transformFromPosition = evaluator.EvaluateTransform(document, new TrackPosition(segmentIndex: 1, localT: fromDistance.LocalT));

        Assert.Same(fromPosition.Segment, fromDistance.Segment);
        AssertDoubleNear(fromPosition.LocalT, fromDistance.LocalT);
        AssertDoubleNear(transformFromPosition.Position.X, transformFromDistance.Position.X);
        AssertDoubleNear(transformFromPosition.Position.Y, transformFromDistance.Position.Y);
        AssertDoubleNear(transformFromPosition.Position.Z, transformFromDistance.Position.Z);
        AssertDoubleNear(transformFromPosition.Rotation.M00, transformFromDistance.Rotation.M00);
        AssertDoubleNear(transformFromPosition.Rotation.M01, transformFromDistance.Rotation.M01);
        AssertDoubleNear(transformFromPosition.Rotation.M02, transformFromDistance.Rotation.M02);
        AssertDoubleNear(transformFromPosition.Rotation.M10, transformFromDistance.Rotation.M10);
        AssertDoubleNear(transformFromPosition.Rotation.M11, transformFromDistance.Rotation.M11);
        AssertDoubleNear(transformFromPosition.Rotation.M12, transformFromDistance.Rotation.M12);
        AssertDoubleNear(transformFromPosition.Rotation.M20, transformFromDistance.Rotation.M20);
        AssertDoubleNear(transformFromPosition.Rotation.M21, transformFromDistance.Rotation.M21);
        AssertDoubleNear(transformFromPosition.Rotation.M22, transformFromDistance.Rotation.M22);
    }

    [Fact]
    public void TrackEvaluator_EvaluateTransformAtDistance_PositionIncreasesCorrectly()
    {
        var evaluator = new TrackEvaluator();
        var document = new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(length: 10.0),
            new CurvedSegment(length: 5.0)
        });

        Transform3d first = evaluator.EvaluateTransformAtDistance(document, 2.0);
        Transform3d second = evaluator.EvaluateTransformAtDistance(document, 12.0);

        Assert.True(second.Position.X > first.Position.X);
        AssertDoubleNear(2.0, first.Position.X);
        AssertDoubleNear(12.0, second.Position.X);
        AssertDoubleNear(0.0, second.Position.Y);
        AssertDoubleNear(0.0, second.Position.Z);
    }

    [Fact]
    public void TrackEvaluator_EvaluateTransformAtDistance_EmptyDocument_ThrowsArgumentOutOfRangeException()
    {
        var evaluator = new TrackEvaluator();
        var document = new TrackDocument();

        Assert.Throws<ArgumentOutOfRangeException>(() => evaluator.EvaluateTransformAtDistance(document, 0.0));
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
