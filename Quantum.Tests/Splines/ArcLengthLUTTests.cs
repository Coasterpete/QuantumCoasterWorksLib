using Quantum.Math;
using Quantum.Splines;

namespace Quantum.Tests.Splines;

public sealed class ArcLengthLUTTests
{
    [Fact]
    public void MapS2T_UsesNonlinearDistanceToParameterMapping()
    {
        var lut = new ArcLengthLUT(
            new PowerCurve(),
            samples: 2,
            tolerance: 1e-6);

        double t = lut.MapS2T(0.3);

        Assert.InRange(System.Math.Abs(t - System.Math.Sqrt(0.3)), 0.0, 1e-4);
    }

    [Fact]
    public void MapS2T_ClampsOutsideDistanceDomain()
    {
        var lut = new ArcLengthLUT(
            new PowerCurve(),
            samples: 2,
            tolerance: 1e-6);

        Assert.Equal(0.0, lut.MapS2T(-1.0));
        Assert.Equal(0.0, lut.MapS2T(0.0));
        Assert.Equal(1.0, lut.MapS2T(lut.TotalLength));
        Assert.Equal(1.0, lut.MapS2T(lut.TotalLength + 1.0));
    }

    [Fact]
    public void MapS2T_IsMonotonicAcrossCurveLength()
    {
        var curve = new CubicBezierCurve(
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(0.0, 4.0, 0.0),
            new Vector3d(5.0, -3.0, 0.0),
            new Vector3d(8.0, 1.0, 0.0));
        var lut = new ArcLengthLUT(curve, samples: 4, tolerance: 1e-5);

        double previousT = 0.0;
        for (int i = 0; i <= 200; i++)
        {
            double s = lut.TotalLength * i / 200.0;
            double t = lut.MapS2T(s);

            Assert.InRange(t, previousT, 1.0);
            previousT = t;
        }
    }

    [Fact]
    public void MapS2T_RemainsMonotonicAcrossStationaryInterval()
    {
        var lut = new ArcLengthLUT(
            new PausedLinearCurve(),
            samples: 2,
            tolerance: 1e-6);

        double previousT = 0.0;
        for (int i = 0; i <= 100; i++)
        {
            double s = lut.TotalLength * i / 100.0;
            double t = lut.MapS2T(s);

            Assert.InRange(t, previousT, 1.0);
            previousT = t;
        }
    }

    [Fact]
    public void DegenerateCurve_HasStableZeroLengthMapping()
    {
        var point = new Vector3d(2.0, -1.0, 4.0);
        var curve = new ConstantCurve(point);
        var lut = new ArcLengthLUT(curve, samples: 2, tolerance: 1e-8);
        var adapter = new ArcLengthCurveAdapter(curve, samples: 2, tolerance: 1e-8);

        Assert.Equal(0.0, lut.TotalLength);
        Assert.Equal(0.0, lut.MapS2T(-1.0));
        Assert.Equal(0.0, lut.MapS2T(0.0));
        Assert.Equal(0.0, lut.MapS2T(1.0));
        AssertVectorNear(point, adapter.EvaluateByLength(-1.0), 0.0);
        AssertVectorNear(point, adapter.EvaluateByLength(1.0), 0.0);
    }

    [Fact]
    public void Adapter_ClampsDistanceEvaluationToCurveEndpoints()
    {
        var curve = new PowerCurve();
        var adapter = new ArcLengthCurveAdapter(curve, samples: 2, tolerance: 1e-6);

        AssertVectorNear(curve.Evaluate(0.0), adapter.EvaluateByLength(-1.0), 0.0);
        AssertVectorNear(curve.Evaluate(1.0), adapter.EvaluateByLength(adapter.Length + 1.0), 0.0);
    }

    [Fact]
    public void TighterTolerance_ImprovesArcLengthAccuracy()
    {
        var curve = new ParabolaCurve();
        var loose = new ArcLengthLUT(curve, samples: 2, tolerance: 1e-2);
        var tight = new ArcLengthLUT(curve, samples: 2, tolerance: 1e-6);
        double exactLength =
            (0.5 * System.Math.Sqrt(5.0)) +
            (0.25 * System.Math.Asinh(2.0));

        double looseError = System.Math.Abs(loose.TotalLength - exactLength);
        double tightError = System.Math.Abs(tight.TotalLength - exactLength);

        Assert.True(tightError < looseError);
        Assert.InRange(tightError, 0.0, 1e-6);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_RejectsInvalidTolerance(double tolerance)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ArcLengthLUT(new PowerCurve(), samples: 2, tolerance: tolerance));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ArcLengthCurveAdapter(new PowerCurve(), samples: 2, tolerance: tolerance));
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual, double tolerance)
    {
        Assert.InRange(System.Math.Abs(expected.X - actual.X), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(expected.Y - actual.Y), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(expected.Z - actual.Z), 0.0, tolerance);
    }

    private sealed class PowerCurve : IParamCurve
    {
        public Vector3d Evaluate(double t)
        {
            return new Vector3d(t * t, 0.0, 0.0);
        }

        public Vector3d Tangent(double t)
        {
            return Vector3d.UnitX;
        }
    }

    private sealed class ParabolaCurve : IParamCurve
    {
        public Vector3d Evaluate(double t)
        {
            return new Vector3d(t, t * t, 0.0);
        }

        public Vector3d Tangent(double t)
        {
            return new Vector3d(1.0, 2.0 * t, 0.0).Normalized();
        }
    }

    private sealed class ConstantCurve : IParamCurve
    {
        private readonly Vector3d _point;

        public ConstantCurve(Vector3d point)
        {
            _point = point;
        }

        public Vector3d Evaluate(double t)
        {
            return _point;
        }

        public Vector3d Tangent(double t)
        {
            return Vector3d.UnitX;
        }
    }

    private sealed class PausedLinearCurve : IParamCurve
    {
        public Vector3d Evaluate(double t)
        {
            double x;
            if (t < 0.4)
                x = t;
            else if (t <= 0.6)
                x = 0.4;
            else
                x = t - 0.2;

            return new Vector3d(x, 0.0, 0.0);
        }

        public Vector3d Tangent(double t)
        {
            return Vector3d.UnitX;
        }
    }
}
