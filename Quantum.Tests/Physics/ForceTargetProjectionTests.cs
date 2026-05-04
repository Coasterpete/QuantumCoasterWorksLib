using Quantum.Math;
using Quantum.Physics;
using Quantum.Splines;
using Xunit;

namespace Quantum.Tests;

public sealed class ForceTargetProjectionTests
{
    private const double ValueTolerance = 1e-6;
    private const double StandardGravity = 9.81;

    [Fact]
    public void ComputeForceVector_ProjectsNormalAndLateralCorrectly()
    {
        TrackFrame frame = new TrackFrame(
            s: 0.0,
            position: Vector3d.Zero,
            tangent: Vector3d.UnitX,
            normal: Vector3d.UnitY,
            binormal: Vector3d.UnitZ);

        Vector3d normalAcceleration = ForceTargetProjection.ComputeForceVector(
            new ForceTargets(normalG: 1.0, lateralG: 0.0, rollRateDegPerSec: 0.0),
            frame);

        Vector3d lateralAcceleration = ForceTargetProjection.ComputeForceVector(
            new ForceTargets(normalG: 0.0, lateralG: 1.0, rollRateDegPerSec: 0.0),
            frame);

        Assert.InRange(System.Math.Abs(Vector3d.Dot(normalAcceleration.Normalized(), frame.Normal) - 1.0), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(Vector3d.Dot(lateralAcceleration.Normalized(), frame.Binormal) - 1.0), 0.0, ValueTolerance);

        Assert.InRange(System.Math.Abs(normalAcceleration.Length - StandardGravity), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(lateralAcceleration.Length - StandardGravity), 0.0, ValueTolerance);
    }
}
