using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using Xunit;
using ExportTrackFrame = Quantum.Track.TrackFrame;
using SplineTrackFrame = Quantum.Splines.TrackFrame;

namespace Quantum.Tests;

public sealed class TrackEvaluatorDistanceSemanticsContractTests
{
    private const double Tolerance = 1e-6;

    [Theory]
    [InlineData(2.0, 0, 0.25, 2.0)]
    [InlineData(8.0, 1, 0.0, 0.0)]
    [InlineData(9.0, 1, 0.25, 1.0)]
    [InlineData(99.0, 1, 1.0, 4.0)]
    public void TrackEvaluator_EvaluateAtDistance_UsesGlobalDistanceToResolveSegmentAndLocalT(
        double globalDistance,
        int expectedSegmentIndex,
        double expectedLocalT,
        double expectedLocalDistance)
    {
        var evaluator = new TrackEvaluator();
        (TrackDocument document, TrackSegment first, TrackSegment second) = CreateDistanceSemanticsDocument();

        TrackEvaluationPoint point = evaluator.EvaluateAtDistance(document, globalDistance);

        TrackSegment expectedSegment = expectedSegmentIndex == 0 ? first : second;
        Assert.Same(expectedSegment, point.Segment);
        AssertDoubleNear(expectedLocalT, point.LocalT);
        AssertDoubleNear(expectedLocalDistance, point.LocalT * point.Segment.Length);
    }

    [Theory]
    [InlineData(2.0, 0, 0.25, 2.0)]
    [InlineData(8.0, 1, 0.0, 0.0)]
    [InlineData(9.0, 1, 0.25, 1.0)]
    [InlineData(99.0, 1, 1.0, 4.0)]
    public void TrackEvaluator_EvaluateFrameAtDistance_DocumentOverload_UsesSegmentLocalFrameDistanceAndPosition(
        double globalDistance,
        int expectedSegmentIndex,
        double expectedLocalT,
        double expectedLocalDistance)
    {
        var evaluator = new TrackEvaluator();
        (TrackDocument document, TrackSegment first, TrackSegment second) = CreateDistanceSemanticsDocument();

        SplineTrackFrame frame = evaluator.EvaluateFrameAtDistance(document, globalDistance);

        TrackSegment expectedSegment = expectedSegmentIndex == 0 ? first : second;
        Vector3d expectedPosition = expectedSegment.Spline!.Evaluate(expectedLocalT);

        AssertDoubleNear(expectedLocalDistance, frame.S);
        AssertVectorNear(frame.Position, expectedPosition);
    }

    [Theory]
    [InlineData(2.0, 0, 0.25, 2.0)]
    [InlineData(8.0, 1, 0.0, 0.0)]
    [InlineData(9.0, 1, 0.25, 1.0)]
    [InlineData(99.0, 1, 1.0, 4.0)]
    public void TrackEvaluator_EvaluateFrameAtDistance_BoundOverload_UsesSegmentLocalFrameDistanceAndPosition(
        double globalDistance,
        int expectedSegmentIndex,
        double expectedLocalT,
        double expectedLocalDistance)
    {
        (TrackDocument document, TrackSegment first, TrackSegment second) = CreateDistanceSemanticsDocument();
        var boundEvaluator = new TrackEvaluator(document);

        ExportTrackFrame frame = boundEvaluator.EvaluateFrameAtDistance(globalDistance);

        TrackSegment expectedSegment = expectedSegmentIndex == 0 ? first : second;
        Vector3d expectedPosition = expectedSegment.Spline!.Evaluate(expectedLocalT);

        AssertDoubleNear(expectedLocalDistance, frame.Distance);
        AssertVectorNear(frame.Position, expectedPosition);
    }

    private static (TrackDocument Document, TrackSegment First, TrackSegment Second) CreateDistanceSemanticsDocument()
    {
        TrackSegment first = new StraightSegment(
            length: 8.0,
            id: "first",
            spline: new LineCurve(
                new Vector3d(0.0, 0.0, 0.0),
                new Vector3d(8.0, 0.0, 0.0)));

        TrackSegment second = new CurvedSegment(
            length: 4.0,
            id: "second",
            spline: new LineCurve(
                new Vector3d(100.0, 10.0, -5.0),
                new Vector3d(104.0, 10.0, -5.0)));

        var document = new TrackDocument(new[] { first, second });
        return (document, first, second);
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
