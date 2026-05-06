using System;
using Quantum.Track;

namespace Quantum.Tests;

public sealed class TrainWheelLayoutTests
{
    [Fact]
    public void Constructor_StoresValues()
    {
        var layout = new TrainWheelLayout(
            wheelCountPerBogie: 2,
            wheelRadius: 0.35,
            wheelWidth: 0.1,
            axleSpacing: 1.0);

        Assert.Equal(2, layout.WheelCountPerBogie);
        Assert.Equal(0.35, layout.WheelRadius);
        Assert.Equal(0.1, layout.WheelWidth);
        Assert.Equal(1.0, layout.AxleSpacing);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenWheelCountPerBogieIsInvalid_ThrowsArgumentOutOfRangeException(int invalidWheelCountPerBogie)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new TrainWheelLayout(
            wheelCountPerBogie: invalidWheelCountPerBogie,
            wheelRadius: 0.35,
            wheelWidth: 0.1,
            axleSpacing: 1.0));

        Assert.Equal("wheelCountPerBogie", exception.ParamName);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WhenWheelRadiusIsInvalid_ThrowsArgumentOutOfRangeException(double invalidWheelRadius)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new TrainWheelLayout(
            wheelCountPerBogie: 2,
            wheelRadius: invalidWheelRadius,
            wheelWidth: 0.1,
            axleSpacing: 1.0));

        Assert.Equal("wheelRadius", exception.ParamName);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WhenWheelWidthIsInvalid_ThrowsArgumentOutOfRangeException(double invalidWheelWidth)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new TrainWheelLayout(
            wheelCountPerBogie: 2,
            wheelRadius: 0.35,
            wheelWidth: invalidWheelWidth,
            axleSpacing: 1.0));

        Assert.Equal("wheelWidth", exception.ParamName);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WhenAxleSpacingIsInvalid_ThrowsArgumentOutOfRangeException(double invalidAxleSpacing)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new TrainWheelLayout(
            wheelCountPerBogie: 2,
            wheelRadius: 0.35,
            wheelWidth: 0.1,
            axleSpacing: invalidAxleSpacing));

        Assert.Equal("axleSpacing", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenAxleSpacingIsZero_StoresValue()
    {
        var layout = new TrainWheelLayout(
            wheelCountPerBogie: 2,
            wheelRadius: 0.35,
            wheelWidth: 0.1,
            axleSpacing: 0.0);

        Assert.Equal(0.0, layout.AxleSpacing);
    }
}
