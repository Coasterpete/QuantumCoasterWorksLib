using Quantum.Track;

namespace Quantum.Tests;

public sealed class KeyframedForceEasingFunctionTests
{
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.25, 0.25)]
    [InlineData(0.5, 0.5)]
    [InlineData(0.75, 0.75)]
    [InlineData(1.0, 1.0)]
    public void Evaluate_TwoPoints_BehavesLikeLinear(double t, double expected)
    {
        IForceEasingFunction easing = new KeyframedForceEasingFunction(new List<(double t, double value)>
        {
            (1.0, 1.0),
            (0.0, 0.0)
        });

        double actual = easing.Evaluate(t);

        Assert.Equal(expected, actual, 10);
    }

    [Theory]
    [InlineData(0.25, 0.5)]
    [InlineData(0.75, 0.6)]
    public void Evaluate_ThreePoints_InterpolatesWithinExpectedSegment(double t, double expected)
    {
        IForceEasingFunction easing = new KeyframedForceEasingFunction(new List<(double t, double value)>
        {
            (0.0, 0.0),
            (0.5, 1.0),
            (1.0, 0.2)
        });

        double actual = easing.Evaluate(t);

        Assert.Equal(expected, actual, 10);
    }

    [Fact]
    public void Evaluate_OutsideRange_ClampsToNearestEdgeValue()
    {
        IForceEasingFunction easing = new KeyframedForceEasingFunction(new List<(double t, double value)>
        {
            (0.2, 2.0),
            (0.8, 8.0)
        });

        Assert.Equal(2.0, easing.Evaluate(-1.0), 10);
        Assert.Equal(8.0, easing.Evaluate(2.0), 10);
    }

    [Fact]
    public void Constructor_InvalidInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new KeyframedForceEasingFunction(null!));

        Assert.Throws<ArgumentOutOfRangeException>(() => new KeyframedForceEasingFunction(new List<(double t, double value)>
        {
            (0.0, 0.0)
        }));

        Assert.Throws<ArgumentOutOfRangeException>(() => new KeyframedForceEasingFunction(new List<(double t, double value)>
        {
            (-0.1, 0.0),
            (1.0, 1.0)
        }));
    }

    [Fact]
    public void Constructor_NonFiniteValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new KeyframedForceEasingFunction(new List<(double t, double value)>
        {
            (0.0, double.NaN),
            (1.0, 1.0)
        }));
    }

    [Theory]
    [InlineData(0.125)]
    [InlineData(0.5)]
    [InlineData(0.875)]
    public void Evaluate_WithNullSegmentEasings_FallsBackToLinearAndMatchesDefaultBehavior(double t)
    {
        List<(double t, double value)> points = new()
        {
            (0.0, 0.0),
            (0.4, 1.0),
            (1.0, 0.0)
        };

        IForceEasingFunction baseline = new KeyframedForceEasingFunction(points);
        IForceEasingFunction withFallback = new KeyframedForceEasingFunction(points, segmentEasings: null);

        Assert.Equal(baseline.Evaluate(t), withFallback.Evaluate(t), 10);
    }

    [Fact]
    public void Evaluate_WithSmoothStepSegmentEasing_ChangesMidpointSampleWithinSegment()
    {
        IForceEasingFunction easing = new KeyframedForceEasingFunction(
            new List<(double t, double value)>
            {
                (0.0, 0.0),
                (0.75, 1.0),
                (1.0, 1.0)
            },
            new IForceEasingFunction?[]
            {
                new BuiltInForceEasingFunction(ForceInterpolationMode.SmoothStep),
                null
            });

        double actual = easing.Evaluate(0.5);

        Assert.Equal(20.0 / 27.0, actual, 10);
    }

    [Fact]
    public void Evaluate_BuiltInForceEasingFunction_CanBeUsedAsSegmentEasing()
    {
        IForceEasingFunction easing = new KeyframedForceEasingFunction(
            new List<(double t, double value)>
            {
                (0.0, 0.0),
                (1.0, 1.0)
            },
            new IForceEasingFunction?[]
            {
                new BuiltInForceEasingFunction(ForceInterpolationMode.Quadratic)
            });

        double actual = easing.Evaluate(0.5);

        Assert.Equal(0.25, actual, 10);
    }

    [Fact]
    public void Constructor_DuplicateTValues_Throws()
    {
        Assert.Throws<ArgumentException>(() => new KeyframedForceEasingFunction(new List<(double t, double value)>
        {
            (0.0, 0.0),
            (0.5, 1.0),
            (0.5, 0.4)
        }));
    }

    [Fact]
    public void Constructor_InvalidSegmentEasingCount_Throws()
    {
        Assert.Throws<ArgumentException>(() => new KeyframedForceEasingFunction(
            new List<(double t, double value)>
            {
                (0.0, 0.0),
                (0.5, 1.0),
                (1.0, 0.0)
            },
            new IForceEasingFunction?[]
            {
                new BuiltInForceEasingFunction(ForceInterpolationMode.Linear)
            }));
    }
}
