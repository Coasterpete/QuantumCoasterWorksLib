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
}
