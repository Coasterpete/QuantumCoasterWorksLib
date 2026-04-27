using System;
using System.Collections.Generic;
using Quantum.Math;
using Quantum.Physics;
using Quantum.Splines;
using Xunit;

namespace Quantum.Tests;

public class FoundationTests
{
    private const double LengthTolerance = 1e-6;
    private const double ValueTolerance = 1e-6;

    [Fact]
    public void Evaluate_ReturnsValidPositions_ForCurrentCurves()
    {
        foreach (var (_, curve) in BuildCurves())
        {
            foreach (double t in SampleTs())
            {
                Vector3d pos = curve.Evaluate(t);
                AssertFinite(pos);
            }
        }
    }

    [Fact]
    public void Tangent_ReturnsNonZeroNormalizedVectors_ForCurrentCurves()
    {
        foreach (var (_, curve) in BuildCurves())
        {
            foreach (double t in SampleTs())
            {
                Vector3d tan = curve.Tangent(t);
                AssertFinite(tan);
                AssertNormalizedNonZero(tan);
            }
        }
    }

    [Fact]
    public void ArcLengthCurveAdapter_MapsDistanceToValidSamples()
    {
        IParamCurve cubic = new CubicBezierCurve(
            new Vector3d(0, 0, 0),
            new Vector3d(3, 6, 0),
            new Vector3d(7, -6, 0),
            new Vector3d(10, 0, 0));

        IArcLengthCurve adapter = new ArcLengthCurveAdapter(cubic, samples: 200);

        double[] sValues = { -1.0, 0.0, adapter.Length * 0.33, adapter.Length, adapter.Length + 1.0 };

        Vector3d startPos = adapter.EvaluateByLength(0.0);
        Vector3d endPos = adapter.EvaluateByLength(adapter.Length);

        foreach (double s in sValues)
        {
            Vector3d pos = adapter.EvaluateByLength(s);
            Vector3d tan = adapter.TangentByLength(s);

            AssertFinite(pos);
            AssertFinite(tan);
            AssertNormalizedNonZero(tan);
        }

        AssertVectorNear(startPos, adapter.EvaluateByLength(-1.0), ValueTolerance);
        AssertVectorNear(endPos, adapter.EvaluateByLength(adapter.Length + 1.0), ValueTolerance);
    }

    [Fact]
    public void ArcLengthLut_HandlesDegenerateIntervalsWithoutNaNOrInfinity()
    {
        IParamCurve curve = new NearDegenerateCurve();
        var lut = new ArcLengthLUT(curve, samples: 100);

        // Chosen to land in a near-zero arc-length interval after a non-degenerate segment.
        double s = 0.500000000000005;

        double mappedT = lut.MapS2T(s);

        Assert.False(double.IsNaN(mappedT));
        Assert.False(double.IsInfinity(mappedT));
        Assert.InRange(mappedT, 0.0, 1.0);
    }

    [Fact]
    public void TrainFollowerState_ClampsWhenLoopDisabled()
    {
        IArcLengthCurve track = new LineCurve(new Vector3d(0, 0, 0), new Vector3d(10, 0, 0));
        var follower = new TrainFollowerState(track, initialDistance: 9.0, speed: 5.0, loopEnabled: false);

        follower.Update(1.0);

        Assert.InRange(follower.Distance, 10.0 - ValueTolerance, 10.0 + ValueTolerance);
        Assert.InRange(follower.Position.X, 10.0 - ValueTolerance, 10.0 + ValueTolerance);
        AssertNormalizedNonZero(follower.Tangent);

        follower.Update(1.0);

        Assert.InRange(follower.Distance, 10.0 - ValueTolerance, 10.0 + ValueTolerance);
    }

    [Fact]
    public void TrainFollowerState_WrapsWhenLoopEnabled()
    {
        IArcLengthCurve track = new LineCurve(new Vector3d(0, 0, 0), new Vector3d(10, 0, 0));
        var follower = new TrainFollowerState(track, initialDistance: 9.0, speed: 5.0, loopEnabled: true);

        follower.Update(1.0);

        Assert.InRange(follower.Distance, 4.0 - ValueTolerance, 4.0 + ValueTolerance);
        Assert.InRange(follower.Position.X, 4.0 - ValueTolerance, 4.0 + ValueTolerance);
        AssertNormalizedNonZero(follower.Tangent);

        follower.Update(1.0);

        Assert.InRange(follower.Distance, 9.0 - ValueTolerance, 9.0 + ValueTolerance);
    }

    private static IEnumerable<(string Name, IParamCurve Curve)> BuildCurves()
    {
        yield return ("Line", new LineCurve(new Vector3d(0, 0, 0), new Vector3d(10, 0, 0)));

        yield return (
            "Quadratic",
            new QuadraticBezierCurve(
                new Vector3d(0, 0, 0),
                new Vector3d(5, 5, 0),
                new Vector3d(10, 0, 0)));

        yield return (
            "Cubic",
            new CubicBezierCurve(
                new Vector3d(0, 0, 0),
                new Vector3d(3, 6, 0),
                new Vector3d(7, -6, 0),
                new Vector3d(10, 0, 0)));

        yield return (
            "BSpline",
            new BSplineCurve(
                new List<Vector3d>
                {
                    new Vector3d(0, 0, 0),
                    new Vector3d(5, 0, 0),
                    new Vector3d(10, 5, 0),
                    new Vector3d(15, 0, 0)
                },
                degree: 3));
    }

    private static IEnumerable<double> SampleTs()
    {
        yield return 0.0;
        yield return 0.25;
        yield return 0.5;
        yield return 0.75;
        yield return 1.0;
    }

    private static void AssertFinite(Vector3d value)
    {
        Assert.False(double.IsNaN(value.X) || double.IsNaN(value.Y) || double.IsNaN(value.Z));
        Assert.False(double.IsInfinity(value.X) || double.IsInfinity(value.Y) || double.IsInfinity(value.Z));
    }

    private static void AssertNormalizedNonZero(Vector3d tangent)
    {
        double len = tangent.Length;
        Assert.False(double.IsNaN(len) || double.IsInfinity(len));
        Assert.True(len > MathUtil.Epsilon, $"Expected non-zero tangent length, got {len}.");
        Assert.InRange(System.Math.Abs(len - 1.0), 0.0, LengthTolerance);
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual, double tolerance)
    {
        Assert.InRange(System.Math.Abs(expected.X - actual.X), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(expected.Y - actual.Y), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(expected.Z - actual.Z), 0.0, tolerance);
    }

    private sealed class NearDegenerateCurve : IParamCurve
    {
        public Vector3d Evaluate(double t)
        {
            if (t <= 0.5)
                return new Vector3d(t, 0.0, 0.0);

            // Extremely small motion creates near-zero arc-length intervals.
            return new Vector3d(0.5 + (t - 0.5) * 1e-12, 0.0, 0.0);
        }

        public Vector3d Tangent(double t)
        {
            return Vector3d.UnitX;
        }
    }
}
