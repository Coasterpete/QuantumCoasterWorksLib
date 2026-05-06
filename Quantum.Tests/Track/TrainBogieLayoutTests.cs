using System;
using Quantum.Track;

namespace Quantum.Tests;

public sealed class TrainBogieLayoutTests
{
    [Fact]
    public void Constructor_StoresValue()
    {
        var layout = new TrainBogieLayout(bogieSpacing: 2.0);

        Assert.Equal(2.0, layout.BogieSpacing);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WhenBogieSpacingIsInvalid_ThrowsArgumentOutOfRangeException(double invalidBogieSpacing)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new TrainBogieLayout(
            bogieSpacing: invalidBogieSpacing));

        Assert.Equal("bogieSpacing", exception.ParamName);
    }
}
