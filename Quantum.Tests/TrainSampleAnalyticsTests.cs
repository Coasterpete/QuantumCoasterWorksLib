using System.Collections.Generic;
using Quantum.FVD;
using Quantum.Math;
using Quantum.Physics;
using Quantum.Splines;
using Xunit;

namespace Quantum.Tests;

public sealed class TrainSampleAnalyticsTests
{
    private const double ValueTolerance = 1e-6;

    [Fact]
    public void GetMaxSpeed_ReturnsPeakSampleSpeed()
    {
        IArcLengthCurve track = new LineCurve(
            new Vector3d(0, 10, 0),
            new Vector3d(30, 10, 0));

        IReadOnlyList<TrainFollowerState> samples = new List<TrainFollowerState>
        {
            new TrainFollowerState(track, initialDistance: 1.0, speed: 2.0, loopEnabled: false),
            new TrainFollowerState(track, initialDistance: 2.0, speed: 4.75, loopEnabled: false),
            new TrainFollowerState(track, initialDistance: 3.0, speed: 3.25, loopEnabled: false)
        };

        double maxSpeed = TrainSampleAnalytics.GetMaxSpeed(samples);

        Assert.InRange(System.Math.Abs(maxSpeed - 4.75), 0.0, ValueTolerance);
    }

    [Fact]
    public void GetTotalDistance_ReturnsFinalSampleDistance()
    {
        IArcLengthCurve track = new LineCurve(
            new Vector3d(0, 10, 0),
            new Vector3d(30, 10, 0));

        IReadOnlyList<TrainFollowerState> samples = new List<TrainFollowerState>
        {
            new TrainFollowerState(track, initialDistance: 3.5, speed: 1.0, loopEnabled: false),
            new TrainFollowerState(track, initialDistance: 5.0, speed: 1.5, loopEnabled: false),
            new TrainFollowerState(track, initialDistance: 7.25, speed: 2.0, loopEnabled: false)
        };

        double totalDistance = TrainSampleAnalytics.GetTotalDistance(samples);

        Assert.InRange(System.Math.Abs(totalDistance - 7.25), 0.0, ValueTolerance);
    }

    [Fact]
    public void GetMinHeightAndMaxHeight_ReturnExpectedHeights_OnKnownSlope()
    {
        IArcLengthCurve track = new LineCurve(
            new Vector3d(0, 10, 0),
            new Vector3d(10, 0, 0));

        double length = track.Length;

        IReadOnlyList<TrainFollowerState> samples = new List<TrainFollowerState>
        {
            new TrainFollowerState(track, initialDistance: 0.0, speed: 0.0, loopEnabled: false),
            new TrainFollowerState(track, initialDistance: length * 0.5, speed: 0.0, loopEnabled: false),
            new TrainFollowerState(track, initialDistance: length, speed: 0.0, loopEnabled: false)
        };

        double minHeight = TrainSampleAnalytics.GetMinHeight(samples);
        double maxHeight = TrainSampleAnalytics.GetMaxHeight(samples);

        Assert.InRange(System.Math.Abs(minHeight - 0.0), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(maxHeight - 10.0), 0.0, ValueTolerance);
    }

    [Fact]
    public void ComputeNormalGError_ConstantTargetAndSimulation_IsNearZero()
    {
        const double deltaTime = 0.1;
        const int steps = 24;

        IArcLengthCurve track = new LineCurve(
            new Vector3d(0, 0, 0),
            new Vector3d(100, 0, 0));

        FvdForceTargetProviderAdapter adapter = CreateConstantNormalGAdapter(normalG: 1.0, endX: 100.0);

        var follower = new TrainFollowerState(track);
        var loop = new TrainStepLoop(
            follower,
            deltaTime,
            gravityMagnitude: 0.0,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            forceTargetProvider: adapter);

        IReadOnlyList<TrainFollowerState> samples = loop.Sample(steps);

        (double meanAbsoluteError, double rmsError) = TrainSampleAnalytics.ComputeNormalGError(samples, adapter, deltaTime);

        Assert.InRange(System.Math.Abs(meanAbsoluteError), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(rmsError), 0.0, ValueTolerance);
    }

    [Fact]
    public void ComputeNormalGError_TargetSimulationMismatch_IsNonZero()
    {
        const double deltaTime = 0.1;
        const int steps = 16;

        IArcLengthCurve track = new LineCurve(
            new Vector3d(0, 0, 0),
            new Vector3d(100, 0, 0));

        FvdForceTargetProviderAdapter adapter = CreateConstantNormalGAdapter(normalG: 1.0, endX: 100.0);

        var follower = new TrainFollowerState(track);
        var loop = new TrainStepLoop(
            follower,
            deltaTime,
            gravityMagnitude: 0.0,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            forceTargetProvider: null);

        IReadOnlyList<TrainFollowerState> samples = loop.Sample(steps);

        (double meanAbsoluteError, double rmsError) = TrainSampleAnalytics.ComputeNormalGError(samples, adapter, deltaTime);

        Assert.InRange(System.Math.Abs(meanAbsoluteError - 1.0), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(rmsError - 1.0), 0.0, ValueTolerance);
        Assert.True(meanAbsoluteError > 0.0);
        Assert.True(rmsError > 0.0);
    }

    private static FvdForceTargetProviderAdapter CreateConstantNormalGAdapter(double normalG, double endX)
    {
        var graph = new FvdGraph(
            new List<FvdControlNode>
            {
                new(0.0, new Vector3d(0.0, 0.0, 0.0), 1.0),
                new(1.0, new Vector3d(endX, 0.0, 0.0), 1.0)
            },
            degree: 1,
            forceSamples: new List<FvdForceSample>(),
            sections: new List<FvdSectionDefinition>
            {
                new(
                    FvdSectionKind.Force,
                    FvdFunctionDomain.Distance,
                    startX: 0.0,
                    endX: endX,
                    new List<FvdSectionFunction>
                    {
                        new(
                            FvdSectionChannel.NormalG,
                            new List<FvdSectionSample>
                            {
                                new(0.0, normalG),
                                new(endX, normalG)
                            })
                    })
            });

        return new FvdForceTargetProviderAdapter(graph, FvdFunctionDomain.Distance);
    }
}
