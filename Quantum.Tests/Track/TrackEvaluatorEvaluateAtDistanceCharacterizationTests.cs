using Quantum.Track;
using Xunit;

namespace Quantum.Tests;

public sealed class TrackEvaluatorEvaluateAtDistanceCharacterizationTests
{
    [Fact]
    public void TrackEvaluator_EvaluateAtDistance_DistanceBelowZero_ClampsToFirstSegmentAtZero()
    {
        var evaluator = new TrackEvaluator();
        var first = new StraightSegment(length: 10.0, id: "s-01");
        var second = new CurvedSegment(length: 5.0, id: "c-01");
        var document = new TrackDocument(new TrackSegment[] { first, second });

        TrackEvaluationPoint point = evaluator.EvaluateAtDistance(document, -3.5);

        Assert.Same(first, point.Segment);
        Assert.Equal(0.0, point.LocalT);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void TrackEvaluator_EvaluateAtDistance_NonFiniteDistance_ThrowsArgumentOutOfRangeExceptionWithDistanceParamName(double distance)
    {
        var evaluator = new TrackEvaluator();
        var document = new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(length: 2.0)
        });

        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => evaluator.EvaluateAtDistance(document, distance));

        Assert.Equal("distance", ex.ParamName);
    }

    [Fact]
    public void TrackEvaluator_EvaluateAtDistance_EmptyDocument_ThrowsArgumentOutOfRangeExceptionForDistance()
    {
        var evaluator = new TrackEvaluator();
        var document = new TrackDocument();

        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => evaluator.EvaluateAtDistance(document, 0.0));

        Assert.Equal("distance", ex.ParamName);
        Assert.Contains("empty track document", ex.Message);
    }

    [Fact]
    public void TrackEvaluator_EvaluateAtDistance_NullSegment_ThrowsInvalidOperationExceptionWithNullSegmentMessage()
    {
        var evaluator = new TrackEvaluator();
        var document = new TrackDocument();
        document.Segments.Add(null!);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => evaluator.EvaluateAtDistance(document, 0.0));

        Assert.Contains("null segment entry", ex.Message);
    }

    [Fact]
    public void TrackEvaluator_EvaluateAtDistance_LastSegmentWithNonPositiveLength_ReturnsLastSegmentAtZero()
    {
        var evaluator = new TrackEvaluator();
        var first = new StraightSegment(length: 10.0, id: "s-01");
        var last = new CurvedSegment(length: 0.0, id: "c-zero");
        var document = new TrackDocument(new TrackSegment[] { first, last });

        TrackEvaluationPoint point = evaluator.EvaluateAtDistance(document, 999.0);

        Assert.Same(last, point.Segment);
        Assert.Equal(0.0, point.LocalT);
    }
}
