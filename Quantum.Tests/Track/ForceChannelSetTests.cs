using Quantum.Track;

namespace Quantum.Tests;

public sealed class ForceChannelSetTests
{
    [Fact]
    public void ForceChannelSet_BlendModes_DefaultToSum()
    {
        var set = new ForceChannelSet();

        Assert.Equal(ForceChannelBlendMode.Sum, set.NormalGBlendMode);
        Assert.Equal(ForceChannelBlendMode.Sum, set.LateralGBlendMode);
        Assert.Equal(ForceChannelBlendMode.Sum, set.RollRateBlendMode);
    }

    [Fact]
    public void ForceChannelSet_NormalGChannels_NullEntry_ThrowsArgumentException()
    {
        var channels = new List<IForceChannel>
        {
            new ForceChannel(new FixedForceEasingFunction(0.1)),
            null!
        };

        var set = new ForceChannelSet();

        Assert.Throws<ArgumentException>(() => set.NormalGChannels = channels);
    }

    [Fact]
    public void ForceChannelSet_LateralGChannels_NullEntry_ThrowsArgumentException()
    {
        var channels = new List<IForceChannel>
        {
            new ForceChannel(new FixedForceEasingFunction(0.2)),
            null!
        };

        var set = new ForceChannelSet();

        Assert.Throws<ArgumentException>(() => set.LateralGChannels = channels);
    }

    [Fact]
    public void ForceChannelSet_RollRateChannels_NullEntry_ThrowsArgumentException()
    {
        var channels = new List<IForceChannel>
        {
            new ForceChannel(new FixedForceEasingFunction(0.3)),
            null!
        };

        var set = new ForceChannelSet();

        Assert.Throws<ArgumentException>(() => set.RollRateChannels = channels);
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
