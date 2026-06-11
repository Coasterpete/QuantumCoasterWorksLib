using Quantum.Math;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class CurvatureTransitionAuthoringTests
{
    private const double Tolerance = 1e-8;

    [Fact]
    public void Compile_PreservesOneToOneAlignmentMetadataAndDefinitionReferences()
    {
        var entry = new StraightSectionDefinition("entry", 5.0, 0.1);
        var transition = new CurvatureTransitionSectionDefinition(
            "transition",
            8.0,
            0.0,
            0.08,
            rollRadians: -0.2);
        var arc = new ConstantCurvatureSectionDefinition("arc", 4.0, 12.5, 0.3);
        var definition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            entry,
            transition,
            arc
        });

        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(definition);

        Assert.Equal(3, compilation.ResolvedSections.Count);
        Assert.Equal(3, compilation.Document.Segments.Count);
        Assert.Equal(3, compilation.Document.Sections.Count);

        for (int i = 0; i < definition.Sections.Count; i++)
        {
            GeometricSectionDefinition source = definition.Sections[i];
            ResolvedSectionInterval<GeometricSectionDefinition> interval =
                compilation.ResolvedSections[i];
            TrackSegment segment = compilation.Document.Segments[i];
            GeometricSection section = Assert.IsType<GeometricSection>(
                compilation.Document.Sections[i]);

            Assert.Same(source, interval.Section);
            Assert.Equal(source.Length, interval.Length, 12);
            Assert.Equal(source.Id, segment.Id);
            Assert.Equal(source.Length, segment.Length, 12);
            Assert.Equal(source.RollRadians, segment.RollRadians, 12);
            Assert.Equal(source.Length, section.Length, 12);
            Assert.Equal(source.RollRadians, section.Roll);
        }

        Assert.Collection(
            compilation.Document.Segments,
            segment => Assert.IsType<StraightSegment>(segment),
            segment => Assert.IsType<CurvedSegment>(segment),
            segment => Assert.IsType<CurvedSegment>(segment));
        Assert.Null(Assert.IsType<GeometricSection>(compilation.Document.Sections[1]).Curvature);
        Assert.Equal(17.0, compilation.TotalLength, 12);
    }

    [Fact]
    public void Compile_ProducesPointAndTangentContinuityAtTransitionBoundaries()
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
    public void EqualZeroCurvatures_MatchStraightGeometryThroughCompiler()
    {
        TrackDocument transition = TrackAuthoringDocumentBuilder.Build(
            new TrackAuthoringDefinition(new GeometricSectionDefinition[]
            {
                new CurvatureTransitionSectionDefinition("transition", 10.0, 0.0, 0.0)
            }));
        TrackDocument straight = TrackAuthoringDocumentBuilder.Build(
            new TrackAuthoringDefinition(new GeometricSectionDefinition[]
            {
                new StraightSectionDefinition("straight", 10.0)
            }));

        Assert.IsType<CurvedSegment>(transition.Segments[0]);
        AssertEquivalentFrames(transition, straight, new[] { 0.0, 2.5, 5.0, 10.0 });
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(-0.05)]
    public void EqualNonzeroCurvatures_MatchConstantArcGeometryThroughCompiler(double curvature)
    {
        const double length = 12.0;
        TrackDocument transition = TrackAuthoringDocumentBuilder.Build(
            new TrackAuthoringDefinition(new GeometricSectionDefinition[]
            {
                new CurvatureTransitionSectionDefinition(
                    "transition",
                    length,
                    curvature,
                    curvature)
            }));
        TrackDocument arc = TrackAuthoringDocumentBuilder.Build(
            new TrackAuthoringDefinition(new GeometricSectionDefinition[]
            {
                new ConstantCurvatureSectionDefinition("arc", length, 1.0 / curvature)
            }));

        AssertEquivalentFrames(transition, arc, new[] { 0.0, 3.0, 6.0, 9.0, length });
    }

    [Fact]
    public void Compile_TransitionGeometryIsFiniteDistanceBasedAndDeterministic()
    {
        TrackAuthoringDefinition definition = CreateMixedDefinition();
        TrackAuthoringCompilation first = TrackAuthoringDocumentBuilder.Compile(definition);
        TrackAuthoringCompilation second = TrackAuthoringDocumentBuilder.Compile(definition);
        double[] distances = { 0.0, 2.0, 5.0, 7.5, 10.0, 13.0, 17.0, 21.0 };

        Assert.Equal(first.TotalLength, second.TotalLength);
        for (int i = 0; i < first.ResolvedSections.Count; i++)
        {
            Assert.Same(first.ResolvedSections[i].Section, second.ResolvedSections[i].Section);
            Assert.Equal(first.ResolvedSections[i].StartDistance, second.ResolvedSections[i].StartDistance);
            Assert.Equal(first.ResolvedSections[i].EndDistance, second.ResolvedSections[i].EndDistance);
        }

        AssertEquivalentFrames(first.Document, second.Document, distances);

        var evaluator = new TrackEvaluator(first.Document);
        foreach (double distance in distances)
        {
            TrackFrame frame = evaluator.EvaluateFrameAtDistance(distance);
            Assert.Equal(distance, frame.Distance, 10);
            AssertFinite(frame.Position);
            AssertFinite(frame.Tangent);
            Assert.InRange(System.Math.Abs(frame.Tangent.Length - 1.0), 0.0, Tolerance);
        }
    }

    [Fact]
    public void BuildBuildDocumentAndCompile_ProduceEquivalentTransitionDocuments()
    {
        TrackAuthoringDefinition definition = CreateMixedDefinition();
        TrackDocument compiled = TrackAuthoringDocumentBuilder.Compile(definition).Document;
        TrackDocument built = TrackAuthoringDocumentBuilder.Build(definition);
        TrackDocument builtDocument = TrackAuthoringDocumentBuilder.BuildDocument(definition);
        double[] distances = { 0.0, 5.0, 9.0, 13.0, 17.0, 21.0 };

        AssertEquivalentDocuments(compiled, built, distances);
        AssertEquivalentDocuments(compiled, builtDocument, distances);
    }

    private static TrackAuthoringDefinition CreateMixedDefinition()
    {
        return new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            new StraightSectionDefinition("entry", 5.0, 0.0),
            new CurvatureTransitionSectionDefinition("transition-in", 8.0, 0.0, 0.08, rollRadians: 0.1),
            new ConstantCurvatureSectionDefinition("arc", 4.0, 12.5, 0.1),
            new CurvatureTransitionSectionDefinition("transition-out", 4.0, 0.08, 0.0, rollRadians: -0.1)
        });
    }

    private static void AssertEquivalentDocuments(
        TrackDocument expected,
        TrackDocument actual,
        IReadOnlyList<double> distances)
    {
        Assert.Equal(expected.TotalLength, actual.TotalLength, 12);
        Assert.Equal(expected.Segments.Count, actual.Segments.Count);
        Assert.Equal(expected.Sections.Count, actual.Sections.Count);

        for (int i = 0; i < expected.Segments.Count; i++)
        {
            Assert.Equal(expected.Segments[i].GetType(), actual.Segments[i].GetType());
            Assert.Equal(expected.Segments[i].Id, actual.Segments[i].Id);
            Assert.Equal(expected.Segments[i].Length, actual.Segments[i].Length);
            Assert.Equal(expected.Segments[i].RollRadians, actual.Segments[i].RollRadians);
        }

        AssertEquivalentFrames(expected, actual, distances);
    }

    private static void AssertEquivalentFrames(
        TrackDocument expected,
        TrackDocument actual,
        IReadOnlyList<double> distances)
    {
        var expectedEvaluator = new TrackEvaluator(expected);
        var actualEvaluator = new TrackEvaluator(actual);

        foreach (double distance in distances)
        {
            TrackFrame expectedFrame = expectedEvaluator.EvaluateFrameAtDistance(distance);
            TrackFrame actualFrame = actualEvaluator.EvaluateFrameAtDistance(distance);

            AssertVectorNear(expectedFrame.Position, actualFrame.Position);
            AssertVectorNear(expectedFrame.Tangent, actualFrame.Tangent);
            AssertVectorNear(expectedFrame.Normal, actualFrame.Normal);
            AssertVectorNear(expectedFrame.Binormal, actualFrame.Binormal);
        }
    }

    private static void AssertFinite(Vector3d value)
    {
        Assert.True(double.IsFinite(value.X));
        Assert.True(double.IsFinite(value.Y));
        Assert.True(double.IsFinite(value.Z));
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual)
    {
        Assert.InRange(System.Math.Abs(expected.X - actual.X), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(expected.Y - actual.Y), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(expected.Z - actual.Z), 0.0, Tolerance);
    }
}
