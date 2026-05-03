using Quantum.Math;
using Quantum.Splines;
using Xunit;

namespace Quantum.Tests;

public sealed class TrackFrameSamplerTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void TrackFrameSampler_ReturnsOrthonormalFrame()
    {
        IParamCurve paramCurve = new CubicBezierCurve(
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(4.0, 2.0, 0.0),
            new Vector3d(7.0, 6.0, 2.0),
            new Vector3d(10.0, 8.0, 3.0));

        IArcLengthCurve curve = new ArcLengthCurveAdapter(paramCurve, samples: 256);
        var sampler = new TrackFrameSampler(curve, Vector3d.UnitY);

        TrackFrame frame = sampler.GetFrameAt(curve.Length * 0.4);

        AssertUnit(frame.Tangent);
        AssertUnit(frame.Normal);
        AssertUnit(frame.Binormal);

        Assert.InRange(System.Math.Abs(Vector3d.Dot(frame.Tangent, frame.Normal)), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(Vector3d.Dot(frame.Tangent, frame.Binormal)), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(Vector3d.Dot(frame.Normal, frame.Binormal)), 0.0, Tolerance);

        Vector3d cross = Vector3d.Cross(frame.Tangent, frame.Normal);
        AssertVectorNear(cross, frame.Binormal, Tolerance);
    }

    private static void AssertUnit(Vector3d vector)
    {
        Assert.InRange(System.Math.Abs(vector.Length - 1.0), 0.0, Tolerance);
    }

    private static void AssertVectorNear(Vector3d actual, Vector3d expected, double tolerance)
    {
        Assert.InRange(System.Math.Abs(actual.X - expected.X), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(actual.Y - expected.Y), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(actual.Z - expected.Z), 0.0, tolerance);
    }
}
