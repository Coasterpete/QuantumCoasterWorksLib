using Quantum.Track;
using Quantum.Track.Internal;

namespace Quantum.Tests;

public sealed class ScalarEasingTests
{
    private static readonly ForceInterpolationEvaluator ForceEvaluator = new();

    [Theory]
    [MemberData(nameof(ValueTable))]
    public void Evaluate_ValueTable_MatchesCurrentFormulas(int modeValue, double t, double expected)
    {
        double actual = ScalarEasing.Evaluate(t, (ScalarEasingMode)modeValue);

        Assert.Equal(expected, actual, 15);
    }

    [Theory]
    [InlineData(ForceInterpolationMode.Constant, 0)]
    [InlineData(ForceInterpolationMode.Linear, 1)]
    [InlineData(ForceInterpolationMode.SmoothStep, 2)]
    [InlineData(ForceInterpolationMode.Quadratic, 3)]
    [InlineData(ForceInterpolationMode.Cubic, 4)]
    [InlineData(ForceInterpolationMode.Quartic, 5)]
    [InlineData(ForceInterpolationMode.Quintic, 6)]
    [InlineData(ForceInterpolationMode.Sinusoidal, 7)]
    public void MapForceInterpolationMode_MapsToSharedScalarMode(
        ForceInterpolationMode mode,
        int expectedScalarMode)
    {
        Assert.Equal(expectedScalarMode, (int)ScalarEasing.MapForceInterpolationMode(mode));
    }

    [Theory]
    [InlineData(BankingProfileInterpolationMode.Constant, 0)]
    [InlineData(BankingProfileInterpolationMode.Linear, 1)]
    [InlineData(BankingProfileInterpolationMode.SmoothStep, 2)]
    [InlineData(BankingProfileInterpolationMode.Quadratic, 3)]
    [InlineData(BankingProfileInterpolationMode.Cubic, 4)]
    [InlineData(BankingProfileInterpolationMode.Quartic, 5)]
    [InlineData(BankingProfileInterpolationMode.Quintic, 6)]
    [InlineData(BankingProfileInterpolationMode.Sinusoidal, 7)]
    public void MapBankingProfileInterpolationMode_MapsToSharedScalarMode(
        BankingProfileInterpolationMode mode,
        int expectedScalarMode)
    {
        Assert.Equal(expectedScalarMode, (int)ScalarEasing.MapBankingProfileInterpolationMode(mode));
    }

    [Fact]
    public void IsSupported_InvalidPublicModes_ReturnsFalse()
    {
        Assert.False(ScalarEasing.IsSupported((ForceInterpolationMode)99));
        Assert.False(ScalarEasing.IsSupported((BankingProfileInterpolationMode)99));
    }

    [Theory]
    [MemberData(nameof(MatchingPublicModes))]
    public void BankingAndForceInterpolation_MatchingModes_ProduceEquivalentNormalizedScalars(
        ForceInterpolationMode forceMode,
        BankingProfileInterpolationMode bankingMode)
    {
        var profile = new BankingProfile(new[]
        {
            new BankingProfileKey(0.0, 0.0, bankingMode),
            new BankingProfileKey(10.0, 1.0, BankingProfileInterpolationMode.Constant)
        });

        foreach (double t in new[] { 0.0, 0.25, 0.5, 0.75 })
        {
            double forceScalar = ForceEvaluator.Evaluate(t, forceMode);
            double bankingScalar = BankingProfileSampler.SampleRollRadians(profile, t * 10.0);

            Assert.Equal(forceScalar, bankingScalar, 15);
        }
    }

