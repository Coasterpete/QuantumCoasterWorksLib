using Quantum.Math;
using Quantum.Splines;

namespace Quantum.Tests;

public sealed class Matrix3x3Tests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void Identity_LeavesVectorsUnchanged()
    {
        Matrix3x3 matrix = Matrix3x3.Identity;

        Vector3d[] vectors =
        {
            Vector3d.Zero,
            Vector3d.UnitX,
            Vector3d.UnitY,
            Vector3d.UnitZ,
            new Vector3d(3.5, -2.25, 7.125)
        };

        foreach (Vector3d vector in vectors)
        {
            AssertVectorNear(matrix.Multiply(vector), vector, Tolerance);
        }
    }

    [Fact]
    public void FromBasis_MapsLocalAxesToBasisVectors()
    {
        Vector3d basisX = new Vector3d(2.0, 3.0, 5.0);
        Vector3d basisY = new Vector3d(-7.0, 11.0, 13.0);
        Vector3d basisZ = new Vector3d(17.0, -19.0, 23.0);

        Matrix3x3 matrix = Matrix3x3.FromBasis(basisX, basisY, basisZ);

        AssertVectorNear(matrix.Multiply(Vector3d.UnitX), basisX, Tolerance);
        AssertVectorNear(matrix.Multiply(Vector3d.UnitY), basisY, Tolerance);
        AssertVectorNear(matrix.Multiply(Vector3d.UnitZ), basisZ, Tolerance);
    }

    [Fact]
    public void TrackFrame_BasisFormsOrthonormalMatrix()
    {
        IParamCurve paramCurve = new CubicBezierCurve(
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(4.0, 2.0, 0.0),
            new Vector3d(7.0, 6.0, 2.0),
            new Vector3d(10.0, 8.0, 3.0));

        IArcLengthCurve curve = new ArcLengthCurveAdapter(paramCurve, samples: 256);
        var sampler = new TrackFrameSampler(curve, Vector3d.UnitY);
        TrackFrame frame = sampler.GetFrameAt(curve.Length * 0.4);

        Matrix3x3 worldFromLocal = Matrix3x3.FromBasis(frame.Tangent, frame.Normal, frame.Binormal);
        Matrix3x3 localFromWorld = worldFromLocal.Transpose();

        AssertVectorNear(localFromWorld.Multiply(frame.Tangent), Vector3d.UnitX, Tolerance);
        AssertVectorNear(localFromWorld.Multiply(frame.Normal), Vector3d.UnitY, Tolerance);
        AssertVectorNear(localFromWorld.Multiply(frame.Binormal), Vector3d.UnitZ, Tolerance);
    }

    private static void AssertVectorNear(Vector3d actual, Vector3d expected, double tolerance)
    {
        Assert.InRange(System.Math.Abs(actual.X - expected.X), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(actual.Y - expected.Y), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(actual.Z - expected.Z), 0.0, tolerance);
    }
}
