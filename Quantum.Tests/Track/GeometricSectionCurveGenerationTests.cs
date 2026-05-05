using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using Xunit;

namespace Quantum.Tests;

public sealed class GeometricSectionCurveGenerationTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void GeometricSection_GenerateCurve_WithStraightSection_ProducesLinearCurve()
    {
        var section = new GeometricSection(length: 10.0);

        IParamCurve curve = section.GenerateCurve();

        Assert.IsType<LineCurve>(curve);
        AssertVectorNear(new Vector3d(0.0, 0.0, 0.0), curve.Evaluate(0.0));
        AssertVectorNear(new Vector3d(5.0, 0.0, 0.0), curve.Evaluate(0.5));
        AssertVectorNear(new Vector3d(10.0, 0.0, 0.0), curve.Evaluate(1.0));
        AssertVectorNear(Vector3d.UnitX, curve.Tangent(0.5));
    }

    [Fact]
    public void GeometricSection_GenerateCurve_WithCurvedSection_ProducesNonZeroCurvature()
    {
        const double expectedCurvature = 0.2;
        var section = new GeometricSection(length: 8.0, curvature: expectedCurvature);

        IParamCurve curve = section.GenerateCurve();

        Assert.IsNotType<LineCurve>(curve);
        var curvatureCurve = Assert.IsAssignableFrom<IParamCurveCurvature>(curve);
        Assert.True(curvatureCurve.TryGetCurvature(0.5, out double sampledCurvature));
        Assert.InRange(System.Math.Abs(sampledCurvature), Tolerance, double.MaxValue);
        Assert.InRange(System.Math.Abs(sampledCurvature - expectedCurvature), 0.0, Tolerance);
    }

    [Fact]
    public void GeometricSection_GenerateCurve_CurveIsStableAndFinite()
    {
        var section = new GeometricSection(length: 12.0, curvature: -0.15);
        IParamCurve curve = section.GenerateCurve();
        var curvatureCurve = Assert.IsAssignableFrom<IParamCurveCurvature>(curve);

        for (int i = 0; i <= 10; i++)
        {
            double t = i / 10.0;

            Vector3d position = curve.Evaluate(t);
            Vector3d tangent = curve.Tangent(t);
            AssertFinite(position);
            AssertFinite(tangent);
            Assert.InRange(System.Math.Abs(tangent.Length - 1.0), 0.0, Tolerance);

            Assert.True(curvatureCurve.TryGetCurvature(t, out double sampledCurvature));
            Assert.True(IsFinite(sampledCurvature));
        }
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual)
    {
        Assert.InRange(System.Math.Abs(expected.X - actual.X), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(expected.Y - actual.Y), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(expected.Z - actual.Z), 0.0, Tolerance);
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