    public static IEnumerable<object[]> ValueTable()
    {
        yield return Row(ScalarEasingMode.Constant, -0.5, 0.0);
        yield return Row(ScalarEasingMode.Constant, 0.0, 0.0);
        yield return Row(ScalarEasingMode.Constant, 0.25, 0.0);
        yield return Row(ScalarEasingMode.Constant, 0.5, 0.0);
        yield return Row(ScalarEasingMode.Constant, 0.75, 0.0);
        yield return Row(ScalarEasingMode.Constant, 1.0, 0.0);
        yield return Row(ScalarEasingMode.Constant, 1.25, 0.0);

        yield return Row(ScalarEasingMode.Linear, -0.5, -0.5);
        yield return Row(ScalarEasingMode.Linear, 0.0, 0.0);
        yield return Row(ScalarEasingMode.Linear, 0.25, 0.25);
        yield return Row(ScalarEasingMode.Linear, 0.5, 0.5);
        yield return Row(ScalarEasingMode.Linear, 0.75, 0.75);
        yield return Row(ScalarEasingMode.Linear, 1.0, 1.0);
        yield return Row(ScalarEasingMode.Linear, 1.25, 1.25);

        yield return Row(ScalarEasingMode.SmoothStep, -0.5, 1.0);
        yield return Row(ScalarEasingMode.SmoothStep, 0.0, 0.0);
        yield return Row(ScalarEasingMode.SmoothStep, 0.25, 0.15625);
        yield return Row(ScalarEasingMode.SmoothStep, 0.5, 0.5);
        yield return Row(ScalarEasingMode.SmoothStep, 0.75, 0.84375);
        yield return Row(ScalarEasingMode.SmoothStep, 1.0, 1.0);
        yield return Row(ScalarEasingMode.SmoothStep, 1.25, 0.78125);

        yield return Row(ScalarEasingMode.Quadratic, -0.5, 0.25);
        yield return Row(ScalarEasingMode.Quadratic, 0.0, 0.0);
        yield return Row(ScalarEasingMode.Quadratic, 0.25, 0.0625);
        yield return Row(ScalarEasingMode.Quadratic, 0.5, 0.25);
        yield return Row(ScalarEasingMode.Quadratic, 0.75, 0.5625);
        yield return Row(ScalarEasingMode.Quadratic, 1.0, 1.0);
        yield return Row(ScalarEasingMode.Quadratic, 1.25, 1.5625);

        yield return Row(ScalarEasingMode.Cubic, -0.5, -0.125);
        yield return Row(ScalarEasingMode.Cubic, 0.0, 0.0);
        yield return Row(ScalarEasingMode.Cubic, 0.25, 0.015625);
        yield return Row(ScalarEasingMode.Cubic, 0.5, 0.125);
        yield return Row(ScalarEasingMode.Cubic, 0.75, 0.421875);
        yield return Row(ScalarEasingMode.Cubic, 1.0, 1.0);
        yield return Row(ScalarEasingMode.Cubic, 1.25, 1.953125);

        yield return Row(ScalarEasingMode.Quartic, -0.5, 0.0625);
        yield return Row(ScalarEasingMode.Quartic, 0.0, 0.0);
        yield return Row(ScalarEasingMode.Quartic, 0.25, 0.00390625);
        yield return Row(ScalarEasingMode.Quartic, 0.5, 0.0625);
        yield return Row(ScalarEasingMode.Quartic, 0.75, 0.31640625);
        yield return Row(ScalarEasingMode.Quartic, 1.0, 1.0);
        yield return Row(ScalarEasingMode.Quartic, 1.25, 2.44140625);

        yield return Row(ScalarEasingMode.Quintic, -0.5, -0.03125);
        yield return Row(ScalarEasingMode.Quintic, 0.0, 0.0);
        yield return Row(ScalarEasingMode.Quintic, 0.25, 0.0009765625);
        yield return Row(ScalarEasingMode.Quintic, 0.5, 0.03125);
        yield return Row(ScalarEasingMode.Quintic, 0.75, 0.2373046875);
        yield return Row(ScalarEasingMode.Quintic, 1.0, 1.0);
        yield return Row(ScalarEasingMode.Quintic, 1.25, 3.0517578125);

        yield return Row(ScalarEasingMode.Sinusoidal, -0.5, 0.2928932188134524);
        yield return Row(ScalarEasingMode.Sinusoidal, 0.0, 0.0);
        yield return Row(ScalarEasingMode.Sinusoidal, 0.25, 0.07612046748871326);
        yield return Row(ScalarEasingMode.Sinusoidal, 0.5, 0.2928932188134524);
        yield return Row(ScalarEasingMode.Sinusoidal, 0.75, 0.6173165676349102);
        yield return Row(ScalarEasingMode.Sinusoidal, 1.0, 0.9999999999999999);
        yield return Row(ScalarEasingMode.Sinusoidal, 1.25, 1.3826834323650898);
    }

    public static IEnumerable<object[]> MatchingPublicModes()
    {
        yield return new object[] { ForceInterpolationMode.Constant, BankingProfileInterpolationMode.Constant };
        yield return new object[] { ForceInterpolationMode.Linear, BankingProfileInterpolationMode.Linear };
        yield return new object[] { ForceInterpolationMode.SmoothStep, BankingProfileInterpolationMode.SmoothStep };
        yield return new object[] { ForceInterpolationMode.Quadratic, BankingProfileInterpolationMode.Quadratic };
        yield return new object[] { ForceInterpolationMode.Cubic, BankingProfileInterpolationMode.Cubic };
        yield return new object[] { ForceInterpolationMode.Quartic, BankingProfileInterpolationMode.Quartic };
        yield return new object[] { ForceInterpolationMode.Quintic, BankingProfileInterpolationMode.Quintic };
        yield return new object[] { ForceInterpolationMode.Sinusoidal, BankingProfileInterpolationMode.Sinusoidal };
    }

    private static object[] Row(ScalarEasingMode mode, double t, double expected)
    {
        return new object[] { (int)mode, t, expected };
    }
}
