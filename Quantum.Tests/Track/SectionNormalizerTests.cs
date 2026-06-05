using Quantum.Track;

namespace Quantum.Tests;

public sealed class SectionNormalizerTests
{
    [Fact]
    public void SectionNormalizer_ForceSectionShorthand_NormalizesToForceSection()
    {
        var source = new ForceSection(
            targetNormalG: 2.5,
            targetLateralG: -0.15,
            length: 12.0);
        var interval = new ResolvedSectionInterval<ForceSection>(
            source,
            startDistance: 4.0,
            endDistance: 16.0,
            includeEndDistance: true);

        SectionDefinition normalized = SectionNormalizer.Normalize(interval);

        Assert.Equal(SectionKind.Force, normalized.Kind);
        Assert.Equal(SectionDomain.Distance, normalized.Domain);
        Assert.Equal(4.0, normalized.StartX);
        Assert.Equal(16.0, normalized.EndX);
        Assert.Equal(new[] { SectionChannel.NormalG, SectionChannel.LateralG }, Channels(normalized));
        Assert.Equal(2.5, normalized.EvaluateAt(SectionChannel.NormalG, 10.0));
        Assert.Equal(-0.15, normalized.EvaluateAt(SectionChannel.LateralG, 10.0));
    }

    [Fact]
    public void SectionNormalizer_GeometricSectionShorthand_NormalizesToGeometrySection()
    {
        var source = new GeometricSection(
            length: 100.0,
            curvature: 0.08,
            roll: -0.05);
        var interval = new ResolvedSectionInterval<GeometricSection>(
            source,
            startDistance: 7.0,
            endDistance: 19.0);

        SectionDefinition normalized = SectionNormalizer.Normalize(interval);

        Assert.Equal(SectionKind.Geometry, normalized.Kind);
        Assert.Equal(SectionDomain.Distance, normalized.Domain);
        Assert.Equal(7.0, normalized.StartX);
        Assert.Equal(19.0, normalized.EndX);
        Assert.Equal(new[] { SectionChannel.Curvature, SectionChannel.Roll }, Channels(normalized));
        Assert.Equal(0.08, normalized.EvaluateAt(SectionChannel.Curvature, 12.0));
        Assert.Equal(-0.05, normalized.EvaluateAt(SectionChannel.Roll, 12.0));
    }

    [Fact]
    public void SectionNormalizer_UsesResolvedIntervalStartAndEndX()
    {
        var first = new ForceSection(targetNormalG: 1.0, length: 5.0);
        var second = new ForceSection(targetNormalG: 3.0, length: 8.0);
        IReadOnlyList<ResolvedSectionInterval<ForceSection>> resolved = ForceTargetResolver.Resolve(new[]
        {
            (first, 5.0),
            (second, 8.0)
        });

        SectionDefinition normalized = SectionNormalizer.Normalize(resolved[1]);

        Assert.Equal(5.0, normalized.StartX);
        Assert.Equal(13.0, normalized.EndX);
        Assert.Equal(3.0, normalized.EvaluateAt(SectionChannel.NormalG, 9.0));
    }

    [Fact]
    public void SectionNormalizer_DomainDefaultsPreserveCurrentBehavior()
    {
        var defaultForce = new ForceSection(targetNormalG: 2.0, length: 10.0);
        var timeForce = new ForceSection(targetNormalG: 2.0, length: 10.0, domain: ForceChannelDomain.Time);
        var geometric = new GeometricSection(length: 10.0);

        SectionDefinition normalizedDefaultForce = SectionNormalizer.Normalize(
            new ResolvedSectionInterval<ForceSection>(defaultForce, 0.0, 10.0));
        SectionDefinition normalizedTimeForce = SectionNormalizer.Normalize(
            new ResolvedSectionInterval<ForceSection>(timeForce, 0.0, 10.0));
        SectionDefinition normalizedGeometric = SectionNormalizer.Normalize(
            new ResolvedSectionInterval<GeometricSection>(geometric, 0.0, 10.0));

        Assert.Equal(SectionDomain.Distance, normalizedDefaultForce.Domain);
        Assert.Equal(SectionDomain.Time, normalizedTimeForce.Domain);
        Assert.Equal(SectionDomain.Distance, normalizedGeometric.Domain);
    }

    [Fact]
    public void SectionNormalizer_ChannelSetDomainOverridesForceSectionDomain()
    {
        var source = new ForceSection(
            targetNormalG: 2.0,
            length: 10.0,
            domain: ForceChannelDomain.Time)
        {
            Channels = new ForceChannelSet
            {
                Domain = ForceChannelDomain.Distance
            }
        };

        SectionDefinition normalized = SectionNormalizer.Normalize(
            new ResolvedSectionInterval<ForceSection>(source, 0.0, 10.0));

        Assert.Equal(SectionDomain.Distance, normalized.Domain);
    }

    [Fact]
    public void SectionNormalizer_ForceValuePluralChannelsOverrideCompatibilityFields()
    {
        var source = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Linear,
            startNormalG: 10.0,
            endNormalG: 20.0,
            normalGChannel: new FixedForceEasingFunction(0.75))
        {
            Channels = new ForceChannelSet
            {
                NormalG = new ForceChannel(new FixedForceEasingFunction(0.25)),
                NormalGChannels = new IForceChannel[]
                {
                    new ForceChannel(new FixedForceEasingFunction(1.25)),
                    new ForceChannel(new FixedForceEasingFunction(2.75))
                }
            }
        };

