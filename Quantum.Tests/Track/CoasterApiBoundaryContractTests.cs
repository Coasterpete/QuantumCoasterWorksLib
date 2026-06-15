using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Quantum.Debug;
using Quantum.IO.DebugViewport.V1;
using Quantum.IO.TrainPose.V1;
using Quantum.Math;
using Quantum.Physics;
using Quantum.Track;
using Quantum.Track.Authoring;
using ExportTrackFrame = Quantum.Track.TrackFrame;
using SplineTrackFrame = Quantum.Splines.TrackFrame;

#pragma warning disable CS0618

namespace Quantum.Tests;

public sealed class CoasterApiBoundaryContractTests
{
    [Fact]
    public void TrackFrame_IsTheCoasterFrameContract()
    {
        Type frameType = typeof(ExportTrackFrame);

        Assert.Equal("Quantum.Track", frameType.Namespace);
        Assert.True(frameType.IsValueType);
        AssertProperty(frameType, nameof(ExportTrackFrame.Distance), typeof(double));
        AssertProperty(frameType, nameof(ExportTrackFrame.Position), typeof(Vector3d));
        AssertProperty(frameType, nameof(ExportTrackFrame.Tangent), typeof(Vector3d));
        AssertProperty(frameType, nameof(ExportTrackFrame.Normal), typeof(Vector3d));
        AssertProperty(frameType, nameof(ExportTrackFrame.Binormal), typeof(Vector3d));

        AssertMethod(
            frameType,
            nameof(ExportTrackFrame.ToMatrix4x4),
            typeof(System.Numerics.Matrix4x4),
            Type.EmptyTypes);
    }

    [Fact]
    public void TrackEvaluator_BoundStationDistanceSampling_ReturnsCoasterTrackFrames()
    {
        Type evaluatorType = typeof(TrackEvaluator);

        AssertConstructor(evaluatorType, typeof(TrackDocument));
        AssertMethod(
            evaluatorType,
            nameof(TrackEvaluator.EvaluateFrameAtDistance),
            typeof(ExportTrackFrame),
            typeof(double));
        AssertMethod(
            evaluatorType,
            nameof(TrackEvaluator.EvaluateFramesAtDistances),
            typeof(ExportTrackFrame[]),
            typeof(IReadOnlyList<double>));
        AssertMethod(
            evaluatorType,
            nameof(TrackEvaluator.EvaluateTrackFrameAtDistance),
            typeof(ExportTrackFrame),
            typeof(TrackDocument),
            typeof(double));
        AssertMethod(
            evaluatorType,
            nameof(TrackEvaluator.EvaluateTrackFramesAtDistances),
            typeof(ExportTrackFrame[]),
            typeof(TrackDocument),
            typeof(IReadOnlyList<double>));
        AssertMethod(
            evaluatorType,
            nameof(TrackEvaluator.GetBoundTrackTotalLength),
            typeof(double),
            Type.EmptyTypes);
        AssertMethod(
            evaluatorType,
            nameof(TrackEvaluator.EvaluateAtDistance),
            typeof(TrackEvaluationPoint),
            typeof(TrackDocument),
            typeof(double));
        AssertMethod(
            evaluatorType,
            nameof(TrackEvaluator.EvaluateAtDistances),
            typeof(TrackEvaluationPoint[]),
            typeof(TrackDocument),
            typeof(IReadOnlyList<double>));
    }

    [Fact]
    public void TrackEvaluator_SupportLayerFrameSampling_IsExplicitlyNamed()
    {
        Type evaluatorType = typeof(TrackEvaluator);

        MethodInfo scalarMethod = AssertMethod(
            evaluatorType,
            nameof(TrackEvaluator.EvaluateSplineFrameAtDistance),
            typeof(SplineTrackFrame),
            typeof(TrackDocument),
            typeof(double));
        MethodInfo batchMethod = AssertMethod(
            evaluatorType,
            nameof(TrackEvaluator.EvaluateSplineFramesAtDistances),
            typeof(SplineTrackFrame[]),
            typeof(TrackDocument),
            typeof(IReadOnlyList<double>));

        AssertObsolete(scalarMethod);
        AssertObsolete(batchMethod);
    }

