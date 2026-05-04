using Quantum.Track;
using Xunit;

namespace Quantum.Tests;

public sealed class TrackFoundationTests
{
    [Fact]
    public void TrackDocument_CanContainMixedSegmentTypes()
    {
        var document = new TrackDocument();

        document.Segments.Add(new StraightSegment(length: 12.0, id: "straight-01"));
        document.Segments.Add(new CurvedSegment(length: 8.5, id: "curve-01"));

        Assert.Equal(2, document.Segments.Count);
        Assert.IsType<StraightSegment>(document.Segments[0]);
        Assert.IsType<CurvedSegment>(document.Segments[1]);
    }

    [Fact]
    public void TrackDocument_PreservesSegmentProperties()
    {
        TrackSegment straight = new StraightSegment(length: 10.0, id: "s-10", forceSegmentReference: "force-a");
        TrackSegment curve = new CurvedSegment(length: 24.5);
        var document = new TrackDocument(new[] { straight, curve });

        Assert.Equal(10.0, document.Segments[0].Length);
        Assert.Equal("s-10", document.Segments[0].Id);
        Assert.Equal("force-a", document.Segments[0].ForceSegmentReference);

        Assert.Equal(24.5, document.Segments[1].Length);
        Assert.Null(document.Segments[1].Id);
        Assert.Null(document.Segments[1].ForceSegmentReference);
    }

    [Fact]
    public void TrackEvaluator_Evaluate_EmptyDocument_ReturnsSuccessWithZeroSegments()
    {
        var evaluator = new TrackEvaluator();
        var document = new TrackDocument();

        TrackEvaluationResult result = evaluator.Evaluate(document);

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.Equal(0, result.EvaluatedSegmentCount);
    }

    [Fact]
    public void TrackEvaluator_Evaluate_DocumentWithSegments_ReturnsExpectedSegmentCount()
    {
        var evaluator = new TrackEvaluator();
        var document = new TrackDocument();
        document.Segments.Add(new StraightSegment(length: 5.0));
        document.Segments.Add(new CurvedSegment(length: 7.0));
        document.Segments.Add(new StraightSegment(length: 3.0));

        TrackEvaluationResult result = evaluator.Evaluate(document);

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.Equal(3, result.EvaluatedSegmentCount);
    }

    [Fact]
    public void TrackEvaluator_Evaluate_NullDocument_ThrowsArgumentNullException()
    {
        var evaluator = new TrackEvaluator();

        Assert.Throws<ArgumentNullException>(() => evaluator.Evaluate(null!));
    }

    [Fact]
    public void TrackEvaluator_Evaluate_DocumentWithNullSegment_ReturnsFailure()
    {
        var evaluator = new TrackEvaluator();
        var document = new TrackDocument();
        document.Segments.Add(new StraightSegment(length: 1.0));
        document.Segments.Add(null!);

        TrackEvaluationResult result = evaluator.Evaluate(document);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Equal(1, result.EvaluatedSegmentCount);
    }
}
