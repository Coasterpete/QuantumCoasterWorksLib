using Quantum.Math;
using Quantum.Physics;
using Quantum.Splines;
using Xunit;

namespace Quantum.Tests;

public sealed class TangentialProjectedValidationTests
{
    private const double ValueTolerance = 1e-6;
    private const double EnergyTolerance = 1e-5;
    private const double StandardGravity = 9.81;

    [Fact]
    public void TangentialProjectedMode_InclinedTrackWithGravity_UsesSlopeProjectedAcceleration()
    {
        IArcLengthCurve track = new LineCurve(
            new Vector3d(0, 20, 0),
            new Vector3d(20, 0, 0));

        var follower = new TrainFollowerState(
            track,
            initialDistance: 0.0,
            speed: 0.0,
            loopEnabled: false);

        double expectedTangentialAcceleration =
            StandardGravity * Vector3d.Dot(new Vector3d(0.0, -1.0, 0.0), follower.Frame.Tangent);

        var loop = new TrainStepLoop(
            follower,
            deltaTime: 0.25,
            gravityMagnitude: StandardGravity,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            new ConstantNormalForceTargetProvider(normalG: 0.0),
            TrainIntegrationMode.TangentialProjected);

        loop.Step();

        Assert.True(follower.TangentialAcceleration.HasValue);
        Assert.InRange(System.Math.Abs(follower.TangentialAcceleration.Value), 0.0, ValueTolerance);
        Assert.InRange(
            System.Math.Abs(follower.Acceleration - expectedTangentialAcceleration),
            0.0,
            ValueTolerance);
    }

    [Fact]
    public void TangentialProjectedMode_NoDrag_ConvertsPotentialEnergyToKineticEnergy()
    {
        IArcLengthCurve track = new LineCurve(
            new Vector3d(0, 40, 0),
            new Vector3d(120, 0, 0));

        var follower = new TrainFollowerState(
            track,
            initialDistance: 0.0,
            speed: 0.0,
            loopEnabled: false);

        var loop = new TrainStepLoop(
            follower,
            deltaTime: 0.05,
            gravityMagnitude: StandardGravity,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            new ConstantNormalForceTargetProvider(normalG: 0.0),
            TrainIntegrationMode.TangentialProjected);

        double initialPotential = StandardGravity * follower.Position.Y;
        double initialKinetic = 0.5 * follower.Speed * follower.Speed;

        const int steps = 80;
        loop.Step(steps);

        double finalPotential = StandardGravity * follower.Position.Y;
        double finalKinetic = 0.5 * follower.Speed * follower.Speed;
        double potentialLoss = initialPotential - finalPotential;
        double kineticGain = finalKinetic - initialKinetic;
        double initialTotalEnergy = initialPotential + initialKinetic;
        double finalTotalEnergy = finalPotential + finalKinetic;

        Assert.True(potentialLoss > 0.0, "Expected positive potential-energy loss on a downhill slope.");
        Assert.InRange(System.Math.Abs(kineticGain - potentialLoss), 0.0, EnergyTolerance);
        Assert.InRange(System.Math.Abs(finalTotalEnergy - initialTotalEnergy), 0.0, EnergyTolerance);
    }

    [Fact]
    public void TangentialProjectedMode_ConstantSlopeAcceleration_MatchesExpectedKinematics()
    {
        IArcLengthCurve track = new LineCurve(
            new Vector3d(0, 20, 0),
            new Vector3d(20, 0, 0));

        var follower = new TrainFollowerState(
            track,
            initialDistance: 0.0,
            speed: 0.0,
            loopEnabled: false);

        var loop = new TrainStepLoop(
            follower,
            deltaTime: 0.1,
            gravityMagnitude: StandardGravity,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            new ConstantNormalForceTargetProvider(normalG: 0.0),
            TrainIntegrationMode.TangentialProjected);

        double acceleration =
            StandardGravity * Vector3d.Dot(new Vector3d(0.0, -1.0, 0.0), follower.Frame.Tangent);

        const int steps = 20;
        double elapsedTime = steps * loop.DeltaTime;
        double expectedSpeed = acceleration * elapsedTime;
        double expectedDistance = 0.5 * acceleration * elapsedTime * elapsedTime;

        loop.Step(steps);

        Assert.InRange(System.Math.Abs(follower.Speed - expectedSpeed), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(follower.Distance - expectedDistance), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(follower.Acceleration - acceleration), 0.0, ValueTolerance);
    }

    [Fact]
    public void TangentialProjectedMode_AndLegacyMode_DivergeWhenNormalGExists()
    {
        IArcLengthCurve track = new LineCurve(
            new Vector3d(0, 80, 0),
            new Vector3d(80, 0, 0));

        var legacyFollower = new TrainFollowerState(track);
        var tangentialFollower = new TrainFollowerState(track);

        const double deltaTime = 0.1;
        const int steps = 20;
        const double normalG = 1.0;
        double elapsedTime = steps * deltaTime;

        double gravityAlongTrack =
            StandardGravity * Vector3d.Dot(new Vector3d(0.0, -1.0, 0.0), legacyFollower.Frame.Tangent);
        double expectedLegacyAcceleration = gravityAlongTrack + (normalG * StandardGravity);
        double expectedTangentialAcceleration = gravityAlongTrack;

        var legacyLoop = new TrainStepLoop(
            legacyFollower,
            deltaTime,
            gravityMagnitude: StandardGravity,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            new ConstantNormalForceTargetProvider(normalG),
            TrainIntegrationMode.LegacyNormalComponent);

        var tangentialLoop = new TrainStepLoop(
            tangentialFollower,
            deltaTime,
            gravityMagnitude: StandardGravity,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            new ConstantNormalForceTargetProvider(normalG),
            TrainIntegrationMode.TangentialProjected);

        legacyLoop.Step(steps);
        tangentialLoop.Step(steps);

        double expectedLegacySpeed = expectedLegacyAcceleration * elapsedTime;
        double expectedTangentialSpeed = expectedTangentialAcceleration * elapsedTime;

        Assert.InRange(System.Math.Abs(legacyFollower.Speed - expectedLegacySpeed), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(tangentialFollower.Speed - expectedTangentialSpeed), 0.0, ValueTolerance);
        Assert.True(
            legacyFollower.Speed > tangentialFollower.Speed + ValueTolerance,
            "Expected legacy mode to include normal-G acceleration in 1D motion while tangential mode does not.");
    }

    private sealed class ConstantNormalForceTargetProvider : IForceTargetProvider
    {
        private readonly double _normalG;

        public ConstantNormalForceTargetProvider(double normalG)
        {
            _normalG = normalG;
        }

        public bool TryGetForceTargets(double x, out ForceTargets targets)
        {
            targets = new ForceTargets(_normalG, lateralG: 0.0, rollRateDegPerSec: 0.0);
            return true;
        }
    }
}
