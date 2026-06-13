using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class TrackAuthoringBoundaryContinuityDiagnosticsTests
{
    [Fact]
    public void Analyze_ContinuousMixedSections_ProducesBoundariesWithoutDiagnostics()
    {
        var definition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            new StraightSectionDefinition("entry", 4.0, rollRadians: 0.2),
            new CurvatureTransitionSectionDefinition(
                "transition-in",
                5.0,
                startCurvature: 0.0,
                endCurvature: 0.05,
                rollRadians: 0.2),
            new ConstantCurvatureSectionDefinition("arc", 6.0, radius: 20.0, rollRadians: 0.2),
            new CurvatureTransitionSectionDefinition(
                "transition-out",
                7.0,
                startCurvature: 0.05,
                endCurvature: 0.0,
                rollRadians: 0.2),
            new StraightSectionDefinition("exit", 3.0, rollRadians: 0.2)
        });

        TrackAuthoringBoundaryContinuityReport report =
            TrackAuthoringBoundaryContinuityDiagnostics.Analyze(definition);

        Assert.Equal(definition.Sections.Count - 1, report.BoundaryCount);
        Assert.Empty(report.Diagnostics);
        Assert.False(report.HasDiagnostics);
    }

    [Fact]
    public void Analyze_CurvatureOnlyDiscontinuity_EmitsCurvatureDiagnostic()
    {
        TrackAuthoringBoundaryContinuityReport report = AnalyzePair(
            new StraightSectionDefinition("before", 4.0, rollRadians: 0.25),
            new ConstantCurvatureSectionDefinition("after", 5.0, radius: 10.0, rollRadians: 0.25));

        TrackAuthoringBoundaryContinuityDiagnostic diagnostic = Assert.Single(report.Diagnostics);
        Assert.Equal(
            TrackAuthoringBoundaryContinuityDiagnosticKind.CurvatureDiscontinuity,
            diagnostic.Kind);
        Assert.Equal(0.1, diagnostic.Delta, 12);
    }

    [Fact]
    public void Analyze_RollOnlyDiscontinuity_EmitsRollDiagnostic()
    {
        TrackAuthoringBoundaryContinuityReport report = AnalyzePair(
            new StraightSectionDefinition("before", 4.0, rollRadians: -0.1),
            new StraightSectionDefinition("after", 5.0, rollRadians: 0.2));

        TrackAuthoringBoundaryContinuityDiagnostic diagnostic = Assert.Single(report.Diagnostics);
        Assert.Equal(
            TrackAuthoringBoundaryContinuityDiagnosticKind.RollDiscontinuity,
            diagnostic.Kind);
        Assert.Equal(0.3, diagnostic.Delta, 12);
    }

    [Fact]
    public void Analyze_CombinedDiscontinuities_EmitsCurvatureThenRoll()
    {
        TrackAuthoringBoundaryContinuityReport report = AnalyzePair(
            new StraightSectionDefinition("before", 4.0),
            new ConstantCurvatureSectionDefinition("after", 5.0, radius: 20.0, rollRadians: 0.4));

        Assert.Equal(2, report.Diagnostics.Count);
        Assert.Equal(
            TrackAuthoringBoundaryContinuityDiagnosticKind.CurvatureDiscontinuity,
            report.Diagnostics[0].Kind);
        Assert.Equal(
            TrackAuthoringBoundaryContinuityDiagnosticKind.RollDiscontinuity,
            report.Diagnostics[1].Kind);
    }

    [Fact]
    public void Analyze_MapsAllSupportedSectionEndpointCurvatures()
    {
        var definition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            new StraightSectionDefinition("straight-a", 2.0),
            new ConstantCurvatureSectionDefinition("constant", 3.0, radius: 20.0),
            new CurvatureTransitionSectionDefinition(
                "transition",
                4.0,
                startCurvature: -0.1,
                endCurvature: 0.2),
            new StraightSectionDefinition("straight-b", 5.0)
        });

        TrackAuthoringBoundaryContinuityReport report =
            TrackAuthoringBoundaryContinuityDiagnostics.Analyze(definition);

        Assert.Equal(0.0, report.Boundaries[0].PreviousEndCurvature);
        Assert.Equal(0.05, report.Boundaries[0].NextStartCurvature, 12);
        Assert.Equal(0.05, report.Boundaries[1].PreviousEndCurvature, 12);
        Assert.Equal(-0.1, report.Boundaries[1].NextStartCurvature);
        Assert.Equal(0.2, report.Boundaries[2].PreviousEndCurvature);
        Assert.Equal(0.0, report.Boundaries[2].NextStartCurvature);
    }

    [Fact]
    public void Analyze_NegativeRadiusAndCurvature_AreSignedAndContinuous()
    {
        var definition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            new CurvatureTransitionSectionDefinition("transition-in", 3.0, 0.0, -0.05),
            new ConstantCurvatureSectionDefinition("right-arc", 4.0, radius: -20.0),
            new CurvatureTransitionSectionDefinition("transition-out", 5.0, -0.05, 0.0)
        });

        TrackAuthoringBoundaryContinuityReport report =
            TrackAuthoringBoundaryContinuityDiagnostics.Analyze(definition);

        Assert.Empty(report.Diagnostics);
        Assert.Equal(-0.05, report.Boundaries[0].PreviousEndCurvature, 12);
        Assert.Equal(-0.05, report.Boundaries[0].NextStartCurvature, 12);
        Assert.Equal(-0.05, report.Boundaries[1].PreviousEndCurvature, 12);
        Assert.Equal(-0.05, report.Boundaries[1].NextStartCurvature, 12);
    }

    [Fact]
    public void Analyze_FullTurnEquivalentRolls_AreContinuous()
    {
        TrackAuthoringBoundaryContinuityReport report = AnalyzePair(
            new StraightSectionDefinition("before", 2.0, rollRadians: -0.25),
            new StraightSectionDefinition("after", 3.0, rollRadians: (2.0 * System.Math.PI) - 0.25));

        Assert.Empty(report.Diagnostics);
        Assert.Equal(0.0, report.Boundaries[0].RollDeltaRadians, 12);
    }

    [Theory]
    [InlineData(0.0625, false)]
    [InlineData(0.125, false)]
    [InlineData(0.25, true)]
    public void Analyze_EmitsOnlyWhenAbsoluteDeltaIsAboveTolerance(
        double delta,
        bool expectedDiagnostic)
    {
        var definition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            new StraightSectionDefinition("before", 2.0),
            new CurvatureTransitionSectionDefinition(
                "after",
                3.0,
                startCurvature: -delta,
                endCurvature: 0.0,
                rollRadians: -delta)
        });
        var tolerances = new TrackAuthoringBoundaryContinuityTolerances(
            curvatureTolerance: 0.125,
            rollToleranceRadians: 0.125);

        TrackAuthoringBoundaryContinuityReport report =
            TrackAuthoringBoundaryContinuityDiagnostics.Analyze(definition, tolerances);

        Assert.Equal(expectedDiagnostic ? 2 : 0, report.DiagnosticCount);
        Assert.All(report.Diagnostics, diagnostic => Assert.Equal(-delta, diagnostic.Delta));
    }

    [Fact]
    public void Analyze_UsesPreviousCumulativeEndDistanceForBoundaryStations()
    {
        var definition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            new StraightSectionDefinition("first", 2.0),
            new StraightSectionDefinition("second", 3.0),
            new StraightSectionDefinition("third", 5.0)
        });

        TrackAuthoringBoundaryContinuityReport report =
            TrackAuthoringBoundaryContinuityDiagnostics.Analyze(definition);

        Assert.Equal(new[] { 2.0, 5.0 }, report.Boundaries.Select(boundary => boundary.Station));
        Assert.Equal(report.Boundaries[1].Station, report.Boundaries[1].StationDistance);
    }

    [Fact]
    public void Analyze_PreservesExactSectionIds()
    {
        TrackAuthoringBoundaryContinuityReport report = AnalyzePair(
            new StraightSectionDefinition("  Previous ID  ", 2.0),
            new StraightSectionDefinition("Next-ID", 3.0, rollRadians: 0.5));

        TrackAuthoringBoundaryContinuityBoundary boundary = Assert.Single(report.Boundaries);
        TrackAuthoringBoundaryContinuityDiagnostic diagnostic = Assert.Single(report.Diagnostics);
        Assert.Equal("  Previous ID  ", boundary.PreviousSectionId);
        Assert.Equal("Next-ID", boundary.NextSectionId);
        Assert.Equal(boundary.PreviousSectionId, diagnostic.PreviousSectionId);
        Assert.Equal(boundary.NextSectionId, diagnostic.NextSectionId);
    }

    [Fact]
    public void Analyze_SingleSection_ReturnsEmptyDiagnosticsAndBoundaries()
    {
        var definition = new TrackAuthoringDefinition(new[]
        {
            new StraightSectionDefinition("only", 2.0)
        });

        TrackAuthoringBoundaryContinuityReport report =
            TrackAuthoringBoundaryContinuityDiagnostics.Analyze(definition);

        Assert.Empty(report.Boundaries);
        Assert.Empty(report.Diagnostics);
        Assert.Equal(0, report.BoundaryCount);
    }

    [Fact]
    public void Analyze_OrdersDiagnosticsByBoundaryThenCurvatureThenRoll()
    {
        var definition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            new StraightSectionDefinition("first", 2.0),
            new ConstantCurvatureSectionDefinition("second", 3.0, radius: 10.0, rollRadians: 0.2),
            new ConstantCurvatureSectionDefinition("third", 4.0, radius: -10.0, rollRadians: -0.2)
        });

        TrackAuthoringBoundaryContinuityReport report =
            TrackAuthoringBoundaryContinuityDiagnostics.Analyze(definition);

        Assert.Equal(
            new[]
            {
                (0, TrackAuthoringBoundaryContinuityDiagnosticKind.CurvatureDiscontinuity),
                (0, TrackAuthoringBoundaryContinuityDiagnosticKind.RollDiscontinuity),
                (1, TrackAuthoringBoundaryContinuityDiagnosticKind.CurvatureDiscontinuity),
                (1, TrackAuthoringBoundaryContinuityDiagnosticKind.RollDiscontinuity)
            },
            report.Diagnostics.Select(diagnostic => (diagnostic.BoundaryIndex, diagnostic.Kind)));
    }

    [Fact]
    public void Analyze_RepeatedAnalysis_IsDeterministic()
    {
        var definition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            new StraightSectionDefinition("first", 2.0, rollRadians: 0.1),
            new ConstantCurvatureSectionDefinition("second", 3.0, radius: -8.0, rollRadians: -0.2),
            new CurvatureTransitionSectionDefinition("third", 4.0, -0.05, 0.0, rollRadians: 0.3)
        });
        var tolerances = new TrackAuthoringBoundaryContinuityTolerances(0.01, 0.05);

        TrackAuthoringBoundaryContinuityReport first =
            TrackAuthoringBoundaryContinuityDiagnostics.Analyze(definition, tolerances);
        TrackAuthoringBoundaryContinuityReport second =
            TrackAuthoringBoundaryContinuityDiagnostics.Analyze(definition, tolerances);

        Assert.Equal(first.Boundaries, second.Boundaries);
        Assert.Equal(first.Diagnostics, second.Diagnostics);
        Assert.Equal(first.Tolerances, second.Tolerances);
    }

    [Fact]
    public void Analyze_NullDefinition_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => TrackAuthoringBoundaryContinuityDiagnostics.Analyze(null!));
        Assert.Throws<ArgumentNullException>(() =>
            TrackAuthoringBoundaryContinuityDiagnostics.Analyze(
                null!,
                TrackAuthoringBoundaryContinuityTolerances.Default));
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Tolerances_RejectInvalidValues(double invalidTolerance)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TrackAuthoringBoundaryContinuityTolerances(invalidTolerance, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TrackAuthoringBoundaryContinuityTolerances(0.0, invalidTolerance));
    }

    [Fact]
    public void Analyze_ExposesDefensiveReadOnlyCollections()
    {
        TrackAuthoringBoundaryContinuityReport report = AnalyzePair(
            new StraightSectionDefinition("before", 2.0),
            new StraightSectionDefinition("after", 3.0, rollRadians: 0.2));
        IList<TrackAuthoringBoundaryContinuityBoundary> boundaries =
            Assert.IsAssignableFrom<IList<TrackAuthoringBoundaryContinuityBoundary>>(report.Boundaries);
        IList<TrackAuthoringBoundaryContinuityDiagnostic> diagnostics =
            Assert.IsAssignableFrom<IList<TrackAuthoringBoundaryContinuityDiagnostic>>(report.Diagnostics);

        Assert.True(boundaries.IsReadOnly);
        Assert.True(diagnostics.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => boundaries.RemoveAt(0));
        Assert.Throws<NotSupportedException>(() => diagnostics.RemoveAt(0));
        Assert.Single(report.Boundaries);
        Assert.Single(report.Diagnostics);
    }

    private static TrackAuthoringBoundaryContinuityReport AnalyzePair(
        GeometricSectionDefinition previous,
        GeometricSectionDefinition next)
    {
        return TrackAuthoringBoundaryContinuityDiagnostics.Analyze(
            new TrackAuthoringDefinition(new[] { previous, next }));
    }
}
