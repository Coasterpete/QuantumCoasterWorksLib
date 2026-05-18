using System.Collections.Generic;
using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;

namespace Quantum.Tests;

public sealed class GSharkTrainCarSpacingParityTests
{
    private const double DistanceTolerance = 1e-9;
    private const double FrameTolerance = 3e-5;

    [Fact]
    public void GetCarTransforms_WithGSharkNurbsAdapter_MatchesLegacyNurbsSpacingAndFrames()
    {
        List<Vector3d> controlPoints = CreateControlPoints();
        List<double> weights = CreateWeights();
        const int degree = 3;
        const double segmentLength = 40.0;
        const double rollRadians = 0.2;
        const double leadDistance = 30.0;
        const double carSpacing = 2.5;
        const int carCount = 6;

        IParamCurve legacyCurve = new NurbsCurve(controlPoints, weights, degree);
        IParamCurve gSharkCurve = new GSharkNurbsCurveAdapter(controlPoints, weights, degree);

        var legacyProvider = new TrainCarTransformProvider(new TrackEvaluator(
            BuildSingleSegmentDocument(segmentLength, rollRadians, legacyCurve)));
        var gSharkProvider = new TrainCarTransformProvider(new TrackEvaluator(
            BuildSingleSegmentDocument(segmentLength, rollRadians, gSharkCurve)));

        IReadOnlyList<TrainCarTransform> legacyCars = legacyProvider.GetCarTransforms(leadDistance, carSpacing, carCount);
        IReadOnlyList<TrainCarTransform> gSharkCars = gSharkProvider.GetCarTransforms(leadDistance, carSpacing, carCount);

        Assert.Equal(legacyCars.Count, gSharkCars.Count);

        for (int i = 0; i < legacyCars.Count; i++)
        {
            TrainCarTransform expected = legacyCars[i];
            TrainCarTransform actual = gSharkCars[i];

            Assert.Equal(expected.CarIndex, actual.CarIndex);
            Assert.InRange(System.Math.Abs(expected.Distance - actual.Distance), 0.0, DistanceTolerance);

            AssertVectorNear(expected.Frame.Position, actual.Frame.Position, FrameTolerance);
            AssertVectorNear(expected.Frame.Tangent, actual.Frame.Tangent, FrameTolerance);
            AssertVectorNear(expected.Frame.Normal, actual.Frame.Normal, FrameTolerance);
            AssertVectorNear(expected.Frame.Binormal, actual.Frame.Binormal, FrameTolerance);
        }
    }

    private static TrackDocument BuildSingleSegmentDocument(double length, double rollRadians, IParamCurve spline)
    {
        return new TrackDocument(new TrackSegment[]
        {
            new CurvedSegment(length, spline: spline, rollRadians: rollRadians)
        });
    }

    private static List<Vector3d> CreateControlPoints()
    {
        return new List<Vector3d>
        {
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(5.0, 3.0, 1.0),
            new Vector3d(10.0, -2.0, 2.0),
            new Vector3d(15.0, 0.0, 3.0)
        };
    }

    private static List<double> CreateWeights()
    {
        return new List<double> { 1.0, 0.9, 1.2, 1.0 };
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual, double tolerance)
    {
        Assert.InRange(System.Math.Abs(expected.X - actual.X), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(expected.Y - actual.Y), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(expected.Z - actual.Z), 0.0, tolerance);
    }
}
