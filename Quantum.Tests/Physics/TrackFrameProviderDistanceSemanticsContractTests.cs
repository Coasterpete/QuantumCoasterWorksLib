using Quantum.Math;
using Quantum.Physics;
using Quantum.Splines;
using Quantum.Track;
using Xunit;
using TrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class TrackFrameProviderDistanceSemanticsContractTests
{
    private const double Tolerance = 1e-6;

    [Theory]
    [InlineData(2.0, 0, 0.25)]
    [InlineData(8.0, 1, 0.0)]
    [InlineData(9.0, 1, 0.25)]
    [InlineData(99.0, 1, 1.0)]
    public void TrackFrameProviderFromDocument_TryGetFrameAtDistance_PreservesClampedGlobalStationDistance(
        double globalDistance,
        int expectedSegmentIndex,
        double expectedLocalT)
    {
        (TrackDocument document, TrackSegment first, TrackSegment second) = CreateDistanceSemanticsDocument();
        var provider = new TrackFrameProviderFromDocument(document);

        bool hasFrame = provider.TryGetFrameAtDistance(globalDistance, out TrackFrame frame);

        Assert.True(hasFrame);

        TrackSegment expectedSegment = expectedSegmentIndex == 0 ? first : second;
        Vector3d expectedPosition = expectedSegment.Spline!.Evaluate(expectedLocalT);

        double totalLength = new TrackEvaluator().GetTrackTotalLength(document);
        double expectedGlobalDistance = System.Math.Clamp(globalDistance, 0.0, totalLength);
        AssertDoubleNear(expectedGlobalDistance, frame.Distance);
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
