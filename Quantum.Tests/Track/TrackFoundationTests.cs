using Quantum.Track;
using Xunit;

namespace Quantum.Tests;

public sealed class TrackFoundationTests
{
    [Fact]
    public void TrackDocument_CanContainSegments()
    {
        var document = new TrackDocument();

        document.Segments.Add(new TrackSegment());
        document.Segments.Add(new TrackSegment());

        Assert.Equal(2, document.Segments.Count);
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
        document.Segments.Add(new TrackSegment());
        document.Segments.Add(new TrackSegment());
        document.Segments.Add(new TrackSegment());

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
        document.Segments.Add(new TrackSegment());
        document.Segments.Add(null!);

        TrackEvaluationResult result = evaluator.Evaluate(document);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Equal(1, result.EvaluatedSegmentCount);
    }
}
