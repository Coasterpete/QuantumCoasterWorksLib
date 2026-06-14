using Quantum.Math;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class TrackStartPoseTests
{
    private const double Tolerance = 1e-12;

    [Fact]
    public void Identity_UsesCanonicalAuthoringBasis()
    {
        TrackStartPose pose = TrackStartPose.Identity;

        AssertVectorNear(Vector3d.Zero, pose.Position);
        AssertVectorNear(Vector3d.UnitX, pose.Tangent);
        AssertVectorNear(Vector3d.UnitY, pose.Normal);
        AssertVectorNear(Vector3d.UnitZ, pose.Binormal);
    }

    [Fact]
    public void Constructor_PreservesValidRotatedBasisWithoutNormalization()
    {
        double angle = 0.37;
        double cos = System.Math.Cos(angle);
        double sin = System.Math.Sin(angle);
        var position = new Vector3d(4.0, -2.0, 7.0);
        var tangent = new Vector3d(cos, sin, 0.0);
        var normal = new Vector3d(-sin, cos, 0.0);
        var binormal = Vector3d.UnitZ;

        var pose = new TrackStartPose(position, tangent, normal, binormal);

        AssertVectorNear(position, pose.Position);
        AssertVectorNear(tangent, pose.Tangent);
        AssertVectorNear(normal, pose.Normal);
        AssertVectorNear(binormal, pose.Binormal);
    }

    [Fact]
    public void Constructor_RejectsNonFinitePositionAndAxes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TrackStartPose(
            new Vector3d(double.NaN, 0.0, 0.0),
            Vector3d.UnitX,
            Vector3d.UnitY,
            Vector3d.UnitZ));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TrackStartPose(
            Vector3d.Zero,
            new Vector3d(double.PositiveInfinity, 0.0, 0.0),
            Vector3d.UnitY,
            Vector3d.UnitZ));
    }

    [Fact]
    public void Constructor_RejectsNearZeroAxis()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TrackStartPose(
            Vector3d.Zero,
            new Vector3d(1e-12, 0.0, 0.0),
            Vector3d.UnitY,
            Vector3d.UnitZ));
    }

    [Fact]
    public void Constructor_RejectsNonUnitAxisWithoutNormalizing()
    {
        Assert.Throws<ArgumentException>(() => new TrackStartPose(
            Vector3d.Zero,
            new Vector3d(1.001, 0.0, 0.0),
            Vector3d.UnitY,
            Vector3d.UnitZ));
    }

    [Fact]
    public void Constructor_RejectsNonOrthogonalBasis()
    {
        double inverseSqrtTwo = 1.0 / System.Math.Sqrt(2.0);

        Assert.Throws<ArgumentException>(() => new TrackStartPose(
            Vector3d.Zero,
            Vector3d.UnitX,
            new Vector3d(inverseSqrtTwo, inverseSqrtTwo, 0.0),
            Vector3d.UnitZ));
    }

    [Fact]
    public void Constructor_RejectsLeftHandedBasis()
    {
        Assert.Throws<ArgumentException>(() => new TrackStartPose(
            Vector3d.Zero,
            Vector3d.UnitX,
            Vector3d.UnitY,
            new Vector3d(0.0, 0.0, -1.0)));
    }

    [Fact]
    public void TrackAuthoringDefinition_RejectsNullStartPose()
    {
        Assert.Throws<ArgumentNullException>(() => new TrackAuthoringDefinition(
            new[] { new StraightSectionDefinition("straight", 1.0) },
            null!));
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual)
    {
        Assert.InRange(System.Math.Abs(expected.X - actual.X), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(expected.Y - actual.Y), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(expected.Z - actual.Z), 0.0, Tolerance);
    }
}
