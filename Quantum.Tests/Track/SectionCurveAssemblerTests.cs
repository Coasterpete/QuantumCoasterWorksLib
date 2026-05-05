using System;
using System.Collections.Generic;
using Quantum.Math;
using Quantum.Track;
using Xunit;

namespace Quantum.Tests;

public sealed class SectionCurveAssemblerTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void SectionCurveAssembler_Assemble_WithStraightSections_ComposesExpectedPositions()
    {
        var sections = new (GeometricSection Section, double Length)[]
        {
            (new GeometricSection(length: 100.0), 5.0),
            (new GeometricSection(length: 100.0), 3.0)
        };

        CompositeSectionCurve curve = SectionCurveAssembler.Assemble(sections);

        Assert.Equal(8.0, curve.TotalLength);
        AssertVectorNear(new Vector3d(0.0, 0.0, 0.0), curve.Evaluate(0.0));
        AssertVectorNear(new Vector3d(2.5, 0.0, 0.0), curve.Evaluate(2.5));
        AssertVectorNear(new Vector3d(5.0, 0.0, 0.0), curve.Evaluate(5.0));
        AssertVectorNear(new Vector3d(8.0, 0.0, 0.0), curve.Evaluate(8.0));
    }

    [Fact]
    public void SectionCurveAssembler_Assemble_WithMultipleSections_BoundaryPositionsAreContinuous()
    {
        CompositeSectionCurve curve = SectionCurveAssembler.Assemble(new[]
        {
            new GeometricSection(length: 4.0, curvature: 0.2),
            new GeometricSection(length: 3.0)
        });

        const double epsilon = 1e-9;
        Vector3d beforeBoundary = curve.Evaluate(4.0 - epsilon);
        Vector3d atBoundary = curve.Evaluate(4.0);
        Vector3d afterBoundary = curve.Evaluate(4.0 + epsilon);

        AssertVectorNear(beforeBoundary, atBoundary, 1e-6);
        AssertVectorNear(atBoundary, afterBoundary, 1e-6);
    }

    [Fact]
    public void SectionCurveAssembler_Assemble_WithCurvedSection_ProducesFinitePositionsAndTangents()
    {
        IReadOnlyList<ResolvedSectionInterval<GeometricSection>> resolved = SectionResolver.Resolve(new[]
        {
            (new GeometricSection(length: 6.0, curvature: -0.25), 6.0)
        });

        CompositeSectionCurve curve = SectionCurveAssembler.Assemble(resolved);

        for (int i = 0; i <= 10; i++)
        {
            double distance = curve.TotalLength * (i / 10.0);
            Vector3d position = curve.Evaluate(distance);
            Vector3d tangent = curve.Tangent(distance);

            AssertFinite(position);
            AssertFinite(tangent);
        }
    }

    [Fact]
    public void SectionCurveAssembler_Assemble_ExactFinalEndpoint_IsStable()
    {
        CompositeSectionCurve curve = SectionCurveAssembler.Assemble(new[]
        {
            new GeometricSection(length: 5.0),
            new GeometricSection(length: 2.0, curvature: 0.15)
        });

        Vector3d endpointA = curve.Evaluate(curve.TotalLength);
        Vector3d endpointB = curve.Evaluate(curve.TotalLength);

        AssertFinite(endpointA);
        AssertVectorNear(endpointA, endpointB);
    }

    [Theory]
    [InlineData(-0.0001)]
    [InlineData(8.0001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void CompositeSectionCurve_Evaluate_OutOfRangeOrNonFiniteDistance_ThrowsArgumentOutOfRangeException(double distance)
    {
        CompositeSectionCurve curve = SectionCurveAssembler.Assemble(new[]
        {
            new GeometricSection(length: 5.0),
            new GeometricSection(length: 3.0)
        });

        Assert.Throws<ArgumentOutOfRangeException>(() => curve.Evaluate(distance));
        Assert.Throws<ArgumentOutOfRangeException>(() => curve.Tangent(distance));
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual, double tolerance = Tolerance)
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
