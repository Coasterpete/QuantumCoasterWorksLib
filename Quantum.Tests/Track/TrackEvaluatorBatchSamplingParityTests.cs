using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using Xunit;
using ExportTrackFrame = Quantum.Track.TrackFrame;
using SplineTrackFrame = Quantum.Splines.TrackFrame;

namespace Quantum.Tests;

public sealed class TrackEvaluatorBatchSamplingParityTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void TrackEvaluator_EvaluateAtDistances_EqualsScalarEvaluateAtDistance_ForMixedDistances()
    {
        var evaluator = new TrackEvaluator();
        TrackDocument document = CreateDocument();
        double[] distances = { -3.0, 0.0, 2.0, 8.0, 9.5, 99.0 };

        TrackEvaluationPoint[] points = evaluator.EvaluateAtDistances(document, distances);

        Assert.Equal(distances.Length, points.Length);

        for (int i = 0; i < distances.Length; i++)
        {
            TrackEvaluationPoint expected = evaluator.EvaluateAtDistance(document, distances[i]);
            AssertTrackEvaluationPointParity(expected, points[i]);
        }
    }

    [Fact]
    public void TrackEvaluator_EvaluateFramesAtDistances_DocumentOverload_EqualsScalarEvaluateFrameAtDistance_ForMixedDistances()
    {
        var evaluator = new TrackEvaluator();
        TrackDocument document = CreateDocument();
        double[] distances = { -1.0, 0.0, 2.0, 8.0, 10.0, 99.0 };

        SplineTrackFrame[] frames = evaluator.EvaluateFramesAtDistances(document, distances);

        Assert.Equal(distances.Length, frames.Length);

        for (int i = 0; i < distances.Length; i++)
        {
            SplineTrackFrame expected = evaluator.EvaluateFrameAtDistance(document, distances[i]);
            AssertSplineFrameNear(expected, frames[i]);
        }
    }

    [Fact]
    public void TrackEvaluator_EvaluateFramesAtDistances_BoundOverload_EqualsScalarEvaluateFrameAtDistance_ForMixedDistances()
    {
        TrackDocument document = CreateDocument();
        var evaluator = new TrackEvaluator(document);
        double[] distances = { -2.0, 0.0, 3.0, 8.0, 11.0, 99.0 };

        ExportTrackFrame[] frames = evaluator.EvaluateFramesAtDistances(distances);

        Assert.Equal(distances.Length, frames.Length);

        for (int i = 0; i < distances.Length; i++)
        {
            ExportTrackFrame expected = evaluator.EvaluateFrameAtDistance(distances[i]);
            AssertExportFrameNear(expected, frames[i]);
        }
    }

    [Fact]
    public void TrackEvaluator_EvaluateFramesAtDistances_DocumentOverload_DuplicateSegmentReferences_MatchesScalarBehavior()
    {
        TrackSegment repeated = new StraightSegment(length: 5.0, id: "shared");
        TrackSegment middle = new StraightSegment(length: 3.0, id: "middle");
        var document = new TrackDocument(new[] { repeated, middle, repeated });
        var evaluator = new TrackEvaluator();
        double[] distances = { 0.0, 4.5, 5.0, 7.5, 9.0, 12.5 };

        SplineTrackFrame[] frames = evaluator.EvaluateFramesAtDistances(document, distances);

        Assert.Equal(distances.Length, frames.Length);

        for (int i = 0; i < distances.Length; i++)
        {
            SplineTrackFrame expected = evaluator.EvaluateFrameAtDistance(document, distances[i]);
            AssertSplineFrameNear(expected, frames[i]);
        }
    }

    [Fact]
    public void TrackEvaluator_EvaluateAtDistances_DuplicateDistances_PreservesDuplicateOutputsAndOrder()
    {
        var evaluator = new TrackEvaluator();
        TrackDocument document = CreateDocument();
        double[] distances = { 3.5, 3.5, 3.5, 9.25, 9.25 };

        TrackEvaluationPoint[] points = evaluator.EvaluateAtDistances(document, distances);

        Assert.Equal(distances.Length, points.Length);

        for (int i = 0; i < distances.Length; i++)
        {
            TrackEvaluationPoint expected = evaluator.EvaluateAtDistance(document, distances[i]);
            AssertTrackEvaluationPointParity(expected, points[i]);
        }

        AssertTrackEvaluationPointParity(points[0], points[1]);
        AssertTrackEvaluationPointParity(points[1], points[2]);
        AssertTrackEvaluationPointParity(points[3], points[4]);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void TrackEvaluator_EvaluateAtDistances_NonFiniteDistance_ThrowsScalarEquivalentArgumentOutOfRangeException(double distance)
    {
        var evaluator = new TrackEvaluator();
        TrackDocument document = CreateDocument();

        ArgumentOutOfRangeException scalar = Assert.Throws<ArgumentOutOfRangeException>(
            () => evaluator.EvaluateAtDistance(document, distance));

        ArgumentOutOfRangeException batch = Assert.Throws<ArgumentOutOfRangeException>(
            () => evaluator.EvaluateAtDistances(document, new[] { distance }));

        Assert.Equal(scalar.ParamName, batch.ParamName);
        Assert.Contains("Distance must be finite.", batch.Message);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void TrackEvaluator_EvaluateFramesAtDistances_DocumentOverload_NonFiniteDistance_ThrowsScalarEquivalentArgumentOutOfRangeException(double distance)
    {
        var evaluator = new TrackEvaluator();
        TrackDocument document = CreateDocument();

        ArgumentOutOfRangeException scalar = Assert.Throws<ArgumentOutOfRangeException>(
            () => evaluator.EvaluateFrameAtDistance(document, distance));

        ArgumentOutOfRangeException batch = Assert.Throws<ArgumentOutOfRangeException>(
            () => evaluator.EvaluateFramesAtDistances(document, new[] { distance }));

        Assert.Equal(scalar.ParamName, batch.ParamName);
        Assert.Contains("Distance must be finite.", batch.Message);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void TrackEvaluator_EvaluateFramesAtDistances_BoundOverload_NonFiniteDistance_ThrowsScalarEquivalentArgumentOutOfRangeException(double distance)
    {
        TrackDocument document = CreateDocument();
        var evaluator = new TrackEvaluator(document);

        ArgumentOutOfRangeException scalar = Assert.Throws<ArgumentOutOfRangeException>(
            () => evaluator.EvaluateFrameAtDistance(distance));

        ArgumentOutOfRangeException batch = Assert.Throws<ArgumentOutOfRangeException>(
            () => evaluator.EvaluateFramesAtDistances(new[] { distance }));

        Assert.Equal(scalar.ParamName, batch.ParamName);
        Assert.Contains("Distance must be finite.", batch.Message);
    }

    [Fact]
    public void TrackEvaluator_EvaluateAtDistances_EmptyDocumentWithNonEmptyDistances_ThrowsScalarEquivalentBehavior()
    {
        var evaluator = new TrackEvaluator();
        var document = new TrackDocument();
        double[] distances = { 0.0, 1.0 };

        ArgumentOutOfRangeException scalar = Assert.Throws<ArgumentOutOfRangeException>(
            () => evaluator.EvaluateAtDistance(document, distances[0]));

        ArgumentOutOfRangeException batch = Assert.Throws<ArgumentOutOfRangeException>(
            () => evaluator.EvaluateAtDistances(document, distances));

        Assert.Equal(scalar.ParamName, batch.ParamName);
        Assert.Contains("empty track document", batch.Message);
    }

    [Fact]
    public void TrackEvaluator_EvaluateFramesAtDistances_DocumentOverload_EmptyDocumentWithNonEmptyDistances_ThrowsScalarEquivalentBehavior()
    {
        var evaluator = new TrackEvaluator();
        var document = new TrackDocument();
        double[] distances = { 0.0, 1.0 };

        ArgumentOutOfRangeException scalar = Assert.Throws<ArgumentOutOfRangeException>(
            () => evaluator.EvaluateFrameAtDistance(document, distances[0]));

        ArgumentOutOfRangeException batch = Assert.Throws<ArgumentOutOfRangeException>(
            () => evaluator.EvaluateFramesAtDistances(document, distances));

        Assert.Equal(scalar.ParamName, batch.ParamName);
        Assert.Contains("empty track document", batch.Message);
    }

    [Fact]
    public void TrackEvaluator_EvaluateFramesAtDistances_BoundOverload_EmptyDocumentWithNonEmptyDistances_ThrowsScalarEquivalentBehavior()
    {
        var document = new TrackDocument();
        var evaluator = new TrackEvaluator(document);
        double[] distances = { 0.0, 1.0 };

        ArgumentOutOfRangeException scalar = Assert.Throws<ArgumentOutOfRangeException>(
            () => evaluator.EvaluateFrameAtDistance(distances[0]));

        ArgumentOutOfRangeException batch = Assert.Throws<ArgumentOutOfRangeException>(
            () => evaluator.EvaluateFramesAtDistances(distances));

        Assert.Equal(scalar.ParamName, batch.ParamName);
        Assert.Contains("empty track document", batch.Message);
    }

    private static TrackDocument CreateDocument()
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
                new Vector3d(100.0, 5.0, -2.0),
                new Vector3d(104.0, 7.0, -2.0)));

        return new TrackDocument(new[] { first, second });
    }

    private static void AssertTrackEvaluationPointParity(TrackEvaluationPoint expected, TrackEvaluationPoint actual)
    {
        Assert.Same(expected.Segment, actual.Segment);
        AssertDoubleNear(expected.LocalT, actual.LocalT);
    }

    private static void AssertSplineFrameNear(SplineTrackFrame expected, SplineTrackFrame actual)
    {
        AssertDoubleNear(expected.S, actual.S);
        AssertVectorNear(expected.Position, actual.Position);
        AssertVectorNear(expected.Tangent, actual.Tangent);
        AssertVectorNear(expected.Normal, actual.Normal);
        AssertVectorNear(expected.Binormal, actual.Binormal);
    }

    private static void AssertExportFrameNear(ExportTrackFrame expected, ExportTrackFrame actual)
    {
        AssertDoubleNear(expected.Distance, actual.Distance);
        AssertVectorNear(expected.Position, actual.Position);
        AssertVectorNear(expected.Tangent, actual.Tangent);
        AssertVectorNear(expected.Normal, actual.Normal);
        AssertVectorNear(expected.Binormal, actual.Binormal);
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual)
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
