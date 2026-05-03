using System;
using System.Collections.Generic;
using Quantum.Math;
using Quantum.Physics;
using Quantum.Splines;
using Xunit;

namespace Quantum.Tests;

public sealed class TrainStepLoopTests
{
    private const double ValueTolerance = 1e-6;

    [Fact]
    public void TrainStepLoop_RunSteps_MatchesDirectFollowerUpdateWithGravity()
    {
        IArcLengthCurve track = new LineCurve(
            new Vector3d(0, 10, 0),
            new Vector3d(100, 0, 0));

        const double deltaTime = 0.05;
        const double gravityMagnitude = 9.81;
        const double linearDrag = 0.08;
        const double quadraticDrag = 0.01;
        const double rollingResistance = 0.05;
        const int steps = 200;
        const double initialDistance = 12.5;
        const double initialSpeed = 3.25;

        var directFollower = new TrainFollowerState(
            track,
            initialDistance: initialDistance,
            speed: initialSpeed,
            loopEnabled: false);

        var loopFollower = new TrainFollowerState(
            track,
            initialDistance: initialDistance,
            speed: initialSpeed,
            loopEnabled: false);

        for (int i = 0; i < steps; i++)
        {
            directFollower.UpdateWithGravity(
                deltaTime,
                gravityMagnitude,
                linearDragCoefficient: linearDrag,
                quadraticDragCoefficient: quadraticDrag,
                rollingResistance: rollingResistance);
        }

        var loop = new TrainStepLoop(
            loopFollower,
            deltaTime,
            gravityMagnitude,
            linearDrag,
            quadraticDrag,
            rollingResistance);

        loop.Step(steps);

        Assert.InRange(System.Math.Abs(directFollower.Distance - loopFollower.Distance), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(directFollower.Speed - loopFollower.Speed), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(directFollower.Position.X - loopFollower.Position.X), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(directFollower.Position.Y - loopFollower.Position.Y), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(directFollower.Position.Z - loopFollower.Position.Z), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(directFollower.Tangent.X - loopFollower.Tangent.X), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(directFollower.Tangent.Y - loopFollower.Tangent.Y), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(directFollower.Tangent.Z - loopFollower.Tangent.Z), 0.0, ValueTolerance);

        Assert.Equal(steps, loop.Tick);
        Assert.InRange(System.Math.Abs(loop.ElapsedTimeSeconds - (steps * deltaTime)), 0.0, ValueTolerance);
    }

    [Fact]
    public void TrainStepLoop_Sample_ReturnsDeterministicSequenceMatchingManualLoop()
    {
        IArcLengthCurve track = new LineCurve(
            new Vector3d(0, 10, 0),
            new Vector3d(100, 0, 0));

        const double deltaTime = 0.05;
        const double gravityMagnitude = 9.81;
        const double linearDrag = 0.08;
        const double quadraticDrag = 0.01;
        const double rollingResistance = 0.05;
        const int steps = 120;
        const double initialDistance = 12.5;
        const double initialSpeed = 3.25;

        var manualFollower = new TrainFollowerState(
            track,
            initialDistance: initialDistance,
            speed: initialSpeed,
            loopEnabled: false);

        var loopFollower = new TrainFollowerState(
            track,
            initialDistance: initialDistance,
            speed: initialSpeed,
            loopEnabled: false);

        var manualSnapshots = new List<TrainFollowerState>(steps);
        for (int i = 0; i < steps; i++)
        {
            manualFollower.UpdateWithGravity(
                deltaTime,
                gravityMagnitude,
                linearDragCoefficient: linearDrag,
                quadraticDragCoefficient: quadraticDrag,
                rollingResistance: rollingResistance);

            manualSnapshots.Add(CloneFollowerState(manualFollower));
        }

        var loop = new TrainStepLoop(
            loopFollower,
            deltaTime,
            gravityMagnitude,
            linearDrag,
            quadraticDrag,
            rollingResistance);

        IReadOnlyList<TrainFollowerState> sampled = loop.Sample(steps);

        Assert.Equal(manualSnapshots.Count, sampled.Count);

        for (int i = 0; i < sampled.Count; i++)
        {
            TrainFollowerState expected = manualSnapshots[i];
            TrainFollowerState actual = sampled[i];

            Assert.InRange(System.Math.Abs(expected.Distance - actual.Distance), 0.0, ValueTolerance);
            Assert.InRange(System.Math.Abs(expected.Speed - actual.Speed), 0.0, ValueTolerance);
            Assert.InRange(System.Math.Abs(expected.Position.X - actual.Position.X), 0.0, ValueTolerance);
            Assert.InRange(System.Math.Abs(expected.Position.Y - actual.Position.Y), 0.0, ValueTolerance);
            Assert.InRange(System.Math.Abs(expected.Position.Z - actual.Position.Z), 0.0, ValueTolerance);
            Assert.InRange(System.Math.Abs(expected.Tangent.X - actual.Tangent.X), 0.0, ValueTolerance);
            Assert.InRange(System.Math.Abs(expected.Tangent.Y - actual.Tangent.Y), 0.0, ValueTolerance);
            Assert.InRange(System.Math.Abs(expected.Tangent.Z - actual.Tangent.Z), 0.0, ValueTolerance);
        }
    }

    private static TrainFollowerState CloneFollowerState(TrainFollowerState source)
    {
        var clone = new TrainFollowerState(
            source.Track,
            initialDistance: source.Distance,
            speed: source.Speed,
            loopEnabled: source.LoopEnabled);

        clone.Acceleration = source.Acceleration;
        return clone;
    }
}
