using System.Collections.Generic;
using System.Numerics;
using Quantum.Math;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class TrainCarTransformProviderCompositeDocumentTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void EvaluateTrainWithBogies_CompositeSectionDocument_SamplesAcrossBoundariesDeterministically()
    {
        TrackDocument document = GeometricSectionTrackDocumentBuilder.BuildZeroRollCompositeDocument(
            CreateZeroRollSections(),
            segmentId: "composite-train-regression");
        TrackSegment segment = Assert.Single(document.Segments);
        Assert.IsType<CompositeSectionCurve>(segment.Spline);
        Assert.Equal(3, document.Sections.Count);

        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        var definition = new TrainConsistDefinition(
            carCount: 2,
            carSpacing: 3.0,
            carLength: 2.0,
            carWidth: 1.2,
            carHeight: 1.4,
            bogieSpacing: 1.2);
        const double leadDistance = 8.5;

        IReadOnlyList<TrainCarWithBogiesTransform> cars = provider.EvaluateTrainWithBogies(
            leadDistance,
            definition);
        IReadOnlyList<TrainCarWithBogiesTransform> repeatedCars = provider.EvaluateTrainWithBogies(
            leadDistance,
            definition);

        Assert.Equal(definition.CarCount, cars.Count);
        Assert.Equal(definition.CarCount, repeatedCars.Count);
        AssertRequestedDistancesAreDeterministic(cars, repeatedCars);

        double[] expectedBodyDistances = { 8.5, 5.5 };
        double[] expectedFrontBogieDistances = { 9.1, 6.1 };
        double[] expectedRearBogieDistances = { 7.9, 4.9 };

        for (int i = 0; i < cars.Count; i++)
        {
            TrainCarWithBogiesTransform car = cars[i];

            AssertBodySampleMatchesTrackEvaluator(evaluator, car.Body, expectedBodyDistances[i]);
            AssertBogieSampleMatchesTrackEvaluator(evaluator, car.FrontBogie, expectedFrontBogieDistances[i]);
            AssertBogieSampleMatchesTrackEvaluator(evaluator, car.RearBogie, expectedRearBogieDistances[i]);
        }
    }

    private static GeometricSection[] CreateZeroRollSections()
    {
        return new[]
        {
            new GeometricSection(length: 5.0, curvature: 0.12, roll: 0.0),
            new GeometricSection(length: 3.0),
            new GeometricSection(length: 4.0, curvature: -0.08, roll: 0.0)
        };
    }

    private static void AssertRequestedDistancesAreDeterministic(
        IReadOnlyList<TrainCarWithBogiesTransform> expected,
        IReadOnlyList<TrainCarWithBogiesTransform> actual)
    {
        Assert.Equal(expected.Count, actual.Count);

        for (int i = 0; i < expected.Count; i++)
        {
            AssertDoubleNear(expected[i].Body.Distance, actual[i].Body.Distance);
            AssertDoubleNear(expected[i].FrontBogie.Distance, actual[i].FrontBogie.Distance);
            AssertDoubleNear(expected[i].RearBogie.Distance, actual[i].RearBogie.Distance);
        }
    }

    private static void AssertBodySampleMatchesTrackEvaluator(
        TrackEvaluator evaluator,
        TrainCarTransform body,
        double expectedDistance)
    {
        ExportTrackFrame expectedFrame = evaluator.EvaluateFrameAtDistance(expectedDistance);

        AssertDoubleNear(expectedDistance, body.Distance);
        AssertTrackFrameMatches(expectedFrame, body.Frame);
        AssertTrackFrameFinite(body.Frame);
        AssertTrackFrameOrthonormal(body.Frame);
        AssertFiniteMatrix(body.Matrix);
        AssertMatrixNear(expectedFrame.ToMatrix4x4(), body.Matrix);
    }

    private static void AssertBogieSampleMatchesTrackEvaluator(
        TrackEvaluator evaluator,
        BogieTransform bogie,
        double expectedDistance)
    {
        ExportTrackFrame expectedFrame = evaluator.EvaluateFrameAtDistance(expectedDistance);

        AssertDoubleNear(expectedDistance, bogie.Distance);
        AssertTrackFrameMatches(expectedFrame, bogie.Frame);
        AssertTrackFrameFinite(bogie.Frame);
        AssertTrackFrameOrthonormal(bogie.Frame);
        AssertFiniteMatrix(bogie.Matrix);
        AssertMatrixNear(Matrix4x4d.FromMatrix4x4(expectedFrame.ToMatrix4x4()), bogie.Matrix);
    }

    private static void AssertTrackFrameMatches(ExportTrackFrame expected, ExportTrackFrame actual)
    {
        AssertDoubleNear(expected.Distance, actual.Distance);
        AssertVectorNear(expected.Position, actual.Position);
        AssertVectorNear(expected.Tangent, actual.Tangent);
        AssertVectorNear(expected.Normal, actual.Normal);
        AssertVectorNear(expected.Binormal, actual.Binormal);
    }

    private static void AssertTrackFrameFinite(ExportTrackFrame frame)
    {
        AssertFinite(frame.Distance);
        AssertFiniteVector(frame.Position);
        AssertFiniteVector(frame.Tangent);
        AssertFiniteVector(frame.Normal);
        AssertFiniteVector(frame.Binormal);
    }

    private static void AssertTrackFrameOrthonormal(ExportTrackFrame frame)
    {
        AssertDoubleNear(1.0, frame.Tangent.Length);
        AssertDoubleNear(1.0, frame.Normal.Length);
        AssertDoubleNear(1.0, frame.Binormal.Length);
        AssertDoubleNear(0.0, Vector3d.Dot(frame.Tangent, frame.Normal));
        AssertDoubleNear(0.0, Vector3d.Dot(frame.Tangent, frame.Binormal));
        AssertDoubleNear(0.0, Vector3d.Dot(frame.Normal, frame.Binormal));
        AssertVectorNear(Vector3d.Cross(frame.Tangent, frame.Normal), frame.Binormal);
    }

    private static void AssertFiniteVector(Vector3d vector)
    {
        AssertFinite(vector.X);
        AssertFinite(vector.Y);
        AssertFinite(vector.Z);
    }

    private static void AssertFiniteMatrix(Matrix4x4 matrix)
    {
        AssertFinite(matrix.M11);
        AssertFinite(matrix.M12);
        AssertFinite(matrix.M13);
        AssertFinite(matrix.M14);
        AssertFinite(matrix.M21);
        AssertFinite(matrix.M22);
        AssertFinite(matrix.M23);
        AssertFinite(matrix.M24);
        AssertFinite(matrix.M31);
        AssertFinite(matrix.M32);
        AssertFinite(matrix.M33);
        AssertFinite(matrix.M34);
        AssertFinite(matrix.M41);
        AssertFinite(matrix.M42);
        AssertFinite(matrix.M43);
        AssertFinite(matrix.M44);
    }

    private static void AssertFiniteMatrix(Matrix4x4d matrix)
    {
        AssertFinite(matrix.M11);
        AssertFinite(matrix.M12);
        AssertFinite(matrix.M13);
        AssertFinite(matrix.M14);
        AssertFinite(matrix.M21);
        AssertFinite(matrix.M22);
        AssertFinite(matrix.M23);
        AssertFinite(matrix.M24);
        AssertFinite(matrix.M31);
        AssertFinite(matrix.M32);
        AssertFinite(matrix.M33);
        AssertFinite(matrix.M34);
        AssertFinite(matrix.M41);
        AssertFinite(matrix.M42);
        AssertFinite(matrix.M43);
        AssertFinite(matrix.M44);
    }

    private static void AssertMatrixNear(Matrix4x4 expected, Matrix4x4 actual)
    {
        AssertDoubleNear(expected.M11, actual.M11);
        AssertDoubleNear(expected.M12, actual.M12);
        AssertDoubleNear(expected.M13, actual.M13);
        AssertDoubleNear(expected.M14, actual.M14);
        AssertDoubleNear(expected.M21, actual.M21);
        AssertDoubleNear(expected.M22, actual.M22);
        AssertDoubleNear(expected.M23, actual.M23);
        AssertDoubleNear(expected.M24, actual.M24);
        AssertDoubleNear(expected.M31, actual.M31);
        AssertDoubleNear(expected.M32, actual.M32);
        AssertDoubleNear(expected.M33, actual.M33);
        AssertDoubleNear(expected.M34, actual.M34);
        AssertDoubleNear(expected.M41, actual.M41);
        AssertDoubleNear(expected.M42, actual.M42);
        AssertDoubleNear(expected.M43, actual.M43);
        AssertDoubleNear(expected.M44, actual.M44);
    }

    private static void AssertMatrixNear(Matrix4x4d expected, Matrix4x4d actual)
    {
        AssertDoubleNear(expected.M11, actual.M11);
        AssertDoubleNear(expected.M12, actual.M12);
        AssertDoubleNear(expected.M13, actual.M13);
        AssertDoubleNear(expected.M14, actual.M14);
        AssertDoubleNear(expected.M21, actual.M21);
        AssertDoubleNear(expected.M22, actual.M22);
        AssertDoubleNear(expected.M23, actual.M23);
        AssertDoubleNear(expected.M24, actual.M24);
        AssertDoubleNear(expected.M31, actual.M31);
        AssertDoubleNear(expected.M32, actual.M32);
        AssertDoubleNear(expected.M33, actual.M33);
        AssertDoubleNear(expected.M34, actual.M34);
        AssertDoubleNear(expected.M41, actual.M41);
        AssertDoubleNear(expected.M42, actual.M42);
        AssertDoubleNear(expected.M43, actual.M43);
        AssertDoubleNear(expected.M44, actual.M44);
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual)
    {
        AssertDoubleNear(expected.X, actual.X);
        AssertDoubleNear(expected.Y, actual.Y);
        AssertDoubleNear(expected.Z, actual.Z);
    }

    private static void AssertFinite(float value)
    {
        Assert.False(float.IsNaN(value));
        Assert.False(float.IsInfinity(value));
    }

    private static void AssertFinite(double value)
    {
        Assert.False(double.IsNaN(value));
        Assert.False(double.IsInfinity(value));
    }

    private static void AssertDoubleNear(double expected, double actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, Tolerance);
    }
}
