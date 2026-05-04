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
    public void TrackEvaluator_CanBeConstructed_AndEvaluateCalled()
    {
        var evaluator = new TrackEvaluator();

        var exception = Record.Exception(() => evaluator.Evaluate());

        Assert.Null(exception);
    }
}
