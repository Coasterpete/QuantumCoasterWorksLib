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
