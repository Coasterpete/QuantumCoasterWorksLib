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
