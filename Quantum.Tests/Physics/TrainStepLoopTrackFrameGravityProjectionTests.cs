using Quantum.Math;
using Quantum.Physics;
using Quantum.Splines;
using Quantum.Track;
using Xunit;
using TrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class TrainStepLoopTrackFrameGravityProjectionTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void TangentialProjectedMode_WithTrackFrameProvider_UsesProviderTangentGravityProjection()
    {
        const double gravityMagnitude = 9.81;
        const double deltaTime = 0.2;

        IArcLengthCurve track = new LineCurve(
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(100.0, 0.0, 0.0));

        var follower = new TrainFollowerState(track);
        Vector3d providedTangent = new Vector3d(1.0, -1.0, 0.0).Normalized();
        var trackFrameProvider = new ConstantTrackFrameProvider(providedTangent);

        var loop = new TrainStepLoop(
            follower,
            deltaTime,
            gravityMagnitude,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            new ZeroForceTargetProvider(),
            TrainIntegrationMode.TangentialProjected,
            trackFrameProvider);

        loop.Step();

        double expectedTangentialGravity = Vector3d.Dot(
            new Vector3d(0.0, -gravityMagnitude, 0.0),
            providedTangent);
        double expectedSpeed = expectedTangentialGravity * deltaTime;
        double expectedDistance = 0.5 * expectedTangentialGravity * deltaTime * deltaTime;

        Assert.Equal(1, trackFrameProvider.CallCount);
        Assert.True(follower.TangentialAcceleration.HasValue);
        Assert.InRange(System.Math.Abs(follower.TangentialAcceleration.Value), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(follower.Acceleration - expectedTangentialGravity), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(follower.Speed - expectedSpeed), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(follower.Distance - expectedDistance), 0.0, Tolerance);
    }

    [Fact]
    public void TangentialProjectedMode_WithoutTrackFrameProvider_MatchesCurrentBehavior()
    {
        IArcLengthCurve track = new LineCurve(
            new Vector3d(0.0, 20.0, 0.0),
            new Vector3d(20.0, 0.0, 0.0));

        const double deltaTime = 0.1;
        const int steps = 25;
        const double gravityMagnitude = 9.81;

        var baselineFollower = new TrainFollowerState(track);
        var tangentialFollower = new TrainFollowerState(track);

        var tangentialLoop = new TrainStepLoop(
            tangentialFollower,
            deltaTime,
            gravityMagnitude,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            new ZeroForceTargetProvider(),
            TrainIntegrationMode.TangentialProjected,
            trackFrameProvider: null);

        Assert.Null(tangentialLoop.TrackFrameProvider);

        for (int i = 0; i < steps; i++)
        {
            baselineFollower.UpdateWithGravity(
                deltaTime,
                gravityMagnitude,
                linearDragCoefficient: 0.0,
                quadraticDragCoefficient: 0.0,
                rollingResistance: 0.0);
        }

        tangentialLoop.Step(steps);

        Assert.InRange(System.Math.Abs(tangentialFollower.Distance - baselineFollower.Distance), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(tangentialFollower.Speed - baselineFollower.Speed), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(tangentialFollower.Acceleration - baselineFollower.Acceleration), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(tangentialFollower.Position.X - baselineFollower.Position.X), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(tangentialFollower.Position.Y - baselineFollower.Position.Y), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(tangentialFollower.Position.Z - baselineFollower.Position.Z), 0.0, Tolerance);
    }

    [Fact]
    public void TangentialProjectedMode_WhenTrackFrameProviderFails_FallsBackToCurrentGravityPath()
    {
        IArcLengthCurve track = new LineCurve(
            new Vector3d(0.0, 20.0, 0.0),
            new Vector3d(20.0, 0.0, 0.0));

        const double deltaTime = 0.1;
        const int steps = 25;
        const double gravityMagnitude = 9.81;

        var baselineFollower = new TrainFollowerState(track);
        var fallbackFollower = new TrainFollowerState(track);
        var failingProvider = new FailingTrackFrameProvider();

        var baselineLoop = new TrainStepLoop(
            baselineFollower,
            deltaTime,
            gravityMagnitude,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            new ZeroForceTargetProvider(),
            TrainIntegrationMode.TangentialProjected);

        var fallbackLoop = new TrainStepLoop(
            fallbackFollower,
            deltaTime,
            gravityMagnitude,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            new ZeroForceTargetProvider(),
            TrainIntegrationMode.TangentialProjected,
            failingProvider);

        baselineLoop.Step(steps);
        fallbackLoop.Step(steps);

        Assert.Equal(steps, failingProvider.CallCount);
        Assert.InRange(System.Math.Abs(fallbackFollower.Distance - baselineFollower.Distance), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(fallbackFollower.Speed - baselineFollower.Speed), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(fallbackFollower.Acceleration - baselineFollower.Acceleration), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(fallbackFollower.Position.X - baselineFollower.Position.X), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(fallbackFollower.Position.Y - baselineFollower.Position.Y), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(fallbackFollower.Position.Z - baselineFollower.Position.Z), 0.0, Tolerance);
    }

    [Fact]
    public void TangentialProjectedMode_WithNullTrackFrameProvider_DoesNotRequireProviderCalls()
    {
        IArcLengthCurve track = new LineCurve(
            new Vector3d(0.0, 10.0, 0.0),
            new Vector3d(50.0, 0.0, 0.0));

        var follower = new TrainFollowerState(track);
        var loop = new TrainStepLoop(
            follower,
            deltaTime: 0.1,
            gravityMagnitude: 9.81,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            new ZeroForceTargetProvider(),
            TrainIntegrationMode.TangentialProjected,
            trackFrameProvider: null);

        Assert.Null(loop.TrackFrameProvider);

        loop.Step();

        Assert.True(loop.Tick == 1);
    }

    private sealed class ZeroForceTargetProvider : IForceTargetProvider
    {
        public bool TryGetForceTargets(double x, out ForceTargets targets)
        {
            targets = new ForceTargets(normalG: 0.0, lateralG: 0.0, rollRateDegPerSec: 0.0);
            return true;
        }
    }

    private sealed class ConstantTrackFrameProvider : ITrackFrameProvider
    {
        private readonly Vector3d _tangent;

        public ConstantTrackFrameProvider(Vector3d tangent)
        {
            _tangent = tangent;
        }

        public int CallCount { get; private set; }

        public bool TryGetFrameAtDistance(double distance, out TrackFrame frame)
        {
            CallCount++;
            frame = new TrackFrame(
                distance,
                position: Vector3d.Zero,
                tangent: _tangent,
                normal: Vector3d.UnitY,
                binormal: Vector3d.UnitZ);
            return true;
        }
    }

    private sealed class FailingTrackFrameProvider : ITrackFrameProvider
    {
        public int CallCount { get; private set; }

        public bool TryGetFrameAtDistance(double distance, out TrackFrame frame)
        {
            CallCount++;
            frame = default;
            return false;
        }
    }
}
