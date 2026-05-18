using Quantum.Track;

namespace Quantum.Tests;

public sealed class ForceTargetSamplerTests
{
    [Fact]
    public void ForceTargetSampler_Sample_ReturnsExpectedNormalGFromForceSection()
    {
        var first = new ForceSection(targetNormalG: 2.25, length: 2.0);
        var second = new ForceSection(targetNormalG: 3.5, targetLateralG: -0.15, length: 3.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (first, 2.0),
            (second, 3.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 1.0);

        Assert.Equal(2.25, sampled.TargetNormalG);
    }

    [Fact]
    public void ForceTargetSampler_Sample_BoundaryBetweenIntervals_UsesNextSection()
    {
        var first = new ForceSection(targetNormalG: 2.0, targetLateralG: 0.1, length: 2.0);
        var second = new ForceSection(targetNormalG: 3.0, targetLateralG: -0.2, length: 3.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (first, 2.0),
            (second, 3.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 2.0);

        Assert.Equal(3.0, sampled.TargetNormalG);
        Assert.Equal(-0.2, sampled.TargetLateralG);
        Assert.Equal(0.0, sampled.NormalizedT);
    }

    [Fact]
    public void ForceTargetSampler_Sample_ExactFinalEndpoint_UsesFinalSection()
    {
        var first = new ForceSection(targetNormalG: 2.0, length: 2.0);
        var second = new ForceSection(targetNormalG: 3.0, targetLateralG: 0.05, length: 3.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (first, 2.0),
            (second, 3.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.Equal(3.0, sampled.TargetNormalG);
        Assert.Equal(0.05, sampled.TargetLateralG);
        Assert.Equal(1.0, sampled.NormalizedT);
    }

    [Fact]
    public void ForceTargetSampler_Sample_NormalizedTPassesThroughCorrectly()
    {
        var first = new ForceSection(targetNormalG: 2.0, length: 5.0);
        var second = new ForceSection(targetNormalG: 2.4, targetLateralG: 0.2, length: 3.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (first, 5.0),
            (second, 3.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 6.25);

        Assert.Equal(6.25, sampled.Distance);
        Assert.Equal(1.25 / 3.0, sampled.NormalizedT, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_LinearNormalG_AtStart_UsesStartValue()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Linear,
            startNormalG: 2.0,
            endNormalG: 4.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 0.0);

        Assert.Equal(2.0, sampled.TargetNormalG);
    }

    [Fact]
    public void ForceTargetSampler_Sample_LinearNormalG_AtMidpoint_UsesInterpolatedValue()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Linear,
            startNormalG: 2.0,
            endNormalG: 4.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(3.0, sampled.TargetNormalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_LinearNormalG_AtFinalEndpoint_UsesEndValue()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Linear,
            startNormalG: 2.0,
            endNormalG: 4.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 10.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(4.0, sampled.TargetNormalG.Value, 10);
        Assert.Equal(1.0, sampled.NormalizedT);
    }

    [Fact]
    public void ForceTargetSampler_Sample_SmoothStepNormalG_AtStart_UsesStartValue()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.SmoothStep,
            startNormalG: 2.0,
            endNormalG: 4.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 0.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(2.0, sampled.TargetNormalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_SmoothStepNormalG_AtFinalEndpoint_UsesEndValue()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.SmoothStep,
            startNormalG: 2.0,
            endNormalG: 4.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 10.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(4.0, sampled.TargetNormalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_SmoothStepNormalG_AtInteriorPoint_UsesEasedValue()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.SmoothStep,
            startNormalG: 2.0,
            endNormalG: 4.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 2.5);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(2.3125, sampled.TargetNormalG.Value, 10);
        Assert.NotEqual(2.5, sampled.TargetNormalG.Value);
    }

    [Fact]
    public void ForceTargetSampler_Sample_QuadraticNormalG_AtStart_UsesStartValue()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Quadratic,
            startNormalG: 2.0,
            endNormalG: 4.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 0.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(2.0, sampled.TargetNormalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_QuadraticNormalG_AtFinalEndpoint_UsesEndValue()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Quadratic,
            startNormalG: 2.0,
            endNormalG: 4.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 10.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(4.0, sampled.TargetNormalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_QuadraticNormalG_AtMidpoint_IsBelowLinearMidpoint()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Quadratic,
            startNormalG: 2.0,
            endNormalG: 4.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(2.5, sampled.TargetNormalG.Value, 10);
        Assert.True(sampled.TargetNormalG.Value < 3.0);
    }

    [Fact]
    public void ForceTargetSampler_Sample_CubicNormalG_AtStart_UsesStartValue()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Cubic,
            startNormalG: 2.0,
            endNormalG: 4.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 0.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(2.0, sampled.TargetNormalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_CubicNormalG_AtFinalEndpoint_UsesEndValue()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Cubic,
            startNormalG: 2.0,
            endNormalG: 4.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 10.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(4.0, sampled.TargetNormalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_CubicNormalG_AtMidpoint_IsBelowLinearMidpointWithStrongerCurveThanQuadratic()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Cubic,
            startNormalG: 2.0,
            endNormalG: 4.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(2.25, sampled.TargetNormalG.Value, 10);
        Assert.True(sampled.TargetNormalG.Value < 3.0);
        Assert.True(sampled.TargetNormalG.Value < 2.5);
    }

    [Fact]
    public void ForceTargetSampler_Sample_QuarticNormalG_AtEndpoints_UsesStartAndEndValues()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Quartic,
            startNormalG: 2.0,
            endNormalG: 4.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget atStart = ForceTargetSampler.Sample(intervals, 0.0);
        SampledForceTarget atEnd = ForceTargetSampler.Sample(intervals, 10.0);

        Assert.True(atStart.TargetNormalG.HasValue);
        Assert.Equal(2.0, atStart.TargetNormalG.Value, 10);
        Assert.True(atEnd.TargetNormalG.HasValue);
        Assert.Equal(4.0, atEnd.TargetNormalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_QuarticNormalG_AtMidpoint_IsBelowCubicMidpoint()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Quartic,
            startNormalG: 2.0,
            endNormalG: 4.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(2.125, sampled.TargetNormalG.Value, 10);
        Assert.True(sampled.TargetNormalG.Value < 2.25);
    }

    [Fact]
    public void ForceTargetSampler_Sample_QuinticNormalG_AtEndpoints_UsesStartAndEndValues()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Quintic,
            startNormalG: 2.0,
            endNormalG: 4.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget atStart = ForceTargetSampler.Sample(intervals, 0.0);
        SampledForceTarget atEnd = ForceTargetSampler.Sample(intervals, 10.0);

        Assert.True(atStart.TargetNormalG.HasValue);
        Assert.Equal(2.0, atStart.TargetNormalG.Value, 10);
        Assert.True(atEnd.TargetNormalG.HasValue);
        Assert.Equal(4.0, atEnd.TargetNormalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_QuinticNormalG_AtMidpoint_IsBelowQuarticMidpoint()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Quintic,
            startNormalG: 2.0,
            endNormalG: 4.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(2.0625, sampled.TargetNormalG.Value, 10);
        Assert.True(sampled.TargetNormalG.Value < 2.125);
    }

    [Fact]
    public void ForceTargetSampler_Sample_SinusoidalNormalG_AtStart_UsesStartValue()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Sinusoidal,
            startNormalG: 2.0,
            endNormalG: 4.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 0.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(2.0, sampled.TargetNormalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_SinusoidalNormalG_AtFinalEndpoint_UsesEndValue()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Sinusoidal,
            startNormalG: 2.0,
            endNormalG: 4.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 10.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(4.0, sampled.TargetNormalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_SinusoidalNormalG_AtMidpoint_IsEasedAndNotLinearMidpoint()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Sinusoidal,
            startNormalG: 2.0,
            endNormalG: 4.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);
        double linearMidpoint = 3.0;
        double expectedSinusoidalMidpoint = 2.0 + (2.0 * (1.0 - System.Math.Cos(System.Math.PI / 4.0)));

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(expectedSinusoidalMidpoint, sampled.TargetNormalG.Value, 10);
        Assert.True(System.Math.Abs(sampled.TargetNormalG.Value - linearMidpoint) > 1e-10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_SinusoidalNormalG_AtMidpoint_IsAboveQuadraticMidpoint()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Sinusoidal,
            startNormalG: 2.0,
            endNormalG: 4.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);
        double quadraticMidpoint = 2.5;

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.True(sampled.TargetNormalG.Value > quadraticMidpoint);
    }

    [Fact]
    public void ForceTargetSampler_Sample_LinearLateralG_UsesInterpolatedValue()
    {
        var section = new ForceSection(
            targetNormalG: 3.0,
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Linear,
            startLateralG: 0.2,
            endLateralG: -0.2);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 2.5);

        Assert.True(sampled.TargetLateralG.HasValue);
        Assert.Equal(0.1, sampled.TargetLateralG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_LinearLongitudinalG_AtMidpoint_UsesInterpolatedValue()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Linear,
            startLongitudinalG: -0.5,
            endLongitudinalG: 1.5);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetLongitudinalG.HasValue);
        Assert.Equal(0.5, sampled.TargetLongitudinalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_SmoothStepLongitudinalG_AtInteriorPoint_UsesEasedValue()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.SmoothStep,
            startLongitudinalG: -1.0,
            endLongitudinalG: 1.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 2.5);

        Assert.True(sampled.TargetLongitudinalG.HasValue);
        Assert.Equal(-0.6875, sampled.TargetLongitudinalG.Value, 10);
        Assert.NotEqual(-0.5, sampled.TargetLongitudinalG.Value);
    }

    [Fact]
    public void ForceTargetSampler_Sample_QuinticLongitudinalG_AtMidpoint_UsesHigherOrderEasedValue()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Quintic,
            startLongitudinalG: 0.0,
            endLongitudinalG: 2.0);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetLongitudinalG.HasValue);
        Assert.Equal(0.0625, sampled.TargetLongitudinalG.Value, 10);
        Assert.True(sampled.TargetLongitudinalG.Value < 0.125);
    }

    [Fact]
    public void ForceTargetSampler_Sample_KeyframedForceEasingFunction_WorksAsLongitudinalGChannel()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Constant,
            startLongitudinalG: 10.0,
            endLongitudinalG: 30.0,
            longitudinalGChannel: new KeyframedForceEasingFunction(new System.Collections.Generic.List<(double t, double value)>
            {
                (0.0, 0.0),
                (0.5, 1.0),
                (1.0, 0.2)
            }));

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 7.5);

