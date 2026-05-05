using System.Numerics;
using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class CameraFrameBuilderTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void BuildRideCamera_AppliesOffsetInTrackLocalSpace()
    {
        var frame = new ExportTrackFrame(
            position: new Vector3d(10.0, 20.0, 30.0),
            tangent: Vector3d.UnitX,
            normal: Vector3d.UnitY,
            binormal: Vector3d.UnitZ);
        Vector3d offset = new Vector3d(2.0, -3.0, 4.0);

        CameraTransform camera = CameraFrameBuilder.BuildRideCamera(frame, offset);

        AssertVectorNear(new Vector3d(12.0, 17.0, 34.0), camera.Position);
        AssertFloatNear(12.0f, camera.Transform.M14);
        AssertFloatNear(17.0f, camera.Transform.M24);
        AssertFloatNear(34.0f, camera.Transform.M34);
    }

    [Fact]
    public void BuildRideCamera_OrientationMatchesFrameAxes()
    {
        Vector3d tangent = new Vector3d(0.0, 1.0, 0.0);
        Vector3d normal = new Vector3d(0.0, 0.0, 1.0);
        Vector3d binormal = new Vector3d(1.0, 0.0, 0.0);
        var frame = new ExportTrackFrame(
            position: new Vector3d(5.0, 6.0, 7.0),
            tangent: tangent,
            normal: normal,
            binormal: binormal);

        CameraTransform camera = CameraFrameBuilder.BuildRideCamera(frame, Vector3d.Zero);

        AssertVectorNear(tangent, camera.Forward);
        AssertVectorNear(normal, camera.Up);
        AssertVectorNear(binormal, camera.Right);

        AssertFloatNear((float)tangent.X, camera.Transform.M11);
        AssertFloatNear((float)tangent.Y, camera.Transform.M21);
        AssertFloatNear((float)tangent.Z, camera.Transform.M31);

        AssertFloatNear((float)normal.X, camera.Transform.M12);
        AssertFloatNear((float)normal.Y, camera.Transform.M22);
        AssertFloatNear((float)normal.Z, camera.Transform.M32);

        AssertFloatNear((float)binormal.X, camera.Transform.M13);
        AssertFloatNear((float)binormal.Y, camera.Transform.M23);
        AssertFloatNear((float)binormal.Z, camera.Transform.M33);
    }

    [Fact]
    public void BuildRideCamera_ReturnsFiniteMatrix()
    {
        Vector3d tangent = new Vector3d(1.0, 2.0, 3.0).Normalized();
        Vector3d normal = new Vector3d(-2.0, 0.5, 1.0).Normalized();
        Vector3d binormal = Vector3d.Cross(tangent, normal).Normalized();
        normal = Vector3d.Cross(binormal, tangent).Normalized();

        var frame = new ExportTrackFrame(
            position: new Vector3d(-3.5, 8.25, 1.125),
            tangent: tangent,
            normal: normal,
            binormal: binormal);

        CameraTransform camera = CameraFrameBuilder.BuildRideCamera(
            frame,
            new Vector3d(0.75, -1.5, 2.25));

        AssertFiniteMatrix(camera.Transform);
    }

    [Fact]
    public void BuildRideCamera_WithZeroOffset_MatchesFramePosition()
    {
        var frame = new ExportTrackFrame(
            position: new Vector3d(-4.0, 9.0, 2.5),
            tangent: Vector3d.UnitX,
            normal: Vector3d.UnitY,
            binormal: Vector3d.UnitZ);

        CameraTransform camera = CameraFrameBuilder.BuildRideCamera(frame, Vector3d.Zero);

        AssertVectorNear(frame.Position, camera.Position);
        AssertFloatNear((float)frame.Position.X, camera.Transform.M14);
        AssertFloatNear((float)frame.Position.Y, camera.Transform.M24);
        AssertFloatNear((float)frame.Position.Z, camera.Transform.M34);
    }

    [Fact]
    public void BuildTargetCamera_ForwardPointsAtTarget()
    {
        Vector3d cameraPosition = new Vector3d(1.0, 2.0, 3.0);
        Vector3d targetPosition = new Vector3d(4.0, 6.0, 3.0);
        Vector3d upHint = new Vector3d(0.0, 1.0, 0.0);

        CameraTransform camera = CameraFrameBuilder.BuildTargetCamera(cameraPosition, targetPosition, upHint);

        Vector3d expectedForward = (targetPosition - cameraPosition).Normalized();
        AssertVectorNear(expectedForward, camera.Forward);
    }

    [Fact]
    public void BuildTargetCamera_UpAndRightAreOrthonormal()
    {
        CameraTransform camera = CameraFrameBuilder.BuildTargetCamera(
            cameraPosition: new Vector3d(-3.0, 5.0, 9.0),
            targetPosition: new Vector3d(1.0, 7.0, 10.0),
            upHint: new Vector3d(0.25, 1.0, -0.75));

        Assert.InRange(System.Math.Abs(camera.Forward.Length - 1.0), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(camera.Up.Length - 1.0), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(camera.Right.Length - 1.0), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(Vector3d.Dot(camera.Forward, camera.Up)), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(Vector3d.Dot(camera.Forward, camera.Right)), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(Vector3d.Dot(camera.Up, camera.Right)), 0.0, Tolerance);
        AssertVectorNear(Vector3d.Cross(camera.Forward, camera.Up), camera.Right);
    }

    [Fact]
    public void BuildTargetCamera_ReturnsFiniteMatrix()
    {
        CameraTransform camera = CameraFrameBuilder.BuildTargetCamera(
            cameraPosition: new Vector3d(250.0, -125.0, 62.5),
            targetPosition: new Vector3d(251.5, -123.0, 61.0),
            upHint: new Vector3d(0.0, 0.0, 1.0));

        AssertFiniteMatrix(camera.Transform);
    }

    [Fact]
    public void BuildTargetCamera_ZeroLengthTargetDirection_Throws()
    {
        Vector3d position = new Vector3d(2.0, 4.0, 8.0);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CameraFrameBuilder.BuildTargetCamera(position, position, Vector3d.UnitY));
    }

    [Fact]
    public void BuildTargetCamera_WithNonFiniteInput_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CameraFrameBuilder.BuildTargetCamera(
                cameraPosition: new Vector3d(double.NaN, 0.0, 0.0),
                targetPosition: new Vector3d(1.0, 0.0, 0.0),
                upHint: Vector3d.UnitY));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CameraFrameBuilder.BuildTargetCamera(
                cameraPosition: Vector3d.Zero,
                targetPosition: new Vector3d(double.PositiveInfinity, 0.0, 0.0),
                upHint: Vector3d.UnitY));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CameraFrameBuilder.BuildTargetCamera(
                cameraPosition: Vector3d.Zero,
                targetPosition: new Vector3d(1.0, 0.0, 0.0),
                upHint: new Vector3d(0.0, double.NegativeInfinity, 0.0)));
    }

    [Fact]
    public void BuildFlyByCamera_ForwardPointsAtTargetFramePosition()
    {
        Vector3d cameraPosition = new Vector3d(-7.0, 3.0, 2.0);
        var targetFrame = new ExportTrackFrame(
            position: new Vector3d(5.0, 8.0, -1.0),
            tangent: Vector3d.UnitX,
            normal: Vector3d.UnitY,
            binormal: Vector3d.UnitZ);
        Vector3d upHint = new Vector3d(0.0, 1.0, 0.0);

        CameraTransform camera = CameraFrameBuilder.BuildFlyByCamera(cameraPosition, targetFrame, upHint);

        Vector3d expectedForward = (targetFrame.Position - cameraPosition).Normalized();
        AssertVectorNear(expectedForward, camera.Forward);
    }

    [Fact]
    public void BuildFlyByCamera_ReturnsFiniteMatrix()
    {
        CameraTransform camera = CameraFrameBuilder.BuildFlyByCamera(
            cameraPosition: new Vector3d(75.0, -12.5, 6.25),
            targetFrame: new ExportTrackFrame(
                position: new Vector3d(77.0, -10.0, 5.0),
                tangent: Vector3d.UnitY,
                normal: Vector3d.UnitZ,
                binormal: Vector3d.UnitX),
            upHint: new Vector3d(0.0, 0.0, 1.0));

        AssertFiniteMatrix(camera.Transform);
    }

    [Fact]
    public void BuildFlyByCamera_MatchesBuildTargetCameraForSameInputs()
    {
        Vector3d cameraPosition = new Vector3d(-2.0, 4.5, 7.25);
        var targetFrame = new ExportTrackFrame(
            position: new Vector3d(3.0, 1.5, -0.25),
            tangent: Vector3d.UnitZ,
            normal: Vector3d.UnitX,
            binormal: Vector3d.UnitY);
        Vector3d upHint = new Vector3d(0.1, 1.0, -0.2);

        CameraTransform targetCamera = CameraFrameBuilder.BuildTargetCamera(
            cameraPosition,
            targetFrame.Position,
            upHint);
        CameraTransform flyByCamera = CameraFrameBuilder.BuildFlyByCamera(
            cameraPosition,
            targetFrame,
            upHint);

        AssertVectorNear(targetCamera.Position, flyByCamera.Position);
        AssertVectorNear(targetCamera.Forward, flyByCamera.Forward);
        AssertVectorNear(targetCamera.Up, flyByCamera.Up);
        AssertVectorNear(targetCamera.Right, flyByCamera.Right);
        AssertMatrixNear(targetCamera.Transform, flyByCamera.Transform);
    }

    [Fact]
    public void BuildFlyByCamera_WithNonFiniteInput_Throws()
    {
        var targetFrame = new ExportTrackFrame(
            position: new Vector3d(1.0, 2.0, 3.0),
            tangent: Vector3d.UnitX,
            normal: Vector3d.UnitY,
            binormal: Vector3d.UnitZ);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CameraFrameBuilder.BuildFlyByCamera(
                cameraPosition: new Vector3d(double.NaN, 0.0, 0.0),
                targetFrame: targetFrame,
                upHint: Vector3d.UnitY));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CameraFrameBuilder.BuildFlyByCamera(
                cameraPosition: Vector3d.Zero,
                targetFrame: targetFrame,
                upHint: new Vector3d(0.0, double.PositiveInfinity, 0.0)));
    }

    [Fact]
    public void BuildFlyViewCamera_DefaultYawAndPitch_FacesExpectedForwardDirection()
    {
        var state = new FlyViewCameraState(
            position: new Vector3d(1.0, 2.0, 3.0),
            yawRadians: 0.0,
            pitchRadians: 0.0);

        CameraTransform camera = CameraFrameBuilder.BuildFlyViewCamera(state);

        AssertVectorNear(Vector3d.UnitX, camera.Forward);
        AssertVectorNear(Vector3d.UnitY, camera.Up);
        AssertVectorNear(Vector3d.UnitZ, camera.Right);
    }

    [Fact]
    public void BuildFlyViewCamera_YawRotatesForward()
    {
        var state = new FlyViewCameraState(
            position: Vector3d.Zero,
            yawRadians: System.Math.PI * 0.5,
            pitchRadians: 0.0);

        CameraTransform camera = CameraFrameBuilder.BuildFlyViewCamera(state);

        AssertVectorNear(Vector3d.UnitZ, camera.Forward);
    }

    [Fact]
    public void BuildFlyViewCamera_PitchRotatesForwardUpAndDown()
    {
        double pitch = System.Math.PI * 0.25;
        var upState = new FlyViewCameraState(
            position: Vector3d.Zero,
            yawRadians: 0.0,
            pitchRadians: pitch);
        var downState = new FlyViewCameraState(
            position: Vector3d.Zero,
            yawRadians: 0.0,
            pitchRadians: -pitch);

        CameraTransform upCamera = CameraFrameBuilder.BuildFlyViewCamera(upState);
        CameraTransform downCamera = CameraFrameBuilder.BuildFlyViewCamera(downState);

        double halfSqrtTwo = System.Math.Sqrt(0.5);
        AssertVectorNear(new Vector3d(halfSqrtTwo, halfSqrtTwo, 0.0), upCamera.Forward);
        AssertVectorNear(new Vector3d(halfSqrtTwo, -halfSqrtTwo, 0.0), downCamera.Forward);
    }

    [Fact]
    public void BuildFlyViewCamera_ReturnsFiniteMatrix()
    {
        var state = new FlyViewCameraState(
            position: new Vector3d(120.5, -40.25, 9.75),
            yawRadians: 2.2,
            pitchRadians: double.MaxValue,
            rollRadians: -1.4);

        CameraTransform camera = CameraFrameBuilder.BuildFlyViewCamera(state);

        AssertFiniteMatrix(camera.Transform);
    }

    [Fact]
    public void BuildFlyViewCamera_WithInvalidInput_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CameraFrameBuilder.BuildFlyViewCamera(new FlyViewCameraState(
                position: new Vector3d(double.NaN, 0.0, 0.0),
                yawRadians: 0.0,
                pitchRadians: 0.0)));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CameraFrameBuilder.BuildFlyViewCamera(new FlyViewCameraState(
                position: Vector3d.Zero,
                yawRadians: double.PositiveInfinity,
                pitchRadians: 0.0)));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CameraFrameBuilder.BuildFlyViewCamera(new FlyViewCameraState(
                position: Vector3d.Zero,
                yawRadians: 0.0,
                pitchRadians: double.NegativeInfinity)));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CameraFrameBuilder.BuildFlyViewCamera(new FlyViewCameraState(
                position: Vector3d.Zero,
                yawRadians: 0.0,
                pitchRadians: 0.0,
                rollRadians: double.NaN)));
    }

    [Fact]
    public void BuildBRollCamera_AppliesOffsetInTrackLocalSpace()
    {
        var evaluator = CreateBoundLineEvaluator(length: 20.0);
        var frame = new ExportTrackFrame(
            distance: 5.0,
            position: new Vector3d(10.0, 20.0, 30.0),
            tangent: Vector3d.UnitX,
            normal: Vector3d.UnitY,
            binormal: Vector3d.UnitZ);
        Vector3d localOffset = new Vector3d(2.0, -3.0, 4.0);

        CameraTransform camera = CameraFrameBuilder.BuildBRollCamera(
            frame,
            localOffset,
            lookAheadDistance: 0.0,
            evaluator,
            upHint: Vector3d.UnitY);

        AssertVectorNear(new Vector3d(12.0, 17.0, 34.0), camera.Position);
        AssertFloatNear(12.0f, camera.Transform.M14);
        AssertFloatNear(17.0f, camera.Transform.M24);
        AssertFloatNear(34.0f, camera.Transform.M34);
    }

    [Fact]
    public void BuildBRollCamera_LookAhead_ShiftsTarget()
    {
        var evaluator = CreateBoundLineEvaluator(length: 20.0);
        ExportTrackFrame targetFrame = evaluator.EvaluateFrameAtDistance(5.0);
        Vector3d localOffset = new Vector3d(0.0, 1.0, 0.0);
        double lookAheadDistance = 3.5;
        Vector3d upHint = Vector3d.UnitY;

        CameraTransform camera = CameraFrameBuilder.BuildBRollCamera(
            targetFrame,
            localOffset,
            lookAheadDistance,
            evaluator,
            upHint);

        Vector3d expectedCameraPosition = targetFrame.Position + localOffset.Y * targetFrame.Normal;
        Vector3d expectedTargetPosition = evaluator.EvaluateFrameAtDistance(
            targetFrame.Distance + lookAheadDistance).Position;
        CameraTransform expectedCamera = CameraFrameBuilder.BuildTargetCamera(
            expectedCameraPosition,
            expectedTargetPosition,
            upHint);

        AssertVectorNear(expectedCamera.Forward, camera.Forward);
        AssertVectorNear(expectedCamera.Up, camera.Up);
        AssertVectorNear(expectedCamera.Right, camera.Right);
        AssertMatrixNear(expectedCamera.Transform, camera.Transform);
    }

    [Fact]
    public void BuildBRollCamera_ZeroLookAhead_MatchesTargetFramePosition()
    {
        var evaluator = CreateBoundLineEvaluator(length: 20.0);
        ExportTrackFrame targetFrame = evaluator.EvaluateFrameAtDistance(6.0);
        Vector3d localOffset = new Vector3d(0.0, -2.0, 0.0);
        Vector3d upHint = Vector3d.UnitY;

        CameraTransform camera = CameraFrameBuilder.BuildBRollCamera(
            targetFrame,
            localOffset,
            lookAheadDistance: 0.0,
            evaluator,
            upHint);

        Vector3d expectedCameraPosition = targetFrame.Position + (targetFrame.Normal * localOffset.Y);
        CameraTransform expectedCamera = CameraFrameBuilder.BuildTargetCamera(
            expectedCameraPosition,
            targetFrame.Position,
            upHint);

        AssertVectorNear(expectedCamera.Forward, camera.Forward);
        AssertVectorNear(expectedCamera.Up, camera.Up);
        AssertVectorNear(expectedCamera.Right, camera.Right);
        AssertMatrixNear(expectedCamera.Transform, camera.Transform);
    }

    [Fact]
    public void BuildBRollCamera_ReturnsFiniteMatrix()
    {
        var evaluator = CreateBoundLineEvaluator(length: 30.0);
        ExportTrackFrame targetFrame = evaluator.EvaluateFrameAtDistance(7.0);

        CameraTransform camera = CameraFrameBuilder.BuildBRollCamera(
            targetFrame,
            localOffset: new Vector3d(0.5, 1.25, -0.75),
            lookAheadDistance: 4.0,
            evaluator,
            upHint: new Vector3d(0.0, 1.0, 0.25));

        AssertFiniteMatrix(camera.Transform);
    }

    [Fact]
    public void BuildBRollCamera_WithInvalidInput_Throws()
    {
        var evaluator = CreateBoundLineEvaluator(length: 10.0);
        ExportTrackFrame targetFrame = evaluator.EvaluateFrameAtDistance(3.0);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CameraFrameBuilder.BuildBRollCamera(
                targetFrame,
                localOffset: new Vector3d(double.NaN, 0.0, 0.0),
                lookAheadDistance: 0.0,
                evaluator,
                upHint: Vector3d.UnitY));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CameraFrameBuilder.BuildBRollCamera(
                targetFrame,
                localOffset: Vector3d.Zero,
                lookAheadDistance: double.PositiveInfinity,
                evaluator,
                upHint: Vector3d.UnitY));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CameraFrameBuilder.BuildBRollCamera(
                targetFrame,
                localOffset: Vector3d.Zero,
                lookAheadDistance: 0.0,
                evaluator,
                upHint: new Vector3d(0.0, double.NegativeInfinity, 0.0)));

        Assert.Throws<ArgumentNullException>(() =>
            CameraFrameBuilder.BuildBRollCamera(
                targetFrame,
                localOffset: Vector3d.Zero,
                lookAheadDistance: 0.0,
                evaluator: null!,
                upHint: Vector3d.UnitY));

        var hugeDistanceFrame = new ExportTrackFrame(
            distance: double.MaxValue,
            position: targetFrame.Position,
            tangent: targetFrame.Tangent,
            normal: targetFrame.Normal,
            binormal: targetFrame.Binormal);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CameraFrameBuilder.BuildBRollCamera(
                hugeDistanceFrame,
                localOffset: Vector3d.Zero,
                lookAheadDistance: double.MaxValue,
                evaluator,
                upHint: Vector3d.UnitY));
    }

    private static TrackEvaluator CreateBoundLineEvaluator(double length)
    {
        IParamCurve spline = new LineCurve(
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(length, 0.0, 0.0));
        var document = new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(length: length, spline: spline)
        });

        return new TrackEvaluator(document);
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual)
    {
        Assert.InRange(System.Math.Abs(expected.X - actual.X), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(expected.Y - actual.Y), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(expected.Z - actual.Z), 0.0, Tolerance);
    }

    private static void AssertFloatNear(float expected, float actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0f, 1e-6f);
    }

    private static void AssertMatrixNear(Matrix4x4 expected, Matrix4x4 actual)
    {
        AssertFloatNear(expected.M11, actual.M11);
        AssertFloatNear(expected.M12, actual.M12);
        AssertFloatNear(expected.M13, actual.M13);
        AssertFloatNear(expected.M14, actual.M14);
        AssertFloatNear(expected.M21, actual.M21);
        AssertFloatNear(expected.M22, actual.M22);
        AssertFloatNear(expected.M23, actual.M23);
        AssertFloatNear(expected.M24, actual.M24);
        AssertFloatNear(expected.M31, actual.M31);
        AssertFloatNear(expected.M32, actual.M32);
        AssertFloatNear(expected.M33, actual.M33);
        AssertFloatNear(expected.M34, actual.M34);
        AssertFloatNear(expected.M41, actual.M41);
        AssertFloatNear(expected.M42, actual.M42);
        AssertFloatNear(expected.M43, actual.M43);
        AssertFloatNear(expected.M44, actual.M44);
    }

    private static void AssertFiniteMatrix(Matrix4x4 matrix)
    {
        AssertFinite(matrix.M11);
        AssertFinite(matrix.M12);
        AssertFinite(matrix.M13);
        AssertFinite(matrix.M14);
        AssertFinite(matrix.M21);
        AssertFinite(matrix.M22);
        AssertFinite(matrix.M23);
        AssertFinite(matrix.M24);
        AssertFinite(matrix.M31);
        AssertFinite(matrix.M32);
        AssertFinite(matrix.M33);
        AssertFinite(matrix.M34);
        AssertFinite(matrix.M41);
        AssertFinite(matrix.M42);
        AssertFinite(matrix.M43);
        AssertFinite(matrix.M44);
    }

    private static void AssertFinite(float value)
    {
        Assert.False(float.IsNaN(value));
        Assert.False(float.IsInfinity(value));
    }
}
