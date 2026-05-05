using Quantum.Track;

namespace Quantum.Tests;

public sealed class ForceChannelDomainTests
{
    [Fact]
    public void ForceSection_Domain_DefaultsToDistance()
    {
        var section = new ForceSection();

        Assert.Equal(ForceChannelDomain.Distance, section.Domain);
    }

    [Fact]
    public void ForceTargetSampler_Sample_ChannelSetWithoutDomain_InheritsForceSectionDomain()
    {
        var section = new ForceSection(
            targetNormalG: 2.0,
            length: 10.0,
            domain: (ForceChannelDomain)123)
        {
            Channels = new ForceChannelSet
            {
                Domain = null
            }
        };

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        Assert.Throws<ArgumentOutOfRangeException>(() => ForceTargetSampler.Sample(intervals, 5.0));
    }

    [Fact]
    public void ForceTargetSampler_Sample_ChannelSetDomain_OverridesForceSectionDomain()
    {
        var section = new ForceSection(
            targetNormalG: 2.0,
            length: 10.0,
            domain: (ForceChannelDomain)123)
        {
            Channels = new ForceChannelSet
            {
                Domain = ForceChannelDomain.Distance
            }
        };

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.Equal(2.0, sampled.TargetNormalG);
    }

    [Fact]
    public void ForceTargetSampler_Sample_ExplicitDistanceDomain_MatchesLegacyDefaultBehavior()
    {
        var legacyDefault = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Linear,
            startNormalG: 10.0,
            endNormalG: 20.0);

        var explicitDistance = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Linear,
            startNormalG: 10.0,
            endNormalG: 20.0,
            domain: ForceChannelDomain.Distance);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> legacyIntervals = ForceTargetResolver.Resolve(new[]
        {
            (legacyDefault, 10.0)
        });
        IReadOnlyList<ResolvedSectionInterval<ForceSection>> explicitDistanceIntervals = ForceTargetResolver.Resolve(new[]
        {
            (explicitDistance, 10.0)
        });

        SampledForceTarget legacySample = ForceTargetSampler.Sample(legacyIntervals, 2.5);
        SampledForceTarget explicitDistanceSample = ForceTargetSampler.Sample(explicitDistanceIntervals, 2.5);

        Assert.Equal(legacySample.Distance, explicitDistanceSample.Distance);
        Assert.Equal(legacySample.NormalizedT, explicitDistanceSample.NormalizedT);
        Assert.Equal(legacySample.TargetNormalG, explicitDistanceSample.TargetNormalG);
        Assert.Equal(legacySample.TargetLateralG, explicitDistanceSample.TargetLateralG);
        Assert.Equal(legacySample.TargetLongitudinalG, explicitDistanceSample.TargetLongitudinalG);
        Assert.Equal(legacySample.TargetRollRateDegPerSec, explicitDistanceSample.TargetRollRateDegPerSec);
    }

    [Fact]
    public void ForceTargetSampler_Sample_TimeDomain_CurrentlyMatchesDistanceDomain()
    {
        var distanceSection = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Constant,
            startNormalG: 10.0,
            endNormalG: 30.0,
            domain: ForceChannelDomain.Distance)
        {
            Channels = new ForceChannelSet
            {
                Domain = ForceChannelDomain.Distance,
                NormalG = new ForceChannel(new FixedForceEasingFunction(0.25))
            }
        };

        var timeSection = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Constant,
            startNormalG: 10.0,
            endNormalG: 30.0,
            domain: ForceChannelDomain.Time)
        {
            Channels = new ForceChannelSet
            {
                Domain = ForceChannelDomain.Time,
                NormalG = new ForceChannel(new FixedForceEasingFunction(0.25))
            }
        };

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> distanceIntervals = ForceTargetResolver.Resolve(new[]
        {
            (distanceSection, 10.0)
        });
        IReadOnlyList<ResolvedSectionInterval<ForceSection>> timeIntervals = ForceTargetResolver.Resolve(new[]
        {
            (timeSection, 10.0)
        });

        SampledForceTarget distanceSample = ForceTargetSampler.Sample(distanceIntervals, 4.0);
        SampledForceTarget timeSample = ForceTargetSampler.Sample(timeIntervals, 4.0);

        Assert.Equal(distanceSample.Distance, timeSample.Distance);
        Assert.Equal(distanceSample.NormalizedT, timeSample.NormalizedT);
        Assert.Equal(distanceSample.TargetNormalG, timeSample.TargetNormalG);
        Assert.Equal(distanceSample.TargetLateralG, timeSample.TargetLateralG);
        Assert.Equal(distanceSample.TargetLongitudinalG, timeSample.TargetLongitudinalG);
        Assert.Equal(distanceSample.TargetRollRateDegPerSec, timeSample.TargetRollRateDegPerSec);
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
