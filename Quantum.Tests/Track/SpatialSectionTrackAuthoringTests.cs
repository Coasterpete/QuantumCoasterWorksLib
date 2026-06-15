using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using Quantum.Track.Authoring;
using TrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class SpatialSectionTrackAuthoringTests
{
    private const double Tolerance = 1e-7;

    [Fact]
    public void Compile_EmitsCurvedSegmentAndNullCurvatureMetadataWithAlignedSource()
    {
        SpatialSectionDefinition spatial = CreateFirstSpatial();
        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(
            new TrackAuthoringDefinition(new GeometricSectionDefinition[] { spatial }));

        CurvedSegment segment = Assert.IsType<CurvedSegment>(compilation.Document.Segments[0]);
        GeometricSection metadata = Assert.IsType<GeometricSection>(compilation.Document.Sections[0]);

        Assert.Equal(spatial.Id, segment.Id);
        Assert.Equal(spatial.Length, segment.Length);
        Assert.Equal(spatial.RollRadians, segment.RollRadians);
        Assert.NotNull(segment.Spline);
        Assert.Equal(spatial.Length, metadata.Length);
        Assert.Null(metadata.Curvature);
        Assert.Equal(spatial.RollRadians, metadata.Roll);
        Assert.Same(spatial, compilation.ResolvedSections[0].Section);
        Assert.Equal(0.0, compilation.ResolvedSections[0].StartDistance);
        Assert.Equal(spatial.Length, compilation.ResolvedSections[0].EndDistance);
        Assert.Equal(spatial.Length, compilation.TotalLength);
        Assert.NotNull(compilation.Runtime);
    }

    [Fact]
    public void Compile_RejectsDeclaredLengthThatDoesNotMatchMeasuredGeometry()
    {
        List<Vector3d> controlPoints = FirstSpatialControlPoints();
        double measuredLength = MeasureLength(controlPoints, degree: 3);
        var definition = new SpatialSectionDefinition(
            "bad-length",
            measuredLength + 0.1,
            controlPoints);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => TrackAuthoringDocumentBuilder.Compile(
                new TrackAuthoringDefinition(new[] { definition })));

        Assert.Contains("DeclaredLengthMismatch", exception.Message);
        Assert.Contains("bad-length", exception.Message);
        Assert.Contains("does not match measured geometric length", exception.Message);
    }

    [Fact]
    public void SpatialSection_LeavesItsEntryPlane()
    {
        SpatialSectionDefinition spatial = CreateFirstSpatial();
        TrackDocument document = TrackAuthoringDocumentBuilder.Build(
            new TrackAuthoringDefinition(new GeometricSectionDefinition[]
            {
                new StraightSectionDefinition("entry", 4.0),
                spatial
            }));
        var evaluator = new TrackEvaluator(document);
        TrackFrame entry = evaluator.EvaluateFrameAtDistance(4.0);
        TrackFrame interior = evaluator.EvaluateFrameAtDistance(4.0 + (spatial.Length * 0.65));
        double departure = Vector3d.Dot(interior.Position - entry.Position, entry.Binormal);

        Assert.True(System.Math.Abs(departure) > 0.25, $"Expected out-of-plane departure, got {departure:R}.");
    }

    [Fact]
    public void MixedStraightSpatialArcSpatialStraight_CompilesWithC0AndG1Boundaries()
    {
        TrackAuthoringCompilation compilation = CompileMixed(TrackStartPose.Identity);

        Assert.Collection(
            compilation.Document.Segments,
            segment => Assert.IsType<StraightSegment>(segment),
            segment => Assert.IsType<CurvedSegment>(segment),
            segment => Assert.IsType<CurvedSegment>(segment),
            segment => Assert.IsType<CurvedSegment>(segment),
            segment => Assert.IsType<StraightSegment>(segment));

        for (int i = 0; i < compilation.Document.Segments.Count - 1; i++)
        {
            TrackSegment current = compilation.Document.Segments[i];
            TrackSegment next = compilation.Document.Segments[i + 1];

            AssertVectorNear(current.Spline!.Evaluate(1.0), next.Spline!.Evaluate(0.0));
            AssertVectorNear(
                current.Spline.Tangent(1.0).Normalized(),
                next.Spline.Tangent(0.0).Normalized());
        }
    }

    [Fact]
    public void MixedLayout_WithArbitraryStartPose_IsRigidTransformOfIdentityLayout()
    {
        GeometricSectionDefinition[] sections = CreateMixedSections();
        TrackStartPose startPose = CreateArbitraryStartPose();
        TrackDocument identityDocument = TrackAuthoringDocumentBuilder.Build(
            new TrackAuthoringDefinition(sections));
        TrackDocument placedDocument = TrackAuthoringDocumentBuilder.Build(
            new TrackAuthoringDefinition(sections, startPose));
        var identityEvaluator = new TrackEvaluator(identityDocument);
        var placedEvaluator = new TrackEvaluator(placedDocument);

        foreach (double distance in BuildSampleDistances(sections))
        {
            TrackFrame local = identityEvaluator.EvaluateFrameAtDistance(distance);
            TrackFrame placed = placedEvaluator.EvaluateFrameAtDistance(distance);

            AssertVectorNear(TransformPoint(startPose, local.Position), placed.Position);
            AssertVectorNear(TransformDirection(startPose, local.Tangent), placed.Tangent);
            AssertVectorNear(TransformDirection(startPose, local.Normal), placed.Normal);
            AssertVectorNear(TransformDirection(startPose, local.Binormal), placed.Binormal);
        }
    }

    [Fact]
    public void SpatialOutgoingBasis_UsesRotationMinimizingTransportForFollowingArc()
    {
        SpatialSectionDefinition spatial = CreateFirstSpatial();
        TrackDocument document = TrackAuthoringDocumentBuilder.Build(
            new TrackAuthoringDefinition(new GeometricSectionDefinition[]
            {
                new StraightSectionDefinition("entry", 4.0),
                spatial,
                new ConstantCurvatureSectionDefinition("arc", 5.0, 12.0)
            }));
        var evaluator = new TrackEvaluator(document);
        double boundary = 4.0 + spatial.Length;
        TrackFrame atBoundary = evaluator.EvaluateFrameAtDistance(boundary);
        TrackFrame afterBoundary = evaluator.EvaluateFrameAtDistance(boundary + 0.01);
        Vector3d bendDirection = (afterBoundary.Tangent - atBoundary.Tangent).Normalized();

        Assert.True(
            Vector3d.Dot(bendDirection, atBoundary.Normal) > 0.999,
            "The following positive-curvature arc must bend toward the transported unbanked normal.");
    }

    [Fact]
    public void DocumentAndRuntimeFrames_MatchAcrossSpatialBoundaries()
    {
        TrackAuthoringCompilation compilation = CompileMixed(CreateArbitraryStartPose());
        var documentEvaluator = new TrackEvaluator(compilation.Document);
        var runtimeEvaluator = new TrackEvaluator(compilation.Runtime);

        foreach (double distance in BuildSampleDistances(compilation.Definition.Sections))
        {
            AssertFrameNear(
                documentEvaluator.EvaluateFrameAtDistance(distance),
                runtimeEvaluator.EvaluateFrameAtDistance(distance));
        }
    }

    [Fact]
    public void TrainBodiesAndBogies_SampleAcrossSpatialBoundaries()
    {
        TrackAuthoringCompilation compilation = CompileMixed(CreateArbitraryStartPose());
        var documentEvaluator = new TrackEvaluator(compilation.Document);
        var runtimeEvaluator = new TrackEvaluator(compilation.Runtime);
        var documentProvider = new TrainCarTransformProvider(documentEvaluator);
        var runtimeProvider = new TrainCarTransformProvider(runtimeEvaluator);
        double firstSpatialStart = compilation.ResolvedSections[1].StartDistance;
        double firstSpatialEnd = compilation.ResolvedSections[1].EndDistance;

        foreach (double boundary in new[] { firstSpatialStart, firstSpatialEnd })
        {
            TrainCarTransform documentBody = Assert.Single(
                documentProvider.EvaluateCarTransforms(boundary, carSpacing: 1.0, carCount: 1));
            TrainCarTransform runtimeBody = Assert.Single(
                runtimeProvider.EvaluateCarTransforms(boundary, carSpacing: 1.0, carCount: 1));
            TrainCarWithBogiesTransform documentCar = Assert.Single(
                documentProvider.EvaluateTrainWithBogies(
                    boundary,
                    carCount: 1,
                    carSpacing: 1.0,
                    bogieSpacing: 1.0));
            TrainCarWithBogiesTransform runtimeCar = Assert.Single(
                runtimeProvider.EvaluateTrainWithBogies(
                    boundary,
                    carCount: 1,
                    carSpacing: 1.0,
                    bogieSpacing: 1.0));

            Assert.Equal(boundary, documentBody.Distance);
            AssertFrameNear(documentEvaluator.EvaluateFrameAtDistance(boundary), documentBody.Frame);
            AssertFrameNear(documentBody.Frame, runtimeBody.Frame);
            Assert.True(documentCar.RearBogie.Distance < boundary);
            Assert.True(documentCar.FrontBogie.Distance > boundary);
            AssertFrameNear(documentCar.Body.Frame, runtimeCar.Body.Frame);
            AssertFrameNear(documentCar.FrontBogie.Frame, runtimeCar.FrontBogie.Frame);
            AssertFrameNear(documentCar.RearBogie.Frame, runtimeCar.RearBogie.Frame);
        }
    }

    [Fact]
    public void RepeatedCompilation_IsDeterministicAndReturnsDistinctSnapshots()
    {
        GeometricSectionDefinition[] sections = CreateMixedSections();
        var definition = new TrackAuthoringDefinition(sections, CreateArbitraryStartPose());
        TrackAuthoringCompilation first = TrackAuthoringDocumentBuilder.Compile(definition);
        TrackAuthoringCompilation second = TrackAuthoringDocumentBuilder.Compile(definition);
        var firstDocumentEvaluator = new TrackEvaluator(first.Document);
        var secondDocumentEvaluator = new TrackEvaluator(second.Document);
        var firstRuntimeEvaluator = new TrackEvaluator(first.Runtime);
        var secondRuntimeEvaluator = new TrackEvaluator(second.Runtime);

        Assert.NotSame(first.Document, second.Document);
        Assert.NotSame(first.Runtime, second.Runtime);
        Assert.Equal(first.TotalLength, second.TotalLength);

        foreach (double distance in BuildSampleDistances(sections))
        {
            AssertFrameNear(
                firstDocumentEvaluator.EvaluateFrameAtDistance(distance),
                secondDocumentEvaluator.EvaluateFrameAtDistance(distance));
            AssertFrameNear(
                firstRuntimeEvaluator.EvaluateFrameAtDistance(distance),
                secondRuntimeEvaluator.EvaluateFrameAtDistance(distance));
        }
    }

    private static TrackAuthoringCompilation CompileMixed(TrackStartPose startPose)
    {
        return TrackAuthoringDocumentBuilder.Compile(
            new TrackAuthoringDefinition(CreateMixedSections(), startPose));
    }

    private static GeometricSectionDefinition[] CreateMixedSections()
    {
        return new GeometricSectionDefinition[]
        {
            new StraightSectionDefinition("entry", 4.0),
            CreateFirstSpatial(),
            new ConstantCurvatureSectionDefinition("arc", 5.0, 12.0),
            CreateSecondSpatial(),
            new StraightSectionDefinition("exit", 3.0)
        };
    }

    private static SpatialSectionDefinition CreateFirstSpatial()
    {
        List<Vector3d> controlPoints = FirstSpatialControlPoints();
        return CreateSpatial("spatial-up", controlPoints, rollRadians: 0.0);
    }

    private static SpatialSectionDefinition CreateSecondSpatial()
    {
        var controlPoints = new List<Vector3d>
        {
            Vector3d.Zero,
            new Vector3d(1.5, 0.0, 0.0),
            new Vector3d(3.0, -1.2, 1.0),
            new Vector3d(5.0, -2.0, 0.5)
        };
        return CreateSpatial("spatial-down", controlPoints, rollRadians: -0.15);
    }

    private static List<Vector3d> FirstSpatialControlPoints()
    {
        return new List<Vector3d>
        {
            Vector3d.Zero,
            new Vector3d(2.0, 0.0, 0.0),
            new Vector3d(4.0, 1.4, 2.0),
            new Vector3d(6.0, 2.0, 3.5)
        };
    }

    private static SpatialSectionDefinition CreateSpatial(
        string id,
        List<Vector3d> controlPoints,
        double rollRadians)
    {
        const int degree = 3;
        var weights = Enumerable.Repeat(1.0, controlPoints.Count).ToList();
        double length = MeasureLength(controlPoints, degree, weights);
        return new SpatialSectionDefinition(
            id,
            length,
            controlPoints,
            degree,
            weights,
            rollRadians);
    }

    private static double MeasureLength(
        List<Vector3d> controlPoints,
        int degree,
        List<double>? weights = null)
    {
        weights ??= Enumerable.Repeat(1.0, controlPoints.Count).ToList();
        var curve = new GSharkNurbsCurveAdapter(controlPoints, weights, degree);
        return new ArcLengthLUT(
            curve,
            TrackSamplingOptions.DefaultArcLengthSamples,
            TrackSamplingOptions.DefaultArcLengthTolerance).TotalLength;
    }

    private static TrackStartPose CreateArbitraryStartPose()
    {
        double inverseSqrtThree = 1.0 / System.Math.Sqrt(3.0);
        double inverseSqrtTwo = 1.0 / System.Math.Sqrt(2.0);
        double inverseSqrtSix = 1.0 / System.Math.Sqrt(6.0);

        return new TrackStartPose(
            new Vector3d(10.0, -3.0, 5.0),
            new Vector3d(inverseSqrtThree, inverseSqrtThree, inverseSqrtThree),
            new Vector3d(-inverseSqrtTwo, inverseSqrtTwo, 0.0),
            new Vector3d(-inverseSqrtSix, -inverseSqrtSix, 2.0 * inverseSqrtSix));
    }

    private static double[] BuildSampleDistances(
        IReadOnlyList<GeometricSectionDefinition> sections)
    {
        var distances = new List<double> { 0.0 };
        double station = 0.0;

        for (int i = 0; i < sections.Count; i++)
        {
            distances.Add(station + (sections[i].Length * 0.35));
            distances.Add(station + (sections[i].Length * 0.7));
            station += sections[i].Length;
            distances.Add(station);
        }

        return distances.ToArray();
    }

    private static Vector3d TransformPoint(TrackStartPose pose, Vector3d localPoint)
    {
        return pose.Position + TransformDirection(pose, localPoint);
    }

    private static Vector3d TransformDirection(TrackStartPose pose, Vector3d localDirection)
    {
        return (pose.Tangent * localDirection.X) +
               (pose.Normal * localDirection.Y) +
               (pose.Binormal * localDirection.Z);
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
