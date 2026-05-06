using System;
using Quantum.Track;

namespace Quantum.Tests;

public sealed class TrainCarGeometryTests
{
    [Fact]
    public void Constructor_StoresValues()
    {
        var geometry = new TrainCarGeometry(length: 3.0, width: 1.2, height: 1.5);

        Assert.Equal(3.0, geometry.Length);
        Assert.Equal(1.2, geometry.Width);
        Assert.Equal(1.5, geometry.Height);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WhenLengthIsInvalid_ThrowsArgumentOutOfRangeException(double invalidLength)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new TrainCarGeometry(
            length: invalidLength,
            width: 1.2,
            height: 1.5));

        Assert.Equal("length", exception.ParamName);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WhenWidthIsInvalid_ThrowsArgumentOutOfRangeException(double invalidWidth)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new TrainCarGeometry(
            length: 3.0,
            width: invalidWidth,
            height: 1.5));

        Assert.Equal("width", exception.ParamName);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WhenHeightIsInvalid_ThrowsArgumentOutOfRangeException(double invalidHeight)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new TrainCarGeometry(
            length: 3.0,
            width: 1.2,
            height: invalidHeight));

        Assert.Equal("height", exception.ParamName);
    }
}
