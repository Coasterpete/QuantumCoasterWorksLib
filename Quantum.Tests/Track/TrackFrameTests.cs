using System;
using System.Numerics;
using Quantum.Math;
using TrackFrameV9 = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class TrackFrameTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void ToMatrix4x4_PreservesPosition()
    {
        var frame = new TrackFrameV9(
            position: new Vector3d(4.5, -2.25, 8.75),
            tangent: Vector3d.UnitX,
            normal: Vector3d.UnitY,
            binormal: Vector3d.UnitZ);

        Matrix4x4 matrix = frame.ToMatrix4x4();

        AssertFloatNear(4.5f, matrix.M14);
        AssertFloatNear(-2.25f, matrix.M24);
        AssertFloatNear(8.75f, matrix.M34);
        AssertFloatNear(1.0f, matrix.M44);
    }

    [Fact]
    public void ToMatrix4x4_UsesCorrectBasisVectors()
    {
        Vector3d tangent = new Vector3d(2.0, 3.0, 5.0);
        Vector3d normal = new Vector3d(-7.0, 11.0, 13.0);
        Vector3d binormal = new Vector3d(17.0, -19.0, 23.0);
        var frame = new TrackFrameV9(
            position: new Vector3d(29.0, 31.0, 37.0),
            tangent: tangent,
            normal: normal,
            binormal: binormal);

        Matrix4x4 matrix = frame.ToMatrix4x4();

        AssertFloatNear(2.0f, matrix.M11);
        AssertFloatNear(-7.0f, matrix.M12);
        AssertFloatNear(17.0f, matrix.M13);
        AssertFloatNear(3.0f, matrix.M21);
        AssertFloatNear(11.0f, matrix.M22);
        AssertFloatNear(-19.0f, matrix.M23);
        AssertFloatNear(5.0f, matrix.M31);
        AssertFloatNear(13.0f, matrix.M32);
        AssertFloatNear(23.0f, matrix.M33);
    }

    [Fact]
    public void ToMatrix4x4_WithIdentityFrame_ProducesIdentityMatrix()
    {
        var frame = new TrackFrameV9(
            position: Vector3d.Zero,
            tangent: Vector3d.UnitX,
            normal: Vector3d.UnitY,
            binormal: Vector3d.UnitZ);

        Matrix4x4 matrix = frame.ToMatrix4x4();

        AssertFloatNear(1.0f, matrix.M11);
        AssertFloatNear(0.0f, matrix.M12);
        AssertFloatNear(0.0f, matrix.M13);
        AssertFloatNear(0.0f, matrix.M14);
        AssertFloatNear(0.0f, matrix.M21);
        AssertFloatNear(1.0f, matrix.M22);
        AssertFloatNear(0.0f, matrix.M23);
        AssertFloatNear(0.0f, matrix.M24);
        AssertFloatNear(0.0f, matrix.M31);
        AssertFloatNear(0.0f, matrix.M32);
        AssertFloatNear(1.0f, matrix.M33);
        AssertFloatNear(0.0f, matrix.M34);
        AssertFloatNear(0.0f, matrix.M41);
        AssertFloatNear(0.0f, matrix.M42);
        AssertFloatNear(0.0f, matrix.M43);
        AssertFloatNear(1.0f, matrix.M44);
    }

    [Fact]
    public void ToMatrix4x4_WithOrthonormalFrame_ProducesOrthonormalMatrix()
    {
        Vector3d tangent = new Vector3d(0.0, 1.0, 0.0);
        Vector3d normal = new Vector3d(0.0, 0.0, 1.0);
        Vector3d binormal = new Vector3d(1.0, 0.0, 0.0);
        var frame = new TrackFrameV9(
            position: new Vector3d(12.0, -3.0, 4.0),
            tangent: tangent,
            normal: normal,
            binormal: binormal);

        Matrix4x4 matrix = frame.ToMatrix4x4();

        Vector3d columnT = new Vector3d(matrix.M11, matrix.M21, matrix.M31);
        Vector3d columnN = new Vector3d(matrix.M12, matrix.M22, matrix.M32);
        Vector3d columnB = new Vector3d(matrix.M13, matrix.M23, matrix.M33);

        Assert.InRange(System.Math.Abs(columnT.Length - 1.0), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(columnN.Length - 1.0), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(columnB.Length - 1.0), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(Vector3d.Dot(columnT, columnN)), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(Vector3d.Dot(columnT, columnB)), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(Vector3d.Dot(columnN, columnB)), 0.0, Tolerance);
    }

    [Fact]
    public void Constructor_WithNonFiniteInput_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TrackFrameV9(
            position: new Vector3d(double.NaN, 0.0, 0.0),
            tangent: Vector3d.UnitX,
            normal: Vector3d.UnitY,
            binormal: Vector3d.UnitZ));

        Assert.Throws<ArgumentOutOfRangeException>(() => TrackFrameV9.CreateFromFrame(
            position: Vector3d.Zero,
            t: new Vector3d(double.PositiveInfinity, 0.0, 0.0),
            n: Vector3d.UnitY,
            b: Vector3d.UnitZ));
    }

    private static void AssertFloatNear(float expected, float actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0f, 1e-6f);
    }
}