        Assert.True(sampled.TargetLongitudinalG.HasValue);
        Assert.Equal(22.0, sampled.TargetLongitudinalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_CustomEasing_OverridesInterpolationModeBehavior()
    {
        var section = new ForceSection(
            targetNormalG: 1.0,
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Constant,
            startNormalG: 10.0,
            endNormalG: 20.0,
            easingFunction: new FixedForceEasingFunction(0.75));

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(17.5, sampled.TargetNormalG.Value, 10);
        Assert.NotEqual(1.0, sampled.TargetNormalG.Value);
    }

    [Fact]
    public void ForceTargetSampler_Sample_NullCustomEasing_PreservesInterpolationModeBehavior()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.SmoothStep,
            startNormalG: 2.0,
            endNormalG: 4.0,
            easingFunction: null);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 2.5);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(2.3125, sampled.TargetNormalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_NormalGChannel_OverridesInterpolationMode()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Cubic,
            startNormalG: 10.0,
            endNormalG: 20.0,
            normalGChannel: new FixedForceEasingFunction(0.75));

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(17.5, sampled.TargetNormalG.Value, 10);
        Assert.NotEqual(11.25, sampled.TargetNormalG.Value);
    }

    [Fact]
    public void ForceTargetSampler_Sample_LateralGChannel_OverridesInterpolationMode()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Quintic,
            startLateralG: -1.0,
            endLateralG: 1.0,
            lateralGChannel: new FixedForceEasingFunction(0.25));

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetLateralG.HasValue);
        Assert.Equal(-0.5, sampled.TargetLateralG.Value, 10);
        Assert.NotEqual(-0.9375, sampled.TargetLateralG.Value);
    }

    [Fact]
    public void ForceTargetSampler_Sample_NullNormalGChannel_FallsBackToExistingInterpolation()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Constant,
            startNormalG: 10.0,
            endNormalG: 20.0,
            easingFunction: new FixedForceEasingFunction(0.75),
            normalGChannel: null);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(17.5, sampled.TargetNormalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_KeyframedForceEasingFunction_WorksAsNormalGChannel()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Constant,
            startNormalG: 10.0,
            endNormalG: 30.0,
            normalGChannel: new KeyframedForceEasingFunction(new System.Collections.Generic.List<(double t, double value)>
            {
                (0.0, 0.0),
                (0.5, 1.0),
                (1.0, 0.2)
            }));

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 7.5);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(22.0, sampled.TargetNormalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_BuiltInForceEasingFunction_WorksAsLateralGChannel()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Linear,
            startLateralG: -2.0,
            endLateralG: 2.0,
            lateralGChannel: new BuiltInForceEasingFunction(ForceInterpolationMode.Quadratic));

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetLateralG.HasValue);
        Assert.Equal(-1.0, sampled.TargetLateralG.Value, 10);
        Assert.NotEqual(0.0, sampled.TargetLateralG.Value);
    }

    [Fact]
    public void ForceTargetSampler_Sample_RollRateChannel_OverridesDefaultBehavior()
    {
        var section = new ForceSection(
            targetNormalG: 2.0,
            length: 10.0,
            rollRateChannel: new FixedForceEasingFunction(12.5));

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetRollRateDegPerSec.HasValue);
        Assert.Equal(12.5, sampled.TargetRollRateDegPerSec.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_NullRollRateChannel_PreservesExistingBehavior()
    {
        var section = new ForceSection(
            targetNormalG: 2.0,
            length: 10.0,
            rollRateChannel: null);

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.Null(sampled.TargetRollRateDegPerSec);
    }

    [Fact]
    public void ForceTargetSampler_Sample_KeyframedForceEasingFunction_WorksAsRollRateChannel()
    {
        var section = new ForceSection(
            targetNormalG: 2.0,
            length: 10.0,
            rollRateChannel: new KeyframedForceEasingFunction(new System.Collections.Generic.List<(double t, double value)>
            {
                (0.0, 0.0),
                (0.5, 8.0),
                (1.0, 4.0)
            }));

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 7.5);

        Assert.True(sampled.TargetRollRateDegPerSec.HasValue);
        Assert.Equal(6.0, sampled.TargetRollRateDegPerSec.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_BuiltInForceEasingFunction_WorksAsRollRateChannel()
    {
        var section = new ForceSection(
            targetNormalG: 2.0,
            length: 10.0,
            rollRateChannel: new BuiltInForceEasingFunction(ForceInterpolationMode.Quadratic));

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetRollRateDegPerSec.HasValue);
        Assert.Equal(0.25, sampled.TargetRollRateDegPerSec.Value, 10);
        Assert.NotEqual(0.5, sampled.TargetRollRateDegPerSec.Value);
    }

    [Fact]
    public void ForceTargetSampler_Sample_ChannelSetNormalG_OverridesNormalGChannel()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Cubic,
            startNormalG: 10.0,
            endNormalG: 20.0,
            normalGChannel: new FixedForceEasingFunction(0.75))
        {
            Channels = new ForceChannelSet
            {
                NormalG = new ForceChannel(new FixedForceEasingFunction(0.25))
            }
        };

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(12.5, sampled.TargetNormalG.Value, 10);
        Assert.NotEqual(17.5, sampled.TargetNormalG.Value);
    }

    [Fact]
    public void ForceTargetSampler_Sample_ChannelSetLateralG_OverridesLateralGChannel()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Quintic,
            startLateralG: -1.0,
            endLateralG: 1.0,
            lateralGChannel: new FixedForceEasingFunction(0.25))
        {
            Channels = new ForceChannelSet
            {
                LateralG = new ForceChannel(new FixedForceEasingFunction(0.75))
            }
        };

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetLateralG.HasValue);
        Assert.Equal(0.5, sampled.TargetLateralG.Value, 10);
        Assert.NotEqual(-0.5, sampled.TargetLateralG.Value);
    }

    [Fact]
    public void ForceTargetSampler_Sample_ChannelSetRollRate_OverridesRollRateChannel()
    {
        var section = new ForceSection(
            targetNormalG: 2.0,
            length: 10.0,
            rollRateChannel: new FixedForceEasingFunction(12.5))
        {
            Channels = new ForceChannelSet
            {
                RollRate = new ForceChannel(new FixedForceEasingFunction(3.25))
            }
        };

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetRollRateDegPerSec.HasValue);
        Assert.Equal(3.25, sampled.TargetRollRateDegPerSec.Value, 10);
        Assert.NotEqual(12.5, sampled.TargetRollRateDegPerSec.Value);
    }

    [Fact]
    public void ForceTargetSampler_Sample_ChannelSetNormalGChannels_SumsAllChannelValues()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Linear,
            startNormalG: 10.0,
            endNormalG: 20.0)
        {
            Channels = new ForceChannelSet
            {
                NormalGChannels = new IForceChannel[]
                {
                    new ForceChannel(new FixedForceEasingFunction(1.5)),
                    new ForceChannel(new FixedForceEasingFunction(-0.25)),
                    new ForceChannel(new FixedForceEasingFunction(0.75))
                }
            }
        };

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(2.0, sampled.TargetNormalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_ChannelSetLateralGChannels_SumsAllChannelValues()
    {
        var section = new ForceSection(length: 10.0)
        {
            Channels = new ForceChannelSet
            {
                LateralGChannels = new IForceChannel[]
                {
                    new ForceChannel(new FixedForceEasingFunction(0.2)),
                    new ForceChannel(new FixedForceEasingFunction(-0.05)),
                    new ForceChannel(new FixedForceEasingFunction(0.35))
                }
            }
        };

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 2.5);

        Assert.True(sampled.TargetLateralG.HasValue);
        Assert.Equal(0.5, sampled.TargetLateralG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_ChannelSetLongitudinalGChannels_SumsAllChannelValues()
    {
        var section = new ForceSection(length: 10.0)
        {
            Channels = new ForceChannelSet
            {
                LongitudinalGChannels = new IForceChannel[]
                {
                    new ForceChannel(new FixedForceEasingFunction(0.2)),
                    new ForceChannel(new FixedForceEasingFunction(-0.05)),
                    new ForceChannel(new FixedForceEasingFunction(0.35))
                }
            }
        };

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 2.5);

        Assert.True(sampled.TargetLongitudinalG.HasValue);
        Assert.Equal(0.5, sampled.TargetLongitudinalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_ChannelSetRollRateChannels_SumsAllChannelValues()
    {
        var section = new ForceSection(
            targetNormalG: 2.0,
            length: 10.0)
        {
            Channels = new ForceChannelSet
            {
                RollRateChannels = new IForceChannel[]
                {
                    new ForceChannel(new FixedForceEasingFunction(2.25)),
                    new ForceChannel(new FixedForceEasingFunction(0.75)),
                    new ForceChannel(new FixedForceEasingFunction(1.5))
                }
            }
        };

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 4.0);

        Assert.True(sampled.TargetRollRateDegPerSec.HasValue);
        Assert.Equal(4.5, sampled.TargetRollRateDegPerSec.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_ChannelSetNormalGChannels_MaxBlendMode_UsesLargestChannelValue()
    {
        var section = new ForceSection(length: 10.0)
        {
            Channels = new ForceChannelSet
            {
                NormalGBlendMode = ForceChannelBlendMode.Max,
                NormalGChannels = new IForceChannel[]
                {
                    new ForceChannel(new FixedForceEasingFunction(1.5)),
                    new ForceChannel(new FixedForceEasingFunction(-0.25)),
                    new ForceChannel(new FixedForceEasingFunction(0.75))
                }
            }
        };

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(1.5, sampled.TargetNormalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_ChannelSetNormalGChannels_OverrideBlendMode_UsesLastChannelValue()
    {
        var section = new ForceSection(length: 10.0)
        {
            Channels = new ForceChannelSet
            {
                NormalGBlendMode = ForceChannelBlendMode.Override,
                NormalGChannels = new IForceChannel[]
                {
                    new ForceChannel(new FixedForceEasingFunction(1.5)),
                    new ForceChannel(new FixedForceEasingFunction(-0.25)),
                    new ForceChannel(new FixedForceEasingFunction(0.75))
                }
            }
        };

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(0.75, sampled.TargetNormalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_ChannelSetBlendModes_AreIndependentPerForceType()
    {
        var section = new ForceSection(length: 10.0)
        {
            Channels = new ForceChannelSet
            {
                NormalGBlendMode = ForceChannelBlendMode.Max,
                NormalGChannels = new IForceChannel[]
                {
                    new ForceChannel(new FixedForceEasingFunction(0.1)),
                    new ForceChannel(new FixedForceEasingFunction(0.8)),
                    new ForceChannel(new FixedForceEasingFunction(0.6))
                },
                LateralGBlendMode = ForceChannelBlendMode.Override,
                LateralGChannels = new IForceChannel[]
                {
                    new ForceChannel(new FixedForceEasingFunction(-0.3)),
                    new ForceChannel(new FixedForceEasingFunction(0.25)),
                    new ForceChannel(new FixedForceEasingFunction(0.05))
                },
                RollRateBlendMode = ForceChannelBlendMode.Sum,
                RollRateChannels = new IForceChannel[]
                {
                    new ForceChannel(new FixedForceEasingFunction(1.25)),
                    new ForceChannel(new FixedForceEasingFunction(2.75)),
                    new ForceChannel(new FixedForceEasingFunction(-0.5))
                }
            }
        };

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(0.8, sampled.TargetNormalG.Value, 10);
        Assert.True(sampled.TargetLateralG.HasValue);
        Assert.Equal(0.05, sampled.TargetLateralG.Value, 10);
        Assert.True(sampled.TargetRollRateDegPerSec.HasValue);
        Assert.Equal(3.5, sampled.TargetRollRateDegPerSec.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_ChannelSetSingleNormalGChannel_IgnoresBlendMode()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Constant,
            startNormalG: 10.0,
            endNormalG: 20.0)
        {
            Channels = new ForceChannelSet
            {
                NormalGBlendMode = ForceChannelBlendMode.Override,
                NormalG = new ForceChannel(new FixedForceEasingFunction(0.25))
            }
        };

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(12.5, sampled.TargetNormalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_ChannelSetNormalGChannels_OverrideSingleChannel()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Constant,
            startNormalG: 10.0,
            endNormalG: 20.0)
        {
            Channels = new ForceChannelSet
            {
                NormalG = new ForceChannel(new FixedForceEasingFunction(0.25)),
                NormalGChannels = new IForceChannel[]
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

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(3.0, sampled.TargetNormalG.Value, 10);
        Assert.NotEqual(12.5, sampled.TargetNormalG.Value);
    }

    [Fact]
    public void ForceTargetSampler_Sample_EmptyChannelSetNormalGChannels_FallsBackToSingleChannel()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Constant,
            startNormalG: 10.0,
            endNormalG: 20.0)
        {
            Channels = new ForceChannelSet
            {
                NormalG = new ForceChannel(new FixedForceEasingFunction(0.25)),
                NormalGChannels = System.Array.Empty<IForceChannel>()
            }
        };

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(12.5, sampled.TargetNormalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_NullChannelSet_FallsBackToExistingBehavior()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Constant,
            startNormalG: 10.0,
            endNormalG: 20.0,
            normalGChannel: new FixedForceEasingFunction(0.75))
        {
            Channels = null
        };

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(17.5, sampled.TargetNormalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_KeyframedForceEasingFunction_WorksViaForceChannel()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Constant,
            startNormalG: 10.0,
            endNormalG: 30.0)
        {
            Channels = new ForceChannelSet
            {
                NormalG = new ForceChannel(
                    new KeyframedForceEasingFunction(new System.Collections.Generic.List<(double t, double value)>
                    {
                        (0.0, 0.0),
                        (0.5, 1.0),
                        (1.0, 0.2)
                    }))
            }
        };

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 7.5);

        Assert.True(sampled.TargetNormalG.HasValue);
        Assert.Equal(22.0, sampled.TargetNormalG.Value, 10);
    }

    [Fact]
    public void ForceTargetSampler_Sample_BuiltInForceEasingFunction_WorksViaForceChannel()
    {
        var section = new ForceSection(
            length: 10.0,
            interpolationMode: ForceInterpolationMode.Linear,
            startLateralG: -2.0,
            endLateralG: 2.0)
        {
            Channels = new ForceChannelSet
            {
                LateralG = new ForceChannel(new BuiltInForceEasingFunction(ForceInterpolationMode.Quadratic))
            }
        };

        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (section, 10.0)
        });

        SampledForceTarget sampled = ForceTargetSampler.Sample(intervals, 5.0);

        Assert.True(sampled.TargetLateralG.HasValue);
        Assert.Equal(-1.0, sampled.TargetLateralG.Value, 10);
        Assert.NotEqual(0.0, sampled.TargetLateralG.Value);
    }

    [Theory]
    [InlineData(-0.0001)]
    [InlineData(5.0001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ForceTargetSampler_Sample_OutOfRangeOrNonFiniteDistance_ThrowsArgumentOutOfRangeException(double distance)
    {
        IReadOnlyList<ResolvedSectionInterval<ForceSection>> intervals = ForceTargetResolver.Resolve(new[]
        {
            (new ForceSection(targetNormalG: 2.0, length: 2.0), 2.0),
            (new ForceSection(targetNormalG: 3.0, length: 3.0), 3.0)
        });

        Assert.Throws<ArgumentOutOfRangeException>(() => ForceTargetSampler.Sample(intervals, distance));
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
