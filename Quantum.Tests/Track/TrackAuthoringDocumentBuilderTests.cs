using Quantum.Math;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class TrackAuthoringDocumentBuilderTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void Compile_ReturnsOrderedResolvedSectionRanges()
    {
        var station = new StraightSectionDefinition("station", 6.0, 0.1);
        var left = new ConstantCurvatureSectionDefinition("left", 8.0, 20.0, -0.25);
        var right = new ConstantCurvatureSectionDefinition("right", 5.0, -15.0, 0.4);
        var definition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            station,
            left,
            right
        });

        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(definition);

        Assert.Same(definition, compilation.Definition);
        Assert.Collection(
            compilation.ResolvedSections,
            interval => AssertInterval(interval, station, 0.0, 6.0, includeEndDistance: false),
            interval => AssertInterval(interval, left, 6.0, 14.0, includeEndDistance: false),
            interval => AssertInterval(interval, right, 14.0, 19.0, includeEndDistance: true));
    }

    [Fact]
    public void Compile_PreservesSourceMetadataAndAlignsDocumentIndices()
    {
        var station = new StraightSectionDefinition("station", 6.0, 0.1);
        var left = new ConstantCurvatureSectionDefinition("left", 8.0, 20.0, -0.25);
        var right = new ConstantCurvatureSectionDefinition("right", 5.0, -15.0, 0.4);
        var definition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            station,
            left,
            right
        });

        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(definition);

        Assert.Equal(definition.Sections.Count, compilation.ResolvedSections.Count);
        Assert.Equal(definition.Sections.Count, compilation.Document.Segments.Count);
        Assert.Equal(definition.Sections.Count, compilation.Document.Sections.Count);

        for (int i = 0; i < definition.Sections.Count; i++)
        {
            GeometricSectionDefinition source = definition.Sections[i];
            ResolvedSectionInterval<GeometricSectionDefinition> interval = compilation.ResolvedSections[i];
            TrackSegment segment = compilation.Document.Segments[i];
            GeometricSection geometricSection = Assert.IsType<GeometricSection>(
                compilation.Document.Sections[i]);

            Assert.Same(source, interval.Section);
            Assert.Equal(source.Id, interval.Section.Id);
            Assert.Equal(source.GetType(), interval.Section.GetType());
            Assert.Equal(source.RollRadians, interval.Section.RollRadians, 12);
            Assert.Equal(source.Length, interval.Length, 12);
            Assert.Equal(source.Id, segment.Id);
            Assert.Equal(source.Length, segment.Length, 12);
            Assert.Equal(source.RollRadians, segment.RollRadians, 12);
            Assert.Equal(source.Length, geometricSection.Length, 12);
            Assert.True(geometricSection.Roll.HasValue);
            Assert.Equal(source.RollRadians, geometricSection.Roll.Value, 12);
        }

        Assert.Same(station, compilation.ResolvedSections[0].Section);
        Assert.Equal(left.Radius, Assert.IsType<ConstantCurvatureSectionDefinition>(
            compilation.ResolvedSections[1].Section).Radius, 12);
        Assert.Equal(right.Radius, Assert.IsType<ConstantCurvatureSectionDefinition>(
            compilation.ResolvedSections[2].Section).Radius, 12);
    }

    [Fact]
    public void Compile_ResolvedSectionLookupUsesFollowingSectionAtSharedBoundaries()
    {
        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(
            new TrackAuthoringDefinition(new GeometricSectionDefinition[]
            {
                new StraightSectionDefinition("station", 6.0),
                new ConstantCurvatureSectionDefinition("left", 8.0, 20.0),
                new StraightSectionDefinition("exit", 5.0)
            }));

        Assert.Same(
            compilation.Definition.Sections[1],
            SectionResolver.Lookup(compilation.ResolvedSections, 6.0).Section);
        Assert.Same(
            compilation.Definition.Sections[2],
            SectionResolver.Lookup(compilation.ResolvedSections, 14.0).Section);
        Assert.Same(
            compilation.Definition.Sections[2],
            SectionResolver.Lookup(compilation.ResolvedSections, 19.0).Section);
    }

    [Fact]
    public void Compile_TotalLengthAgreesWithFinalIntervalAndDocument()
    {
        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(
            new TrackAuthoringDefinition(new GeometricSectionDefinition[]
            {
                new StraightSectionDefinition("station", 6.0),
                new ConstantCurvatureSectionDefinition("left", 8.0, 20.0),
                new StraightSectionDefinition("exit", 5.0)
            }));

        Assert.Equal(19.0, compilation.TotalLength, 12);
        Assert.Equal(compilation.TotalLength, compilation.ResolvedSections[^1].EndDistance, 12);
        Assert.Equal(compilation.TotalLength, compilation.Document.TotalLength, 12);
    }

    [Fact]
    public void Compile_ExposesReadOnlyResolvedSectionSnapshot()
    {
        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(
            CreateMixedDefinition());
        IList<ResolvedSectionInterval<GeometricSectionDefinition>> exposed =
            Assert.IsAssignableFrom<IList<ResolvedSectionInterval<GeometricSectionDefinition>>>(
                compilation.ResolvedSections);

        Assert.True(exposed.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => exposed.RemoveAt(0));
        Assert.Equal(compilation.Definition.Sections.Count, compilation.ResolvedSections.Count);
    }

    [Fact]
    public void Compile_RecompilingRestoresAlignmentAfterDocumentMutation()
    {
        TrackAuthoringDefinition definition = CreateMixedDefinition();
        TrackAuthoringCompilation first = TrackAuthoringDocumentBuilder.Compile(definition);

        first.Document.Segments.RemoveAt(0);
        first.Document.Sections.RemoveAt(0);

        TrackAuthoringCompilation second = TrackAuthoringDocumentBuilder.Compile(definition);

        Assert.Equal(definition.Sections.Count - 1, first.Document.Segments.Count);
        Assert.Equal(definition.Sections.Count, first.ResolvedSections.Count);
        Assert.Equal(definition.Sections.Count, second.Document.Segments.Count);
        Assert.Equal(definition.Sections.Count, second.Document.Sections.Count);
        Assert.Equal(definition.Sections.Count, second.ResolvedSections.Count);
        Assert.NotSame(first.Document, second.Document);
    }

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
    public void RepeatedCompilations_ProduceIdenticalRangesAndEquivalentFrameSamples()
    {
        TrackAuthoringDefinition definition = CreateMixedDefinition();
        TrackAuthoringCompilation firstCompilation = TrackAuthoringDocumentBuilder.Compile(definition);
        TrackAuthoringCompilation secondCompilation = TrackAuthoringDocumentBuilder.Compile(definition);
        var firstEvaluator = new TrackEvaluator(firstCompilation.Document);
        var secondEvaluator = new TrackEvaluator(secondCompilation.Document);
        double[] distances = { 0.0, 2.5, 5.0, 7.0, 11.0, 14.0, 18.0 };

        Assert.Equal(firstCompilation.ResolvedSections.Count, secondCompilation.ResolvedSections.Count);
        for (int i = 0; i < firstCompilation.ResolvedSections.Count; i++)
        {
            ResolvedSectionInterval<GeometricSectionDefinition> firstInterval =
                firstCompilation.ResolvedSections[i];
            ResolvedSectionInterval<GeometricSectionDefinition> secondInterval =
                secondCompilation.ResolvedSections[i];

            Assert.Same(firstInterval.Section, secondInterval.Section);
            Assert.Equal(firstInterval.StartDistance, secondInterval.StartDistance, 12);
            Assert.Equal(firstInterval.EndDistance, secondInterval.EndDistance, 12);
            Assert.Equal(firstInterval.IncludeEndDistance, secondInterval.IncludeEndDistance);
        }

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
    public void BuildAndBuildDocument_RemainEquivalentToCompileDocument()
    {
        TrackAuthoringDefinition definition = CreateMixedDefinition();
        TrackDocument compiled = TrackAuthoringDocumentBuilder.Compile(definition).Document;
        TrackDocument built = TrackAuthoringDocumentBuilder.Build(definition);
        TrackDocument builtDocument = TrackAuthoringDocumentBuilder.BuildDocument(definition);

        AssertEquivalentDocuments(compiled, built);
        AssertEquivalentDocuments(compiled, builtDocument);
    }

    [Fact]
    public void BuilderEntryPoints_NullDefinition_ThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TrackAuthoringDocumentBuilder.Compile(null!));
        Assert.Throws<ArgumentNullException>(() => TrackAuthoringDocumentBuilder.Build(null!));
        Assert.Throws<ArgumentNullException>(() => TrackAuthoringDocumentBuilder.BuildDocument(null!));
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

    private static void AssertInterval(
        ResolvedSectionInterval<GeometricSectionDefinition> interval,
        GeometricSectionDefinition expectedSection,
        double expectedStart,
        double expectedEnd,
        bool includeEndDistance)
    {
        Assert.Same(expectedSection, interval.Section);
        Assert.Equal(expectedStart, interval.StartDistance, 12);
        Assert.Equal(expectedEnd, interval.EndDistance, 12);
        Assert.Equal(includeEndDistance, interval.IncludeEndDistance);
    }

    private static void AssertEquivalentDocuments(TrackDocument expected, TrackDocument actual)
    {
        Assert.Equal(expected.TotalLength, actual.TotalLength, 12);
        Assert.Equal(expected.Segments.Count, actual.Segments.Count);
        Assert.Equal(expected.Sections.Count, actual.Sections.Count);

        for (int i = 0; i < expected.Segments.Count; i++)
        {
            TrackSegment expectedSegment = expected.Segments[i];
            TrackSegment actualSegment = actual.Segments[i];

            Assert.Equal(expectedSegment.GetType(), actualSegment.GetType());
            Assert.Equal(expectedSegment.Id, actualSegment.Id);
            Assert.Equal(expectedSegment.Length, actualSegment.Length, 12);
            Assert.Equal(expectedSegment.RollRadians, actualSegment.RollRadians, 12);
        }

        var expectedEvaluator = new TrackEvaluator(expected);
        var actualEvaluator = new TrackEvaluator(actual);
        double[] distances = { 0.0, 5.0, 11.0, expected.TotalLength };

        foreach (double distance in distances)
        {
            TrackFrame expectedFrame = expectedEvaluator.EvaluateFrameAtDistance(distance);
            TrackFrame actualFrame = actualEvaluator.EvaluateFrameAtDistance(distance);

            AssertVectorNear(expectedFrame.Position, actualFrame.Position, 1e-10);
            AssertVectorNear(expectedFrame.Tangent, actualFrame.Tangent, 1e-10);
            AssertVectorNear(expectedFrame.Normal, actualFrame.Normal, 1e-10);
            AssertVectorNear(expectedFrame.Binormal, actualFrame.Binormal, 1e-10);
        }
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