    [Fact]
    public void PhysicsTrainDebugAndExportFrameApis_ExposeCanonicalTrackFrame()
    {
        AssertMethod(
            typeof(TrackPhysicsAdapter),
            nameof(TrackPhysicsAdapter.GetFrameAtDistance),
            typeof(ExportTrackFrame),
            typeof(TrackDocument),
            typeof(double));

        MethodInfo providerMethod = AssertMethod(
            typeof(ITrackFrameProvider),
            nameof(ITrackFrameProvider.TryGetFrameAtDistance),
            typeof(bool),
            typeof(double),
            typeof(ExportTrackFrame).MakeByRefType());

        AssertProperty(typeof(TrainFollowerState), nameof(TrainFollowerState.Frame), typeof(ExportTrackFrame));
        AssertProperty(typeof(TrainCarTransform), nameof(TrainCarTransform.Frame), typeof(ExportTrackFrame));
        AssertProperty(
            typeof(DebugViewportSnapshotV1Source),
            nameof(DebugViewportSnapshotV1Source.SampledFrames),
            typeof(IReadOnlyList<ExportTrackFrame>));
        AssertMethod(
            typeof(DebugTrackContinuousSampler),
            nameof(DebugTrackContinuousSampler.SampleContinuousFrames),
            typeof(ExportTrackFrame[]),
            typeof(TrackDocument),
            typeof(TrackEvaluator),
            typeof(IReadOnlyList<double>),
            typeof(int),
            typeof(int),
            typeof(double));

        AssertDoesNotExposeSplineType(providerMethod.ReturnType, providerMethod.Name);
        foreach (ParameterInfo parameter in providerMethod.GetParameters())
        {
            AssertDoesNotExposeSplineType(parameter.ParameterType, providerMethod.Name);
        }
    }

    [Fact]
    public void LegacyFrameAndTrainCompatibilityApis_AreObsolete()
    {
        AssertObsolete(typeof(SplineTrackFrame));
        AssertObsolete(typeof(Quantum.Splines.TrackFrameSampler));
        AssertObsolete(typeof(TransportedTrackFrameSampler));

        AssertObsolete(AssertMethod(
            typeof(TrackEvaluator),
            nameof(TrackEvaluator.EvaluateFrameAtDistance),
            typeof(SplineTrackFrame),
            typeof(TrackDocument),
            typeof(double)));
        AssertObsolete(AssertMethod(
            typeof(TrackEvaluator),
            nameof(TrackEvaluator.EvaluateFramesAtDistances),
            typeof(SplineTrackFrame[]),
            typeof(TrackDocument),
            typeof(IReadOnlyList<double>)));
        AssertObsolete(AssertMethod(
            typeof(TrainCarTransformProvider),
            nameof(TrainCarTransformProvider.GetCarTransforms),
            typeof(IReadOnlyList<TrainCarTransform>),
            typeof(double),
            typeof(double),
            typeof(int)));
    }

