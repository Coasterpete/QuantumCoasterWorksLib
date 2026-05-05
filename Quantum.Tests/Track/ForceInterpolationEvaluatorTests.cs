using Quantum.Track;

namespace Quantum.Tests;

public sealed class ForceInterpolationEvaluatorTests
{
    private readonly ForceInterpolationEvaluator _evaluator = new();

    [Theory]
    [InlineData(ForceInterpolationMode.Constant)]
    [InlineData(ForceInterpolationMode.Linear)]
    [InlineData(ForceInterpolationMode.SmoothStep)]
    [InlineData(ForceInterpolationMode.Quadratic)]
    [InlineData(ForceInterpolationMode.Cubic)]
    [InlineData(ForceInterpolationMode.Quartic)]
    [InlineData(ForceInterpolationMode.Quintic)]
    [InlineData(ForceInterpolationMode.Sinusoidal)]
    public void Evaluate_AtZero_ReturnsZero(ForceInterpolationMode mode)
    {
        double value = _evaluator.Evaluate(0.0, mode);

        Assert.Equal(0.0, value, 10);
    }

    [Theory]
    [InlineData(ForceInterpolationMode.Linear)]
    [InlineData(ForceInterpolationMode.SmoothStep)]
    [InlineData(ForceInterpolationMode.Quadratic)]
    [InlineData(ForceInterpolationMode.Cubic)]
    [InlineData(ForceInterpolationMode.Quartic)]
    [InlineData(ForceInterpolationMode.Quintic)]
    [InlineData(ForceInterpolationMode.Sinusoidal)]
    public void Evaluate_AtOne_NonConstantModes_ReturnOne(ForceInterpolationMode mode)
    {
        double value = _evaluator.Evaluate(1.0, mode);

        Assert.Equal(1.0, value, 10);
    }

    [Fact]
    public void Evaluate_AtOne_ConstantMode_ReturnsZero()
    {
        double value = _evaluator.Evaluate(1.0, ForceInterpolationMode.Constant);

        Assert.Equal(0.0, value, 10);
    }

    [Fact]
    public void Evaluate_AtMidpoint_Linear_ReturnsHalf()
    {
        double midpoint = _evaluator.Evaluate(0.5, ForceInterpolationMode.Linear);

        Assert.Equal(0.5, midpoint, 10);
    }

    [Fact]
    public void Evaluate_AtMidpoint_SmoothStep_ReturnsHalf()
    {
        double midpoint = _evaluator.Evaluate(0.5, ForceInterpolationMode.SmoothStep);

        Assert.Equal(0.5, midpoint, 10);
    }

    [Fact]
    public void Evaluate_AtMidpoint_HigherOrderModes_FollowExpectedOrdering()
    {
        double quadraticMidpoint = _evaluator.Evaluate(0.5, ForceInterpolationMode.Quadratic);
        double cubicMidpoint = _evaluator.Evaluate(0.5, ForceInterpolationMode.Cubic);
        double quarticMidpoint = _evaluator.Evaluate(0.5, ForceInterpolationMode.Quartic);
        double quinticMidpoint = _evaluator.Evaluate(0.5, ForceInterpolationMode.Quintic);
        double sinusoidalMidpoint = _evaluator.Evaluate(0.5, ForceInterpolationMode.Sinusoidal);

        Assert.True(quadraticMidpoint < 0.5);
        Assert.True(cubicMidpoint < quadraticMidpoint);
        Assert.True(quarticMidpoint < cubicMidpoint);
        Assert.True(quinticMidpoint < quarticMidpoint);
        Assert.True(sinusoidalMidpoint > quadraticMidpoint);
    }
}
