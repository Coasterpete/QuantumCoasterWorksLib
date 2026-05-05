using Quantum.Math;
using Quantum.Physics;
using Quantum.Splines;
using Quantum.Track;
using Xunit;

namespace Quantum.Tests;

public sealed class TrackPhysicsAdapterTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void TrackPhysicsAdapter_GetFrameAtDistance_PhysicsLayerCanQueryTrackFrame()
    {
        var adapter = new TrackPhysicsAdapter();
        var document = new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(
                length: 10.0,
                spline: new LineCurve(
                    new Vector3d(0.0, 0.0, 0.0),
                    new Vector3d(0.0, 0.0, 10.0)))
        });

        TrackFrame frame = adapter.GetFrameAtDistance(document, 2.5);

        AssertDoubleNear(2.5, frame.S);
        AssertDoubleNear(0.0, frame.Position.X);
        AssertDoubleNear(0.0, frame.Position.Y);
        AssertDoubleNear(2.5, frame.Position.Z);
        AssertVectorNear(frame.Tangent, Vector3d.UnitZ);
    }

    [Fact]
    public void TrackPhysicsAdapter_GetFrameAtDistance_MatchesTrackEvaluatorResults()
    {
        var evaluator = new TrackEvaluator();
        var adapter = new TrackPhysicsAdapter(evaluator);
        var document = new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(length: 8.0),
            new CurvedSegment(length: 4.0)
        });
        const double distance = 9.5;

        TrackFrame expected = evaluator.EvaluateFrameAtDistance(document, distance);
        TrackFrame actual = adapter.GetFrameAtDistance(document, distance);

        AssertDoubleNear(expected.S, actual.S);
        AssertVectorNear(expected.Position, actual.Position);
        AssertVectorNear(expected.Tangent, actual.Tangent);
        AssertVectorNear(expected.Normal, actual.Normal);
        AssertVectorNear(expected.Binormal, actual.Binormal);
    }

    [Fact]
    public void TrackPhysicsAdapter_GetTransformAtDistance_MatchesTrackEvaluatorResults()
    {
        var evaluator = new TrackEvaluator();
        var adapter = new TrackPhysicsAdapter(evaluator);
        var document = new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(length: 8.0),
            new CurvedSegment(length: 4.0)
        });
        const double distance = 9.5;

        Transform3d expected = evaluator.EvaluateTransformAtDistance(document, distance);
        Transform3d actual = adapter.GetTransformAtDistance(document, distance);

        AssertDoubleNear(expected.Position.X, actual.Position.X);
        AssertDoubleNear(expected.Position.Y, actual.Position.Y);
        AssertDoubleNear(expected.Position.Z, actual.Position.Z);
        AssertDoubleNear(expected.Rotation.M00, actual.Rotation.M00);
        AssertDoubleNear(expected.Rotation.M01, actual.Rotation.M01);
        AssertDoubleNear(expected.Rotation.M02, actual.Rotation.M02);
        AssertDoubleNear(expected.Rotation.M10, actual.Rotation.M10);
        AssertDoubleNear(expected.Rotation.M11, actual.Rotation.M11);
        AssertDoubleNear(expected.Rotation.M12, actual.Rotation.M12);
        AssertDoubleNear(expected.Rotation.M20, actual.Rotation.M20);
        AssertDoubleNear(expected.Rotation.M21, actual.Rotation.M21);
        AssertDoubleNear(expected.Rotation.M22, actual.Rotation.M22);
    }

    [Fact]
    public void TrackPhysicsAdapter_ReadOnlySampling_DoesNotChangeTrainStepLoopBehavior()
    {
        IArcLengthCurve followerTrack = new LineCurve(
            new Vector3d(0.0, 10.0, 0.0),
            new Vector3d(100.0, 0.0, 0.0));

        const double deltaTime = 0.05;
        const double gravityMagnitude = 9.81;
        const double linearDrag = 0.08;
        const double quadraticDrag = 0.01;
        const double rollingResistance = 0.05;
        const int steps = 120;
        const double initialDistance = 12.5;
        const double initialSpeed = 3.25;

        var baselineFollower = new TrainFollowerState(
            followerTrack,
            initialDistance: initialDistance,
            speed: initialSpeed,
            loopEnabled: false);
        var sampledFollower = new TrainFollowerState(
            followerTrack,
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
        var sampledLoop = new TrainStepLoop(
            sampledFollower,
            deltaTime,
            gravityMagnitude,
            linearDrag,
            quadraticDrag,
            rollingResistance);

        var adapter = new TrackPhysicsAdapter();
        var sampleDocument = new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(length: 100.0)
        });

        for (int i = 0; i < steps; i++)
        {
            _ = adapter.GetFrameAtDistance(sampleDocument, sampledFollower.Distance);
            _ = adapter.GetTransformAtDistance(sampleDocument, sampledFollower.Distance);
            sampledLoop.Step();
            baselineLoop.Step();
        }

        AssertDoubleNear(baselineFollower.Distance, sampledFollower.Distance);
        AssertDoubleNear(baselineFollower.Speed, sampledFollower.Speed);
        AssertDoubleNear(baselineFollower.Acceleration, sampledFollower.Acceleration);
        AssertDoubleNear(baselineFollower.Position.X, sampledFollower.Position.X);
        AssertDoubleNear(baselineFollower.Position.Y, sampledFollower.Position.Y);
        AssertDoubleNear(baselineFollower.Position.Z, sampledFollower.Position.Z);
        AssertDoubleNear(baselineFollower.Tangent.X, sampledFollower.Tangent.X);
        AssertDoubleNear(baselineFollower.Tangent.Y, sampledFollower.Tangent.Y);
        AssertDoubleNear(baselineFollower.Tangent.Z, sampledFollower.Tangent.Z);
        Assert.Equal(baselineLoop.Tick, sampledLoop.Tick);
        AssertDoubleNear(baselineLoop.ElapsedTimeSeconds, sampledLoop.ElapsedTimeSeconds);
    }

    private static void AssertVectorNear(Vector3d actual, Vector3d expected)
    {
        AssertDoubleNear(expected.X, actual.X);
        AssertDoubleNear(expected.Y, actual.Y);
        AssertDoubleNear(expected.Z, actual.Z);
    }

    private static void AssertDoubleNear(double expected, double actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, Tolerance);
    }
}
