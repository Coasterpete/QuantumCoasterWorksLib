using Quantum.Math;
using Quantum.Splines;
using Quantum.Track.Authoring.Internal;
using Xunit;

namespace Quantum.Tests;

public sealed class DistanceCurvatureTransitionCurveTests
{
    private const double TightTolerance = 1e-12;
    private const double PositionTolerance = 1e-10;

    [Fact]
    public void LineReduction_ProducesExactDistanceDomainLine()
    {
        var curve = new DistanceCurvatureTransitionCurve(12.0, 0.0, 0.0);

        Assert.Equal(12.0, curve.Length);
        AssertVectorNear(new Vector3d(3.0, 0.0, 0.0), curve.Evaluate(0.25), TightTolerance);
        AssertVectorNear(new Vector3d(7.5, 0.0, 0.0), curve.EvaluateByLength(7.5), TightTolerance);
        AssertVectorNear(Vector3d.UnitX, curve.TangentByLength(7.5), TightTolerance);

        Assert.True(curve.TryGetCurvature(0.5, out double curvature));
        Assert.Equal(0.0, curvature);
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(-0.2)]
    [InlineData(1e-10)]
    [InlineData(-1e-10)]
    public void ConstantCurvatureReduction_MatchesAnalyticArc(double curvature)
    {
        const double length = 5.0;
        var curve = new DistanceCurvatureTransitionCurve(length, curvature, curvature);
        double heading = curvature * length;
        double halfHeadingSin = System.Math.Sin(0.5 * heading);

        Vector3d expectedEnd = new Vector3d(
            System.Math.Sin(heading) / curvature,
            (2.0 * halfHeadingSin * halfHeadingSin) / curvature,
            0.0);

        AssertVectorNear(expectedEnd, curve.EvaluateByLength(length), TightTolerance);
        AssertVectorNear(
            new Vector3d(System.Math.Cos(heading), System.Math.Sin(heading), 0.0),
            curve.TangentByLength(length),
            TightTolerance);
    }

    [Fact]
    public void PositiveTransition_InterpolatesCurvatureAndTurnsPositive()
    {
        var curve = new DistanceCurvatureTransitionCurve(10.0, 0.0, 0.2);

        AssertCurvatureNear(curve, 0.0, 0.0);
        AssertCurvatureNear(curve, 0.5, 0.1);
        AssertCurvatureNear(curve, 1.0, 0.2);
        Assert.True(curve.Evaluate(1.0).Y > 0.0);
        AssertVectorNear(
            new Vector3d(System.Math.Cos(1.0), System.Math.Sin(1.0), 0.0),
            curve.Tangent(1.0),
            PositionTolerance);
    }

    [Fact]
    public void NegativeTransition_InterpolatesCurvatureAndTurnsNegative()
    {
        var curve = new DistanceCurvatureTransitionCurve(10.0, 0.0, -0.2);

        AssertCurvatureNear(curve, 0.0, 0.0);
        AssertCurvatureNear(curve, 0.5, -0.1);
        AssertCurvatureNear(curve, 1.0, -0.2);
        Assert.True(curve.Evaluate(1.0).Y < 0.0);
        AssertVectorNear(
            new Vector3d(System.Math.Cos(-1.0), System.Math.Sin(-1.0), 0.0),
            curve.Tangent(1.0),
            PositionTolerance);
    }

    [Fact]
    public void SignCrossingTransition_ReturnsToInitialHeadingWithFiniteOffset()
    {
        var curve = new DistanceCurvatureTransitionCurve(10.0, -0.1, 0.1);

        AssertCurvatureNear(curve, 0.0, -0.1);
        AssertCurvatureNear(curve, 0.5, 0.0);
        AssertCurvatureNear(curve, 1.0, 0.1);
        AssertVectorNear(Vector3d.UnitX, curve.Tangent(1.0), PositionTolerance);
        Assert.True(curve.Evaluate(1.0).Y < 0.0);
    }

    [Fact]
    public void Evaluation_IsFiniteAndDeterministic()
    {
        var curve = new DistanceCurvatureTransitionCurve(
            18.0,
            -0.08,
            0.14,
            DistanceCurvatureTransitionInterpolationMode.Linear);

        for (int i = 0; i <= 100; i++)
        {
            double t = i / 100.0;
            Vector3d firstPosition = curve.Evaluate(t);
            Vector3d secondPosition = curve.Evaluate(t);
            Vector3d firstTangent = curve.Tangent(t);
            Vector3d secondTangent = curve.Tangent(t);

            AssertFinite(firstPosition);
            AssertFinite(firstTangent);
            Assert.Equal(firstPosition.X, secondPosition.X);
            Assert.Equal(firstPosition.Y, secondPosition.Y);
            Assert.Equal(firstPosition.Z, secondPosition.Z);
            Assert.Equal(firstTangent.X, secondTangent.X);
            Assert.Equal(firstTangent.Y, secondTangent.Y);
            Assert.Equal(firstTangent.Z, secondTangent.Z);
            Assert.InRange(System.Math.Abs(firstTangent.Length - 1.0), 0.0, TightTolerance);
        }
    }

    [Fact]
    public void SampledPolylineLength_ApproximatesDeclaredArcLength()
    {
        const double expectedLength = 20.0;
        const int segmentCount = 4000;
        IArcLengthCurve curve = new DistanceCurvatureTransitionCurve(
            expectedLength,
            -0.12,
            0.18);

        double sampledLength = 0.0;
        Vector3d previous = curve.EvaluateByLength(0.0);

        for (int i = 1; i <= segmentCount; i++)
        {
            double distance = expectedLength * i / segmentCount;
            Vector3d current = curve.EvaluateByLength(distance);
            sampledLength += (current - previous).Length;
            previous = current;
        }

        Assert.InRange(System.Math.Abs(sampledLength - expectedLength), 0.0, 1e-6);
    }

    [Fact]
    public void CurveFoundation_RemainsInternal()
    {
        Assert.False(typeof(DistanceCurvatureTransitionCurve).IsPublic);
        Assert.False(typeof(DistanceCurvatureTransitionInterpolationMode).IsPublic);
    }

    private static void AssertCurvatureNear(
        DistanceCurvatureTransitionCurve curve,
        double t,
        double expected)
    {
        Assert.True(curve.TryGetCurvature(t, out double actual));
        Assert.InRange(System.Math.Abs(actual - expected), 0.0, TightTolerance);
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual, double tolerance)
    {
        Assert.InRange(System.Math.Abs(expected.X - actual.X), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(expected.Y - actual.Y), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(expected.Z - actual.Z), 0.0, tolerance);
    }

    private static void AssertFinite(Vector3d value)
    {
        Assert.True(IsFinite(value.X));
        Assert.True(IsFinite(value.Y));
        Assert.True(IsFinite(value.Z));
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
