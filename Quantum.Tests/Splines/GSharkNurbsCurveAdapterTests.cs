using System.Collections.Generic;
using Quantum.Math;
using Quantum.Splines;

namespace Quantum.Tests;

public sealed class GSharkNurbsCurveAdapterTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void EvaluateAndTangent_AreFinite_AndTangentIsNormalized()
    {
        IParamCurve curve = CreateAdapterCurve();

        foreach (double t in SampleParameters())
        {
            Vector3d position = curve.Evaluate(t);
            Vector3d tangent = curve.Tangent(t);

            AssertFinite(position);
            AssertFinite(tangent);
            Assert.InRange(System.Math.Abs(tangent.Length - 1.0), 0.0, Tolerance);
        }
    }

    [Fact]
    public void EvaluateAndTangent_AreNearLegacyNurbsCurve()
    {
        List<Vector3d> controlPoints = CreateControlPoints();
        List<double> weights = CreateWeights();

        IParamCurve legacy = new NurbsCurve(controlPoints, weights, degree: 3);
        IParamCurve adapter = new GSharkNurbsCurveAdapter(controlPoints, weights, degree: 3);

        foreach (double t in SampleParameters())
        {
            Vector3d legacyPosition = legacy.Evaluate(t);
            Vector3d adapterPosition = adapter.Evaluate(t);
            Vector3d legacyTangent = legacy.Tangent(t);
            Vector3d adapterTangent = adapter.Tangent(t);

            AssertVectorNear(legacyPosition, adapterPosition, 2e-5);
            AssertVectorNear(legacyTangent, adapterTangent, 2e-5);
        }
    }

    [Fact]
    public void Vector3dAndGSharkConversions_RoundTripWithNoLossBeyondTolerance()
    {
        Vector3d source = new Vector3d(12.25, -3.5, 8.875);

        Vector3d fromPoint = source
            .ToGSharkPoint3()
            .ToQuantumVector3d();

        Vector3d fromVector = source
            .ToGSharkVector3()
            .ToQuantumVector3d();

        AssertVectorNear(source, fromPoint, Tolerance);
        AssertVectorNear(source, fromVector, Tolerance);
    }

    private static IParamCurve CreateAdapterCurve()
    {
        return new GSharkNurbsCurveAdapter(
            CreateControlPoints(),
            CreateWeights(),
            degree: 3);
    }

    private static List<Vector3d> CreateControlPoints()
    {
        return new List<Vector3d>
        {
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(5.0, 3.0, 1.0),
            new Vector3d(10.0, -2.0, 2.0),
            new Vector3d(15.0, 0.0, 3.0)
        };
    }

    private static List<double> CreateWeights()
    {
        return new List<double> { 1.0, 0.9, 1.2, 1.0 };
    }

    private static IEnumerable<double> SampleParameters()
    {
        yield return 0.0;
        yield return 0.1;
        yield return 0.25;
        yield return 0.5;
        yield return 0.75;
        yield return 0.9;
        yield return 1.0;
    }

    private static void AssertFinite(Vector3d value)
    {
        Assert.False(double.IsNaN(value.X) || double.IsInfinity(value.X));
        Assert.False(double.IsNaN(value.Y) || double.IsInfinity(value.Y));
        Assert.False(double.IsNaN(value.Z) || double.IsInfinity(value.Z));
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual, double tolerance)
    {
        Assert.InRange(System.Math.Abs(expected.X - actual.X), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(expected.Y - actual.Y), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(expected.Z - actual.Z), 0.0, tolerance);
    }
}
