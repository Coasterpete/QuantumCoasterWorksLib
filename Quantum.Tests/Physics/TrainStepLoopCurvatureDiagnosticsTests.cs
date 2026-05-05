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
        Assert.True(follower.CombinedWorldAccelerationVector.HasValue);
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

        Assert.True(follower.TangentialAcceleration.HasValue);
        Vector3d expectedCombinedAcceleration =
            (follower.TangentialAcceleration.Value * follower.Frame.Tangent) +
            normalAccelerationVector;
        Vector3d combinedWorldAcceleration = follower.CombinedWorldAccelerationVector.Value;
        Assert.InRange(System.Math.Abs(combinedWorldAcceleration.X - expectedCombinedAcceleration.X), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(combinedWorldAcceleration.Y - expectedCombinedAcceleration.Y), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(combinedWorldAcceleration.Z - expectedCombinedAcceleration.Z), 0.0, Tolerance);
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
        Assert.True(follower.TangentialAcceleration.HasValue);
        Assert.True(follower.CombinedWorldAccelerationVector.HasValue);
        Vector3d expectedCombinedAcceleration = follower.TangentialAcceleration.Value * follower.Frame.Tangent;
        Vector3d combinedWorldAcceleration = follower.CombinedWorldAccelerationVector.Value;
        Assert.InRange(System.Math.Abs(combinedWorldAcceleration.X - expectedCombinedAcceleration.X), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(combinedWorldAcceleration.Y - expectedCombinedAcceleration.Y), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(combinedWorldAcceleration.Z - expectedCombinedAcceleration.Z), 0.0, Tolerance);
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
        Assert.True(baselineFollower.CombinedWorldAccelerationVector.HasValue);
        Assert.True(curvatureFollower.NormalAcceleration.HasValue);
        Assert.True(curvatureFollower.NormalAccelerationVector.HasValue);
        Assert.True(curvatureFollower.CombinedWorldAccelerationVector.HasValue);
        Assert.InRange(
            System.Math.Abs(curvatureFollower.NormalAcceleration.Value - expectedNormalAcceleration),
            0.0,
            Tolerance);
        Assert.InRange(
            System.Math.Abs(curvatureFollower.NormalAccelerationVector.Value.Length - expectedNormalAcceleration),
            0.0,
            Tolerance);

        Assert.True(baselineFollower.TangentialAcceleration.HasValue);
        Assert.True(curvatureFollower.TangentialAcceleration.HasValue);

        Vector3d expectedBaselineCombined =
            baselineFollower.TangentialAcceleration.Value * baselineFollower.Frame.Tangent;
        Vector3d baselineCombined = baselineFollower.CombinedWorldAccelerationVector.Value;
        Assert.InRange(System.Math.Abs(baselineCombined.X - expectedBaselineCombined.X), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(baselineCombined.Y - expectedBaselineCombined.Y), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(baselineCombined.Z - expectedBaselineCombined.Z), 0.0, Tolerance);

        Vector3d expectedCurvatureCombined =
            (curvatureFollower.TangentialAcceleration.Value * curvatureFollower.Frame.Tangent) +
            curvatureFollower.NormalAccelerationVector.Value;
        Vector3d curvatureCombined = curvatureFollower.CombinedWorldAccelerationVector.Value;
        Assert.InRange(System.Math.Abs(curvatureCombined.X - expectedCurvatureCombined.X), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(curvatureCombined.Y - expectedCurvatureCombined.Y), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(curvatureCombined.Z - expectedCurvatureCombined.Z), 0.0, Tolerance);
    }

    [Fact]
    public void TangentialProjectedMode_CurvatureInfluenceMultiplier_WhenEnabled_AffectsMotion()
    {
        const double radius = 30.0;
        const double curvature = 1.0 / radius;
        const double deltaTime = 0.05;
        const int steps = 120;
        const double initialSpeed = 9.0;
        const double curvatureInfluenceMultiplier = 0.02;

        IArcLengthCurve track = new LineCurve(
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(300.0, 0.0, 0.0));

        var disabledFollower = new TrainFollowerState(track, speed: initialSpeed);
        var enabledFollower = new TrainFollowerState(track, speed: initialSpeed);

        var disabledLoop = new TrainStepLoop(
            disabledFollower,
            deltaTime,
            gravityMagnitude: 0.0,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            new ZeroForceTargetProvider(),
            TrainIntegrationMode.TangentialProjected,
            new ConstantCurvatureTrackFrameProvider(curvature),
            curvatureNormalSpeedInfluenceMultiplier: 0.0);

        var enabledLoop = new TrainStepLoop(
            enabledFollower,
            deltaTime,
            gravityMagnitude: 0.0,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            new ZeroForceTargetProvider(),
            TrainIntegrationMode.TangentialProjected,
            new ConstantCurvatureTrackFrameProvider(curvature),
            curvatureNormalSpeedInfluenceMultiplier: curvatureInfluenceMultiplier);

        disabledLoop.Step(steps);
        enabledLoop.Step(steps);

        Assert.True(
            enabledFollower.Speed > disabledFollower.Speed + Tolerance,
            "Expected curvature influence multiplier to increase forward speed when enabled.");
        Assert.True(
            enabledFollower.Distance > disabledFollower.Distance + Tolerance,
            "Expected curvature influence multiplier to increase traveled distance when enabled.");
        Assert.True(enabledFollower.NormalAcceleration.HasValue);
        Assert.True(enabledFollower.NormalAccelerationVector.HasValue);
        Assert.True(enabledFollower.CombinedWorldAccelerationVector.HasValue);
    }

    [Fact]
    public void TangentialProjectedMode_CurvatureInfluenceMultiplier_WithoutCurvatureProvider_DoesNotChangeMotion()
    {
        const double deltaTime = 0.05;
        const int steps = 120;
        const double initialSpeed = 9.0;

        IArcLengthCurve track = new LineCurve(
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(300.0, 0.0, 0.0));

        var disabledFollower = new TrainFollowerState(track, speed: initialSpeed);
        var enabledFollower = new TrainFollowerState(track, speed: initialSpeed);

        var disabledLoop = new TrainStepLoop(
            disabledFollower,
            deltaTime,
            gravityMagnitude: 0.0,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            new ZeroForceTargetProvider(),
            TrainIntegrationMode.TangentialProjected,
            trackFrameProvider: null,
            curvatureNormalSpeedInfluenceMultiplier: 0.0);

        var enabledLoop = new TrainStepLoop(
            enabledFollower,
            deltaTime,
            gravityMagnitude: 0.0,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            new ZeroForceTargetProvider(),
            TrainIntegrationMode.TangentialProjected,
            trackFrameProvider: null,
            curvatureNormalSpeedInfluenceMultiplier: 0.02);

        disabledLoop.Step(steps);
        enabledLoop.Step(steps);

        Assert.InRange(System.Math.Abs(enabledFollower.Distance - disabledFollower.Distance), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(enabledFollower.Speed - disabledFollower.Speed), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(enabledFollower.Acceleration - disabledFollower.Acceleration), 0.0, Tolerance);
        Assert.Null(enabledFollower.NormalAcceleration);
        Assert.Null(enabledFollower.NormalAccelerationVector);
    }

    [Fact]
    public void TangentialProjectedMode_CurvatureInfluenceMultiplier_WhenCurvatureLookupFails_FallsBackWithoutMotionChange()
    {
        const double deltaTime = 0.05;
        const int steps = 120;
        const double initialSpeed = 9.0;

        IArcLengthCurve track = new LineCurve(
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(300.0, 0.0, 0.0));

        var baselineFollower = new TrainFollowerState(track, speed: initialSpeed);
        var fallbackFollower = new TrainFollowerState(track, speed: initialSpeed);
        var failingProvider = new FailingCurvatureTrackFrameProvider();

        var baselineLoop = new TrainStepLoop(
            baselineFollower,
            deltaTime,
            gravityMagnitude: 0.0,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            new ZeroForceTargetProvider(),
            TrainIntegrationMode.TangentialProjected,
            trackFrameProvider: null,
            curvatureNormalSpeedInfluenceMultiplier: 0.0);

        var fallbackLoop = new TrainStepLoop(
            fallbackFollower,
            deltaTime,
            gravityMagnitude: 0.0,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            new ZeroForceTargetProvider(),
            TrainIntegrationMode.TangentialProjected,
            failingProvider,
            curvatureNormalSpeedInfluenceMultiplier: 0.02);

        baselineLoop.Step(steps);
        fallbackLoop.Step(steps);

        Assert.Equal(steps, failingProvider.CurvatureCallCount);
        Assert.InRange(System.Math.Abs(fallbackFollower.Distance - baselineFollower.Distance), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(fallbackFollower.Speed - baselineFollower.Speed), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(fallbackFollower.Acceleration - baselineFollower.Acceleration), 0.0, Tolerance);
        Assert.Null(fallbackFollower.NormalAcceleration);
        Assert.Null(fallbackFollower.NormalAccelerationVector);
    }

    [Fact]
    public void TangentialProjectedMode_CurvatureInfluenceMultiplier_ConstantCurvature_RemainsStableAndDeterministic()
    {
        const double radius = 60.0;
        const double curvature = 1.0 / radius;
        const double deltaTime = 0.02;
        const int steps = 800;
        const double initialSpeed = 7.5;
        const double curvatureInfluenceMultiplier = 0.01;

        IArcLengthCurve track = new LineCurve(
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(1000.0, 0.0, 0.0));

        var followerA = new TrainFollowerState(track, speed: initialSpeed);
        var followerB = new TrainFollowerState(track, speed: initialSpeed);

        var loopA = new TrainStepLoop(
            followerA,
            deltaTime,
            gravityMagnitude: 0.0,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            new ZeroForceTargetProvider(),
            TrainIntegrationMode.TangentialProjected,
            new ConstantCurvatureTrackFrameProvider(curvature),
            curvatureNormalSpeedInfluenceMultiplier: curvatureInfluenceMultiplier);

        var loopB = new TrainStepLoop(
            followerB,
            deltaTime,
            gravityMagnitude: 0.0,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            new ZeroForceTargetProvider(),
            TrainIntegrationMode.TangentialProjected,
            new ConstantCurvatureTrackFrameProvider(curvature),
            curvatureNormalSpeedInfluenceMultiplier: curvatureInfluenceMultiplier);

        loopA.Step(steps);
        loopB.Step(steps);

        Assert.True(IsFinite(followerA.Distance));
        Assert.True(IsFinite(followerA.Speed));
        Assert.True(IsFinite(followerA.Acceleration));
        Assert.True(followerA.NormalAcceleration.HasValue);
        Assert.True(followerA.NormalAccelerationVector.HasValue);
        Assert.True(IsFinite(followerA.NormalAcceleration.Value));
        Assert.True(IsFiniteVector(followerA.NormalAccelerationVector.Value));

        Assert.InRange(System.Math.Abs(followerA.Distance - followerB.Distance), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(followerA.Speed - followerB.Speed), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(followerA.Acceleration - followerB.Acceleration), 0.0, Tolerance);
    }

    [Fact]
    public void TangentialProjectedMode_CurvatureInfluenceMultiplier_EnergyRemainsBounded()
    {
        const double radius = 75.0;
        const double curvature = 1.0 / radius;
        const double deltaTime = 0.05;
        const int steps = 600;
        const double initialSpeed = 8.0;
        const double curvatureInfluenceMultiplier = 0.01;

        IArcLengthCurve track = new LineCurve(
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(2000.0, 0.0, 0.0));

        var follower = new TrainFollowerState(track, speed: initialSpeed);
        var loop = new TrainStepLoop(
            follower,
            deltaTime,
            gravityMagnitude: 0.0,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            new ZeroForceTargetProvider(),
            TrainIntegrationMode.TangentialProjected,
            new ConstantCurvatureTrackFrameProvider(curvature),
            curvatureNormalSpeedInfluenceMultiplier: curvatureInfluenceMultiplier);

        double initialKineticEnergy = 0.5 * initialSpeed * initialSpeed;

        loop.Step(steps);

        double finalKineticEnergy = 0.5 * follower.Speed * follower.Speed;

        Assert.True(IsFinite(finalKineticEnergy));
        Assert.True(finalKineticEnergy > 0.0);
        Assert.InRange(finalKineticEnergy, 0.0, initialKineticEnergy * 2.0);
    }

    [Fact]
    public void LegacyNormalComponentMode_ClearsCurvatureAndCombinedDiagnosticsFromPreviousState()
    {
        IArcLengthCurve track = new LineCurve(
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(100.0, 0.0, 0.0));

        var follower = new TrainFollowerState(track, speed: 4.0);
        follower.ProjectedAcceleration = new Vector3d(1.0, 2.0, 3.0);
        follower.NormalAcceleration = 7.0;
        follower.NormalAccelerationVector = new Vector3d(0.0, 7.0, 0.0);
        follower.BinormalAcceleration = 3.0;
        follower.CombinedWorldAccelerationVector = new Vector3d(4.0, 5.0, 6.0);

        var loop = new TrainStepLoop(
            follower,
            deltaTime: 0.1,
            gravityMagnitude: 0.0,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            integrationMode: TrainIntegrationMode.LegacyNormalComponent);

        loop.Step();

        Assert.Null(follower.ProjectedAcceleration);
        Assert.Null(follower.NormalAcceleration);
        Assert.Null(follower.NormalAccelerationVector);
        Assert.Null(follower.BinormalAcceleration);
        Assert.Null(follower.CombinedWorldAccelerationVector);
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

    private sealed class FailingCurvatureTrackFrameProvider : ITrackFrameProvider
    {
        public int CurvatureCallCount { get; private set; }

        public bool TryGetFrameAtDistance(double distance, out TrackFrame frame)
        {
            frame = default;
            return false;
        }

        public bool TryGetCurvatureAtDistance(double distance, out double curvature)
        {
            CurvatureCallCount++;
            curvature = 0.0;
            return false;
        }
    }

    private static bool IsFinite(double value)
    {
        return !(double.IsNaN(value) || double.IsInfinity(value));
    }

    private static bool IsFiniteVector(Vector3d value)
    {
        return IsFinite(value.X) &&
               IsFinite(value.Y) &&
               IsFinite(value.Z);
    }
}
