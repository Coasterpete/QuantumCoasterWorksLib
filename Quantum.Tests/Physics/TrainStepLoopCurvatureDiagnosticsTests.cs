using Quantum.Math;
using Quantum.Physics;
using Quantum.Splines;

namespace Quantum.Tests;

public sealed class TrainStepLoopCurvatureDiagnosticsTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void TangentialProjectedMode_WithCurvatureProvider_ComputesExpectedNormalAccelerationDiagnostics()
    {
        const double radius = 20.0;
        const double curvature = 1.0 / radius;
        const double initialSpeed = 12.0;
        const double expectedNormalAcceleration = (initialSpeed * initialSpeed) / radius;

        IArcLengthCurve track = new LineCurve(
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(100.0, 0.0, 0.0));

        var follower = new TrainFollowerState(track, speed: initialSpeed);
        var loop = new TrainStepLoop(
            follower,
            deltaTime: 0.1,
            gravityMagnitude: 0.0,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            new ZeroForceTargetProvider(),
            TrainIntegrationMode.TangentialProjected,
            new ConstantCurvatureTrackFrameProvider(curvature));

        loop.Step();

        Assert.True(follower.NormalAcceleration.HasValue);
        Assert.True(follower.NormalAccelerationVector.HasValue);
        Assert.InRange(
            System.Math.Abs(follower.NormalAcceleration.Value - expectedNormalAcceleration),
            0.0,
            Tolerance);

        Vector3d normalAccelerationVector = follower.NormalAccelerationVector.Value;
        double vectorMagnitude = normalAccelerationVector.Length;
        double alignment = Vector3d.Dot(normalAccelerationVector.Normalized(), follower.Frame.Normal);

        Assert.InRange(System.Math.Abs(vectorMagnitude - expectedNormalAcceleration), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(vectorMagnitude - follower.NormalAcceleration.Value), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(alignment - 1.0), 0.0, Tolerance);
    }

    [Fact]
    public void TangentialProjectedMode_WithoutCurvatureProvider_DoesNotSetNormalAccelerationDiagnostic()
    {
        IArcLengthCurve track = new LineCurve(
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(100.0, 0.0, 0.0));

        var follower = new TrainFollowerState(track, speed: 8.0);
        var loop = new TrainStepLoop(
            follower,
            deltaTime: 0.1,
            gravityMagnitude: 0.0,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            new ZeroForceTargetProvider(),
            TrainIntegrationMode.TangentialProjected,
            trackFrameProvider: null);

        loop.Step();

        Assert.Null(follower.NormalAcceleration);
        Assert.Null(follower.NormalAccelerationVector);
    }

    [Fact]
    public void TangentialProjectedMode_CurvatureDiagnostics_DoNotChangeMotion()
    {
        const double radius = 50.0;
        const double curvature = 1.0 / radius;
        const double deltaTime = 0.05;
        const int steps = 40;
        const double initialSpeed = 10.0;

        IArcLengthCurve track = new LineCurve(
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(200.0, 0.0, 0.0));

        var baselineFollower = new TrainFollowerState(track, speed: initialSpeed);
        var curvatureFollower = new TrainFollowerState(track, speed: initialSpeed);

        var baselineLoop = new TrainStepLoop(
            baselineFollower,
            deltaTime,
            gravityMagnitude: 0.0,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            new ZeroForceTargetProvider(),
            TrainIntegrationMode.TangentialProjected,
            trackFrameProvider: null);

        var curvatureLoop = new TrainStepLoop(
            curvatureFollower,
            deltaTime,
            gravityMagnitude: 0.0,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            new ZeroForceTargetProvider(),
            TrainIntegrationMode.TangentialProjected,
            new ConstantCurvatureTrackFrameProvider(curvature));

        baselineLoop.Step(steps);
        curvatureLoop.Step(steps);

        Assert.InRange(System.Math.Abs(curvatureFollower.Distance - baselineFollower.Distance), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(curvatureFollower.Speed - baselineFollower.Speed), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(curvatureFollower.Acceleration - baselineFollower.Acceleration), 0.0, Tolerance);

        double expectedNormalAcceleration = (initialSpeed * initialSpeed) / radius;
        Assert.Null(baselineFollower.NormalAcceleration);
        Assert.Null(baselineFollower.NormalAccelerationVector);
        Assert.True(curvatureFollower.NormalAcceleration.HasValue);
        Assert.True(curvatureFollower.NormalAccelerationVector.HasValue);
        Assert.InRange(
            System.Math.Abs(curvatureFollower.NormalAcceleration.Value - expectedNormalAcceleration),
            0.0,
            Tolerance);
        Assert.InRange(
            System.Math.Abs(curvatureFollower.NormalAccelerationVector.Value.Length - expectedNormalAcceleration),
            0.0,
            Tolerance);
    }

    private sealed class ZeroForceTargetProvider : IForceTargetProvider
    {
        public bool TryGetForceTargets(double x, out ForceTargets targets)
        {
            targets = new ForceTargets(normalG: 0.0, lateralG: 0.0, rollRateDegPerSec: 0.0);
            return true;
        }
    }

    private sealed class ConstantCurvatureTrackFrameProvider : ITrackFrameProvider
    {
        private readonly double _curvature;

        public ConstantCurvatureTrackFrameProvider(double curvature)
        {
            _curvature = curvature;
        }

        public bool TryGetFrameAtDistance(double distance, out TrackFrame frame)
        {
            frame = default;
            return false;
        }

        public bool TryGetCurvatureAtDistance(double distance, out double curvature)
        {
            curvature = _curvature;
            return true;
        }
    }
}