    [Fact]
    public void CoasterAssemblies_OnlyExposeSplineTrackFrameThroughObsoleteCompatibilityMembers()
    {
        Assembly[] assemblies =
        {
            typeof(TrackEvaluator).Assembly,
            typeof(TrackPhysicsAdapter).Assembly,
            typeof(DebugViewportSnapshotV1Source).Assembly,
            typeof(SamplingPerfCommand).Assembly
        };

        IEnumerable<MethodInfo> publicMethods = assemblies
            .SelectMany(assembly => assembly.GetExportedTypes())
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly));

        foreach (MethodInfo method in publicMethods)
        {
            bool exposesLegacySplineFrame = ContainsType(method.ReturnType, typeof(SplineTrackFrame)) ||
                method.GetParameters().Any(parameter => ContainsType(parameter.ParameterType, typeof(SplineTrackFrame)));

            if (exposesLegacySplineFrame)
            {
                AssertObsolete(method);
            }
        }
    }

    [Fact]
    public void TrackDocumentAndSegment_RemainCenterlineEvaluationBoundary()
    {
        AssertProperty(typeof(TrackDocument), nameof(TrackDocument.Segments), typeof(IList<TrackSegment>));
        AssertProperty(typeof(TrackDocument), nameof(TrackDocument.Sections), typeof(IList<TrackSection>));
        AssertProperty(typeof(TrackDocument), nameof(TrackDocument.StartPose), typeof(TrackStartPose));
        AssertProperty(typeof(TrackDocument), nameof(TrackDocument.TotalLength), typeof(double));

        AssertProperty(typeof(TrackSegment), nameof(TrackSegment.Length), typeof(double));
        AssertProperty(typeof(TrackSegment), nameof(TrackSegment.Id), typeof(string));
        AssertProperty(typeof(TrackSegment), nameof(TrackSegment.ForceSegmentReference), typeof(string));
        AssertProperty(typeof(TrackSegment), nameof(TrackSegment.RollRadians), typeof(double));
    }

    [Fact]
    public void TrackAuthoringApis_ExposeNoSplineUiOrEngineTypes()
    {
        Type[] authoringTypes =
        {
            typeof(TrackStartPose),
            typeof(TrackAuthoringDefinition),
            typeof(GeometricSectionDefinition),
            typeof(StraightSectionDefinition),
            typeof(ConstantCurvatureSectionDefinition),
            typeof(CurvatureTransitionSectionDefinition),
            typeof(SpatialSectionDefinition),
            typeof(CurvatureTransitionInterpolationMode),
            typeof(TrackAuthoringCompilation),
            typeof(TrackAuthoringDocumentBuilder),
            typeof(TrackAuthoringBoundaryContinuityDiagnostics),
            typeof(TrackAuthoringBoundaryContinuityTolerances),
            typeof(TrackAuthoringBoundaryContinuityDiagnosticKind),
            typeof(TrackAuthoringBoundaryContinuityBoundary),
            typeof(TrackAuthoringBoundaryContinuityDiagnostic),
            typeof(TrackAuthoringBoundaryContinuityReport),
            typeof(TrackAuthoringGeometryContinuityDiagnostics),
            typeof(TrackAuthoringGeometryContinuityTolerances),
            typeof(TrackAuthoringGeometryContinuityDiagnosticKind),
            typeof(TrackAuthoringGeometryContinuityBoundary),
            typeof(TrackAuthoringGeometryContinuityDiagnostic),
            typeof(TrackAuthoringGeometryContinuityReport)
        };

        foreach (Type type in authoringTypes)
        {
            foreach (ConstructorInfo constructor in type.GetConstructors())
            {
                foreach (ParameterInfo parameter in constructor.GetParameters())
                {
                    AssertDoesNotExposeAuthoringFrameworkType(parameter.ParameterType, constructor.Name);
                }
            }

            foreach (PropertyInfo property in type.GetProperties(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly))
            {
                AssertDoesNotExposeAuthoringFrameworkType(property.PropertyType, property.Name);
            }

            foreach (MethodInfo method in type.GetMethods(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly))
            {
                AssertDoesNotExposeAuthoringFrameworkType(method.ReturnType, method.Name);
                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    AssertDoesNotExposeAuthoringFrameworkType(parameter.ParameterType, method.Name);
                }
            }
        }
    }

    [Fact]
    public void TrackSpatialAuthoring_ExposesBackendPoseContract()
    {
        Assert.True(typeof(TrackStartPose).IsSealed);
        AssertConstructor(
            typeof(TrackStartPose),
            typeof(Vector3d),
            typeof(Vector3d),
            typeof(Vector3d),
            typeof(Vector3d));
        AssertProperty(typeof(TrackStartPose), nameof(TrackStartPose.Position), typeof(Vector3d));
        AssertProperty(typeof(TrackStartPose), nameof(TrackStartPose.Tangent), typeof(Vector3d));
        AssertProperty(typeof(TrackStartPose), nameof(TrackStartPose.Normal), typeof(Vector3d));
        AssertProperty(typeof(TrackStartPose), nameof(TrackStartPose.Binormal), typeof(Vector3d));

        PropertyInfo? identity = typeof(TrackStartPose).GetProperty(
            nameof(TrackStartPose.Identity),
            BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(identity);
        Assert.Equal(typeof(TrackStartPose), identity.PropertyType);

        AssertConstructor(
            typeof(TrackAuthoringDefinition),
            typeof(IEnumerable<GeometricSectionDefinition>));
        AssertConstructor(
            typeof(TrackAuthoringDefinition),
            typeof(IEnumerable<GeometricSectionDefinition>),
            typeof(TrackStartPose));
        AssertProperty(
            typeof(TrackAuthoringDefinition),
            nameof(TrackAuthoringDefinition.StartPose),
            typeof(TrackStartPose));
    }

    [Fact]
    public void TrackAuthoringBoundaryContinuityDiagnostics_ExposeBackendScalarContract()
    {
        Assert.Equal(
            new[]
            {
                TrackAuthoringBoundaryContinuityDiagnosticKind.CurvatureDiscontinuity,
                TrackAuthoringBoundaryContinuityDiagnosticKind.RollDiscontinuity
            },
            Enum.GetValues<TrackAuthoringBoundaryContinuityDiagnosticKind>());

        AssertConstructor(
            typeof(TrackAuthoringBoundaryContinuityTolerances),
            typeof(double),
            typeof(double));
        AssertProperty(
            typeof(TrackAuthoringBoundaryContinuityTolerances),
            nameof(TrackAuthoringBoundaryContinuityTolerances.CurvatureTolerance),
            typeof(double));
        AssertProperty(
            typeof(TrackAuthoringBoundaryContinuityTolerances),
            nameof(TrackAuthoringBoundaryContinuityTolerances.RollToleranceRadians),
            typeof(double));

        AssertProperty(
            typeof(TrackAuthoringBoundaryContinuityBoundary),
            nameof(TrackAuthoringBoundaryContinuityBoundary.PreviousSectionId),
            typeof(string));
        AssertProperty(
            typeof(TrackAuthoringBoundaryContinuityBoundary),
            nameof(TrackAuthoringBoundaryContinuityBoundary.NextSectionId),
            typeof(string));
        AssertProperty(
            typeof(TrackAuthoringBoundaryContinuityBoundary),
            nameof(TrackAuthoringBoundaryContinuityBoundary.Station),
            typeof(double));
        AssertProperty(
            typeof(TrackAuthoringBoundaryContinuityBoundary),
            nameof(TrackAuthoringBoundaryContinuityBoundary.CurvatureDelta),
            typeof(double));
        AssertProperty(
            typeof(TrackAuthoringBoundaryContinuityBoundary),
            nameof(TrackAuthoringBoundaryContinuityBoundary.RollDeltaRadians),
            typeof(double));

        AssertProperty(
            typeof(TrackAuthoringBoundaryContinuityDiagnostic),
            nameof(TrackAuthoringBoundaryContinuityDiagnostic.Kind),
            typeof(TrackAuthoringBoundaryContinuityDiagnosticKind));
        AssertProperty(
            typeof(TrackAuthoringBoundaryContinuityDiagnostic),
            nameof(TrackAuthoringBoundaryContinuityDiagnostic.Boundary),
            typeof(TrackAuthoringBoundaryContinuityBoundary));
        AssertProperty(
            typeof(TrackAuthoringBoundaryContinuityDiagnostic),
            nameof(TrackAuthoringBoundaryContinuityDiagnostic.Delta),
            typeof(double));
        AssertProperty(
            typeof(TrackAuthoringBoundaryContinuityDiagnostic),
            nameof(TrackAuthoringBoundaryContinuityDiagnostic.Tolerance),
            typeof(double));

        AssertProperty(
            typeof(TrackAuthoringBoundaryContinuityReport),
            nameof(TrackAuthoringBoundaryContinuityReport.Boundaries),
            typeof(IReadOnlyList<TrackAuthoringBoundaryContinuityBoundary>));
        AssertProperty(
            typeof(TrackAuthoringBoundaryContinuityReport),
            nameof(TrackAuthoringBoundaryContinuityReport.Diagnostics),
            typeof(IReadOnlyList<TrackAuthoringBoundaryContinuityDiagnostic>));
        AssertProperty(
            typeof(TrackAuthoringBoundaryContinuityReport),
            nameof(TrackAuthoringBoundaryContinuityReport.Tolerances),
            typeof(TrackAuthoringBoundaryContinuityTolerances));

        AssertMethod(
            typeof(TrackAuthoringBoundaryContinuityDiagnostics),
            nameof(TrackAuthoringBoundaryContinuityDiagnostics.Analyze),
            typeof(TrackAuthoringBoundaryContinuityReport),
            typeof(TrackAuthoringDefinition));
        AssertMethod(
            typeof(TrackAuthoringBoundaryContinuityDiagnostics),
            nameof(TrackAuthoringBoundaryContinuityDiagnostics.Analyze),
            typeof(TrackAuthoringBoundaryContinuityReport),
            typeof(TrackAuthoringDefinition),
            typeof(TrackAuthoringBoundaryContinuityTolerances));
    }

    [Fact]
    public void CurvatureTransitionAuthoring_ExposesPublicScalarContract()
    {
        Assert.True(typeof(CurvatureTransitionSectionDefinition).IsPublic);
        Assert.True(typeof(CurvatureTransitionInterpolationMode).IsPublic);
        Assert.Equal(
            new[] { CurvatureTransitionInterpolationMode.Linear },
            Enum.GetValues<CurvatureTransitionInterpolationMode>());

        AssertConstructor(
            typeof(CurvatureTransitionSectionDefinition),
            typeof(string),
            typeof(double),
            typeof(double),
            typeof(double),
            typeof(CurvatureTransitionInterpolationMode),
            typeof(double));
        AssertProperty(
            typeof(CurvatureTransitionSectionDefinition),
            nameof(CurvatureTransitionSectionDefinition.StartCurvature),
            typeof(double));
        AssertProperty(
            typeof(CurvatureTransitionSectionDefinition),
            nameof(CurvatureTransitionSectionDefinition.EndCurvature),
            typeof(double));
        AssertProperty(
            typeof(CurvatureTransitionSectionDefinition),
            nameof(CurvatureTransitionSectionDefinition.InterpolationMode),
            typeof(CurvatureTransitionInterpolationMode));
    }

    [Fact]
    public void TrackAuthoringCompilation_ExposesDeterministicCompilationContract()
    {
        AssertProperty(
            typeof(TrackAuthoringCompilation),
            nameof(TrackAuthoringCompilation.Definition),
            typeof(TrackAuthoringDefinition));
        AssertProperty(
            typeof(TrackAuthoringCompilation),
            nameof(TrackAuthoringCompilation.Document),
            typeof(TrackDocument));
        AssertProperty(
            typeof(TrackAuthoringCompilation),
            nameof(TrackAuthoringCompilation.Runtime),
            typeof(CompiledTrackRuntime));
        AssertProperty(
            typeof(TrackAuthoringCompilation),
            nameof(TrackAuthoringCompilation.ResolvedSections),
            typeof(IReadOnlyList<ResolvedSectionInterval<GeometricSectionDefinition>>));
        AssertProperty(
            typeof(TrackAuthoringCompilation),
            nameof(TrackAuthoringCompilation.TotalLength),
            typeof(double));

        AssertMethod(
            typeof(TrackAuthoringDocumentBuilder),
            nameof(TrackAuthoringDocumentBuilder.Compile),
            typeof(TrackAuthoringCompilation),
            typeof(TrackAuthoringDefinition));
        AssertMethod(
            typeof(TrackAuthoringGeometryContinuityDiagnostics),
            nameof(TrackAuthoringGeometryContinuityDiagnostics.Analyze),
            typeof(TrackAuthoringGeometryContinuityReport),
            typeof(TrackAuthoringCompilation));
        AssertMethod(
            typeof(TrackAuthoringGeometryContinuityDiagnostics),
            nameof(TrackAuthoringGeometryContinuityDiagnostics.Analyze),
            typeof(TrackAuthoringGeometryContinuityReport),
            typeof(TrackAuthoringCompilation),
            typeof(TrackAuthoringGeometryContinuityTolerances));
    }

    [Fact]
    public void EvaluateTrainPose_RemainsTheTrainPoseEntryPoint()
    {
        AssertMethod(
            typeof(TrainCarTransformProvider),
            nameof(TrainCarTransformProvider.EvaluateTrainPose),
            typeof(TrainPoseResult),
            typeof(double),
            typeof(TrainConsistDefinition));
        AssertMethod(
            typeof(TrainCarTransformProvider),
            nameof(TrainCarTransformProvider.EvaluateTrainPose),
            typeof(TrainPoseResult),
            typeof(double),
            typeof(TrainConsistDefinition),
            typeof(BankingProfile));
        AssertMethod(
            typeof(TrainCarTransformProvider),
            nameof(TrainCarTransformProvider.EvaluateCarTransforms),
            typeof(IReadOnlyList<TrainCarTransform>),
            typeof(double),
            typeof(double),
            typeof(int),
            typeof(BankingProfile));

        AssertProperty(typeof(TrainPoseResult), nameof(TrainPoseResult.LeadDistance), typeof(double));
        AssertProperty(typeof(TrainPoseResult), nameof(TrainPoseResult.Definition), typeof(TrainConsistDefinition));
        AssertProperty(
            typeof(TrainPoseResult),
            nameof(TrainPoseResult.CarsReadOnly),
            typeof(IReadOnlyList<ArticulatedTrainCarWithWheelsTransform>));
    }

    [Fact]
    public void TrainPoseExportV1_RemainsTheVersionedJsonBoundary()
    {
        Assert.Equal("quantum.train_pose", TrainPoseExportV1Dto.ContractName);
        Assert.Equal(1, TrainPoseExportV1Dto.ContractVersion);

        AssertMethod(
            typeof(TrainPoseExportV1Mapper),
            nameof(TrainPoseExportV1Mapper.Export),
            typeof(TrainPoseExportV1Dto),
            typeof(TrainPoseResult));
        AssertMethod(
            typeof(TrainPoseExportV1Json),
            nameof(TrainPoseExportV1Json.Serialize),
            typeof(string),
            typeof(TrainPoseExportV1Dto),
            typeof(bool));
        AssertMethod(
            typeof(TrainPoseExportV1Json),
            nameof(TrainPoseExportV1Json.Deserialize),
            typeof(TrainPoseExportV1Dto),
            typeof(string));
    }

    [Fact]
    public void TrainPoseAndExportEntryPoints_DoNotExposeSplineTypes()
    {
        MethodInfo[] publicBoundaryMethods =
        {
            AssertMethod(
                typeof(TrackEvaluator),
                nameof(TrackEvaluator.EvaluateFrameAtDistance),
                typeof(ExportTrackFrame),
                typeof(double)),
            AssertMethod(
                typeof(TrackEvaluator),
                nameof(TrackEvaluator.EvaluateFramesAtDistances),
                typeof(ExportTrackFrame[]),
                typeof(IReadOnlyList<double>)),
            AssertMethod(
                typeof(TrainCarTransformProvider),
                nameof(TrainCarTransformProvider.EvaluateTrainPose),
                typeof(TrainPoseResult),
                typeof(double),
                typeof(TrainConsistDefinition)),
            AssertMethod(
                typeof(TrainCarTransformProvider),
                nameof(TrainCarTransformProvider.EvaluateTrainPose),
                typeof(TrainPoseResult),
                typeof(double),
                typeof(TrainConsistDefinition),
                typeof(BankingProfile)),
            AssertMethod(
                typeof(TrainPoseExportV1Mapper),
                nameof(TrainPoseExportV1Mapper.Export),
                typeof(TrainPoseExportV1Dto),
                typeof(TrainPoseResult)),
            AssertMethod(
                typeof(TrainPoseExportV1Json),
                nameof(TrainPoseExportV1Json.Serialize),
                typeof(string),
                typeof(TrainPoseExportV1Dto),
                typeof(bool)),
            AssertMethod(
                typeof(TrainPoseExportV1Json),
                nameof(TrainPoseExportV1Json.Deserialize),
                typeof(TrainPoseExportV1Dto),
                typeof(string))
        };

        foreach (MethodInfo method in publicBoundaryMethods)
        {
            AssertDoesNotExposeSplineType(method.ReturnType, method.Name);

            foreach (ParameterInfo parameter in method.GetParameters())
            {
                AssertDoesNotExposeSplineType(parameter.ParameterType, method.Name);
            }
        }
    }

    private static void AssertConstructor(Type declaringType, params Type[] parameterTypes)
    {
        ConstructorInfo? constructor = declaringType.GetConstructor(parameterTypes);
        Assert.NotNull(constructor);
    }

    private static void AssertProperty(Type declaringType, string propertyName, Type expectedType)
    {
        PropertyInfo? property = declaringType.GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(property);
        Assert.Equal(expectedType, property.PropertyType);
    }

    private static MethodInfo AssertMethod(
        Type declaringType,
        string methodName,
        Type expectedReturnType,
        params Type[] parameterTypes)
    {
        MethodInfo? method = declaringType.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static,
            binder: null,
            types: parameterTypes,
            modifiers: null);

        Assert.NotNull(method);
        Assert.Equal(expectedReturnType, method.ReturnType);
        return method;
    }

    private static void AssertDoesNotExposeSplineType(Type type, string methodName)
    {
        Assert.False(
            IsSplineType(type),
            $"{methodName} exposes support-layer spline type {type.FullName}.");

        if (type.IsGenericType)
        {
            foreach (Type argumentType in type.GetGenericArguments())
            {
                AssertDoesNotExposeSplineType(argumentType, methodName);
            }
        }

        if (type.HasElementType)
        {
            Type? elementType = type.GetElementType();
            Assert.NotNull(elementType);
            AssertDoesNotExposeSplineType(elementType, methodName);
        }
    }

    private static void AssertDoesNotExposeAuthoringFrameworkType(Type type, string memberName)
    {
        string typeNamespace = type.Namespace ?? string.Empty;
        string[] forbiddenNamespacePrefixes =
        {
            "Quantum.Splines",
            "UnityEngine",
            "UnityEditor",
            "Avalonia",
            "Silk.NET",
            "OpenTK"
        };

        foreach (string prefix in forbiddenNamespacePrefixes)
        {
            Assert.False(
                string.Equals(typeNamespace, prefix, StringComparison.Ordinal) ||
                typeNamespace.StartsWith(prefix + ".", StringComparison.Ordinal),
                $"{memberName} exposes forbidden authoring dependency type {type.FullName}.");
        }

        if (type.IsGenericType)
        {
            foreach (Type argumentType in type.GetGenericArguments())
            {
                AssertDoesNotExposeAuthoringFrameworkType(argumentType, memberName);
            }
        }

        if (type.HasElementType)
        {
            Type? elementType = type.GetElementType();
            Assert.NotNull(elementType);
            AssertDoesNotExposeAuthoringFrameworkType(elementType, memberName);
        }
    }

    private static bool ContainsType(Type candidate, Type expected)
    {
        if (candidate == expected)
        {
            return true;
        }

        if (candidate.HasElementType)
        {
            Type? elementType = candidate.GetElementType();
            return elementType != null && ContainsType(elementType, expected);
        }

        return candidate.IsGenericType &&
            candidate.GetGenericArguments().Any(argument => ContainsType(argument, expected));
    }

    private static void AssertObsolete(MemberInfo member)
    {
        Assert.NotNull(member.GetCustomAttribute<ObsoleteAttribute>());
    }

    private static bool IsSplineType(Type type)
    {
        return string.Equals(type.Namespace, "Quantum.Splines", StringComparison.Ordinal) ||
               (type.Namespace?.StartsWith("Quantum.Splines.", StringComparison.Ordinal) ?? false);
    }
}

#pragma warning restore CS0618
