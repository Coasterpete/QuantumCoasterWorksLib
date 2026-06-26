using System.Numerics;
using System.Reflection;
using Quantum.Debug;
using Quantum.IO.DebugViewport.V1;
using Quantum.IO.TrainPose.V1;
using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using Quantum.Track.Authoring;
using TrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class TrainRuntimeIntegrationTests
{
    private const double Tolerance = 1e-8;

    [Fact]
    public void EvaluateCarTransforms_DocumentAndRuntimeProvidersMatch()
    {
        ProviderPair pair = CreateProviderPair();

        IReadOnlyList<TrainCarTransform> expected = pair.Document.EvaluateCarTransforms(
            leadDistance: 24.0,
            carSpacing: 6.0,
            carCount: 3);
        IReadOnlyList<TrainCarTransform> actual = pair.Runtime.EvaluateCarTransforms(
            leadDistance: 24.0,
            carSpacing: 6.0,
            carCount: 3);

        AssertCarTransformsNear(expected, actual);
#pragma warning disable CS0618
        AssertCarTransformsNear(
            pair.Document.GetCarTransforms(24.0, 6.0, 3),
            pair.Runtime.GetCarTransforms(24.0, 6.0, 3));
#pragma warning restore CS0618
    }

    [Fact]
    public void BogiesWheelsArticulationAndFullPose_DocumentAndRuntimeProvidersMatch()
    {
        ProviderPair pair = CreateProviderPair();
        TrainConsistDefinition definition = CreateConsistDefinition();

        AssertCarsWithBogiesNear(
            pair.Document.EvaluateTrainWithBogies(24.0, definition),
            pair.Runtime.EvaluateTrainWithBogies(24.0, definition));
        AssertCarsWithBogiesNear(
            pair.Document.EvaluateTrainWithBogies(24.0, 3, 6.0, 4.0),
            pair.Runtime.EvaluateTrainWithBogies(24.0, 3, 6.0, 4.0));
        AssertCarsWithWheelsNear(
            pair.Document.EvaluateTrainWithBogiesAndWheels(24.0, definition),
            pair.Runtime.EvaluateTrainWithBogiesAndWheels(24.0, definition));
        AssertArticulatedCarsNear(
            pair.Document.EvaluateArticulatedTrain(24.0, definition),
            pair.Runtime.EvaluateArticulatedTrain(24.0, definition));
        AssertArticulatedCarsWithWheelsNear(
            pair.Document.EvaluateArticulatedTrainWithWheels(24.0, definition),
            pair.Runtime.EvaluateArticulatedTrainWithWheels(24.0, definition));

        AssertPoseJsonEqual(
            pair.Document.EvaluateTrainPose(24.0, definition),
            pair.Runtime.EvaluateTrainPose(24.0, definition));
    }

    [Fact]
    public void BankingProfileBodyAndFullPose_DocumentAndRuntimeProvidersMatch()
    {
        ProviderPair pair = CreateProviderPair();
        TrainConsistDefinition definition = CreateConsistDefinition();
        BankingProfile profile = CreateBankingProfile(pair.Compilation.TotalLength);

        AssertCarTransformsNear(
            pair.Document.EvaluateCarTransforms(24.0, 6.0, 3, profile),
            pair.Runtime.EvaluateCarTransforms(24.0, 6.0, 3, profile));
#pragma warning disable CS0618
        AssertCarTransformsNear(
            pair.Document.GetCarTransforms(24.0, 6.0, 3, profile),
            pair.Runtime.GetCarTransforms(24.0, 6.0, 3, profile));
#pragma warning restore CS0618
        AssertPoseJsonEqual(
            pair.Document.EvaluateTrainPose(24.0, definition, profile),
            pair.Runtime.EvaluateTrainPose(24.0, definition, profile));
    }

    [Fact]
    public void CompositeMultiSegmentBoundarySampling_DocumentAndRuntimeProvidersMatch()
    {
        ProviderPair pair = CreateProviderPair();
        TrainConsistDefinition definition = CreateConsistDefinition();

        TrainPoseResult expected = pair.Document.EvaluateTrainPose(24.0, definition);
        TrainPoseResult actual = pair.Runtime.EvaluateTrainPose(24.0, definition);

        AssertPoseJsonEqual(expected, actual);
        double[] boundaries = { 24.0, 18.0, 12.0 };
        for (int i = 0; i < boundaries.Length; i++)
        {
            ArticulatedTrainCarTransform body = actual.CarsReadOnly[i].Body;
            Assert.True(body.RearBogie.Distance < boundaries[i]);
            Assert.True(body.FrontBogie.Distance > boundaries[i]);
        }
    }

    [Fact]
    public void RuntimeProviderPose_RemainsStableAfterSourceSegmentsAreReplacedOrCleared()
    {
        TrackDocument document = CreateMutableDocument();
        var provider = new TrainCarTransformProvider(
            new TrackEvaluator(new CompiledTrackRuntime(document)));
        TrainConsistDefinition definition = CreateConsistDefinition();
        TrainPoseResult expected = provider.EvaluateTrainPose(24.0, definition);

        ReplaceWithShiftedSegment(document);
        AssertPoseJsonEqual(expected, provider.EvaluateTrainPose(24.0, definition));

        document.Segments.Clear();
        AssertPoseJsonEqual(expected, provider.EvaluateTrainPose(24.0, definition));
    }

    [Fact]
    public void DocumentProviderPose_ObservesSegmentReplacementOnNextCall()
    {
        TrackDocument document = CreateMutableDocument();
        var provider = new TrainCarTransformProvider(new TrackEvaluator(document));
        TrainConsistDefinition definition = CreateConsistDefinition();
        TrainPoseResult before = provider.EvaluateTrainPose(24.0, definition);

        ReplaceWithShiftedSegment(document);
        TrainPoseResult after = provider.EvaluateTrainPose(24.0, definition);

        Assert.NotEqual(
            SerializePose(before),
            SerializePose(after));
        AssertNear(24.0, before.CarsReadOnly[0].Body.OriginalBody.Frame.Position.X);
        AssertNear(124.0, after.CarsReadOnly[0].Body.OriginalBody.Frame.Position.X);
    }

    [Fact]
    public void RepeatedRuntimePoseEvaluation_DoesNotRereadArcLengthCompilationMetadata()
    {
        var curve = new CountingArcLengthLineCurve(30.0);
        var document = new TrackDocument(new[]
        {
            new StraightSegment(curve.Length, "counting", spline: curve)
        });
        var provider = new TrainCarTransformProvider(
            new TrackEvaluator(new CompiledTrackRuntime(document)));
        TrainConsistDefinition definition = CreateConsistDefinition();
        int readsAfterCompilation = curve.LengthReadCount;

        provider.EvaluateTrainPose(24.0, definition);
        provider.EvaluateTrainPose(24.0, definition);

        Assert.True(readsAfterCompilation > 0);
        Assert.Equal(readsAfterCompilation, curve.LengthReadCount);
    }

    [Fact]
    public void DocumentProviderPoseEvaluation_CompilesOneRuntimeSnapshotPerCall()
    {
        var curve = new CountingArcLengthLineCurve(30.0);
        var document = new TrackDocument(new[]
        {
            new StraightSegment(curve.Length, "counting", spline: curve)
        });
        var provider = new TrainCarTransformProvider(new TrackEvaluator(document));
        TrainConsistDefinition definition = CreateConsistDefinition();
        int readsBeforePoseEvaluation = curve.LengthReadCount;

        provider.EvaluateTrainPose(24.0, definition);
        Assert.Equal(readsBeforePoseEvaluation + 1, curve.LengthReadCount);

        provider.EvaluateTrainPose(24.0, definition);
        Assert.Equal(readsBeforePoseEvaluation + 2, curve.LengthReadCount);
    }

    [Fact]
    public void InvalidTrainDistances_DocumentAndRuntimeProvidersThrowEquivalentExceptions()
    {
        ProviderPair pair = CreateProviderPair();
        Action<TrainCarTransformProvider>[] operations =
        {
            provider => provider.EvaluateCarTransforms(double.NaN, 6.0, 3),
            provider => provider.EvaluateCarTransforms(31.0, 6.0, 3),
            provider => provider.EvaluateCarTransforms(5.0, 6.0, 2),
            provider => provider.EvaluateTrainWithBogies(29.5, 1, 6.0, 2.0),
            provider => provider.EvaluateTrainWithBogies(0.5, 1, 6.0, 2.0)
        };

        foreach (Action<TrainCarTransformProvider> operation in operations)
        {
            Exception expected = Assert.ThrowsAny<Exception>(() => operation(pair.Document));
            Exception actual = Assert.ThrowsAny<Exception>(() => operation(pair.Runtime));

            Assert.Equal(expected.GetType(), actual.GetType());
            Assert.Equal(expected.Message, actual.Message);
            Assert.Equal((expected as ArgumentException)?.ParamName, (actual as ArgumentException)?.ParamName);
        }
    }

    [Fact]
    public void AuthoringProofExports_RemainByteEquivalentToDocumentBoundTrainEvaluation()
    {
        AuthoringPipelineProofScenario authoring = AuthoringPipelineProofScenario.CreateDeterministic();
        AssertProofExportParity(
            AuthoringPipelineProofScenario.FixtureName,
            authoring.Compilation,
            authoring.Frames,
            authoring.TrainPose,
            authoring.TrainPoseExport,
            authoring.Snapshot);

        TransitionAuthoringProofScenario transition = TransitionAuthoringProofScenario.CreateDeterministic();
        AssertProofExportParity(
            TransitionAuthoringProofScenario.FixtureName,
            transition.Compilation,
            transition.Frames,
            transition.TrainPose,
            transition.TrainPoseExport,
            transition.Snapshot);
    }

    [Fact]
    public void TrainCarTransformProvider_PublicConstructionSurfaceRemainsEvaluatorOnly()
    {
        ConstructorInfo constructor = Assert.Single(typeof(TrainCarTransformProvider).GetConstructors());
        ParameterInfo parameter = Assert.Single(constructor.GetParameters());

        Assert.Equal(typeof(TrackEvaluator), parameter.ParameterType);
        Assert.DoesNotContain(
            typeof(TrainCarTransformProvider).GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.ReturnType == typeof(TrainCarTransformProvider));
    }

    private static ProviderPair CreateProviderPair()
    {
        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(
            new TrackAuthoringDefinition(new GeometricSectionDefinition[]
            {
                new StraightSectionDefinition("entry", 6.0),
                new CurvatureTransitionSectionDefinition(
                    "transition-in",
                    6.0,
                    0.0,
                    0.05,
                    rollRadians: 0.1),
                new ConstantCurvatureSectionDefinition("arc", 6.0, 20.0, 0.2),
                new CurvatureTransitionSectionDefinition(
                    "transition-out",
                    6.0,
                    0.05,
                    0.0,
                    rollRadians: -0.1),
                new StraightSectionDefinition("exit", 6.0)
            }));

        return new ProviderPair(
            compilation,
            new TrainCarTransformProvider(new TrackEvaluator(compilation.Document)),
            new TrainCarTransformProvider(new TrackEvaluator(compilation.Runtime)));
    }

    private static TrainConsistDefinition CreateConsistDefinition()
    {
        return new TrainConsistDefinition(
            carCount: 3,
            carSpacing: 6.0,
            carLength: 5.0,
            carWidth: 1.8,
            carHeight: 2.2,
            bogieSpacing: 4.0,
            wheelLayout: new TrainWheelLayout(
                wheelCountPerBogie: 4,
                wheelRadius: 0.45,
                wheelWidth: 0.3,
                axleSpacing: 1.2));
    }

    private static BankingProfile CreateBankingProfile(double totalLength)
    {
        return new BankingProfile(new[]
        {
            new BankingProfileKey(0.0, 0.0, BankingProfileInterpolationMode.Linear),
            new BankingProfileKey(15.0, 0.35, BankingProfileInterpolationMode.SmoothStep),
            new BankingProfileKey(totalLength, -0.2, BankingProfileInterpolationMode.Constant)
        });
    }

    private static TrackDocument CreateMutableDocument()
    {
        return new TrackDocument(new TrackSegment[]
        {
            CreateLineSegment("s0", 0.0, 10.0),
            CreateLineSegment("s1", 10.0, 20.0),
            CreateLineSegment("s2", 20.0, 30.0)
        });
    }

    private static StraightSegment CreateLineSegment(string id, double startX, double endX)
    {
        var curve = new LineCurve(
            new Vector3d(startX, 0.0, 0.0),
            new Vector3d(endX, 0.0, 0.0));
        return new StraightSegment(curve.Length, id, spline: curve);
    }

    private static void ReplaceWithShiftedSegment(TrackDocument document)
    {
        document.Segments.Clear();
        document.Segments.Add(CreateLineSegment("replacement", 100.0, 130.0));
    }

    private static void AssertProofExportParity(
        string fixtureName,
        TrackAuthoringCompilation compilation,
        IReadOnlyList<TrackFrame> runtimeFrames,
        TrainPoseResult runtimePose,
        TrainPoseExportV1Dto runtimePoseExport,
        DebugViewportSnapshotV1Dto runtimeSnapshot)
    {
        var evaluator = new TrackEvaluator(compilation.Document);
        var provider = new TrainCarTransformProvider(evaluator);
        double[] distances = runtimeFrames.Select(frame => frame.Distance).ToArray();
        TrackFrame[] documentFrames = evaluator.EvaluateFramesAtDistances(distances);
        TrainPoseResult documentPose = provider.EvaluateTrainPose(
            runtimePose.LeadDistance,
            runtimePose.Definition);
        TrainPoseExportV1Dto documentPoseExport = TrainPoseExportV1Mapper.Export(documentPose);
        var source = new DebugViewportSnapshotV1Source
        {
            Units = "meters",
            SourceFixtureName = fixtureName,
            SampledFrames = documentFrames,
            Lines = TrackFrameDebugGizmoBuilder.BuildAxes(documentFrames[documentFrames.Length / 2], 4.0),
            Boxes = BuildTrainBodyBoxes(documentPose),
            TrainPose = documentPose
        };
        DebugViewportSnapshotV1Dto documentSnapshot = DebugViewportSnapshotV1Mapper.Export(source);

        Assert.Equal(
            TrainPoseExportV1Json.Serialize(documentPoseExport, indented: true),
            TrainPoseExportV1Json.Serialize(runtimePoseExport, indented: true));
        Assert.Equal(
            DebugViewportSnapshotV1Json.Serialize(documentSnapshot, indented: true),
            DebugViewportSnapshotV1Json.Serialize(runtimeSnapshot, indented: true));
    }

    private static DebugViewportBoxSource[] BuildTrainBodyBoxes(TrainPoseResult trainPose)
    {
        var boxes = new DebugViewportBoxSource[trainPose.CarsReadOnly.Count];
        TrainCarGeometry geometry = trainPose.Definition.CarGeometry;

        for (int i = 0; i < boxes.Length; i++)
        {
            boxes[i] = new DebugViewportBoxSource(
                DebugViewportSnapshotV1Vocabulary.TrainBodyRole,
                "car-" + i,
                trainPose.CarsReadOnly[i].Body.ArticulatedFrame,
                geometry.Length,
                geometry.Width,
                geometry.Height);
        }

        return boxes;
    }

    private static void AssertPoseJsonEqual(TrainPoseResult expected, TrainPoseResult actual)
    {
        Assert.Equal(SerializePose(expected), SerializePose(actual));
    }

    private static string SerializePose(TrainPoseResult pose)
    {
        return TrainPoseExportV1Json.Serialize(TrainPoseExportV1Mapper.Export(pose), indented: false);
    }

    private static void AssertCarTransformsNear(
        IReadOnlyList<TrainCarTransform> expected,
        IReadOnlyList<TrainCarTransform> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            AssertCarTransformNear(expected[i], actual[i]);
        }
    }

    private static void AssertCarsWithBogiesNear(
        IReadOnlyList<TrainCarWithBogiesTransform> expected,
        IReadOnlyList<TrainCarWithBogiesTransform> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            AssertCarTransformNear(expected[i].Body, actual[i].Body);
            AssertBogieTransformNear(expected[i].FrontBogie, actual[i].FrontBogie);
            AssertBogieTransformNear(expected[i].RearBogie, actual[i].RearBogie);
        }
    }

    private static void AssertCarsWithWheelsNear(
        IReadOnlyList<TrainCarWithBogiesAndWheelsTransform> expected,
        IReadOnlyList<TrainCarWithBogiesAndWheelsTransform> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            AssertCarTransformNear(expected[i].Body, actual[i].Body);
            AssertBogieWithWheelsNear(expected[i].FrontBogie, actual[i].FrontBogie);
            AssertBogieWithWheelsNear(expected[i].RearBogie, actual[i].RearBogie);
        }
    }

    private static void AssertArticulatedCarsNear(
        IReadOnlyList<ArticulatedTrainCarTransform> expected,
        IReadOnlyList<ArticulatedTrainCarTransform> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            AssertArticulatedCarNear(expected[i], actual[i]);
        }
    }

    private static void AssertArticulatedCarsWithWheelsNear(
        IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> expected,
        IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            AssertArticulatedCarNear(expected[i].Body, actual[i].Body);
            AssertBogieWithWheelsNear(expected[i].FrontBogie, actual[i].FrontBogie);
            AssertBogieWithWheelsNear(expected[i].RearBogie, actual[i].RearBogie);
        }
    }

    private static void AssertCarTransformNear(TrainCarTransform expected, TrainCarTransform actual)
    {
        Assert.Equal(expected.CarIndex, actual.CarIndex);
        AssertNear(expected.Distance, actual.Distance);
        AssertFrameNear(expected.Frame, actual.Frame);
        AssertMatrixNear(expected.Matrix, actual.Matrix);
    }

    private static void AssertBogieTransformNear(BogieTransform expected, BogieTransform actual)
    {
        Assert.Equal(expected.CarIndex, actual.CarIndex);
        Assert.Equal(expected.BogieIndex, actual.BogieIndex);
        AssertNear(expected.Distance, actual.Distance);
        AssertFrameNear(expected.Frame, actual.Frame);
        AssertMatrixNear(expected.Matrix, actual.Matrix);
    }

    private static void AssertBogieWithWheelsNear(
        TrainBogieWithWheelsTransform expected,
        TrainBogieWithWheelsTransform actual)
    {
        AssertBogieTransformNear(expected.Bogie, actual.Bogie);
        Assert.Equal(expected.WheelsReadOnly.Count, actual.WheelsReadOnly.Count);

        for (int i = 0; i < expected.WheelsReadOnly.Count; i++)
        {
            WheelTransform expectedWheel = expected.WheelsReadOnly[i];
            WheelTransform actualWheel = actual.WheelsReadOnly[i];
            Assert.Equal(expectedWheel.CarIndex, actualWheel.CarIndex);
            Assert.Equal(expectedWheel.BogieIndex, actualWheel.BogieIndex);
            Assert.Equal(expectedWheel.WheelIndex, actualWheel.WheelIndex);
            AssertNear(expectedWheel.LocalOffsetX, actualWheel.LocalOffsetX);
            AssertNear(expectedWheel.LocalOffsetY, actualWheel.LocalOffsetY);
            AssertNear(expectedWheel.LocalOffsetZ, actualWheel.LocalOffsetZ);
            AssertFrameNear(expectedWheel.Frame, actualWheel.Frame);
            AssertMatrixNear(expectedWheel.Matrix, actualWheel.Matrix);
        }
    }

    private static void AssertArticulatedCarNear(
        ArticulatedTrainCarTransform expected,
        ArticulatedTrainCarTransform actual)
    {
        AssertCarTransformNear(expected.OriginalBody, actual.OriginalBody);
        AssertBogieTransformNear(expected.FrontBogie, actual.FrontBogie);
        AssertBogieTransformNear(expected.RearBogie, actual.RearBogie);
        AssertFrameNear(expected.ArticulatedFrame, actual.ArticulatedFrame);
        AssertMatrixNear(expected.ArticulatedMatrix, actual.ArticulatedMatrix);
        AssertNear(expected.CenterDistance, actual.CenterDistance);
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

    private static void AssertMatrixNear(Matrix4x4 expected, Matrix4x4 actual)
    {
        AssertNear(expected.M11, actual.M11); AssertNear(expected.M12, actual.M12);
        AssertNear(expected.M13, actual.M13); AssertNear(expected.M14, actual.M14);
        AssertNear(expected.M21, actual.M21); AssertNear(expected.M22, actual.M22);
        AssertNear(expected.M23, actual.M23); AssertNear(expected.M24, actual.M24);
        AssertNear(expected.M31, actual.M31); AssertNear(expected.M32, actual.M32);
        AssertNear(expected.M33, actual.M33); AssertNear(expected.M34, actual.M34);
        AssertNear(expected.M41, actual.M41); AssertNear(expected.M42, actual.M42);
        AssertNear(expected.M43, actual.M43); AssertNear(expected.M44, actual.M44);
    }

    private static void AssertMatrixNear(Matrix4x4d expected, Matrix4x4d actual)
    {
        AssertNear(expected.M11, actual.M11); AssertNear(expected.M12, actual.M12);
        AssertNear(expected.M13, actual.M13); AssertNear(expected.M14, actual.M14);
        AssertNear(expected.M21, actual.M21); AssertNear(expected.M22, actual.M22);
        AssertNear(expected.M23, actual.M23); AssertNear(expected.M24, actual.M24);
        AssertNear(expected.M31, actual.M31); AssertNear(expected.M32, actual.M32);
        AssertNear(expected.M33, actual.M33); AssertNear(expected.M34, actual.M34);
        AssertNear(expected.M41, actual.M41); AssertNear(expected.M42, actual.M42);
        AssertNear(expected.M43, actual.M43); AssertNear(expected.M44, actual.M44);
    }

    private static void AssertNear(double expected, double actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, Tolerance);
    }

    private readonly struct ProviderPair
    {
        public ProviderPair(
            TrackAuthoringCompilation compilation,
            TrainCarTransformProvider document,
            TrainCarTransformProvider runtime)
        {
            Compilation = compilation;
            Document = document;
            Runtime = runtime;
        }

        public TrackAuthoringCompilation Compilation { get; }

        public TrainCarTransformProvider Document { get; }

        public TrainCarTransformProvider Runtime { get; }
    }

    private sealed class CountingArcLengthLineCurve : IArcLengthCurve
    {
        private readonly double _length;

        public CountingArcLengthLineCurve(double length)
        {
            _length = length;
        }

        public int LengthReadCount { get; private set; }

        public double Length
        {
            get
            {
                LengthReadCount++;
                return _length;
            }
        }

        public Vector3d Evaluate(double t) => new Vector3d(_length * t, 0.0, 0.0);

        public Vector3d Tangent(double t) => Vector3d.UnitX;

        public Vector3d EvaluateByLength(double s) => new Vector3d(s, 0.0, 0.0);

        public Vector3d TangentByLength(double s) => Vector3d.UnitX;
    }
}
