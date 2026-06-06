using Quantum.Track;

namespace Quantum.Tests;

public sealed class NormalizedSectionEvaluatorTests
{
    [Fact]
    public void NormalizedSectionEvaluator_OverlappingSectionsWithSameKindAndDomain_AreRejected()
    {
        SectionDefinition first = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 1.0);
        SectionDefinition overlapping = ForceSectionDefinition(
            startX: 9.5,
            endX: 20.0,
            SectionChannel.NormalG,
            startValue: 2.0,
            endValue: 2.0);

        Assert.Throws<ArgumentException>(() =>
            new NormalizedSectionEvaluator(new[] { first, overlapping }));
    }

    [Fact]
    public void NormalizedSectionEvaluator_TouchingSectionsReuseChannel_AndRightHandWinsAtBoundary()
    {
        SectionDefinition left = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 1.0);
        SectionDefinition right = ForceSectionDefinition(
            startX: 10.0,
            endX: 20.0,
            SectionChannel.NormalG,
            startValue: 2.0,
            endValue: 2.0);
        var evaluator = new NormalizedSectionEvaluator(new[] { left, right });

        double value = evaluator.EvaluateDistanceChannelAt(
            SectionKind.Force,
            SectionChannel.NormalG,
            distance: 10.0);

        Assert.Equal(2.0, value);
        Assert.Same(right, evaluator.ResolveDistanceSection(SectionKind.Force, 10.0));
    }

    [Fact]
    public void NormalizedSectionEvaluator_FinalEndpoint_IsInclusiveForLastDistanceSection()
    {
        SectionDefinition left = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 1.0);
        SectionDefinition right = ForceSectionDefinition(
            startX: 10.0,
            endX: 20.0,
            SectionChannel.NormalG,
            startValue: 2.0,
            endValue: 3.0);
        var evaluator = new NormalizedSectionEvaluator(new[] { left, right });

        bool evaluated = evaluator.TryEvaluateDistanceChannelAt(
            SectionKind.Force,
            SectionChannel.NormalG,
            distance: 20.0,
            out double value,
            out SectionEvaluationDiagnostic diagnostic);

        Assert.True(evaluated);
        Assert.Equal(SectionEvaluationDiagnostic.None, diagnostic);
        Assert.Equal(3.0, value);
    }

    [Fact]
    public void NormalizedSectionEvaluator_NoDistanceCoverage_ReturnsFalseAndDiagnostic()
    {
        SectionDefinition first = ForceSectionDefinition(
            startX: 0.0,
            endX: 5.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 1.0);
        SectionDefinition second = ForceSectionDefinition(
            startX: 7.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 2.0,
            endValue: 2.0);
        var evaluator = new NormalizedSectionEvaluator(new[] { first, second });

        bool evaluated = evaluator.TryEvaluateDistanceChannelAt(
            SectionKind.Force,
            SectionChannel.NormalG,
            distance: 6.0,
            out double value,
            out SectionEvaluationDiagnostic diagnostic);

        Assert.False(evaluated);
        Assert.Equal(0.0, value);
        Assert.Equal(SectionEvaluationDiagnostic.OutsideSectionCoverage, diagnostic);
    }

    [Fact]
    public void NormalizedSectionEvaluator_TimeDomainSections_AreDataOnlyForDistanceEvaluation()
    {
        SectionDefinition timeSection = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 4.0,
            endValue: 4.0,
            domain: SectionDomain.Time);
        var evaluator = new NormalizedSectionEvaluator(new[] { timeSection });

        bool evaluated = evaluator.TryEvaluateDistanceChannelAt(
            SectionKind.Force,
            SectionChannel.NormalG,
            distance: 5.0,
            out double value,
            out SectionEvaluationDiagnostic diagnostic);

        Assert.False(evaluated);
        Assert.Equal(0.0, value);
        Assert.Equal(SectionEvaluationDiagnostic.NoSection, diagnostic);
    }

    [Fact]
    public void NormalizedSectionEvaluator_OverlappingSectionsWithDifferentDomains_AreIndependent()
    {
        SectionDefinition distanceSection = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 1.0);
        SectionDefinition timeSection = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 2.0,
            endValue: 2.0,
            domain: SectionDomain.Time);
        var evaluator = new NormalizedSectionEvaluator(new[] { distanceSection, timeSection });

        double value = evaluator.EvaluateDistanceChannelAt(
            SectionKind.Force,
            SectionChannel.NormalG,
            distance: 5.0);

        Assert.Equal(1.0, value);
        Assert.Same(distanceSection, evaluator.ResolveDistanceSection(SectionKind.Force, 5.0));
    }

    [Fact]
    public void NormalizedSectionEvaluator_TryEvaluateDistanceChannelAt_MissingValidChannel_ReturnsMissingChannelDiagnostic()
    {
        SectionDefinition section = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 1.0);
        var evaluator = new NormalizedSectionEvaluator(new[] { section });

        bool evaluated = evaluator.TryEvaluateDistanceChannelAt(
            SectionKind.Force,
            SectionChannel.LateralG,
            distance: 5.0,
            out double value,
            out SectionEvaluationDiagnostic diagnostic);

        Assert.False(evaluated);
        Assert.Equal(default, value);
        Assert.Equal(SectionEvaluationDiagnostic.MissingChannel, diagnostic);
    }

    [Fact]
    public void NormalizedSectionEvaluator_TryEvaluateDistanceChannelAt_PresentChannel_UsesDistanceFunctionLookup()
    {
        SectionDefinition section = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 3.0);
        var evaluator = new NormalizedSectionEvaluator(new[] { section });

        bool evaluated = evaluator.TryEvaluateDistanceChannelAt(
            SectionKind.Force,
            SectionChannel.NormalG,
            distance: 2.5,
            out double value,
            out SectionEvaluationDiagnostic diagnostic);

        Assert.True(evaluated);
        Assert.Equal(1.5, value);
        Assert.Equal(SectionEvaluationDiagnostic.None, diagnostic);
    }

    [Fact]
    public void NormalizedSectionEvaluator_TryGetDistanceFunctionAt_ReturnsFunctionForPresentChannel()
    {
        SectionDefinition section = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 2.0);
        var evaluator = new NormalizedSectionEvaluator(new[] { section });

        bool found = evaluator.TryGetDistanceFunctionAt(
            SectionKind.Force,
            SectionChannel.NormalG,
            distance: 5.0,
            out SectionFunction? function,
            out SectionEvaluationDiagnostic diagnostic);

        Assert.True(found);
        Assert.Same(section.Functions[0], function);
        Assert.Equal(SectionEvaluationDiagnostic.None, diagnostic);
    }

    [Fact]
    public void NormalizedSectionEvaluator_TryGetDistanceFunctionAt_MissingValidChannel_ReturnsMissingChannelDiagnostic()
    {
        SectionDefinition section = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 1.0);
        var evaluator = new NormalizedSectionEvaluator(new[] { section });

        bool found = evaluator.TryGetDistanceFunctionAt(
            SectionKind.Force,
            SectionChannel.LateralG,
            distance: 5.0,
            out SectionFunction? function,
            out SectionEvaluationDiagnostic diagnostic);

        Assert.False(found);
        Assert.Null(function);
        Assert.Equal(SectionEvaluationDiagnostic.MissingChannel, diagnostic);
    }

    [Fact]
    public void NormalizedSectionEvaluator_TryGetDistanceFunctionAt_NoSection_ReturnsResolutionDiagnostic()
    {
        var evaluator = new NormalizedSectionEvaluator(Array.Empty<SectionDefinition>());

        bool found = evaluator.TryGetDistanceFunctionAt(
            SectionKind.Force,
            SectionChannel.NormalG,
            distance: 5.0,
            out SectionFunction? function,
            out SectionEvaluationDiagnostic diagnostic);

        Assert.False(found);
        Assert.Null(function);
        Assert.Equal(SectionEvaluationDiagnostic.NoSection, diagnostic);
    }

    [Fact]
    public void NormalizedSectionEvaluator_TryGetDistanceFunctionAt_InvalidChannel_IsRejected()
    {
        SectionDefinition section = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 1.0);
        var evaluator = new NormalizedSectionEvaluator(new[] { section });

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            evaluator.TryGetDistanceFunctionAt(
                SectionKind.Force,
                (SectionChannel)999,
                distance: 5.0,
                out _,
                out _));

        Assert.Equal("channel", exception.ParamName);
    }

    [Fact]
    public void NormalizedSectionEvaluator_ContainsDistanceSectionAt_ReturnsTrueForMatchingSection()
    {
        SectionDefinition section = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 1.0);
        var evaluator = new NormalizedSectionEvaluator(new[] { section });

        bool contains = evaluator.ContainsDistanceSectionAt(
            SectionKind.Force,
            distance: 5.0);

        Assert.True(contains);
    }

    [Fact]
    public void NormalizedSectionEvaluator_ContainsDistanceSectionAt_ReturnsFalseWhenNoSectionExists()
    {
        SectionDefinition section = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 1.0);
        var evaluator = new NormalizedSectionEvaluator(new[] { section });

        bool contains = evaluator.ContainsDistanceSectionAt(
            SectionKind.Force,
            distance: 15.0);

        Assert.False(contains);
    }

    [Fact]
    public void NormalizedSectionEvaluator_TryGetDistanceSectionAt_ReturnsSectionForMatchingDistance()
    {
        SectionDefinition section = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 1.0);
        var evaluator = new NormalizedSectionEvaluator(new[] { section });

        bool found = evaluator.TryGetDistanceSectionAt(
            SectionKind.Force,
            distance: 5.0,
            out SectionDefinition? resolved,
            out SectionEvaluationDiagnostic diagnostic);

        Assert.True(found);
        Assert.Same(section, resolved);
        Assert.Equal(SectionEvaluationDiagnostic.None, diagnostic);
    }

    [Fact]
    public void NormalizedSectionEvaluator_TryGetDistanceSectionAt_ReturnsFalseAndDiagnosticWhenNoSectionExists()
    {
        SectionDefinition timeSection = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 1.0,
            domain: SectionDomain.Time);
        var evaluator = new NormalizedSectionEvaluator(new[] { timeSection });

        bool found = evaluator.TryGetDistanceSectionAt(
            SectionKind.Force,
            distance: 5.0,
            out SectionDefinition? resolved,
            out SectionEvaluationDiagnostic diagnostic);

        Assert.False(found);
        Assert.Null(resolved);
        Assert.Equal(SectionEvaluationDiagnostic.NoSection, diagnostic);
    }

    [Fact]
    public void NormalizedSectionEvaluator_TryGetDistanceSectionAt_InvalidKind_IsRejected()
    {
        var evaluator = new NormalizedSectionEvaluator(Array.Empty<SectionDefinition>());

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            evaluator.TryGetDistanceSectionAt((SectionKind)999, distance: 5.0, out _, out _));

        Assert.Equal("kind", exception.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NormalizedSectionEvaluator_TryGetDistanceSectionAt_NonFiniteDistance_IsRejected(double distance)
    {
        var evaluator = new NormalizedSectionEvaluator(Array.Empty<SectionDefinition>());

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            evaluator.TryGetDistanceSectionAt(SectionKind.Force, distance, out _, out _));

        Assert.Equal("x", exception.ParamName);
    }

    [Fact]
    public void NormalizedSectionEvaluator_TryInspectDistanceSectionAt_ReturnsInspectionForMatchingSection()
    {
        SectionDefinition section = ForceSectionDefinition(
            startX: 2.0,
            endX: 12.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 3.0);
        var evaluator = new NormalizedSectionEvaluator(new[] { section });

        bool found = evaluator.TryInspectDistanceSectionAt(
            SectionKind.Force,
            distance: 8.0,
            out DistanceSectionInspection? inspection);

        Assert.True(found);
        Assert.NotNull(inspection);
        Assert.Equal(SectionKind.Force, inspection.Kind);
        Assert.Equal(SectionDomain.Distance, inspection.Domain);
        Assert.Equal(2.0, inspection.StartX);
        Assert.Equal(12.0, inspection.EndX);
        Assert.Equal(SectionEvaluationDiagnostic.None, inspection.Diagnostic);
    }

    [Fact]
    public void NormalizedSectionEvaluator_TryInspectDistanceSectionAt_IncludesSectionChannels()
    {
        var section = new SectionDefinition(
            SectionKind.Force,
            SectionDomain.Distance,
            startX: 0.0,
            endX: 10.0,
            new List<SectionFunction>
            {
                Function(SectionChannel.LongitudinalG, 0.0, 10.0, -0.2, 0.4),
                Function(SectionChannel.NormalG, 0.0, 10.0, 1.0, 2.0),
                Function(SectionChannel.LateralG, 0.0, 10.0, -0.5, 0.5)
            });
        var evaluator = new NormalizedSectionEvaluator(new[] { section });

        bool found = evaluator.TryInspectDistanceSectionAt(
            SectionKind.Force,
            distance: 4.0,
            out DistanceSectionInspection? inspection);

        Assert.True(found);
        Assert.NotNull(inspection);
        Assert.Equal(
            new[]
            {
                SectionChannel.LongitudinalG,
                SectionChannel.NormalG,
                SectionChannel.LateralG
            },
            inspection.Channels);
    }

    [Fact]
    public void NormalizedSectionEvaluator_TryInspectDistanceSectionAt_IncludesEvaluatedChannelValues()
    {
        var section = new SectionDefinition(
            SectionKind.Force,
            SectionDomain.Distance,
            startX: 0.0,
            endX: 10.0,
            new List<SectionFunction>
            {
                Function(SectionChannel.NormalG, 0.0, 10.0, 1.0, 3.0),
                Function(SectionChannel.LateralG, 0.0, 10.0, -0.5, 0.5)
            });
        var evaluator = new NormalizedSectionEvaluator(new[] { section });

        bool found = evaluator.TryInspectDistanceSectionAt(
            SectionKind.Force,
            distance: 2.5,
            out DistanceSectionInspection? inspection);

        Assert.True(found);
        Assert.NotNull(inspection);
        Assert.Equal(SectionEvaluationDiagnostic.None, inspection.Diagnostic);
        Assert.Equal(2, inspection.ChannelValues.Count);
        Assert.Equal(SectionChannel.NormalG, inspection.ChannelValues[0].Channel);
        Assert.Equal(1.5, inspection.ChannelValues[0].Value);
        Assert.Equal(SectionChannel.LateralG, inspection.ChannelValues[1].Channel);
        Assert.Equal(-0.25, inspection.ChannelValues[1].Value);
    }

    [Fact]
    public void NormalizedSectionEvaluator_TryInspectDistanceSectionAt_ChannelValuesPreserveFunctionOrder()
    {
        var section = new SectionDefinition(
            SectionKind.Force,
            SectionDomain.Distance,
            startX: 0.0,
            endX: 10.0,
            new List<SectionFunction>
            {
                Function(SectionChannel.LongitudinalG, 0.0, 10.0, -0.2, 0.4),
                Function(SectionChannel.NormalG, 0.0, 10.0, 1.0, 2.0),
                Function(SectionChannel.LateralG, 0.0, 10.0, -0.5, 0.5)
            });
        var evaluator = new NormalizedSectionEvaluator(new[] { section });

        bool found = evaluator.TryInspectDistanceSectionAt(
            SectionKind.Force,
            distance: 5.0,
            out DistanceSectionInspection? inspection);

        Assert.True(found);
        Assert.NotNull(inspection);
        Assert.Equal(
            new[]
            {
                SectionChannel.LongitudinalG,
                SectionChannel.NormalG,
                SectionChannel.LateralG
            },
            ChannelValues(inspection));
        Assert.Equal(0.1, inspection.ChannelValues[0].Value, 10);
        Assert.Equal(1.5, inspection.ChannelValues[1].Value, 10);
        Assert.Equal(0.0, inspection.ChannelValues[2].Value, 10);
    }

    [Fact]
    public void NormalizedSectionEvaluator_InspectDistanceAt_ReturnsActiveInspectionsForAllKinds()
    {
        SectionDefinition force = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 2.0);
        SectionDefinition geometry = GeometrySectionDefinition(
            startX: 0.0,
            endX: 10.0,
            curvature: 0.05,
            rollRadians: 0.1);
        var evaluator = new NormalizedSectionEvaluator(new[] { force, geometry });

        IReadOnlyList<DistanceSectionInspection> inspections = evaluator.InspectDistanceAt(distance: 5.0);

        Assert.Equal(2, inspections.Count);
        Assert.Equal(SectionKind.Force, inspections[0].Kind);
        Assert.Equal(SectionKind.Geometry, inspections[1].Kind);
        Assert.Equal(SectionDomain.Distance, inspections[0].Domain);
        Assert.Equal(SectionDomain.Distance, inspections[1].Domain);
        Assert.Equal(SectionEvaluationDiagnostic.None, inspections[0].Diagnostic);
        Assert.Equal(SectionEvaluationDiagnostic.None, inspections[1].Diagnostic);
    }

    [Fact]
    public void NormalizedSectionEvaluator_InspectDistanceAt_PreservesSectionKindOrder()
    {
        SectionDefinition geometry = GeometrySectionDefinition(
            startX: 0.0,
            endX: 10.0,
            curvature: 0.05,
            rollRadians: 0.1);
        SectionDefinition force = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 2.0);
        var evaluator = new NormalizedSectionEvaluator(new[] { geometry, force });

        IReadOnlyList<DistanceSectionInspection> inspections = evaluator.InspectDistanceAt(distance: 5.0);

        Assert.Equal(2, inspections.Count);
        Assert.Equal(SectionKind.Force, inspections[0].Kind);
        Assert.Equal(SectionKind.Geometry, inspections[1].Kind);
    }

    [Fact]
    public void NormalizedSectionEvaluator_InspectDistanceAt_SkipsMissingKinds()
    {
        SectionDefinition geometry = GeometrySectionDefinition(
            startX: 0.0,
            endX: 10.0,
            curvature: 0.05,
            rollRadians: 0.1);
        var evaluator = new NormalizedSectionEvaluator(new[] { geometry });

        IReadOnlyList<DistanceSectionInspection> inspections = evaluator.InspectDistanceAt(distance: 5.0);

        Assert.Single(inspections);
        Assert.Equal(SectionKind.Geometry, inspections[0].Kind);
    }

    [Fact]
    public void NormalizedSectionEvaluator_InspectDistanceAt_IncludesEvaluatedChannelValues()
    {
        var force = new SectionDefinition(
            SectionKind.Force,
            SectionDomain.Distance,
            startX: 0.0,
            endX: 10.0,
            new List<SectionFunction>
            {
                Function(SectionChannel.NormalG, 0.0, 10.0, 1.0, 3.0),
                Function(SectionChannel.LateralG, 0.0, 10.0, -0.5, 0.5)
            });
        SectionDefinition geometry = GeometrySectionDefinition(
            startX: 0.0,
            endX: 10.0,
            curvature: 0.1,
            rollRadians: 0.2);
        var evaluator = new NormalizedSectionEvaluator(new[] { force, geometry });

        IReadOnlyList<DistanceSectionInspection> inspections = evaluator.InspectDistanceAt(distance: 2.5);

        Assert.Equal(2, inspections.Count);
        Assert.Equal(SectionKind.Force, inspections[0].Kind);
        Assert.Equal(2, inspections[0].ChannelValues.Count);
        Assert.Equal(SectionChannel.NormalG, inspections[0].ChannelValues[0].Channel);
        Assert.Equal(1.5, inspections[0].ChannelValues[0].Value);
        Assert.Equal(SectionChannel.LateralG, inspections[0].ChannelValues[1].Channel);
        Assert.Equal(-0.25, inspections[0].ChannelValues[1].Value);
        Assert.Equal(SectionKind.Geometry, inspections[1].Kind);
        Assert.Equal(2, inspections[1].ChannelValues.Count);
        Assert.Equal(SectionChannel.Curvature, inspections[1].ChannelValues[0].Channel);
        Assert.Equal(0.1, inspections[1].ChannelValues[0].Value);
        Assert.Equal(SectionChannel.Roll, inspections[1].ChannelValues[1].Channel);
        Assert.Equal(0.2, inspections[1].ChannelValues[1].Value);
    }

    [Fact]
    public void NormalizedSectionEvaluator_InspectDistance_ReturnsSnapshotWithDistance()
    {
        SectionDefinition force = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 2.0);
        var evaluator = new NormalizedSectionEvaluator(new[] { force });

        DistanceInspectionSnapshot snapshot = evaluator.InspectDistance(distance: 4.25);

        Assert.Equal(4.25, snapshot.Distance);
    }

    [Fact]
    public void NormalizedSectionEvaluator_InspectDistance_ReturnsSnapshotWithActiveSections()
    {
        SectionDefinition force = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 2.0);
        SectionDefinition geometry = GeometrySectionDefinition(
            startX: 0.0,
            endX: 10.0,
            curvature: 0.05,
            rollRadians: 0.1);
        var evaluator = new NormalizedSectionEvaluator(new[] { geometry, force });

        DistanceInspectionSnapshot snapshot = evaluator.InspectDistance(distance: 5.0);

        Assert.Equal(2, snapshot.Sections.Count);
        Assert.Equal(SectionKind.Force, snapshot.Sections[0].Kind);
        Assert.Equal(SectionKind.Geometry, snapshot.Sections[1].Kind);
        Assert.Equal(SectionDomain.Distance, snapshot.Sections[0].Domain);
        Assert.Equal(SectionDomain.Distance, snapshot.Sections[1].Domain);
        Assert.Equal(SectionEvaluationDiagnostic.None, snapshot.Sections[0].Diagnostic);
        Assert.Equal(SectionEvaluationDiagnostic.None, snapshot.Sections[1].Diagnostic);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NormalizedSectionEvaluator_InspectDistanceAt_NonFiniteDistance_IsRejected(double distance)
    {
        var evaluator = new NormalizedSectionEvaluator(Array.Empty<SectionDefinition>());

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            evaluator.InspectDistanceAt(distance));

        Assert.Equal("x", exception.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void DistanceInspectionSnapshot_NonFiniteDistance_IsRejected(double distance)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DistanceInspectionSnapshot(distance, Array.Empty<DistanceSectionInspection>()));

        Assert.Equal("distance", exception.ParamName);
    }

    [Fact]
    public void DistanceInspectionSnapshot_NullInspections_IsRejected()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            new DistanceInspectionSnapshot(distance: 5.0, sections: null!));

        Assert.Equal("sections", exception.ParamName);
    }

    [Fact]
    public void DistanceInspectionSnapshot_DefensivelyCopiesInspections()
    {
        DistanceSectionInspection first = Inspection(SectionKind.Force);
        DistanceSectionInspection second = Inspection(SectionKind.Geometry);
        var inspections = new List<DistanceSectionInspection> { first };

        DistanceInspectionSnapshot snapshot = new DistanceInspectionSnapshot(
            distance: 5.0,
            sections: inspections);

        inspections[0] = second;
        inspections.Add(second);

        Assert.Single(snapshot.Sections);
        Assert.Same(first, snapshot.Sections[0]);
    }

    [Fact]
    public void DistanceSectionInspection_NullChannelValues_IsRejected()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            new DistanceSectionInspection(
                SectionKind.Force,
                SectionDomain.Distance,
                startX: 0.0,
                endX: 10.0,
                channels: new[] { SectionChannel.NormalG },
                channelValues: null!,
                SectionEvaluationDiagnostic.None));

        Assert.Equal("channelValues", exception.ParamName);
    }

    [Fact]
    public void DistanceSectionChannelInspection_InvalidChannel_IsRejected()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DistanceSectionChannelInspection((SectionChannel)999, value: 1.0));

        Assert.Equal("channel", exception.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void DistanceSectionChannelInspection_NonFiniteValue_IsRejected(double value)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DistanceSectionChannelInspection(SectionChannel.NormalG, value));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void NormalizedSectionEvaluator_TryInspectDistanceSectionAt_ReturnsFalseWhenNoSectionExists()
    {
        SectionDefinition timeSection = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 1.0,
            domain: SectionDomain.Time);
        var evaluator = new NormalizedSectionEvaluator(new[] { timeSection });

        bool found = evaluator.TryInspectDistanceSectionAt(
            SectionKind.Force,
            distance: 5.0,
            out DistanceSectionInspection? inspection);

        Assert.False(found);
        Assert.Null(inspection);
    }

    [Fact]
    public void NormalizedSectionEvaluator_TryInspectDistanceSectionAt_InvalidKind_IsRejected()
    {
        var evaluator = new NormalizedSectionEvaluator(Array.Empty<SectionDefinition>());

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            evaluator.TryInspectDistanceSectionAt((SectionKind)999, distance: 5.0, out _));

        Assert.Equal("kind", exception.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NormalizedSectionEvaluator_TryInspectDistanceSectionAt_NonFiniteDistance_IsRejected(double distance)
    {
        var evaluator = new NormalizedSectionEvaluator(Array.Empty<SectionDefinition>());

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            evaluator.TryInspectDistanceSectionAt(SectionKind.Force, distance, out _));

        Assert.Equal("x", exception.ParamName);
    }

    [Fact]
    public void NormalizedSectionEvaluator_ContainsDistanceSectionAt_InvalidKind_IsRejected()
    {
        var evaluator = new NormalizedSectionEvaluator(Array.Empty<SectionDefinition>());

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            evaluator.ContainsDistanceSectionAt((SectionKind)999, distance: 5.0));

        Assert.Equal("kind", exception.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NormalizedSectionEvaluator_ContainsDistanceSectionAt_NonFiniteDistance_IsRejected(double distance)
    {
        var evaluator = new NormalizedSectionEvaluator(Array.Empty<SectionDefinition>());

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            evaluator.ContainsDistanceSectionAt(SectionKind.Force, distance));

        Assert.Equal("x", exception.ParamName);
    }

    [Fact]
    public void SectionDefinition_DuplicateChannelWithinSection_IsRejected()
    {
        SectionFunction first = Function(
            SectionChannel.NormalG,
            startX: 0.0,
            endX: 10.0,
            startValue: 1.0,
            endValue: 1.0);
        SectionFunction duplicate = Function(
            SectionChannel.NormalG,
            startX: 0.0,
            endX: 10.0,
            startValue: 2.0,
            endValue: 2.0);

        Assert.Throws<ArgumentException>(() =>
            new SectionDefinition(
                SectionKind.Force,
                SectionDomain.Distance,
                startX: 0.0,
                endX: 10.0,
                new List<SectionFunction> { first, duplicate }));
    }

    [Fact]
    public void SectionDefinition_TryGetFunction_ReturnsTrueAndFunctionForPresentChannel()
    {
        SectionDefinition section = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 2.0);

        bool found = section.TryGetFunction(
            SectionChannel.NormalG,
            out SectionFunction? function);

        Assert.True(found);
        Assert.Same(section.Functions[0], function);
    }

    [Fact]
    public void SectionDefinition_TryGetFunction_ReturnsFalseForMissingValidChannel()
    {
        SectionDefinition section = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 2.0);

        bool found = section.TryGetFunction(
            SectionChannel.LateralG,
            out SectionFunction? function);

        Assert.False(found);
        Assert.Null(function);
    }

    [Fact]
    public void SectionDefinition_TryGetFunction_InvalidChannel_IsRejected()
    {
        SectionDefinition section = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 2.0);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            section.TryGetFunction((SectionChannel)999, out _));

        Assert.Equal("channel", exception.ParamName);
    }

    [Fact]
    public void SectionDefinition_GetFunction_ReturnsFunctionForPresentChannel()
    {
        SectionDefinition section = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 2.0);

        SectionFunction function = section.GetFunction(SectionChannel.NormalG);

        Assert.Same(section.Functions[0], function);
    }

    [Fact]
    public void SectionDefinition_GetFunction_MissingValidChannel_ThrowsInvalidOperationException()
    {
        SectionDefinition section = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 2.0);

        Assert.Throws<InvalidOperationException>(() =>
            section.GetFunction(SectionChannel.LateralG));
    }

    [Fact]
    public void SectionDefinition_GetFunction_InvalidChannel_IsRejected()
    {
        SectionDefinition section = ForceSectionDefinition(
            startX: 0.0,
            endX: 10.0,
            SectionChannel.NormalG,
            startValue: 1.0,
            endValue: 2.0);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            section.GetFunction((SectionChannel)999));

        Assert.Equal("channel", exception.ParamName);
    }

    [Fact]
    public void NormalizedSectionEvaluator_DistanceForceChannels_MatchForceTargetSampler()
    {
        var first = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Linear,
            startNormalG: 1.0,
            endNormalG: 3.0,
            startLateralG: -0.2,
            endLateralG: 0.4,
            startLongitudinalG: -0.5,
            endLongitudinalG: 0.5,
            rollRateChannel: new FixedForceEasingFunction(6.0));
        var second = new ForceSection(
            targetNormalG: 4.0,
            targetLateralG: -0.1,
            targetLongitudinalG: 0.25,
            length: 5.0,
            rollRateChannel: new FixedForceEasingFunction(2.5));
        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (first, 10.0),
            (second, 5.0)
        });
        IReadOnlyList<SectionDefinition> normalized = SectionNormalizer.NormalizeForceSections(intervals);
        var evaluator = new NormalizedSectionEvaluator(normalized);
        const double distance = 5.0;

        SampledForceTarget expected = ForceTargetSampler.Sample(intervals, distance);

        AssertChannelMatches(
            evaluator,
            SectionChannel.NormalG,
            distance,
            expected.TargetNormalG);
        AssertChannelMatches(
            evaluator,
            SectionChannel.LateralG,
            distance,
            expected.TargetLateralG);
        AssertChannelMatches(
            evaluator,
            SectionChannel.LongitudinalG,
            distance,
            expected.TargetLongitudinalG);
        AssertChannelMatches(
            evaluator,
            SectionChannel.RollRateDegPerSec,
            distance,
            expected.TargetRollRateDegPerSec);
    }

    [Fact]
    public void NormalizedSectionEvaluator_DistanceForceChannelSetDefinitions_MatchForceTargetSampler()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Linear,
            startNormalG: 10.0,
            endNormalG: 20.0,
            startLateralG: -1.0,
            endLateralG: 1.0,
            startLongitudinalG: -0.5,
            endLongitudinalG: 0.5,
            normalGChannel: new FixedForceEasingFunction(0.75),
            lateralGChannel: new FixedForceEasingFunction(0.2),
            longitudinalGChannel: new FixedForceEasingFunction(0.1),
            rollRateChannel: new FixedForceEasingFunction(5.0))
        {
            Channels = new ForceChannelSet
            {
                NormalGChannels = new IForceChannel[]
                {
                    new ForceChannel(new FixedForceEasingFunction(1.0)),
                    new ForceChannel(new FixedForceEasingFunction(2.0))
                },
                LateralG = new ForceChannel(new FixedForceEasingFunction(0.75)),
                LongitudinalGChannels = new IForceChannel[]
                {
                    new ForceChannel(new FixedForceEasingFunction(-0.2)),
                    new ForceChannel(new FixedForceEasingFunction(0.1))
                },
                RollRateBlendMode = ForceChannelBlendMode.Override,
                RollRateChannels = new IForceChannel[]
                {
                    new ForceChannel(new FixedForceEasingFunction(1.0)),
                    new ForceChannel(new FixedForceEasingFunction(2.0))
                }
            }
        };
        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });
        IReadOnlyList<SectionDefinition> normalized = SectionNormalizer.NormalizeForceSections(intervals);
        var evaluator = new NormalizedSectionEvaluator(normalized);
        const double distance = 5.0;

        SampledForceTarget expected = ForceTargetSampler.Sample(intervals, distance);

        Assert.Equal(
            new[]
            {
                SectionChannel.NormalG,
                SectionChannel.LateralG,
                SectionChannel.LongitudinalG,
                SectionChannel.RollRateDegPerSec
            },
            Channels(normalized[0]));
        AssertChannelMatches(
            evaluator,
            SectionChannel.NormalG,
            distance,
            expected.TargetNormalG);
        AssertChannelMatches(
            evaluator,
            SectionChannel.LateralG,
            distance,
            expected.TargetLateralG);
        AssertChannelMatches(
            evaluator,
            SectionChannel.LongitudinalG,
            distance,
            expected.TargetLongitudinalG);
        AssertChannelMatches(
            evaluator,
            SectionChannel.RollRateDegPerSec,
            distance,
            expected.TargetRollRateDegPerSec);
    }

    [Fact]
    public void NormalizedSectionEvaluator_EvaluateDistanceAllAt_ReturnsDefinedChannelsInStableOrder()
    {
        var source = new ForceSection(
            targetNormalG: 2.0,
            targetLateralG: -0.2,
            targetLongitudinalG: 0.4,
            length: 10.0,
            rollRateChannel: new FixedForceEasingFunction(6.0));
        SectionDefinition normalized = SectionNormalizer.Normalize(
            new ResolvedSectionInterval<ForceSection>(source, 0.0, 10.0));
        var evaluator = new NormalizedSectionEvaluator(new[] { normalized });

        IReadOnlyList<SectionChannelEvaluation> evaluations = evaluator.EvaluateDistanceAllAt(
            SectionKind.Force,
            distance: 5.0);

        Assert.Equal(4, evaluations.Count);
        Assert.Equal(SectionChannel.NormalG, evaluations[0].Channel);
        Assert.Equal(2.0, evaluations[0].Value);
        Assert.Equal(SectionChannel.LateralG, evaluations[1].Channel);
        Assert.Equal(-0.2, evaluations[1].Value);
        Assert.Equal(SectionChannel.LongitudinalG, evaluations[2].Channel);
        Assert.Equal(0.4, evaluations[2].Value);
        Assert.Equal(SectionChannel.RollRateDegPerSec, evaluations[3].Channel);
        Assert.Equal(6.0, evaluations[3].Value);
    }

    [Fact]
    public void NormalizedSectionEvaluator_NormalizedGeometrySections_EvaluateCurvatureAndRoll()
    {
        var source = new GeometricSection(
            length: 12.0,
            curvature: 0.08,
            roll: -0.05);
        var interval = new ResolvedSectionInterval<GeometricSection>(
            source,
            startDistance: 4.0,
            endDistance: 16.0);
        SectionDefinition normalized = SectionNormalizer.Normalize(interval);
        var evaluator = new NormalizedSectionEvaluator(new[] { normalized });

        Assert.Equal(SectionKind.Geometry, normalized.Kind);
        Assert.Equal(SectionDomain.Distance, normalized.Domain);
        Assert.Equal(4.0, normalized.StartX);
        Assert.Equal(16.0, normalized.EndX);
        Assert.Equal(
            new[] { SectionChannel.Curvature, SectionChannel.Roll },
            Channels(normalized));
        AssertGeometryChannelMatches(
            evaluator,
            SectionChannel.Curvature,
            distance: 9.0,
            expectedValue: 0.08);
        AssertGeometryChannelMatches(
            evaluator,
            SectionChannel.Roll,
            distance: 9.0,
            expectedValue: -0.05);
    }

    [Fact]
    public void NormalizedSectionEvaluator_TouchingGeometrySections_AndRightHandWinsAtBoundary()
    {
        SectionDefinition left = GeometrySectionDefinition(
            startX: 0.0,
            endX: 10.0,
            curvature: 0.05,
            rollRadians: 0.1);
        SectionDefinition right = GeometrySectionDefinition(
            startX: 10.0,
            endX: 20.0,
            curvature: -0.12,
            rollRadians: -0.2);
        var evaluator = new NormalizedSectionEvaluator(new[] { left, right });

        AssertGeometryChannelMatches(
            evaluator,
            SectionChannel.Curvature,
            distance: 10.0,
            expectedValue: -0.12);
        AssertGeometryChannelMatches(
            evaluator,
            SectionChannel.Roll,
            distance: 10.0,
            expectedValue: -0.2);
        Assert.Same(right, evaluator.ResolveDistanceSection(SectionKind.Geometry, 10.0));
    }

    [Fact]
    public void NormalizedSectionEvaluator_FinalEndpoint_IsInclusiveForLastGeometrySection()
    {
        SectionDefinition left = GeometrySectionDefinition(
            startX: 0.0,
            endX: 10.0,
            curvature: 0.05,
            rollRadians: 0.1);
        SectionDefinition right = GeometrySectionDefinition(
            startX: 10.0,
            endX: 20.0,
            curvature: -0.12,
            rollRadians: -0.2);
        var evaluator = new NormalizedSectionEvaluator(new[] { left, right });

        bool curvatureEvaluated = evaluator.TryEvaluateDistanceChannelAt(
            SectionKind.Geometry,
            SectionChannel.Curvature,
            distance: 20.0,
            out double curvature,
            out SectionEvaluationDiagnostic curvatureDiagnostic);
        bool rollEvaluated = evaluator.TryEvaluateDistanceChannelAt(
            SectionKind.Geometry,
            SectionChannel.Roll,
            distance: 20.0,
            out double roll,
            out SectionEvaluationDiagnostic rollDiagnostic);

        Assert.True(curvatureEvaluated);
        Assert.Equal(SectionEvaluationDiagnostic.None, curvatureDiagnostic);
        Assert.Equal(-0.12, curvature);
        Assert.True(rollEvaluated);
        Assert.Equal(SectionEvaluationDiagnostic.None, rollDiagnostic);
        Assert.Equal(-0.2, roll);
    }

    [Fact]
    public void NormalizedSectionEvaluator_NoGeometryDistanceCoverage_ReturnsFalseAndDiagnostic()
    {
        SectionDefinition first = GeometrySectionDefinition(
            startX: 0.0,
            endX: 5.0,
            curvature: 0.05,
            rollRadians: 0.1);
        SectionDefinition second = GeometrySectionDefinition(
            startX: 7.0,
            endX: 10.0,
            curvature: -0.12,
            rollRadians: -0.2);
        var evaluator = new NormalizedSectionEvaluator(new[] { first, second });

        bool evaluated = evaluator.TryEvaluateDistanceChannelAt(
            SectionKind.Geometry,
            SectionChannel.Roll,
            distance: 6.0,
            out double value,
            out SectionEvaluationDiagnostic diagnostic);

        Assert.False(evaluated);
        Assert.Equal(0.0, value);
        Assert.Equal(SectionEvaluationDiagnostic.OutsideSectionCoverage, diagnostic);
    }

    private static void AssertChannelMatches(
        NormalizedSectionEvaluator evaluator,
        SectionChannel channel,
        double distance,
        double? expectedValue)
    {
        bool evaluated = evaluator.TryEvaluateDistanceChannelAt(
            SectionKind.Force,
            channel,
            distance,
            out double actualValue,
            out SectionEvaluationDiagnostic diagnostic);

        Assert.True(evaluated);
        Assert.Equal(SectionEvaluationDiagnostic.None, diagnostic);
        Assert.True(expectedValue.HasValue);
        Assert.Equal(expectedValue.Value, actualValue, 10);
    }

    private static void AssertGeometryChannelMatches(
        NormalizedSectionEvaluator evaluator,
        SectionChannel channel,
        double distance,
        double expectedValue)
    {
        bool evaluated = evaluator.TryEvaluateDistanceChannelAt(
            SectionKind.Geometry,
            channel,
            distance,
            out double actualValue,
            out SectionEvaluationDiagnostic diagnostic);

        Assert.True(evaluated);
        Assert.Equal(SectionEvaluationDiagnostic.None, diagnostic);
        Assert.Equal(expectedValue, actualValue, 10);
    }

    private static SectionDefinition GeometrySectionDefinition(
        double startX,
        double endX,
        double curvature,
        double rollRadians)
    {
        return SectionNormalizer.Normalize(
            new ResolvedSectionInterval<GeometricSection>(
                new GeometricSection(
                    length: endX - startX,
                    curvature: curvature,
                    roll: rollRadians),
                startX,
                endX));
    }

    private static SectionChannel[] Channels(SectionDefinition definition)
    {
        var channels = new SectionChannel[definition.Functions.Count];
        for (int i = 0; i < definition.Functions.Count; i++)
        {
            channels[i] = definition.Functions[i].Channel;
        }

        return channels;
    }

    private static SectionChannel[] ChannelValues(DistanceSectionInspection inspection)
    {
        var channels = new SectionChannel[inspection.ChannelValues.Count];
        for (int i = 0; i < inspection.ChannelValues.Count; i++)
        {
            channels[i] = inspection.ChannelValues[i].Channel;
        }

        return channels;
    }

    private static SectionDefinition ForceSectionDefinition(
        double startX,
        double endX,
        SectionChannel channel,
        double startValue,
        double endValue,
        SectionDomain domain = SectionDomain.Distance)
    {
        return new SectionDefinition(
            SectionKind.Force,
            domain,
            startX,
            endX,
            new List<SectionFunction>
            {
                Function(channel, startX, endX, startValue, endValue)
            });
    }

    private static SectionFunction Function(
        SectionChannel channel,
        double startX,
        double endX,
        double startValue,
        double endValue)
    {
        return new SectionFunction(
            channel,
            new List<SectionSample>
            {
                new SectionSample(startX, startValue),
                new SectionSample(endX, endValue)
            });
    }

    private static DistanceSectionInspection Inspection(SectionKind kind)
    {
        return new DistanceSectionInspection(
            kind,
            SectionDomain.Distance,
            startX: 0.0,
            endX: 10.0,
            new[] { kind == SectionKind.Force ? SectionChannel.NormalG : SectionChannel.Curvature },
            SectionEvaluationDiagnostic.None);
    }

    private sealed class FixedForceEasingFunction : IForceEasingFunction
    {
        private readonly double _value;

        public FixedForceEasingFunction(double value)
        {
            _value = value;
        }

        public double Evaluate(double t)
        {
            return _value;
        }
    }
}
