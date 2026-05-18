using System;
using System.Collections.Generic;
using System.Reflection;
using Quantum.IO.TrainPose.V1;
using Quantum.Math;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;

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
    public void TrackDocumentAndSegment_RemainCenterlineEvaluationBoundary()
    {
        AssertProperty(typeof(TrackDocument), nameof(TrackDocument.Segments), typeof(IList<TrackSegment>));
        AssertProperty(typeof(TrackDocument), nameof(TrackDocument.Sections), typeof(IList<TrackSection>));
        AssertProperty(typeof(TrackDocument), nameof(TrackDocument.TotalLength), typeof(double));

        AssertProperty(typeof(TrackSegment), nameof(TrackSegment.Length), typeof(double));
        AssertProperty(typeof(TrackSegment), nameof(TrackSegment.Id), typeof(string));
        AssertProperty(typeof(TrackSegment), nameof(TrackSegment.ForceSegmentReference), typeof(string));
        AssertProperty(typeof(TrackSegment), nameof(TrackSegment.RollRadians), typeof(double));
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

    private static bool IsSplineType(Type type)
    {
        return string.Equals(type.Namespace, "Quantum.Splines", StringComparison.Ordinal) ||
               (type.Namespace?.StartsWith("Quantum.Splines.", StringComparison.Ordinal) ?? false);
    }
}
