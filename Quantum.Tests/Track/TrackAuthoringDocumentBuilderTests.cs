using Quantum.Math;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class TrackAuthoringDocumentBuilderTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void Build_GeneratesOrderedSegmentsWithIdsLengthsKindsAndRolls()
    {
        var definition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            new StraightSectionDefinition("station", 6.0, 0.1),
            new ConstantCurvatureSectionDefinition("left", 8.0, 20.0, -0.25),
            new ConstantCurvatureSectionDefinition("right", 5.0, -15.0, 0.4)
        });

        TrackDocument document = TrackAuthoringDocumentBuilder.Build(definition);

        Assert.Collection(
            document.Segments,
            segment => AssertSegment<StraightSegment>(segment, "station", 6.0, 0.1),
            segment => AssertSegment<CurvedSegment>(segment, "left", 8.0, -0.25),
            segment => AssertSegment<CurvedSegment>(segment, "right", 5.0, 0.4));
        Assert.Equal(19.0, document.TotalLength, 10);
        Assert.Equal(3, document.Sections.Count);
    }

    [Theory]
    [InlineData(10.0, 10.0, 1.0)]
    [InlineData(-10.0, -10.0, -1.0)]
    public void Build_GeneratesPositiveAndNegativeSignedRadiusArcs(
        double radius,
        double expectedEndY,
        double expectedTangentY)
    {
        double length = System.Math.Abs(radius) * System.Math.PI * 0.5;
        var definition = new TrackAuthoringDefinition(new[]
        {
            new ConstantCurvatureSectionDefinition("quarter-turn", length, radius)
        });

        TrackDocument document = TrackAuthoringDocumentBuilder.Build(definition);
        TrackFrame endpoint = new TrackEvaluator(document).EvaluateFrameAtDistance(length);

        AssertVectorNear(new Vector3d(10.0, expectedEndY, 0.0), endpoint.Position);
        AssertVectorNear(new Vector3d(0.0, expectedTangentY, 0.0), endpoint.Tangent);
    }

    [Fact]
    public void Build_ProducesPointAndTangentContinuityAtSectionBoundaries()
    {
        TrackDocument document = TrackAuthoringDocumentBuilder.Build(CreateMixedDefinition());

        for (int i = 0; i < document.Segments.Count - 1; i++)
        {
            TrackSegment current = document.Segments[i];
            TrackSegment next = document.Segments[i + 1];

            Assert.NotNull(current.Spline);
            Assert.NotNull(next.Spline);
            AssertVectorNear(current.Spline!.Evaluate(1.0), next.Spline!.Evaluate(0.0));
            AssertVectorNear(
                current.Spline.Tangent(1.0).Normalized(),
                next.Spline.Tangent(0.0).Normalized());
        }
    }

    [Fact]
    public void RepeatedBuilds_ProduceEquivalentFrameSamples()
    {
        TrackAuthoringDefinition definition = CreateMixedDefinition();
        var firstEvaluator = new TrackEvaluator(TrackAuthoringDocumentBuilder.Build(definition));
        var secondEvaluator = new TrackEvaluator(TrackAuthoringDocumentBuilder.Build(definition));
        double[] distances = { 0.0, 2.5, 5.0, 7.0, 11.0, 14.0, 18.0 };

        foreach (double distance in distances)
        {
            TrackFrame first = firstEvaluator.EvaluateFrameAtDistance(distance);
            TrackFrame second = secondEvaluator.EvaluateFrameAtDistance(distance);

            Assert.Equal(first.Distance, second.Distance, 12);
            AssertVectorNear(first.Position, second.Position, 1e-10);
            AssertVectorNear(first.Tangent, second.Tangent, 1e-10);
            AssertVectorNear(first.Normal, second.Normal, 1e-10);
            AssertVectorNear(first.Binormal, second.Binormal, 1e-10);
        }
    }

    [Fact]
    public void GeneratedMixedDocument_WorksThroughTrackEvaluator()
    {
        TrackDocument document = TrackAuthoringDocumentBuilder.BuildDocument(CreateMixedDefinition());
        var evaluator = new TrackEvaluator(document);

        TrackEvaluationResult validation = evaluator.Evaluate(document);
        TrackFrame[] frames = evaluator.EvaluateFramesAtDistances(
            new[] { 0.0, 4.0, 5.0, 9.0, document.TotalLength });

        Assert.True(validation.Success, validation.Error);
        Assert.Equal(document.Segments.Count, validation.EvaluatedSegmentCount);
        Assert.Equal(5, frames.Length);
        Assert.All(frames, AssertFiniteOrthonormal);
        Assert.Equal(document.TotalLength, frames[^1].Distance, 10);
    }

    private static TrackAuthoringDefinition CreateMixedDefinition()
    {
        return new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            new StraightSectionDefinition("entry", 5.0, 0.0),
            new ConstantCurvatureSectionDefinition("left-arc", 6.0, 18.0, 0.15),
            new StraightSectionDefinition("middle", 3.0, 0.15),
            new ConstantCurvatureSectionDefinition("right-arc", 4.0, -14.0, -0.1)
        });
    }

    private static void AssertSegment<TSegment>(
        TrackSegment segment,
        string expectedId,
        double expectedLength,
        double expectedRoll)
        where TSegment : TrackSegment
    {
        Assert.IsType<TSegment>(segment);
        Assert.Equal(expectedId, segment.Id);
        Assert.Equal(expectedLength, segment.Length, 10);
        Assert.Equal(expectedRoll, segment.RollRadians, 10);
        Assert.NotNull(segment.Spline);
    }

    private static void AssertFiniteOrthonormal(TrackFrame frame)
    {
        AssertFinite(frame.Position);
        AssertVectorNear(new Vector3d(1.0, 1.0, 1.0), new Vector3d(
            frame.Tangent.Length,
            frame.Normal.Length,
            frame.Binormal.Length));
        AssertNear(0.0, Vector3d.Dot(frame.Tangent, frame.Normal));
        AssertNear(0.0, Vector3d.Dot(frame.Tangent, frame.Binormal));
        AssertNear(0.0, Vector3d.Dot(frame.Normal, frame.Binormal));
        AssertVectorNear(Vector3d.Cross(frame.Tangent, frame.Normal), frame.Binormal);
    }

    private static void AssertFinite(Vector3d value)
    {
        Assert.True(double.IsFinite(value.X));
        Assert.True(double.IsFinite(value.Y));
        Assert.True(double.IsFinite(value.Z));
    }

    private static void AssertVectorNear(
        Vector3d expected,
        Vector3d actual,
        double tolerance = Tolerance)
    {
        AssertNear(expected.X, actual.X, tolerance);
        AssertNear(expected.Y, actual.Y, tolerance);
        AssertNear(expected.Z, actual.Z, tolerance);
    }

    private static void AssertNear(
        double expected,
        double actual,
        double tolerance = Tolerance)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, tolerance);
    }
}
