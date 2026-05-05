using Quantum.Track;

namespace Quantum.Tests;

public sealed class BuiltInForceEasingFunctionTests
{
    [Theory]
    [InlineData(ForceInterpolationMode.Constant, 0.0)]
    [InlineData(ForceInterpolationMode.Linear, 0.5)]
    [InlineData(ForceInterpolationMode.SmoothStep, 0.5)]
    [InlineData(ForceInterpolationMode.Quadratic, 0.25)]
    [InlineData(ForceInterpolationMode.Cubic, 0.125)]
    [InlineData(ForceInterpolationMode.Quartic, 0.0625)]
    [InlineData(ForceInterpolationMode.Quintic, 0.03125)]
    [InlineData(ForceInterpolationMode.Sinusoidal, 0.2928932188134524)]
    public void Evaluate_AtMidpoint_ReturnsExpectedValue(
        ForceInterpolationMode mode,
        double expected)
    {
        IForceEasingFunction easing = new BuiltInForceEasingFunction(mode);

        double actual = easing.Evaluate(0.5);

        Assert.Equal(expected, actual, 10);
    }
}
