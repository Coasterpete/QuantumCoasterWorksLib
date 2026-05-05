using Quantum.Math;
using Quantum.Physics;
using Quantum.Splines;
using Quantum.Track;

namespace Quantum.Tests;

public sealed class TrackFrameProviderFromDocumentTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void ITrackFrameProvider_DefaultCurvatureContract_ReturnsFalseAndZero()
    {
        ITrackFrameProvider provider = new FrameOnlyTrackFrameProvider();

        bool hasCurvature = provider.TryGetCurvatureAtDistance(distance: 1.0, out double curvature);

        Assert.False(hasCurvature);
        AssertDoubleNear(0.0, curvature);
    }

    [Fact]
    public void TrackFrameProviderFromDocument_CanBeConstructedAndQueried()
    {
        var document = new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(
                length: 10.0,
                spline: new LineCurve(
                    new Vector3d(0.0, 0.0, 0.0),
                    new Vector3d(0.0, 0.0, 10.0)))
        });

        var provider = new TrackFrameProviderFromDocument(new TrackPhysicsAdapter(), document);

        bool hasFrame = provider.TryGetFrameAtDistance(2.5, out TrackFrame frame);

        Assert.True(hasFrame);
        AssertDoubleNear(2.5, frame.S);
        AssertDoubleNear(0.0, frame.Position.X);
        AssertDoubleNear(0.0, frame.Position.Y);
        AssertDoubleNear(2.5, frame.Position.Z);
        AssertVectorNear(frame.Tangent, Vector3d.UnitZ);
    }

    [Fact]
    public void TrackFrameProviderFromDocument_ReturnsFramesMatchingTrackPhysicsAdapter()
    {
        var adapter = new TrackPhysicsAdapter();
        var document = new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(length: 8.0),
            new CurvedSegment(length: 4.0)
        });
        const double distance = 9.5;

        var provider = new TrackFrameProviderFromDocument(adapter, document);
        TrackFrame expected = adapter.GetFrameAtDistance(document, distance);

        bool hasFrame = provider.TryGetFrameAtDistance(distance, out TrackFrame actual);

        Assert.True(hasFrame);
        AssertDoubleNear(expected.S, actual.S);
        AssertVectorNear(expected.Position, actual.Position);
        AssertVectorNear(expected.Tangent, actual.Tangent);
        AssertVectorNear(expected.Normal, actual.Normal);
        AssertVectorNear(expected.Binormal, actual.Binormal);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void TrackFrameProviderFromDocument_TryGetFrameAtDistance_InvalidDistance_ReturnsFalse(double invalidDistance)
    {
        var document = new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(length: 8.0)
        });
        var provider = new TrackFrameProviderFromDocument(new TrackPhysicsAdapter(), document);

        bool hasFrame = provider.TryGetFrameAtDistance(invalidDistance, out TrackFrame frame);

        Assert.False(hasFrame);
        Assert.Equal(default, frame);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void TrackFrameProviderFromDocument_TryGetCurvatureAtDistance_InvalidDistance_ReturnsFalseAndZero(double invalidDistance)
    {
        var document = new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(length: 8.0)
        });
        var provider = new TrackFrameProviderFromDocument(new TrackPhysicsAdapter(), document);

        bool hasCurvature = provider.TryGetCurvatureAtDistance(invalidDistance, out double curvature);

        Assert.False(hasCurvature);
        AssertDoubleNear(0.0, curvature);
    }

    [Fact]
    public void TrainStepLoop_WithUnusedTrackFrameProvider_MatchesBaselineBehavior()
    {
        IArcLengthCurve track = new LineCurve(
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

        var recordingProvider = new RecordingTrackFrameProvider();
        var providerLoop = new TrainStepLoop(
            providerFollower,
            deltaTime,
            gravityMagnitude,
            linearDrag,
            quadraticDrag,
            rollingResistance,
            recordingProvider);

        baselineLoop.Step(steps);
        providerLoop.Step(steps);

        AssertDoubleNear(baselineFollower.Distance, providerFollower.Distance);
        AssertDoubleNear(baselineFollower.Speed, providerFollower.Speed);
        AssertDoubleNear(baselineFollower.Acceleration, providerFollower.Acceleration);
        AssertDoubleNear(baselineFollower.Position.X, providerFollower.Position.X);
        AssertDoubleNear(baselineFollower.Position.Y, providerFollower.Position.Y);
        AssertDoubleNear(baselineFollower.Position.Z, providerFollower.Position.Z);
        AssertDoubleNear(baselineFollower.Tangent.X, providerFollower.Tangent.X);
        AssertDoubleNear(baselineFollower.Tangent.Y, providerFollower.Tangent.Y);
        AssertDoubleNear(baselineFollower.Tangent.Z, providerFollower.Tangent.Z);
        Assert.Equal(baselineLoop.Tick, providerLoop.Tick);
        AssertDoubleNear(baselineLoop.ElapsedTimeSeconds, providerLoop.ElapsedTimeSeconds);
        Assert.Equal(0, recordingProvider.CallCount);
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

    private sealed class FrameOnlyTrackFrameProvider : ITrackFrameProvider
    {
        public bool TryGetFrameAtDistance(double distance, out TrackFrame frame)
        {
            frame = default;
            return false;
        }
    }

    private sealed class RecordingTrackFrameProvider : ITrackFrameProvider
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
