using Quantum.Math;
using Quantum.Splines;

namespace Quantum.Tests;

public sealed class Transform3dTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void IdentityTransform_LeavesPointAndDirectionUnchanged()
    {
        Transform3d transform = Transform3d.Identity;
        Vector3d localPoint = new Vector3d(3.25, -1.5, 4.75);
        Vector3d localDirection = new Vector3d(-2.0, 5.5, 0.125);

        AssertVectorNear(transform.TransformPoint(localPoint), localPoint, Tolerance);
        AssertVectorNear(transform.TransformDirection(localDirection), localDirection, Tolerance);
    }

    [Fact]
    public void RotationOnly_TransformsDirectionsWithoutTranslation()
    {
        Matrix3x3 rotation = Matrix3x3.FromBasis(
            x: new Vector3d(0.0, 1.0, 0.0),
            y: new Vector3d(-1.0, 0.0, 0.0),
            z: Vector3d.UnitZ);

        Transform3d transform = new Transform3d(rotation, Vector3d.Zero);

        AssertVectorNear(transform.TransformDirection(Vector3d.UnitX), Vector3d.UnitY, Tolerance);
        AssertVectorNear(transform.TransformDirection(Vector3d.UnitY), new Vector3d(-1.0, 0.0, 0.0), Tolerance);
        AssertVectorNear(transform.TransformPoint(Vector3d.UnitX), Vector3d.UnitY, Tolerance);
    }

    [Fact]
    public void TranslationOnly_ShiftsPointsButNotDirections()
    {
        Vector3d translation = new Vector3d(10.0, -2.0, 5.0);
        Transform3d transform = new Transform3d(Matrix3x3.Identity, translation);

        Vector3d point = new Vector3d(1.0, 2.0, 3.0);
        Vector3d direction = new Vector3d(4.0, 5.0, 6.0);

        AssertVectorNear(transform.TransformPoint(point), point + translation, Tolerance);
        AssertVectorNear(transform.TransformDirection(direction), direction, Tolerance);
    }

    [Fact]
    public void FromTrackFrame_UsesFrameBasisAndProvidedPosition()
    {
        CurveFrame frame = new CurveFrame(
            s: 12.0,
            position: new Vector3d(99.0, 99.0, 99.0),
            tangent: Vector3d.UnitX,
            normal: Vector3d.UnitY,
            binormal: Vector3d.UnitZ);

        Vector3d desiredPosition = new Vector3d(7.0, 8.0, 9.0);
        Transform3d transform = Transform3d.FromTrackFrame(frame, desiredPosition);

        AssertVectorNear(transform.Rotation.Multiply(Vector3d.UnitX), frame.Tangent, Tolerance);
        AssertVectorNear(transform.Rotation.Multiply(Vector3d.UnitY), frame.Normal, Tolerance);
        AssertVectorNear(transform.Rotation.Multiply(Vector3d.UnitZ), frame.Binormal, Tolerance);
        AssertVectorNear(transform.Position, desiredPosition, Tolerance);
    }

    private static void AssertVectorNear(Vector3d actual, Vector3d expected, double tolerance)
    {
        Assert.InRange(System.Math.Abs(actual.X - expected.X), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(actual.Y - expected.Y), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(actual.Z - expected.Z), 0.0, tolerance);
    }
}
