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

    [Fact]
    public void ForceTargetSampler_Sample_WithElapsedTime_DistanceDomain_MatchesLegacyDistanceBehavior()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Constant,
            startNormalG: 0.0,
            endNormalG: 100.0,
            domain: ForceChannelDomain.Distance)
        {
            Channels = new ForceChannelSet
            {
                Domain = ForceChannelDomain.Distance,
                NormalG = new ForceChannel(new IdentityForceEasingFunction())
            }
        };

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget legacySample = ForceTargetSampler.Sample(intervals, 2.0);
        SampledForceTarget elapsedTimeSample = ForceTargetSampler.Sample(intervals, 2.0, elapsedTime: 9.0);

        AssertSampledTargetsEqual(legacySample, elapsedTimeSample);
    }

    [Fact]
    public void ForceTargetSampler_Sample_WithElapsedTime_TimeDomain_UsesElapsedTimeInsteadOfDistance()
    {
        var section = new ForceSection(
            length: 10.0,
            duration: 8.0,
            interpolationMode: ForceInterpolationMode.Constant,
            startNormalG: 0.0,
            endNormalG: 100.0,
            domain: ForceChannelDomain.Time)
        {
            Channels = new ForceChannelSet
            {
                Domain = ForceChannelDomain.Time,
                NormalG = new ForceChannel(new IdentityForceEasingFunction())
            }
        };

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget legacyDistanceSample = ForceTargetSampler.Sample(intervals, 2.0);
        SampledForceTarget elapsedTimeSample = ForceTargetSampler.Sample(intervals, 2.0, elapsedTime: 6.0);

        Assert.Equal(20.0, legacyDistanceSample.TargetNormalG);
        Assert.Equal(75.0, elapsedTimeSample.TargetNormalG);
        Assert.Equal(0.75, elapsedTimeSample.NormalizedT, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_WithElapsedTime_TimeDomain_AtElapsedTimeZero_SamplesStart()
    {
        var section = new ForceSection(
            length: 10.0,
            duration: 8.0,
            interpolationMode: ForceInterpolationMode.Linear,
            startNormalG: 10.0,
            endNormalG: 30.0,
            domain: ForceChannelDomain.Time);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sample = ForceTargetSampler.Sample(intervals, distance: 9.0, elapsedTime: 0.0);

        Assert.Equal(0.0, sample.NormalizedT);
        Assert.Equal(10.0, sample.TargetNormalG);
    }

    [Fact]
    public void ForceTargetSampler_Sample_WithElapsedTime_TimeDomain_AtDuration_SamplesEnd()
    {
        var section = new ForceSection(
            length: 10.0,
            duration: 8.0,
            interpolationMode: ForceInterpolationMode.Linear,
            startNormalG: 10.0,
            endNormalG: 30.0,
            domain: ForceChannelDomain.Time);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sample = ForceTargetSampler.Sample(intervals, distance: 1.0, elapsedTime: 8.0);

        Assert.Equal(1.0, sample.NormalizedT);
        Assert.Equal(30.0, sample.TargetNormalG);
    }

    [Fact]
    public void ForceTargetSampler_Sample_WithElapsedTime_TimeDomain_MissingDuration_ThrowsInvalidOperationException()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Linear,
            startNormalG: 10.0,
            endNormalG: 30.0,
            domain: ForceChannelDomain.Time);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        Assert.Throws<InvalidOperationException>(
            () => ForceTargetSampler.Sample(intervals, distance: 5.0, elapsedTime: 2.0));
    }

    [Fact]
    public void ForceTargetSampler_Sample_WithElapsedTime_TimeDomain_InvalidDuration_ThrowsInvalidOperationException()
    {
        var section = new ForceSection(
            length: 10.0,
            duration: 0.0,
            interpolationMode: ForceInterpolationMode.Linear,
            startNormalG: 10.0,
            endNormalG: 30.0,
            domain: ForceChannelDomain.Time);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        Assert.Throws<InvalidOperationException>(
            () => ForceTargetSampler.Sample(intervals, distance: 5.0, elapsedTime: 2.0));
    }

    [Fact]
    public void ForceTargetSampler_Sample_WithElapsedTime_ChannelSetDomain_OverridesForceSectionDomain()
    {
        var section = new ForceSection(
            length: 10.0,
            duration: 8.0,
            interpolationMode: ForceInterpolationMode.Constant,
            startNormalG: 0.0,
            endNormalG: 100.0,
            domain: ForceChannelDomain.Time)
        {
            Channels = new ForceChannelSet
            {
                Domain = ForceChannelDomain.Distance,
                NormalG = new ForceChannel(new IdentityForceEasingFunction())
            }
        };

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sample = ForceTargetSampler.Sample(intervals, distance: 2.5, elapsedTime: 7.5);

        Assert.Equal(0.25, sample.NormalizedT, 10);
        Assert.Equal(25.0, sample.TargetNormalG);
    }

    private static void AssertSampledTargetsEqual(SampledForceTarget expected, SampledForceTarget actual)
    {
        Assert.Equal(expected.Distance, actual.Distance);
        Assert.Equal(expected.NormalizedT, actual.NormalizedT);
        Assert.Equal(expected.TargetNormalG, actual.TargetNormalG);
        Assert.Equal(expected.TargetLateralG, actual.TargetLateralG);
        Assert.Equal(expected.TargetLongitudinalG, actual.TargetLongitudinalG);
        Assert.Equal(expected.TargetRollRateDegPerSec, actual.TargetRollRateDegPerSec);
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

    private sealed class IdentityForceEasingFunction : IForceEasingFunction
    {
        public double Evaluate(double t)
        {
            return t;
        }
    }
}
