using Quantum.Debug;
using Quantum.Track;
using SystemMath = System.Math;

namespace Quantum.Tests;

public sealed class BankingProfileFixtureTests
{
    private const double Tolerance = 1e-9;

    public static IEnumerable<object[]> FixtureData
    {
        get
        {
            foreach (BankingProfileFixture fixture in BankingProfileFixtures.All())
            {
                yield return new object[] { fixture };
            }
        }
    }

    [Fact]
    public void BankingProfileFixtures_All_CoversRequestedFixtureSet()
    {
        IReadOnlyList<BankingProfileFixture> fixtures = BankingProfileFixtures.All();

        Assert.Equal(
            new[]
            {
                BankingProfileFixtures.ConstantFlatName,
                BankingProfileFixtures.ConstantBankedName,
                BankingProfileFixtures.LinearRollRampName,
                BankingProfileFixtures.SmoothStepRollRampName,
                BankingProfileFixtures.RollHoldWithMultipleKeysName,
                BankingProfileFixtures.UnwrappedOver360RollName
            },
            fixtures.Select(fixture => fixture.Name).ToArray());
    }

    [Theory]
    [MemberData(nameof(FixtureData))]
    public void BankingProfileFixtures_ProduceFiniteOrderedKeysAndSamples(BankingProfileFixture fixture)
    {
        Assert.False(string.IsNullOrWhiteSpace(fixture.Name));
        Assert.True(fixture.Profile.Keys.Count > 0, $"{fixture.Name} should have keys.");
        Assert.True(fixture.SampleDistances.Count >= 2, $"{fixture.Name} should have reusable sample distances.");

        double previousKeyDistance = double.NegativeInfinity;
        for (int i = 0; i < fixture.Profile.Keys.Count; i++)
        {
            BankingProfileKey key = fixture.Profile.Keys[i];

            Assert.True(IsFinite(key.Distance), $"{fixture.Name} key {i} distance should be finite.");
            Assert.True(IsFinite(key.RollRadians), $"{fixture.Name} key {i} roll should be finite.");
            Assert.True(key.Distance > previousKeyDistance, $"{fixture.Name} keys should be strictly ordered.");
            Assert.True(IsSupportedMode(key.InterpolationToNext), $"{fixture.Name} key {i} mode should be supported.");

            previousKeyDistance = key.Distance;
        }

        double previousSampleDistance = double.NegativeInfinity;
        for (int i = 0; i < fixture.SampleDistances.Count; i++)
        {
            double distance = fixture.SampleDistances[i];

            Assert.True(IsFinite(distance), $"{fixture.Name} sample {i} distance should be finite.");
            Assert.True(distance >= previousSampleDistance, $"{fixture.Name} sample distances should be monotonic.");
            previousSampleDistance = distance;
        }
    }

    [Theory]
    [MemberData(nameof(FixtureData))]
    public void BankingProfileFixtures_SampleDeterministically(BankingProfileFixture fixture)
    {
        BankingProfileDiagnosticsReport first = BankingProfileDiagnostics.Sample(
            fixture.Profile,
            fixture.SampleDistances);
        BankingProfileDiagnosticsReport second = BankingProfileDiagnostics.Sample(
            fixture.Profile,
            fixture.SampleDistances);

        AssertSummariesEqual(first.Summary, second.Summary);
        Assert.Equal(fixture.SampleDistances.Count, first.Summary.SampleCount);
        Assert.Equal(first.Samples.Count, second.Samples.Count);

        for (int i = 0; i < first.Samples.Count; i++)
        {
            AssertSamplesEqual(first.Samples[i], second.Samples[i]);
            Assert.True(IsFinite(first.Samples[i].RollRadians), $"{fixture.Name} sample {i} roll should be finite.");
            Assert.True(IsFinite(first.Samples[i].RollDegrees), $"{fixture.Name} sample {i} roll degrees should be finite.");
        }
    }

