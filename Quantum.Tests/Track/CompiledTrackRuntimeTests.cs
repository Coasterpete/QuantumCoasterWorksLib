using System.Reflection;
using Quantum.Math;
using Quantum.Physics;
using Quantum.Splines;
using Quantum.Track;
using Quantum.Track.Authoring;
using TrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class CompiledTrackRuntimeTests
{
    private const double Tolerance = 1e-8;

    [Fact]
    public void RuntimeScalarAndBatchFrames_MatchDocumentEvaluatorAcrossSamplingSemantics()
    {
        TrackDocument document = CreateMixedDocument();
        var currentEvaluator = new TrackEvaluator(document);
        var runtimeEvaluator = new TrackEvaluator(new CompiledTrackRuntime(document));
        double[] distances =
        {
            16.0,
            -5.0,
            0.0,
            2.5,
            5.0,
            9.0,
            13.0,
            15.0,
            17.0,
            19.0,
            21.0,
            99.0,
            9.0,
            2.5
        };

        TrackFrame[] runtimeBatch = runtimeEvaluator.EvaluateFramesAtDistances(distances);

        Assert.Equal(distances.Length, runtimeBatch.Length);
        for (int i = 0; i < distances.Length; i++)
        {
            TrackFrame expected = currentEvaluator.EvaluateFrameAtDistance(distances[i]);
            TrackFrame runtimeScalar = runtimeEvaluator.EvaluateFrameAtDistance(distances[i]);

            AssertFrameNear(expected, runtimeScalar);
            AssertFrameNear(expected, runtimeBatch[i]);
        }

        AssertFrameNear(runtimeBatch[5], runtimeBatch[12]);
        AssertFrameNear(runtimeBatch[3], runtimeBatch[13]);
    }

    [Fact]
    public void RuntimePointTangentTransformAndCurvature_MatchCurrentEvaluationPaths()
    {
        TrackDocument document = CreateMixedDocument();
        var currentEvaluator = new TrackEvaluator(document);
        var runtimeEvaluator = new TrackEvaluator(new CompiledTrackRuntime(document));
        var physicsAdapter = new TrackPhysicsAdapter();
        double[] distances = { -1.0, 0.0, 5.0, 8.5, 13.0, 15.0, 18.5, 25.0 };

        TrackEvaluationPoint[] runtimePoints = runtimeEvaluator.EvaluateAtDistances(distances);

        for (int i = 0; i < distances.Length; i++)
        {
            double distance = distances[i];
            TrackEvaluationPoint expectedPoint = new TrackEvaluator().EvaluateAtDistance(
                document,
                distance);
            TrackEvaluationPoint runtimePoint = runtimeEvaluator.EvaluateAtDistance(distance);
            TrackFrame expectedFrame = currentEvaluator.EvaluateFrameAtDistance(distance);
            TrackFrame runtimeFrame = runtimeEvaluator.EvaluateFrameAtDistance(distance);
            Transform3d expectedTransform = new TrackEvaluator().EvaluateTransformAtDistance(
                document,
                distance);
            Transform3d runtimeTransform = runtimeEvaluator.EvaluateTransformAtDistance(distance);

            Assert.Same(expectedPoint.Segment, runtimePoint.Segment);
            Assert.Same(expectedPoint.Segment, runtimePoints[i].Segment);
            AssertNear(expectedPoint.LocalT, runtimePoint.LocalT);
            AssertNear(expectedPoint.LocalT, runtimePoints[i].LocalT);
            AssertVectorNear(expectedFrame.Position, runtimeFrame.Position);
            AssertVectorNear(expectedFrame.Tangent, runtimeFrame.Tangent);
            AssertTransformNear(expectedTransform, runtimeTransform);

            Assert.True(physicsAdapter.TryGetCurvatureAtDistance(
                document,
                distance,
                out double expectedCurvature));
            Assert.True(runtimeEvaluator.TryGetCurvatureAtDistance(
                distance,
                out double runtimeCurvature));
            AssertNear(expectedCurvature, runtimeCurvature);
        }
    }

    [Fact]
    public void RuntimeBankingProfileFrames_MatchDocumentSampling()
    {
        TrackDocument document = CreateMixedDocument();
        var profile = new BankingProfile(new[]
        {
            new BankingProfileKey(0.0, 0.0, BankingProfileInterpolationMode.Linear),
            new BankingProfileKey(10.0, 0.4, BankingProfileInterpolationMode.SmoothStep),
            new BankingProfileKey(21.0, -0.2, BankingProfileInterpolationMode.Constant)
        });
        double[] distances = { 21.0, 0.0, 7.5, 13.0, 7.5, -2.0, 30.0 };

        TrackFrame[] expected = BankingProfileSampler.SampleFramesAtDistances(
            document,
            profile,
            distances);
        TrackFrame[] actual = BankingProfileSampler.SampleFramesAtDistances(
            new TrackEvaluator(new CompiledTrackRuntime(document)),
            profile,
            distances);

        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            AssertFrameNear(expected[i], actual[i]);
        }
    }

    [Fact]
    public void RuntimeMetadataAndSamples_RemainStableAfterDocumentSegmentMutation()
    {
        var originalCurve = new LineCurve(Vector3d.Zero, new Vector3d(5.0, 0.0, 0.0));
        var document = new TrackDocument(new[]
        {
            new StraightSegment(originalCurve.Length, "original", spline: originalCurve)
        });
        var runtime = new CompiledTrackRuntime(document);
        var runtimeEvaluator = new TrackEvaluator(runtime);
        var liveDocumentEvaluator = new TrackEvaluator(document);
        TrackFrame beforeMutation = runtimeEvaluator.EvaluateFrameAtDistance(2.0);

        document.Segments.Clear();
        var replacementCurve = new LineCurve(
            new Vector3d(100.0, 0.0, 0.0),
            new Vector3d(110.0, 0.0, 0.0));
        document.Segments.Add(new StraightSegment(
            replacementCurve.Length,
            "replacement",
            spline: replacementCurve));

        TrackFrame afterMutation = runtimeEvaluator.EvaluateFrameAtDistance(2.0);
        TrackFrame liveDocumentFrame = liveDocumentEvaluator.EvaluateFrameAtDistance(2.0);

        Assert.Equal(1, runtime.SegmentCount);
        AssertNear(5.0, runtime.TotalLength);
        AssertNear(5.0, runtimeEvaluator.GetBoundTrackTotalLength());
        AssertFrameNear(beforeMutation, afterMutation);
        AssertVectorNear(new Vector3d(102.0, 0.0, 0.0), liveDocumentFrame.Position);
    }

    [Fact]
    public void RuntimeEvaluator_RepeatedScalarSamplesDoNotRebuildCompiledState()
    {
        var curve = new CountingLineCurve(10.0);
        var document = new TrackDocument(new[]
        {
            new StraightSegment(10.0, spline: curve)
        });
        var evaluator = new TrackEvaluator(new CompiledTrackRuntime(document));

        evaluator.EvaluateFrameAtDistance(4.0);
        int evaluatesAfterFirstSample = curve.EvaluateCount;
        int tangentsAfterFirstSample = curve.TangentCount;

        evaluator.EvaluateFrameAtDistance(4.0);

        Assert.Equal(evaluatesAfterFirstSample + 1, curve.EvaluateCount);
        Assert.Equal(tangentsAfterFirstSample + 1, curve.TangentCount);
    }

    [Fact]
    public void RuntimeConstructors_RejectNullArgumentsClearly()
    {
        ArgumentNullException runtimeException = Assert.Throws<ArgumentNullException>(
            () => new CompiledTrackRuntime(null!));
        ArgumentNullException evaluatorException = Assert.Throws<ArgumentNullException>(
            () => new TrackEvaluator((CompiledTrackRuntime)null!));

        Assert.Equal("document", runtimeException.ParamName);
        Assert.Equal("runtime", evaluatorException.ParamName);
    }

    [Fact]
    public void RuntimeConstructor_RejectsInvalidDocumentStateClearly()
    {
        var document = new TrackDocument(new[]
        {
            new StraightSegment(0.0)
        });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new CompiledTrackRuntime(document));

        Assert.Contains("finite declared length greater than zero", exception.Message);
    }

    [Fact]
    public void RuntimePublicApi_ExposesOnlyBackendDomainTypes()
    {
        Type runtimeType = typeof(CompiledTrackRuntime);
        Assert.True(runtimeType.IsPublic);
        Assert.True(runtimeType.IsSealed);
        Assert.NotNull(runtimeType.GetConstructor(new[] { typeof(TrackDocument) }));
        Assert.Equal(typeof(double), runtimeType.GetProperty(nameof(CompiledTrackRuntime.TotalLength))!.PropertyType);
        Assert.Equal(typeof(int), runtimeType.GetProperty(nameof(CompiledTrackRuntime.SegmentCount))!.PropertyType);
        Assert.NotNull(typeof(TrackEvaluator).GetConstructor(new[] { runtimeType }));

        foreach (MemberInfo member in runtimeType.GetMembers(
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.DeclaredOnly))
        {
            switch (member)
            {
                case ConstructorInfo constructor:
                    foreach (ParameterInfo parameter in constructor.GetParameters())
                    {
                        AssertBackendType(parameter.ParameterType, member.Name);
                    }

                    break;

                case PropertyInfo property:
                    AssertBackendType(property.PropertyType, member.Name);
                    break;

                case MethodInfo method:
                    AssertBackendType(method.ReturnType, member.Name);
                    foreach (ParameterInfo parameter in method.GetParameters())
                    {
                        AssertBackendType(parameter.ParameterType, member.Name);
                    }

                    break;
            }
        }
    }

    private static TrackDocument CreateMixedDocument()
    {
        return TrackAuthoringDocumentBuilder.Build(
            new TrackAuthoringDefinition(new GeometricSectionDefinition[]
            {
                new StraightSectionDefinition("straight", 5.0),
                new CurvatureTransitionSectionDefinition("transition-in", 8.0, 0.0, 0.08, rollRadians: 0.1),
                new ConstantCurvatureSectionDefinition("arc", 4.0, 12.5, 0.1),
                new CurvatureTransitionSectionDefinition("transition-out", 4.0, 0.08, 0.0, rollRadians: -0.1)
            }));
    }

    private static void AssertBackendType(Type type, string memberName)
    {
        string typeNamespace = type.Namespace ?? string.Empty;
        string[] forbiddenPrefixes =
        {
            "Quantum.Splines",
            "UnityEngine",
            "UnityEditor",
            "Quantum.Editor",
            "Quantum.Rendering"
        };

        foreach (string prefix in forbiddenPrefixes)
        {
            Assert.False(
                string.Equals(typeNamespace, prefix, StringComparison.Ordinal) ||
                typeNamespace.StartsWith(prefix + ".", StringComparison.Ordinal),
                $"{memberName} exposes forbidden runtime dependency type {type.FullName}.");
        }

        if (type.IsGenericType)
        {
            foreach (Type argument in type.GetGenericArguments())
            {
                AssertBackendType(argument, memberName);
            }
        }

        if (type.HasElementType)
        {
            AssertBackendType(type.GetElementType()!, memberName);
        }
    }

    private static void AssertFrameNear(TrackFrame expected, TrackFrame actual)
    {
        AssertNear(expected.Distance, actual.Distance);
        AssertVectorNear(expected.Position, actual.Position);
        AssertVectorNear(expected.Tangent, actual.Tangent);
        AssertVectorNear(expected.Normal, actual.Normal);
        AssertVectorNear(expected.Binormal, actual.Binormal);
    }

    private static void AssertTransformNear(Transform3d expected, Transform3d actual)
    {
        AssertVectorNear(expected.Position, actual.Position);
        AssertVectorNear(
            expected.TransformDirection(Vector3d.UnitX),
            actual.TransformDirection(Vector3d.UnitX));
        AssertVectorNear(
            expected.TransformDirection(Vector3d.UnitY),
            actual.TransformDirection(Vector3d.UnitY));
        AssertVectorNear(
            expected.TransformDirection(Vector3d.UnitZ),
            actual.TransformDirection(Vector3d.UnitZ));
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

    private sealed class CountingLineCurve : IParamCurve
    {
        private readonly double _length;

        public CountingLineCurve(double length)
        {
            _length = length;
        }

        public int EvaluateCount { get; private set; }

        public int TangentCount { get; private set; }

        public Vector3d Evaluate(double t)
        {
            EvaluateCount++;
            return new Vector3d(_length * t, 0.0, 0.0);
        }

        public Vector3d Tangent(double t)
        {
            TangentCount++;
            return Vector3d.UnitX;
        }
    }
}
