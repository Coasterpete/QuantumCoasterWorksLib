using System;
using Quantum.Track;

namespace Quantum.Tests;

public sealed class TrainConsistDefinitionTests
{
    [Fact]
    public void Constructor_StoresAllValues()
    {
        var definition = new TrainConsistDefinition(
            carCount: 4,
            carSpacing: 2.5,
            carLength: 3.0,
            carWidth: 1.2,
            carHeight: 1.5,
            bogieSpacing: 2.0);

        Assert.Equal(4, definition.CarCount);
        Assert.Equal(2.5, definition.CarSpacing);
        Assert.Equal(3.0, definition.CarLength);
        Assert.Equal(1.2, definition.CarWidth);
        Assert.Equal(1.5, definition.CarHeight);
        Assert.Equal(2.0, definition.BogieSpacing);
        Assert.Equal(3.0, definition.CarGeometry.Length);
        Assert.Equal(1.2, definition.CarGeometry.Width);
        Assert.Equal(1.5, definition.CarGeometry.Height);
        Assert.Equal(2.0, definition.BogieLayout.BogieSpacing);
        Assert.Null(definition.WheelLayout);
    }

    [Fact]
    public void ObjectConstructor_MatchesScalarConstructorValues()
    {
        var scalar = new TrainConsistDefinition(
            carCount: 4,
            carSpacing: 2.5,
            carLength: 3.0,
            carWidth: 1.2,
            carHeight: 1.5,
            bogieSpacing: 2.0);
        var geometry = new TrainCarGeometry(length: 3.0, width: 1.2, height: 1.5);
        var layout = new TrainBogieLayout(bogieSpacing: 2.0);
        var valueObject = new TrainConsistDefinition(
            carCount: 4,
            carSpacing: 2.5,
            carGeometry: geometry,
            bogieLayout: layout);

        Assert.Equal(scalar.CarCount, valueObject.CarCount);
        Assert.Equal(scalar.CarSpacing, valueObject.CarSpacing);
        Assert.Equal(scalar.CarLength, valueObject.CarLength);
        Assert.Equal(scalar.CarWidth, valueObject.CarWidth);
        Assert.Equal(scalar.CarHeight, valueObject.CarHeight);
        Assert.Equal(scalar.BogieSpacing, valueObject.BogieSpacing);
        Assert.Equal(scalar.WheelLayout, valueObject.WheelLayout);
    }

    [Fact]
    public void Constructor_WithNullWheelLayout_IsValid()
    {
        var definition = new TrainConsistDefinition(
            carCount: 4,
            carSpacing: 2.5,
            carLength: 3.0,
            carWidth: 1.2,
            carHeight: 1.5,
            bogieSpacing: 2.0,
            wheelLayout: null);

        Assert.Null(definition.WheelLayout);
    }

    [Fact]
    public void Constructor_WithWheelLayout_StoresWheelLayout()
    {
        var wheelLayout = new TrainWheelLayout(
            wheelCountPerBogie: 2,
            wheelRadius: 0.35,
            wheelWidth: 0.1,
            axleSpacing: 1.0);

        var definition = new TrainConsistDefinition(
            carCount: 4,
            carSpacing: 2.5,
            carGeometry: new TrainCarGeometry(3.0, 1.2, 1.5),
            bogieLayout: new TrainBogieLayout(2.0),
            wheelLayout: wheelLayout);

        Assert.Same(wheelLayout, definition.WheelLayout);
    }

    [Fact]
    public void ObjectConstructor_WhenCarGeometryIsNull_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new TrainConsistDefinition(
            carCount: 4,
            carSpacing: 2.5,
            carGeometry: null!,
            bogieLayout: new TrainBogieLayout(2.0)));

        Assert.Equal("carGeometry", exception.ParamName);
    }

    [Fact]
    public void ObjectConstructor_WhenBogieLayoutIsNull_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new TrainConsistDefinition(
            carCount: 4,
            carSpacing: 2.5,
            carGeometry: new TrainCarGeometry(3.0, 1.2, 1.5),
            bogieLayout: null!));

        Assert.Equal("bogieLayout", exception.ParamName);
    }

    [Fact]
    public void ObjectConstructor_WhenBogieSpacingExceedsCarLength_ThrowsArgumentOutOfRangeException()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new TrainConsistDefinition(
            carCount: 4,
            carSpacing: 2.5,
            carGeometry: new TrainCarGeometry(2.5, 1.2, 1.5),
            bogieLayout: new TrainBogieLayout(2.6)));

        Assert.Equal("bogieLayout", exception.ParamName);
        Assert.Contains("less than or equal", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenCarCountIsInvalid_ThrowsArgumentOutOfRangeException(int invalidCarCount)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateDefinition(carCount: invalidCarCount));

        Assert.Equal("carCount", exception.ParamName);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WhenCarSpacingIsInvalid_ThrowsArgumentOutOfRangeException(double invalidCarSpacing)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateDefinition(carSpacing: invalidCarSpacing));

        Assert.Equal("carSpacing", exception.ParamName);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WhenCarLengthIsInvalid_ThrowsArgumentOutOfRangeException(double invalidCarLength)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateDefinition(carLength: invalidCarLength));

        Assert.Equal("carLength", exception.ParamName);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WhenCarWidthIsInvalid_ThrowsArgumentOutOfRangeException(double invalidCarWidth)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateDefinition(carWidth: invalidCarWidth));

        Assert.Equal("carWidth", exception.ParamName);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WhenCarHeightIsInvalid_ThrowsArgumentOutOfRangeException(double invalidCarHeight)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateDefinition(carHeight: invalidCarHeight));

        Assert.Equal("carHeight", exception.ParamName);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WhenBogieSpacingIsInvalid_ThrowsArgumentOutOfRangeException(double invalidBogieSpacing)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateDefinition(bogieSpacing: invalidBogieSpacing));

        Assert.Equal("bogieSpacing", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenBogieSpacingExceedsCarLength_ThrowsArgumentOutOfRangeException()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateDefinition(
            carLength: 2.5,
            bogieSpacing: 2.6));

        Assert.Equal("bogieSpacing", exception.ParamName);
        Assert.Contains("less than or equal", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static TrainConsistDefinition CreateDefinition(
        int carCount = 4,
        double carSpacing = 2.5,
        double carLength = 3.0,
        double carWidth = 1.2,
        double carHeight = 1.5,
        double bogieSpacing = 2.0)
    {
        return new TrainConsistDefinition(
            carCount: carCount,
            carSpacing: carSpacing,
            carLength: carLength,
            carWidth: carWidth,
            carHeight: carHeight,
            bogieSpacing: bogieSpacing);
    }
}
