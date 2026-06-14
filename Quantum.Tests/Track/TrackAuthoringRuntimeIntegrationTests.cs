using System.Reflection;
using Quantum.Math;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class TrackAuthoringRuntimeIntegrationTests
{
    private const double Tolerance = 1e-8;

    [Fact]
    public void Compile_ExposesRuntimeMatchingDocumentShapeAndLength()
    {
        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(
            CreateM140Definition());

        Assert.NotNull(compilation.Runtime);
        Assert.Equal(compilation.Document.Segments.Count, compilation.Runtime.SegmentCount);
        AssertNear(compilation.Document.TotalLength, compilation.Runtime.TotalLength);
        Assert.Same(TrackSamplingOptions.Default, compilation.Runtime.SamplingOptions);
    }

    [Fact]
    public void AuthoredM140AndM141Layouts_RuntimeAndDocumentEvaluatorsMatch()
    {
        TrackAuthoringDefinition[] definitions =
        {
            CreateM140Definition(),
            CreateM141Definition()
        };

        foreach (TrackAuthoringDefinition definition in definitions)
        {
            TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(definition);
            var documentEvaluator = new TrackEvaluator(compilation.Document);
            var runtimeEvaluator = new TrackEvaluator(compilation.Runtime);
            double[] distances = BuildParityDistances(compilation.ResolvedSections);

            foreach (double distance in distances)
            {
                AssertFrameNear(
                    documentEvaluator.EvaluateFrameAtDistance(distance),
                    runtimeEvaluator.EvaluateFrameAtDistance(distance));
            }
        }
    }

    [Fact]
    public void RuntimeBoundarySamples_SelectFollowingSegmentAndFinalEndpoint()
    {
        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(
            CreateM141Definition());
        var evaluator = new TrackEvaluator(compilation.Runtime);

        for (int i = 1; i < compilation.ResolvedSections.Count; i++)
        {
            double boundary = compilation.ResolvedSections[i].StartDistance;
            TrackEvaluationPoint point = evaluator.EvaluateAtDistance(boundary);

            Assert.Same(compilation.Document.Segments[i], point.Segment);
            Assert.Equal(0.0, point.LocalT, 12);
        }

        TrackEvaluationPoint endpoint = evaluator.EvaluateAtDistance(compilation.TotalLength);
        Assert.Same(compilation.Document.Segments[^1], endpoint.Segment);
        Assert.Equal(1.0, endpoint.LocalT, 12);
    }

    [Fact]
    public void RuntimeAndCompilationMetadata_RemainStableAfterDocumentSegmentsAreReplacedOrCleared()
    {
        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(
            CreateM140Definition());
        var runtimeEvaluator = new TrackEvaluator(compilation.Runtime);
        TrackFrame expected = runtimeEvaluator.EvaluateFrameAtDistance(7.0);
        int expectedResolvedCount = compilation.ResolvedSections.Count;
        double expectedTotalLength = compilation.TotalLength;

        compilation.Document.Segments[0] = new StraightSegment(1.0, "replacement");
        AssertFrameNear(expected, runtimeEvaluator.EvaluateFrameAtDistance(7.0));

        compilation.Document.Segments.Clear();

        AssertFrameNear(expected, runtimeEvaluator.EvaluateFrameAtDistance(7.0));
        Assert.Equal(3, compilation.Runtime.SegmentCount);
        AssertNear(expectedTotalLength, compilation.Runtime.TotalLength);
        Assert.Equal(expectedResolvedCount, compilation.ResolvedSections.Count);
        AssertNear(expectedTotalLength, compilation.TotalLength);
        Assert.Equal(0.0, compilation.Document.TotalLength);
    }

    [Fact]
    public void LiveDocumentEvaluator_ObservesDocumentSegmentMutation()
    {
        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(
            CreateM140Definition());
        var evaluator = new TrackEvaluator(compilation.Document);

        TrackEvaluationPoint before = evaluator.EvaluateAtDistance(2.0);
        double beforeLength = evaluator.GetBoundTrackTotalLength();

        compilation.Document.Segments.RemoveAt(0);

        TrackEvaluationPoint after = evaluator.EvaluateAtDistance(2.0);
        double afterLength = evaluator.GetBoundTrackTotalLength();

        Assert.Equal("entry", before.Segment.Id);
        Assert.Equal("left-arc", after.Segment.Id);
        AssertNear(5.0, beforeLength - afterLength);
    }

    [Fact]
    public void Recompile_ProducesEquivalentSamplesWithDistinctRuntimeSnapshots()
    {
        TrackAuthoringDefinition definition = CreateM141Definition();
        TrackAuthoringCompilation first = TrackAuthoringDocumentBuilder.Compile(definition);
        TrackAuthoringCompilation second = TrackAuthoringDocumentBuilder.Compile(definition);
        var firstEvaluator = new TrackEvaluator(first.Runtime);
        var secondEvaluator = new TrackEvaluator(second.Runtime);

        Assert.NotSame(first.Document, second.Document);
        Assert.NotSame(first.Runtime, second.Runtime);

        foreach (double distance in BuildParityDistances(first.ResolvedSections))
        {
            AssertFrameNear(
                firstEvaluator.EvaluateFrameAtDistance(distance),
                secondEvaluator.EvaluateFrameAtDistance(distance));
        }
    }

    [Fact]
    public void RuntimeCompilationFailure_ThrowsDeterministicInvalidOperationException()
    {
        // Each default quarter-probe advances by a full turn, aliasing the arc to zero length.
        var definition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            new ConstantCurvatureSectionDefinition(
                "aliased-arc",
                800.0 * System.Math.PI,
                1.0)
        });

        InvalidOperationException first = Assert.Throws<InvalidOperationException>(
            () => TrackAuthoringDocumentBuilder.Compile(definition));
        InvalidOperationException second = Assert.Throws<InvalidOperationException>(
            () => TrackAuthoringDocumentBuilder.Compile(definition));

        Assert.Equal(first.Message, second.Message);
        Assert.Contains(nameof(TrackRuntimeDiagnosticCode.InvalidMeasuredLength), first.Message);
        Assert.Contains("segment index 0", first.Message);
        Assert.Contains("ID 'aliased-arc'", first.Message);
        Assert.Contains("finite geometric length greater than zero", first.Message);
    }

    [Fact]
    public void ApiBoundary_ExposesRuntimeAndPreservesBuilderEntryPointSignatures()
    {
        PropertyInfo? runtimeProperty = typeof(TrackAuthoringCompilation).GetProperty(
            nameof(TrackAuthoringCompilation.Runtime));

        Assert.NotNull(runtimeProperty);
        Assert.Equal(typeof(CompiledTrackRuntime), runtimeProperty.PropertyType);
        AssertBuilderMethod(
            nameof(TrackAuthoringDocumentBuilder.Build),
            typeof(TrackDocument));
        AssertBuilderMethod(
            nameof(TrackAuthoringDocumentBuilder.BuildDocument),
            typeof(TrackDocument));
        AssertBuilderMethod(
            nameof(TrackAuthoringDocumentBuilder.Compile),
            typeof(TrackAuthoringCompilation));
    }

    private static TrackAuthoringDefinition CreateM140Definition()
    {
        return new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            new StraightSectionDefinition("entry", 5.0),
            new ConstantCurvatureSectionDefinition("left-arc", 6.0, 18.0, 0.15),
            new StraightSectionDefinition("exit", 4.0, 0.15)
        });
    }

    private static TrackAuthoringDefinition CreateM141Definition()
    {
        return new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            new StraightSectionDefinition("entry", 5.0),
            new CurvatureTransitionSectionDefinition(
                "transition-in",
                8.0,
                0.0,
                0.08,
                rollRadians: 0.1),
            new ConstantCurvatureSectionDefinition("arc", 4.0, 12.5, 0.1),
            new CurvatureTransitionSectionDefinition(
                "transition-out",
                4.0,
                0.08,
                0.0,
                rollRadians: -0.1)
        });
    }

    private static double[] BuildParityDistances(
        IReadOnlyList<ResolvedSectionInterval<GeometricSectionDefinition>> intervals)
    {
        var distances = new List<double> { 0.0 };
        for (int i = 0; i < intervals.Count; i++)
        {
            ResolvedSectionInterval<GeometricSectionDefinition> interval = intervals[i];
            distances.Add(interval.StartDistance + (interval.Length * 0.5));
            distances.Add(interval.EndDistance);
        }

        return distances.ToArray();
    }

    private static void AssertBuilderMethod(string name, Type returnType)
    {
        MethodInfo[] methods = typeof(TrackAuthoringDocumentBuilder).GetMethods(
                BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == name)
            .ToArray();
        MethodInfo method = Assert.Single(methods);

        Assert.Equal(returnType, method.ReturnType);
        ParameterInfo parameter = Assert.Single(method.GetParameters());
        Assert.Equal(typeof(TrackAuthoringDefinition), parameter.ParameterType);
    }

    private static void AssertFrameNear(TrackFrame expected, TrackFrame actual)
    {
        AssertNear(expected.Distance, actual.Distance);
        AssertVectorNear(expected.Position, actual.Position);
        AssertVectorNear(expected.Tangent, actual.Tangent);
        AssertVectorNear(expected.Normal, actual.Normal);
        AssertVectorNear(expected.Binormal, actual.Binormal);
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual)
    {
        AssertNear(expected.X, actual.X);
        AssertNear(expected.Y, actual.Y);
        AssertNear(expected.Z, actual.Z);
    }

    private static void AssertNear(double expected, double actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, Tolerance);
    }
}
