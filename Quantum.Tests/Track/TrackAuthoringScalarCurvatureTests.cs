using Quantum.Math;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class TrackAuthoringScalarCurvatureTests
{
    [Theory]
    [InlineData(25.0)]
    [InlineData(-25.0)]
    [InlineData(0.125)]
    [InlineData(-0.125)]
    public void SignedRadiusAndCurvature_RoundTrip(double radius)
    {
        double curvature = TrackAuthoringScalarCurvature.FromSignedRadius(radius);
        double roundTrip = TrackAuthoringScalarCurvature.ToSignedRadius(curvature);

        Assert.Equal(radius, roundTrip, 12);
        Assert.Equal(System.Math.Sign(radius), System.Math.Sign(curvature));
    }

    [Fact]
    public void ZeroCurvature_IsStraightAndHasNoFiniteRadius()
    {
        Assert.Equal(
            TrackAuthoringCurvatureDirection.Straight,
            TrackAuthoringScalarCurvature.DirectionOf(0.0));
        Assert.Equal(
            0.0,
            TrackAuthoringScalarCurvature.FromMagnitudeAndDirection(
                0.0,
                TrackAuthoringCurvatureDirection.Straight));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrackAuthoringScalarCurvature.ToSignedRadius(0.0));
    }

    [Fact]
    public void DirectionAndMagnitude_PreserveSignedConvention()
    {
        Assert.Equal(
            TrackAuthoringCurvatureDirection.Positive,
            TrackAuthoringScalarCurvature.DirectionOf(0.04));
        Assert.Equal(
            TrackAuthoringCurvatureDirection.Negative,
            TrackAuthoringScalarCurvature.DirectionOf(-0.04));
        Assert.Equal(
            0.04,
            TrackAuthoringScalarCurvature.FromMagnitudeAndDirection(
                0.04,
                TrackAuthoringCurvatureDirection.Positive));
        Assert.Equal(
            -0.04,
            TrackAuthoringScalarCurvature.FromMagnitudeAndDirection(
                0.04,
                TrackAuthoringCurvatureDirection.Negative));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void FromSignedRadius_RejectsInvalidNumericInputs(double radius)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrackAuthoringScalarCurvature.FromSignedRadius(radius));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ToSignedRadius_RejectsInvalidNumericInputs(double curvature)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrackAuthoringScalarCurvature.ToSignedRadius(curvature));
    }

    [Fact]
    public void DirectionAndMagnitude_RejectInvalidOrInconsistentInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrackAuthoringScalarCurvature.DirectionOf(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrackAuthoringScalarCurvature.DirectionOf(double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrackAuthoringScalarCurvature.FromMagnitudeAndDirection(
                double.NaN,
                TrackAuthoringCurvatureDirection.Positive));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrackAuthoringScalarCurvature.FromMagnitudeAndDirection(
                double.PositiveInfinity,
                TrackAuthoringCurvatureDirection.Positive));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrackAuthoringScalarCurvature.FromMagnitudeAndDirection(
                -0.1,
                TrackAuthoringCurvatureDirection.Negative));
        Assert.Throws<ArgumentException>(() =>
            TrackAuthoringScalarCurvature.FromMagnitudeAndDirection(
                0.1,
                TrackAuthoringCurvatureDirection.Straight));
        Assert.Throws<ArgumentException>(() =>
            TrackAuthoringScalarCurvature.FromMagnitudeAndDirection(
                0.0,
                TrackAuthoringCurvatureDirection.Positive));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrackAuthoringScalarCurvature.FromMagnitudeAndDirection(
                0.1,
                (TrackAuthoringCurvatureDirection)99));
    }

    [Fact]
    public void EndpointExtraction_IsExactForSupportedPlanarSections()
    {
        var straight = new StraightSectionDefinition("straight", 5.0);
        var arc = new ConstantCurvatureSectionDefinition("arc", 5.0, -20.0);
        var transition = new CurvatureTransitionSectionDefinition(
            "transition",
            5.0,
            -0.05,
            0.08);

        AssertEndpoint(straight, 0.0, 0.0);
        AssertEndpoint(arc, -0.05, -0.05);
        AssertEndpoint(transition, -0.05, 0.08);
    }

    [Fact]
    public void SpatialEndpointExtraction_ReportsUnavailableWithoutApproximation()
    {
        var spatial = new SpatialSectionDefinition(
            "spatial",
            3.0,
            new[]
            {
                Vector3d.Zero,
                new Vector3d(1.0, 0.0, 0.0),
                new Vector3d(2.0, 1.0, 0.0),
                new Vector3d(3.0, 1.0, 1.0)
            });

        Assert.False(TrackAuthoringScalarCurvature.TryGetStartCurvature(
            spatial,
            out double start));
        Assert.False(TrackAuthoringScalarCurvature.TryGetEndCurvature(
            spatial,
            out double end));
        Assert.Equal(0.0, start);
        Assert.Equal(0.0, end);
    }

    private static void AssertEndpoint(
        TrackAuthoringSectionDefinition section,
        double expectedStart,
        double expectedEnd)
    {
        Assert.True(TrackAuthoringScalarCurvature.TryGetStartCurvature(
            section,
            out double start));
        Assert.True(TrackAuthoringScalarCurvature.TryGetEndCurvature(
            section,
            out double end));
        Assert.Equal(expectedStart, start, 12);
        Assert.Equal(expectedEnd, end, 12);
    }
}
