using Quantum.Math;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class TrackSpatialAuthoringFoundationTests
{
    private const double Tolerance = 1e-7;

    [Fact]
    public void LegacyConstructor_MatchesExplicitIdentityGeometryAndFrames()
    {
        GeometricSectionDefinition[] sections = CreateMixedSections();
        var legacy = new TrackAuthoringDefinition(sections);
        var explicitIdentity = new TrackAuthoringDefinition(
            sections,
            new TrackStartPose(
                Vector3d.Zero,
                Vector3d.UnitX,
                Vector3d.UnitY,
                Vector3d.UnitZ));

        TrackDocument legacyDocument = TrackAuthoringDocumentBuilder.Build(legacy);
        TrackDocument explicitDocument = TrackAuthoringDocumentBuilder.Build(explicitIdentity);
        var legacyEvaluator = new TrackEvaluator(legacyDocument);
        var explicitEvaluator = new TrackEvaluator(explicitDocument);

        Assert.Same(TrackStartPose.Identity, legacy.StartPose);
        foreach (double distance in BuildSampleDistances(legacyDocument.TotalLength))
        {
            AssertFrameNear(
                legacyEvaluator.EvaluateFrameAtDistance(distance),
                explicitEvaluator.EvaluateFrameAtDistance(distance));
        }
    }

    [Fact]
    public void DefinitionAndGeneratedDocument_PreserveStartPoseReferenceAndValues()
    {
        TrackStartPose startPose = CreateArbitraryStartPose();
        var definition = new TrackAuthoringDefinition(
            new[] { new StraightSectionDefinition("straight", 4.0) },
            startPose);

        TrackDocument document = TrackAuthoringDocumentBuilder.Build(definition);

        Assert.Same(startPose, definition.StartPose);
        Assert.Same(startPose, document.StartPose);
        AssertVectorNear(startPose.Position, document.StartPose!.Position);
        AssertVectorNear(startPose.Tangent, document.StartPose.Tangent);
        AssertVectorNear(startPose.Normal, document.StartPose.Normal);
        AssertVectorNear(startPose.Binormal, document.StartPose.Binormal);
        Assert.Null(new TrackDocument().StartPose);
    }

    [Fact]
    public void StraightSection_UsesTranslatedAndTiltedStartPose()
    {
        TrackStartPose startPose = CreateArbitraryStartPose();
        const double length = 7.5;
        TrackDocument document = TrackAuthoringDocumentBuilder.Build(
            new TrackAuthoringDefinition(
                new[] { new StraightSectionDefinition("straight", length) },
                startPose));

        TrackFrame start = new TrackEvaluator(document).EvaluateFrameAtDistance(0.0);
        TrackFrame end = new TrackEvaluator(document).EvaluateFrameAtDistance(length);

        AssertVectorNear(startPose.Position, start.Position);
        AssertVectorNear(startPose.Tangent, start.Tangent);
        AssertVectorNear(startPose.Position + (startPose.Tangent * length), end.Position);
        AssertVectorNear(startPose.Tangent, end.Tangent);
    }

    [Theory]
    [InlineData(10.0, 1.0)]
    [InlineData(-10.0, -1.0)]
    public void SignedQuarterArcs_UseArbitraryConstructionPlane(
        double radius,
        double normalSign)
    {
        TrackStartPose startPose = CreateArbitraryStartPose();
        double length = System.Math.Abs(radius) * System.Math.PI * 0.5;
        TrackDocument document = TrackAuthoringDocumentBuilder.Build(
            new TrackAuthoringDefinition(
                new[] { new ConstantCurvatureSectionDefinition("arc", length, radius) },
                startPose));

        TrackFrame end = new TrackEvaluator(document).EvaluateFrameAtDistance(length);
        Vector3d expectedPosition = startPose.Position +
                                    (startPose.Tangent * System.Math.Abs(radius)) +
                                    (startPose.Normal * (System.Math.Abs(radius) * normalSign));
        Vector3d expectedTangent = startPose.Normal * normalSign;

        AssertVectorNear(expectedPosition, end.Position);
        AssertVectorNear(expectedTangent, end.Tangent);
        AssertPointInStartPlane(startPose, end.Position);
    }

    [Fact]
    public void TransitionLayout_TransformsFromLocalAuthoringPlane()
    {
        GeometricSectionDefinition[] sections = CreateMixedSections();
        TrackStartPose startPose = CreateArbitraryStartPose();
        TrackDocument localDocument = TrackAuthoringDocumentBuilder.Build(
            new TrackAuthoringDefinition(sections));
        TrackDocument spatialDocument = TrackAuthoringDocumentBuilder.Build(
            new TrackAuthoringDefinition(sections, startPose));
        var localEvaluator = new TrackEvaluator(localDocument);
        var spatialEvaluator = new TrackEvaluator(spatialDocument);

        foreach (double distance in BuildSampleDistances(localDocument.TotalLength))
        {
            TrackFrame local = localEvaluator.EvaluateFrameAtDistance(distance);
            TrackFrame spatial = spatialEvaluator.EvaluateFrameAtDistance(distance);

            AssertVectorNear(TransformPoint(startPose, local.Position), spatial.Position);
            AssertVectorNear(TransformDirection(startPose, local.Tangent), spatial.Tangent);
            AssertPointInStartPlane(startPose, spatial.Position);
        }
    }

    [Fact]
    public void SpatialSectionBoundaries_RemainPositionAndTangentContinuous()
    {
        TrackDocument document = TrackAuthoringDocumentBuilder.Build(
            new TrackAuthoringDefinition(CreateMixedSections(), CreateArbitraryStartPose()));

        for (int i = 0; i < document.Segments.Count - 1; i++)
        {
            TrackSegment current = document.Segments[i];
            TrackSegment next = document.Segments[i + 1];

            AssertVectorNear(current.Spline!.Evaluate(1.0), next.Spline!.Evaluate(0.0));
            AssertVectorNear(
                current.Spline.Tangent(1.0).Normalized(),
                next.Spline.Tangent(0.0).Normalized());
        }
    }

    [Fact]
    public void CanonicalFrameAtStationZero_UsesAuthoredUnbankedBasis()
    {
        TrackStartPose startPose = CreateArbitraryStartPose();
        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(
            new TrackAuthoringDefinition(
                new[] { new StraightSectionDefinition("straight", 5.0) },
                startPose));

        TrackFrame documentFrame = new TrackEvaluator(compilation.Document)
            .EvaluateFrameAtDistance(0.0);
        TrackFrame runtimeFrame = new TrackEvaluator(compilation.Runtime)
            .EvaluateFrameAtDistance(0.0);

        AssertFrameMatchesPose(startPose, documentFrame);
        AssertFrameMatchesPose(startPose, runtimeFrame);
    }

    [Fact]
    public void SegmentBanking_RotatesFromAuthoredNormalWithoutChangingCenterline()
    {
        TrackStartPose startPose = CreateArbitraryStartPose();
        TrackDocument unbanked = TrackAuthoringDocumentBuilder.Build(
            new TrackAuthoringDefinition(
                new[] { new StraightSectionDefinition("straight", 8.0) },
                startPose));
        TrackDocument banked = TrackAuthoringDocumentBuilder.Build(
            new TrackAuthoringDefinition(
                new[]
                {
                    new StraightSectionDefinition(
                        "straight",
                        8.0,
                        rollRadians: System.Math.PI * 0.5)
                },
                startPose));
        TrackFrame unbankedFrame = new TrackEvaluator(unbanked).EvaluateFrameAtDistance(4.0);
        TrackFrame bankedFrame = new TrackEvaluator(banked).EvaluateFrameAtDistance(4.0);

        AssertVectorNear(unbankedFrame.Position, bankedFrame.Position);
        AssertVectorNear(unbankedFrame.Tangent, bankedFrame.Tangent);
        AssertVectorNear(startPose.Binormal, bankedFrame.Normal);
        AssertVectorNear(startPose.Normal * -1.0, bankedFrame.Binormal);
    }

    [Fact]
    public void SpatialDocumentAndRuntimeEvaluators_MatchAndRemainBatchDeterministic()
    {
        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(
            new TrackAuthoringDefinition(CreateMixedSections(), CreateArbitraryStartPose()));
        var documentEvaluator = new TrackEvaluator(compilation.Document);
        var runtimeEvaluator = new TrackEvaluator(compilation.Runtime);
        double[] distances =
        {
            compilation.TotalLength,
            0.0,
            7.0,
            3.0,
            7.0,
            compilation.TotalLength * 0.5
        };
        TrackFrame[] documentBatch = documentEvaluator.EvaluateFramesAtDistances(distances);
        TrackFrame[] runtimeBatch = runtimeEvaluator.EvaluateFramesAtDistances(distances);

        for (int i = 0; i < distances.Length; i++)
        {
            AssertFrameNear(documentEvaluator.EvaluateFrameAtDistance(distances[i]), documentBatch[i]);
            AssertFrameNear(runtimeEvaluator.EvaluateFrameAtDistance(distances[i]), runtimeBatch[i]);
            AssertFrameNear(documentBatch[i], runtimeBatch[i]);
        }

        AssertFrameNear(documentBatch[2], documentBatch[4]);
        AssertFrameNear(runtimeBatch[2], runtimeBatch[4]);
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

    private static GeometricSectionDefinition[] CreateMixedSections()
    {
        return new GeometricSectionDefinition[]
        {
            new StraightSectionDefinition("entry", 4.0),
            new CurvatureTransitionSectionDefinition("transition", 6.0, 0.0, 0.08),
            new ConstantCurvatureSectionDefinition("arc", 5.0, 12.5),
            new StraightSectionDefinition("exit", 3.0)
        };
    }

    private static double[] BuildSampleDistances(double totalLength)
    {
        return new[]
        {
            0.0,
            2.0,
            4.0,
            7.0,
            10.0,
            12.5,
            15.0,
            totalLength
        };
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

    private static void AssertPointInStartPlane(TrackStartPose pose, Vector3d point)
    {
        AssertNear(0.0, Vector3d.Dot(point - pose.Position, pose.Binormal));
    }

    private static void AssertFrameMatchesPose(TrackStartPose pose, TrackFrame frame)
    {
        AssertVectorNear(pose.Position, frame.Position);
        AssertVectorNear(pose.Tangent, frame.Tangent);
        AssertVectorNear(pose.Normal, frame.Normal);
        AssertVectorNear(pose.Binormal, frame.Binormal);
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
