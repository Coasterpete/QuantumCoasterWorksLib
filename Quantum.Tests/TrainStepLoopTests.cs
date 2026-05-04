using System;
using System.Collections.Generic;
using System.Reflection;
using Quantum.FVD;
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
    public void Step_NegativeSteps_Throws()
    {
        IArcLengthCurve track = new LineCurve(
            new Vector3d(0, 0, 0),
            new Vector3d(100, 0, 0));

        var follower = new TrainFollowerState(track);
        var loop = new TrainStepLoop(
            follower,
            deltaTime: 0.1,
            gravityMagnitude: 0.0,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0);

        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => loop.Step(-1));
        Assert.Equal("steps", ex.ParamName);
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

    [Fact]
    public void TrainStepLoop_WithForceTargetProvider_ZeroTargets_MatchesBaseline()
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

        var baselineFollower = new TrainFollowerState(
            track,
            initialDistance: initialDistance,
            speed: initialSpeed,
            loopEnabled: false);

        var providerFollower = new TrainFollowerState(
            track,
            initialDistance: initialDistance,
            speed: initialSpeed,
            loopEnabled: false);

        var baselineLoop = new TrainStepLoop(
            baselineFollower,
            deltaTime,
            gravityMagnitude,
            linearDrag,
            quadraticDrag,
            rollingResistance);

        object provider = CreateZeroForceTargetProviderStubOrFail();

        TrainStepLoop providerLoop = CreateTrainStepLoopWithForceTargetProviderOrFail(
            providerFollower,
            deltaTime,
            gravityMagnitude,
            linearDrag,
            quadraticDrag,
            rollingResistance,
            provider);

        IReadOnlyList<TrainFollowerState> baselineSamples = baselineLoop.Sample(steps);
        IReadOnlyList<TrainFollowerState> withProviderSamples = providerLoop.Sample(steps);

        Assert.Equal(baselineSamples.Count, withProviderSamples.Count);

        for (int i = 0; i < baselineSamples.Count; i++)
        {
            TrainFollowerState expected = baselineSamples[i];
            TrainFollowerState actual = withProviderSamples[i];

            Assert.InRange(System.Math.Abs(expected.Distance - actual.Distance), 0.0, ValueTolerance);
            Assert.InRange(System.Math.Abs(expected.Speed - actual.Speed), 0.0, ValueTolerance);
            Assert.InRange(System.Math.Abs(expected.Position.X - actual.Position.X), 0.0, ValueTolerance);
            Assert.InRange(System.Math.Abs(expected.Position.Y - actual.Position.Y), 0.0, ValueTolerance);
            Assert.InRange(System.Math.Abs(expected.Position.Z - actual.Position.Z), 0.0, ValueTolerance);
            Assert.InRange(System.Math.Abs(expected.Tangent.X - actual.Tangent.X), 0.0, ValueTolerance);
            Assert.InRange(System.Math.Abs(expected.Tangent.Y - actual.Tangent.Y), 0.0, ValueTolerance);
            Assert.InRange(System.Math.Abs(expected.Tangent.Z - actual.Tangent.Z), 0.0, ValueTolerance);
        }

        Assert.Equal(baselineLoop.Tick, providerLoop.Tick);
        Assert.InRange(
            System.Math.Abs(baselineLoop.ElapsedTimeSeconds - providerLoop.ElapsedTimeSeconds),
            0.0,
            ValueTolerance);
    }

    [Fact]
    public void TrainStepLoop_WithForceTargetProvider_NormalGInfluencesAcceleration()
    {
        IArcLengthCurve track = new LineCurve(
            new Vector3d(0, 0, 0),
            new Vector3d(100, 0, 0));

        const double deltaTime = 0.1;
        const double normalG = 1.0;
        const double expectedAcceleration = normalG * 9.81;
        const double gravityMagnitude = 0.0;
        const double linearDrag = 0.0;
        const double quadraticDrag = 0.0;
        const double rollingResistance = 0.0;
        const int steps = 10;
        double elapsedTime = steps * deltaTime;
        double expectedSpeed = expectedAcceleration * elapsedTime;
        double expectedDistance = 0.5 * expectedAcceleration * elapsedTime * elapsedTime;

        var baselineFollower = new TrainFollowerState(track);
        var providerFollower = new TrainFollowerState(track);

        var baselineLoop = new TrainStepLoop(
            baselineFollower,
            deltaTime,
            gravityMagnitude,
            linearDrag,
            quadraticDrag,
            rollingResistance);

        var providerLoop = new TrainStepLoop(
            providerFollower,
            deltaTime,
            gravityMagnitude,
            linearDrag,
            quadraticDrag,
            rollingResistance,
            new ConstantNormalForceTargetProvider(normalG));

        baselineLoop.Step(steps);
        providerLoop.Step(steps);

        Assert.InRange(System.Math.Abs(baselineFollower.Speed), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(baselineFollower.Distance), 0.0, ValueTolerance);
        Assert.True(
            providerFollower.Speed > baselineFollower.Speed + ValueTolerance,
            "Expected constant positive NormalG target to increase speed relative to baseline.");
        Assert.InRange(System.Math.Abs(providerFollower.Speed - expectedSpeed), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(providerFollower.Distance - expectedDistance), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(providerFollower.Acceleration - expectedAcceleration), 0.0, ValueTolerance);
    }

    [Fact]
    public void TrainStepLoop_WithRealFvdAdapter_NormalGConstant_ProducesExpectedKinematics()
    {
        IArcLengthCurve track = new LineCurve(
            new Vector3d(0, 0, 0),
            new Vector3d(100, 0, 0));

        const double deltaTime = 0.1;
        const int steps = 10;
        const double normalG = 1.0;
        const double gravityMagnitude = 0.0;
        const double linearDrag = 0.0;
        const double quadraticDrag = 0.0;
        const double rollingResistance = 0.0;
        const double expectedAcceleration = normalG * 9.81;
        double elapsedTime = steps * deltaTime;
        double expectedSpeed = expectedAcceleration * elapsedTime;
        double expectedDistance = 0.5 * expectedAcceleration * elapsedTime * elapsedTime;

        var graph = new FvdGraph(
            new List<FvdControlNode>
            {
                new(0.0, new Vector3d(0.0, 0.0, 0.0), 1.0),
                new(1.0, new Vector3d(10.0, 0.0, 0.0), 1.0)
            },
            degree: 1,
            forceSamples: new List<FvdForceSample>(),
            sections: new List<FvdSectionDefinition>
            {
                new(
                    FvdSectionKind.Force,
                    FvdFunctionDomain.Distance,
                    startX: 0.0,
                    endX: 10.0,
                    new List<FvdSectionFunction>
                    {
                        new(
                            FvdSectionChannel.NormalG,
                            new List<FvdSectionSample>
                            {
                                new(0.0, normalG),
                                new(10.0, normalG)
                            })
                    })
            });

        var adapter = new FvdForceTargetProviderAdapter(graph, FvdFunctionDomain.Distance);
        var follower = new TrainFollowerState(track);
        var loop = new TrainStepLoop(
            follower,
            deltaTime,
            gravityMagnitude,
            linearDrag,
            quadraticDrag,
            rollingResistance,
            adapter);

        loop.Step(steps);

        Assert.InRange(System.Math.Abs(follower.Speed - expectedSpeed), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(follower.Distance - expectedDistance), 0.0, ValueTolerance);
    }

    [Fact]
    public void TrainStepLoop_WithForceTargetProvider_LateralAndRollDoNotChangeCurrent1dIntegration()
    {
        IArcLengthCurve track = new LineCurve(
            new Vector3d(0, 0, 0),
            new Vector3d(100, 0, 0));

        const double deltaTime = 0.1;
        const double normalG = 1.0;
        const double lateralG = 2.5;
        const double rollRateDegPerSec = 45.0;
        const double gravityMagnitude = 0.0;
        const double linearDrag = 0.0;
        const double quadraticDrag = 0.0;
        const double rollingResistance = 0.0;
        const int steps = 10;

        var normalOnlyFollower = new TrainFollowerState(track);
        var normalPlusLateralFollower = new TrainFollowerState(track);

        var normalOnlyLoop = new TrainStepLoop(
            normalOnlyFollower,
            deltaTime,
            gravityMagnitude,
            linearDrag,
            quadraticDrag,
            rollingResistance,
            new ConstantNormalForceTargetProvider(normalG));

        var normalPlusLateralLoop = new TrainStepLoop(
            normalPlusLateralFollower,
            deltaTime,
            gravityMagnitude,
            linearDrag,
            quadraticDrag,
            rollingResistance,
            new ConstantForceTargetProvider(normalG, lateralG, rollRateDegPerSec));

        normalOnlyLoop.Step(steps);
        normalPlusLateralLoop.Step(steps);

        Assert.InRange(System.Math.Abs(normalOnlyFollower.Speed - normalPlusLateralFollower.Speed), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(normalOnlyFollower.Distance - normalPlusLateralFollower.Distance), 0.0, ValueTolerance);
        Assert.InRange(
            System.Math.Abs(normalOnlyFollower.Acceleration - normalPlusLateralFollower.Acceleration),
            0.0,
            ValueTolerance);
    }

    [Fact]
    public void TrainStepLoop_WithForceTargetProvider_ReturnsFalse_MatchesBaseline()
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

        var baselineFollower = new TrainFollowerState(
            track,
            initialDistance: initialDistance,
            speed: initialSpeed,
            loopEnabled: false);

        var providerFollower = new TrainFollowerState(
            track,
            initialDistance: initialDistance,
            speed: initialSpeed,
            loopEnabled: false);

        var baselineLoop = new TrainStepLoop(
            baselineFollower,
            deltaTime,
            gravityMagnitude,
            linearDrag,
            quadraticDrag,
            rollingResistance);

        var providerLoop = new TrainStepLoop(
            providerFollower,
            deltaTime,
            gravityMagnitude,
            linearDrag,
            quadraticDrag,
            rollingResistance,
            new FalseForceTargetProvider());

        IReadOnlyList<TrainFollowerState> baselineSamples = baselineLoop.Sample(steps);
        IReadOnlyList<TrainFollowerState> withProviderSamples = providerLoop.Sample(steps);

        Assert.Equal(baselineSamples.Count, withProviderSamples.Count);

        for (int i = 0; i < baselineSamples.Count; i++)
        {
            TrainFollowerState expected = baselineSamples[i];
            TrainFollowerState actual = withProviderSamples[i];

            Assert.InRange(System.Math.Abs(expected.Distance - actual.Distance), 0.0, ValueTolerance);
            Assert.InRange(System.Math.Abs(expected.Speed - actual.Speed), 0.0, ValueTolerance);
            Assert.InRange(System.Math.Abs(expected.Acceleration - actual.Acceleration), 0.0, ValueTolerance);
            Assert.InRange(System.Math.Abs(expected.Position.X - actual.Position.X), 0.0, ValueTolerance);
            Assert.InRange(System.Math.Abs(expected.Position.Y - actual.Position.Y), 0.0, ValueTolerance);
            Assert.InRange(System.Math.Abs(expected.Position.Z - actual.Position.Z), 0.0, ValueTolerance);
            Assert.InRange(System.Math.Abs(expected.Tangent.X - actual.Tangent.X), 0.0, ValueTolerance);
            Assert.InRange(System.Math.Abs(expected.Tangent.Y - actual.Tangent.Y), 0.0, ValueTolerance);
            Assert.InRange(System.Math.Abs(expected.Tangent.Z - actual.Tangent.Z), 0.0, ValueTolerance);
        }

        Assert.Equal(baselineLoop.Tick, providerLoop.Tick);
        Assert.InRange(
            System.Math.Abs(baselineLoop.ElapsedTimeSeconds - providerLoop.ElapsedTimeSeconds),
            0.0,
            ValueTolerance);
    }

    [Fact]
    public void TrainStepLoop_WithForceTargetProvider_SamplesOncePerStepAtPreStepDistance()
    {
        IArcLengthCurve track = new LineCurve(
            new Vector3d(0, 0, 0),
            new Vector3d(100, 0, 0));

        const double deltaTime = 0.2;
        const int steps = 6;
        const double initialDistance = 1.5;
        const double initialSpeed = 4.0;

        var stepByStepFollower = new TrainFollowerState(
            track,
            initialDistance: initialDistance,
            speed: initialSpeed,
            loopEnabled: false);

        var batchFollower = new TrainFollowerState(
            track,
            initialDistance: initialDistance,
            speed: initialSpeed,
            loopEnabled: false);

        var stepByStepProvider = new RecordingForceTargetProvider();
        var batchProvider = new RecordingForceTargetProvider();

        var stepByStepLoop = new TrainStepLoop(
            stepByStepFollower,
            deltaTime,
            gravityMagnitude: 0.0,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            stepByStepProvider);

        var batchLoop = new TrainStepLoop(
            batchFollower,
            deltaTime,
            gravityMagnitude: 0.0,
            linearDragCoefficient: 0.0,
            quadraticDragCoefficient: 0.0,
            rollingResistance: 0.0,
            batchProvider);

        var preStepDistances = new List<double>(steps);
        for (int i = 0; i < steps; i++)
        {
            preStepDistances.Add(stepByStepFollower.Distance);
            stepByStepLoop.Step();
        }

        batchLoop.Step(steps);

        Assert.Equal(steps, stepByStepProvider.SampledDistances.Count);
        Assert.Equal(steps, batchProvider.SampledDistances.Count);

        for (int i = 0; i < steps; i++)
        {
            Assert.InRange(
                System.Math.Abs(stepByStepProvider.SampledDistances[i] - preStepDistances[i]),
                0.0,
                ValueTolerance);

            Assert.InRange(
                System.Math.Abs(batchProvider.SampledDistances[i] - preStepDistances[i]),
                0.0,
                ValueTolerance);
        }

        Assert.InRange(
            System.Math.Abs(stepByStepFollower.Distance - batchFollower.Distance),
            0.0,
            ValueTolerance);
        Assert.InRange(
            System.Math.Abs(stepByStepFollower.Speed - batchFollower.Speed),
            0.0,
            ValueTolerance);
    }

    [Fact]
    public void TrainStepLoop_SampleForDuration_UsesFloorStepsAndMatchesSample()
    {
        IArcLengthCurve track = new LineCurve(
            new Vector3d(0, 10, 0),
            new Vector3d(100, 0, 0));

        const double deltaTime = 0.05;
        const double gravityMagnitude = 9.81;
        const double linearDrag = 0.08;
        const double quadraticDrag = 0.01;
        const double rollingResistance = 0.05;
        const double durationSeconds = 1.03;
        const double initialDistance = 12.5;
        const double initialSpeed = 3.25;

        int expectedSteps = (int)System.Math.Floor(durationSeconds / deltaTime);

        var durationFollower = new TrainFollowerState(
            track,
            initialDistance: initialDistance,
            speed: initialSpeed,
            loopEnabled: false);

        var stepFollower = new TrainFollowerState(
            track,
            initialDistance: initialDistance,
            speed: initialSpeed,
            loopEnabled: false);

        var durationLoop = new TrainStepLoop(
            durationFollower,
            deltaTime,
            gravityMagnitude,
            linearDrag,
            quadraticDrag,
            rollingResistance);

        var stepLoop = new TrainStepLoop(
            stepFollower,
            deltaTime,
            gravityMagnitude,
            linearDrag,
            quadraticDrag,
            rollingResistance);

        IReadOnlyList<TrainFollowerState> byDuration = durationLoop.SampleForDuration(durationSeconds);
        IReadOnlyList<TrainFollowerState> bySteps = stepLoop.Sample(expectedSteps);

        Assert.Equal(expectedSteps, byDuration.Count);
        Assert.Equal(bySteps.Count, byDuration.Count);

        for (int i = 0; i < byDuration.Count; i++)
        {
            TrainFollowerState expected = bySteps[i];
            TrainFollowerState actual = byDuration[i];

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

    private static TrainStepLoop CreateTrainStepLoopWithForceTargetProviderOrFail(
        TrainFollowerState follower,
        double deltaTime,
        double gravityMagnitude,
        double linearDragCoefficient,
        double quadraticDragCoefficient,
        double rollingResistance,
        object forceTargetProvider)
    {
        Type providerType = RequireForceTargetProviderInterfaceType();

        Assert.True(
            providerType.IsInstanceOfType(forceTargetProvider),
            "Expected stub provider to implement Quantum.Physics.IForceTargetProvider.");

        ConstructorInfo? constructor = typeof(TrainStepLoop).GetConstructor(
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[]
            {
                typeof(TrainFollowerState),
                typeof(double),
                typeof(double),
                typeof(double),
                typeof(double),
                typeof(double),
                providerType
            },
            modifiers: null);

        Assert.True(
            constructor is not null,
            "Expected constructor: TrainStepLoop(TrainFollowerState follower, double deltaTime, double gravityMagnitude, double linearDragCoefficient, double quadraticDragCoefficient, double rollingResistance, IForceTargetProvider forceTargetProvider).");

        object? loop = constructor!.Invoke(
            new object?[]
            {
                follower,
                deltaTime,
                gravityMagnitude,
                linearDragCoefficient,
                quadraticDragCoefficient,
                rollingResistance,
                forceTargetProvider
            });

        return Assert.IsType<TrainStepLoop>(loop);
    }

    private static object CreateZeroForceTargetProviderStubOrFail()
    {
        Type providerType = RequireForceTargetProviderInterfaceType();

        object provider = DispatchProxy.Create(providerType, typeof(ZeroForceTargetProviderDispatchProxy));

        Assert.True(
            providerType.IsInstanceOfType(provider),
            "Expected zero-force stub to be assignable to Quantum.Physics.IForceTargetProvider.");

        return provider;
    }

    private static Type RequireForceTargetProviderInterfaceType()
    {
        Type? providerType = typeof(TrainStepLoop).Assembly.GetType("Quantum.Physics.IForceTargetProvider");

        Assert.True(
            providerType is not null,
            "Expected Quantum.Physics.IForceTargetProvider to exist.");

        Assert.True(
            providerType!.IsInterface,
            "Expected Quantum.Physics.IForceTargetProvider to be an interface.");

        return providerType!;
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

    private class ZeroForceTargetProviderDispatchProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
                throw new InvalidOperationException("Expected a provider method invocation.");

            ParameterInfo[] parameters = targetMethod.GetParameters();

            if (args is not null)
            {
                for (int i = 0; i < parameters.Length && i < args.Length; i++)
                {
                    Type parameterType = parameters[i].ParameterType;
                    if (!parameterType.IsByRef)
                        continue;

                    Type? elementType = parameterType.GetElementType();
                    args[i] = elementType is null ? null : CreateZeroValue(elementType);
                }
            }

            Type returnType = targetMethod.ReturnType;
            if (returnType == typeof(void))
                return null;

            if (returnType == typeof(bool))
                return true;

            return CreateZeroValue(returnType);
        }

        private static object? CreateZeroValue(Type type)
        {
            if (type == typeof(string))
                return string.Empty;

            if (type.IsValueType)
                return Activator.CreateInstance(type);

            if (TryCreateReferenceTypeInstance(type, out object? instance) && instance is not null)
            {
                TrySetDoubleProperty(instance, "NormalG", 0.0);
                TrySetDoubleProperty(instance, "LateralG", 0.0);
                TrySetDoubleProperty(instance, "RollRate", 0.0);
                TrySetDoubleProperty(instance, "RollRateDegPerSec", 0.0);
                return instance;
            }

            return null;
        }

        private static bool TryCreateReferenceTypeInstance(Type type, out object? instance)
        {
            instance = null;

            if (type.IsInterface || type.IsAbstract)
                return false;

            try
            {
                instance = Activator.CreateInstance(type);
                if (instance is not null)
                    return true;
            }
            catch
            {
                // Fall through to constructor probing.
            }

            ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < constructors.Length; i++)
            {
                ConstructorInfo constructor = constructors[i];
                ParameterInfo[] parameters = constructor.GetParameters();
                var values = new object?[parameters.Length];

                bool canBuildArguments = true;
                for (int p = 0; p < parameters.Length; p++)
                {
                    if (!TryCreateConstructorArgument(parameters[p].ParameterType, out object? value))
                    {
                        canBuildArguments = false;
                        break;
                    }

                    values[p] = value;
                }

                if (!canBuildArguments)
                    continue;

                try
                {
                    instance = constructor.Invoke(values);
                    if (instance is not null)
                        return true;
                }
                catch
                {
                    // Try next constructor.
                }
            }

            return false;
        }

        private static bool TryCreateConstructorArgument(Type type, out object? value)
        {
            if (type == typeof(string))
            {
                value = string.Empty;
                return true;
            }

            if (type.IsValueType)
            {
                value = Activator.CreateInstance(type);
                return true;
            }

            if (type == typeof(object))
            {
                value = new object();
                return true;
            }

            if (TryCreateReferenceTypeInstance(type, out object? instance))
            {
                value = instance;
                return true;
            }

            value = null;
            return false;
        }

        private static void TrySetDoubleProperty(object instance, string propertyName, double value)
        {
            PropertyInfo? property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);

            if (property is null || !property.CanWrite || property.PropertyType != typeof(double))
                return;

            property.SetValue(instance, value);
        }
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

    private sealed class ConstantForceTargetProvider : IForceTargetProvider
    {
        private readonly double _normalG;
        private readonly double _lateralG;
        private readonly double _rollRateDegPerSec;

        public ConstantForceTargetProvider(double normalG, double lateralG, double rollRateDegPerSec)
        {
            _normalG = normalG;
            _lateralG = lateralG;
            _rollRateDegPerSec = rollRateDegPerSec;
        }

        public bool TryGetForceTargets(double x, out ForceTargets targets)
        {
            targets = new ForceTargets(_normalG, _lateralG, _rollRateDegPerSec);
            return true;
        }
    }

    private sealed class FalseForceTargetProvider : IForceTargetProvider
    {
        public bool TryGetForceTargets(double x, out ForceTargets targets)
        {
            targets = default;
            return false;
        }
    }

    private sealed class RecordingForceTargetProvider : IForceTargetProvider
    {
        public List<double> SampledDistances { get; } = new();

        public bool TryGetForceTargets(double x, out ForceTargets targets)
        {
            SampledDistances.Add(x);
            targets = default;
            return false;
        }
    }
}
