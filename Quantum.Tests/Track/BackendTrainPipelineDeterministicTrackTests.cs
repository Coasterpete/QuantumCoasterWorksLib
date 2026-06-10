using System;
using System.Collections.Generic;
using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class BackendTrainPipelineDeterministicTrackTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void DeterministicTrackProfile_HasMeaningfulElevationAndCurvatureChange()
    {
        TrackDocument document = BuildDeterministicBackendVisualizerTrack();
        var evaluator = new TrackEvaluator(document);

        const int sampleCount = 129;
        double minY = double.PositiveInfinity;
        double maxY = double.NegativeInfinity;
        double minZ = double.PositiveInfinity;
        double maxZ = double.NegativeInfinity;

        for (int i = 0; i < sampleCount; i++)
        {
            double distance = i * document.TotalLength / (sampleCount - 1.0);
            ExportTrackFrame frame = evaluator.EvaluateFrameAtDistance(distance);
            minY = System.Math.Min(minY, frame.Position.Y);
            maxY = System.Math.Max(maxY, frame.Position.Y);
            minZ = System.Math.Min(minZ, frame.Position.Z);
            maxZ = System.Math.Max(maxZ, frame.Position.Z);
        }

        Assert.True(maxY - minY > 25.0, "Expected a stronger vertical profile for debug visualization.");
        Assert.True(maxZ - minZ > 70.0, "Expected stronger lateral curvature for debug visualization.");
        Assert.True(document.TotalLength > 300.0, "Expected enough total length for multi-car spacing tests.");
    }

    [Fact]
    public void DeterministicTrackProfile_PreservesStableDistanceBasedCarPlacement()
    {
        TrackDocument document = BuildDeterministicBackendVisualizerTrack();
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);

        const int carCount = 8;
        const double carSpacing = 6.0;
        double[] leadDistances = { 42.0, 128.0, 214.0, 320.0 };

        foreach (double leadDistance in leadDistances)
        {
            IReadOnlyList<TrainCarTransform> cars = provider.GetCarTransforms(
                leadDistance,
                carSpacing,
                carCount);

            Assert.Equal(carCount, cars.Count);

            for (int i = 1; i < cars.Count; i++)
            {
                double actualSpacing = cars[i - 1].Distance - cars[i].Distance;
                Assert.True(System.Math.Abs(actualSpacing - carSpacing) <= Tolerance);
            }
        }
    }

    [Fact]
    public void DeterministicTrackProfile_BankingChangesFrameRollWithoutChangingTangentDirection()
    {
        TrackDocument rolled = BuildDeterministicBackendVisualizerTrack();
        TrackDocument unrolled = BuildDeterministicBackendVisualizerTrack(withRoll: false);

        var rolledEvaluator = new TrackEvaluator(rolled);
        var unrolledEvaluator = new TrackEvaluator(unrolled);

        double[] distances = { 126.0, 198.0 };
        foreach (double distance in distances)
        {
            ExportTrackFrame rolledFrame = rolledEvaluator.EvaluateFrameAtDistance(distance);
            ExportTrackFrame unrolledFrame = unrolledEvaluator.EvaluateFrameAtDistance(distance);

            Assert.True((rolledFrame.Normal - unrolledFrame.Normal).Length > 1e-3);
            Assert.True((rolledFrame.Binormal - unrolledFrame.Binormal).Length > 1e-3);
            Assert.True((rolledFrame.Tangent - unrolledFrame.Tangent).Length <= 1e-6);
        }
    }

    private static TrackDocument BuildDeterministicBackendVisualizerTrack(bool withRoll = true)
    {
        double c1Roll = withRoll ? 0.18 : 0.0;
        double c2Roll = withRoll ? 0.34 : 0.0;
        double c3Roll = withRoll ? 0.16 : 0.0;
        double s4Roll = withRoll ? 0.06 : 0.0;

        TrackSegment[] segments =
        {
            new StraightSegment(
                length: 52.3450093132096,
                id: "s0",
                spline: new LineCurve(
                    new Vector3d(0.0, 0.0, 0.0),
                    new Vector3d(52.0, 6.0, 0.0)),
                rollRadians: 0.0),
            new CurvedSegment(
                length: 102.5673744475657,
                id: "c1",
                spline: new CubicBezierCurve(
                    new Vector3d(52.0, 6.0, 0.0),
                    new Vector3d(80.0, 10.0, 3.0),
                    new Vector3d(105.0, 31.0, 45.0),
                    new Vector3d(122.0, 34.0, 66.0)),
                rollRadians: c1Roll),
            new CurvedSegment(
                length: 79.23453083610931,
                id: "c2",
                spline: new CubicBezierCurve(
                    new Vector3d(122.0, 34.0, 66.0),
                    new Vector3d(139.0, 37.0, 87.0),
                    new Vector3d(157.0, 28.0, 36.0),
                    new Vector3d(176.0, 24.0, 22.0)),
                rollRadians: c2Roll),
            new CurvedSegment(
                length: 76.41747274859141,
                id: "c3",
                spline: new CubicBezierCurve(
                    new Vector3d(176.0, 24.0, 22.0),
                    new Vector3d(195.0, 20.0, 8.0),
                    new Vector3d(220.0, 12.0, -8.0),
                    new Vector3d(244.0, 10.0, -6.0)),
                rollRadians: c3Roll),
            new StraightSegment(
                length: 54.037024344425184,
                id: "s4",
                spline: new LineCurve(
                    new Vector3d(244.0, 10.0, -6.0),
                    new Vector3d(298.0, 8.0, -6.0)),
                rollRadians: s4Roll)
        };

        return new TrackDocument(segments);
    }
}