        SectionDefinition normalized = SectionNormalizer.Normalize(
            new ResolvedSectionInterval<ForceSection>(source, 0.0, 10.0));

        Assert.Equal(new[] { SectionChannel.NormalG }, Channels(normalized));
        Assert.Equal(4.0, normalized.EvaluateAt(SectionChannel.NormalG, 5.0), 10);
        Assert.NotEqual(12.5, normalized.EvaluateAt(SectionChannel.NormalG, 5.0));
        Assert.NotEqual(17.5, normalized.EvaluateAt(SectionChannel.NormalG, 5.0));
    }

    [Fact]
    public void SectionNormalizer_EmptyPluralChannelsFallBackToSingleChannel()
    {
        var source = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Linear,
            startNormalG: 10.0,
            endNormalG: 20.0,
            normalGChannel: new FixedForceEasingFunction(0.75))
        {
            Channels = new ForceChannelSet
            {
                NormalG = new ForceChannel(new FixedForceEasingFunction(0.25)),
                NormalGChannels = Array.Empty<IForceChannel>()
            }
        };

        SectionDefinition normalized = SectionNormalizer.Normalize(
            new ResolvedSectionInterval<ForceSection>(source, 0.0, 10.0));

        Assert.Equal(12.5, normalized.EvaluateAt(SectionChannel.NormalG, 5.0), 10);
        Assert.NotEqual(17.5, normalized.EvaluateAt(SectionChannel.NormalG, 5.0));
    }

    [Fact]
    public void SectionNormalizer_ForceValueScalarFieldsRemainCompatibilityFallback()
    {
        var source = new ForceSection(
            targetLateralG: -0.2,
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Linear,
            startNormalG: 10.0,
            endNormalG: 20.0);

        SectionDefinition normalized = SectionNormalizer.Normalize(
            new ResolvedSectionInterval<ForceSection>(source, 0.0, 10.0));

        Assert.Equal(new[] { SectionChannel.NormalG, SectionChannel.LateralG }, Channels(normalized));
        Assert.Equal(12.5, normalized.EvaluateAt(SectionChannel.NormalG, 2.5), 10);
        Assert.Equal(-0.2, normalized.EvaluateAt(SectionChannel.LateralG, 2.5), 10);
    }

    [Fact]
    public void SectionNormalizer_DirectRollRateChannelDoesNotRequireScalarForceValues()
    {
        var source = new ForceSection(length: 10.0)
        {
            Channels = new ForceChannelSet
            {
                RollRate = new ForceChannel(new FixedForceEasingFunction(3.25))
            }
        };

        SectionDefinition normalized = SectionNormalizer.Normalize(
            new ResolvedSectionInterval<ForceSection>(source, 0.0, 10.0));

        Assert.Equal(new[] { SectionChannel.RollRateDegPerSec }, Channels(normalized));
        Assert.Equal(3.25, normalized.EvaluateAt(SectionChannel.RollRateDegPerSec, 5.0), 10);
    }

    [Fact]
    public void SectionNormalizer_InvalidResolvedIntervalRange_IsRejected()
    {
        var source = new ForceSection(targetNormalG: 2.0, length: 10.0);
        var zeroLengthInterval = new ResolvedSectionInterval<ForceSection>(
            source,
            startDistance: 5.0,
            endDistance: 5.0);

        Assert.Throws<ArgumentException>(() => SectionNormalizer.Normalize(zeroLengthInterval));
    }

    [Fact]
    public void SectionDefinition_InvalidRangesAreRejected()
    {
        var function = new SectionFunction(
            SectionChannel.NormalG,
            new List<SectionSample>
            {
                new SectionSample(0.0, 1.0),
                new SectionSample(1.0, 2.0)
            });

        Assert.Throws<ArgumentException>(() =>
            new SectionDefinition(
                SectionKind.Force,
                SectionDomain.Distance,
                startX: 1.0,
                endX: 1.0,
                new List<SectionFunction> { function }));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SectionDefinition(
                SectionKind.Force,
                SectionDomain.Distance,
                startX: double.NaN,
                endX: 1.0,
                new List<SectionFunction> { function }));
    }

    [Fact]
    public void SectionDefinition_SampleOutsideRangeIsRejected()
    {
        var function = new SectionFunction(
            SectionChannel.NormalG,
            new List<SectionSample>
            {
                new SectionSample(-0.1, 1.0),
                new SectionSample(1.0, 2.0)
            });

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SectionDefinition(
                SectionKind.Force,
                SectionDomain.Distance,
                startX: 0.0,
                endX: 1.0,
                new List<SectionFunction> { function }));
    }

    [Fact]
    public void SectionFunction_InvalidChannel_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SectionFunction(
                (SectionChannel)999,
                new List<SectionSample>
                {
                    new SectionSample(0.0, 1.0)
                }));
    }

    [Fact]
    public void SectionDefinition_EvaluateAt_InvalidChannel_IsRejected()
    {
        var definition = new SectionDefinition(
            SectionKind.Force,
            SectionDomain.Distance,
            startX: 0.0,
            endX: 1.0,
            new List<SectionFunction>
            {
                new SectionFunction(
                    SectionChannel.NormalG,
                    new List<SectionSample>
                    {
                        new SectionSample(0.0, 1.0),
                        new SectionSample(1.0, 2.0)
                    })
            });

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            definition.EvaluateAt((SectionChannel)999, 0.5));
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