    [Fact]
    public void BankingProfileFixtures_ExposeExpectedRollShapes()
    {
        IReadOnlyDictionary<string, BankingProfileFixture> fixtures =
            BankingProfileFixtures.All().ToDictionary(fixture => fixture.Name);

        AssertAllSampledRollsNear(0.0, fixtures[BankingProfileFixtures.ConstantFlatName]);
        AssertAllSampledRollsNear(ToRadians(25.0), fixtures[BankingProfileFixtures.ConstantBankedName]);

        BankingProfile linearRamp = fixtures[BankingProfileFixtures.LinearRollRampName].Profile;
        AssertNear(ToRadians(22.5), BankingProfileSampler.SampleRollRadians(linearRamp, 50.0));

        BankingProfile smoothRamp = fixtures[BankingProfileFixtures.SmoothStepRollRampName].Profile;
        AssertNear(ToRadians(7.03125), BankingProfileSampler.SampleRollRadians(smoothRamp, 25.0));
        AssertNear(ToRadians(22.5), BankingProfileSampler.SampleRollRadians(smoothRamp, 50.0));

        BankingProfile rollHold = fixtures[BankingProfileFixtures.RollHoldWithMultipleKeysName].Profile;
        AssertNear(ToRadians(30.0), BankingProfileSampler.SampleRollRadians(rollHold, 30.0));
        AssertNear(ToRadians(30.0), BankingProfileSampler.SampleRollRadians(rollHold, 50.0));

        BankingProfile unwrapped = fixtures[BankingProfileFixtures.UnwrappedOver360RollName].Profile;
        AssertNear(ToRadians(225.0), BankingProfileSampler.SampleRollRadians(unwrapped, 50.0));
        Assert.True(BankingProfileSampler.SampleRollRadians(unwrapped, 100.0) > (2.0 * SystemMath.PI));
    }

    private static void AssertSummariesEqual(
        BankingProfileDiagnosticsSummary expected,
        BankingProfileDiagnosticsSummary actual)
    {
        Assert.Equal(expected.SampleCount, actual.SampleCount);
        AssertNear(expected.MinRollRadians, actual.MinRollRadians);
        AssertNear(expected.MaxRollRadians, actual.MaxRollRadians);
        AssertNear(expected.MinRollDegrees, actual.MinRollDegrees);
        AssertNear(expected.MaxRollDegrees, actual.MaxRollDegrees);
        AssertNear(expected.MaxAbsoluteRollSlopeRadPerMeter, actual.MaxAbsoluteRollSlopeRadPerMeter);
    }

    private static void AssertSamplesEqual(
        BankingProfileDiagnosticsSample expected,
        BankingProfileDiagnosticsSample actual)
    {
        Assert.Equal(expected.SampleIndex, actual.SampleIndex);
        AssertNear(expected.Distance, actual.Distance);
        AssertNear(expected.RollRadians, actual.RollRadians);
        AssertNear(expected.RollDegrees, actual.RollDegrees);
        Assert.Equal(expected.InterpolationMode, actual.InterpolationMode);
        Assert.Equal(expected.SourceKind, actual.SourceKind);
        Assert.Equal(expected.SourceStartKeyIndex, actual.SourceStartKeyIndex);
        Assert.Equal(expected.SourceEndKeyIndex, actual.SourceEndKeyIndex);
        AssertNear(expected.SourceStartDistance, actual.SourceStartDistance);
        AssertNear(expected.SourceEndDistance, actual.SourceEndDistance);
        Assert.Equal(
            expected.ApproximateRollSlopeRadPerMeter.HasValue,
            actual.ApproximateRollSlopeRadPerMeter.HasValue);

        if (expected.ApproximateRollSlopeRadPerMeter.HasValue)
        {
            AssertNear(
                expected.ApproximateRollSlopeRadPerMeter.Value,
                actual.ApproximateRollSlopeRadPerMeter!.Value);
        }
    }

    private static void AssertAllSampledRollsNear(double expectedRollRadians, BankingProfileFixture fixture)
    {
        for (int i = 0; i < fixture.SampleDistances.Count; i++)
        {
            double actual = BankingProfileSampler.SampleRollRadians(fixture.Profile, fixture.SampleDistances[i]);
            AssertNear(expectedRollRadians, actual);
        }
    }

    private static bool IsSupportedMode(BankingProfileInterpolationMode mode)
    {
        switch (mode)
        {
            case BankingProfileInterpolationMode.Constant:
            case BankingProfileInterpolationMode.Linear:
            case BankingProfileInterpolationMode.SmoothStep:
            case BankingProfileInterpolationMode.Quadratic:
            case BankingProfileInterpolationMode.Cubic:
            case BankingProfileInterpolationMode.Quartic:
            case BankingProfileInterpolationMode.Quintic:
            case BankingProfileInterpolationMode.Sinusoidal:
                return true;

            default:
                return false;
        }
    }

    private static double ToRadians(double degrees)
    {
        return degrees * SystemMath.PI / 180.0;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static void AssertNear(double expected, double actual)
    {
        Assert.InRange(SystemMath.Abs(expected - actual), 0.0, Tolerance);
    }
}
