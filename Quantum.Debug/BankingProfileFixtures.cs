using System;
using System.Collections.Generic;
using System.Linq;
using Quantum.Track;
using SystemMath = System.Math;

namespace Quantum.Debug
{
    public sealed class BankingProfileFixture
    {
        public BankingProfileFixture(
            string name,
            BankingProfile profile,
            IReadOnlyList<double> sampleDistances)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Fixture name cannot be empty.", nameof(name));
            }

            if (sampleDistances is null)
            {
                throw new ArgumentNullException(nameof(sampleDistances));
            }

            double[] distanceArray = sampleDistances.ToArray();
            if (distanceArray.Length == 0)
            {
                throw new ArgumentException("Fixture sample distances cannot be empty.", nameof(sampleDistances));
            }

            Name = name;
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            SampleDistances = Array.AsReadOnly(distanceArray);
        }

        public string Name { get; }

        public BankingProfile Profile { get; }

        public IReadOnlyList<double> SampleDistances { get; }

        public override string ToString()
        {
            return Name;
        }
    }

    public static class BankingProfileFixtures
    {
        public const string ConstantFlatName = "constant-flat";
        public const string ConstantBankedName = "constant-banked";
        public const string LinearRollRampName = "linear-roll-ramp";
        public const string SmoothStepRollRampName = "smoothstep-roll-ramp";
        public const string RollHoldWithMultipleKeysName = "roll-hold-with-multiple-keys";
        public const string UnwrappedOver360RollName = "unwrapped-over-360-roll";

        private const int DefaultSampleCount = 11;
        private const double ProfileLength = 100.0;
        private const double DegreesToRadians = SystemMath.PI / 180.0;

        public static IReadOnlyList<BankingProfileFixture> All()
        {
            return new[]
            {
                ConstantFlat(),
                ConstantBanked(),
                LinearRollRamp(),
                SmoothStepRollRamp(),
                RollHoldWithMultipleKeys(),
                UnwrappedOver360Roll()
            };
        }

        public static BankingProfileFixture DefaultDiagnostics()
        {
            return RollHoldWithMultipleKeys();
        }

        public static BankingProfileFixture ConstantFlat()
        {
            return CreateFixture(
                ConstantFlatName,
                new BankingProfileKey(0.0, 0.0, BankingProfileInterpolationMode.Constant),
                new BankingProfileKey(ProfileLength, 0.0, BankingProfileInterpolationMode.Constant));
        }

        public static BankingProfileFixture ConstantBanked()
        {
            double rollRadians = ToRadians(25.0);
            return CreateFixture(
                ConstantBankedName,
                new BankingProfileKey(0.0, rollRadians, BankingProfileInterpolationMode.Constant),
                new BankingProfileKey(ProfileLength, rollRadians, BankingProfileInterpolationMode.Constant));
        }

        public static BankingProfileFixture LinearRollRamp()
        {
            return CreateFixture(
                LinearRollRampName,
                new BankingProfileKey(0.0, 0.0, BankingProfileInterpolationMode.Linear),
                new BankingProfileKey(ProfileLength, ToRadians(45.0), BankingProfileInterpolationMode.Constant));
        }

        public static BankingProfileFixture SmoothStepRollRamp()
        {
            return CreateFixture(
                SmoothStepRollRampName,
                new BankingProfileKey(0.0, 0.0, BankingProfileInterpolationMode.SmoothStep),
                new BankingProfileKey(ProfileLength, ToRadians(45.0), BankingProfileInterpolationMode.Constant));
        }

        public static BankingProfileFixture RollHoldWithMultipleKeys()
        {
            return CreateFixture(
                RollHoldWithMultipleKeysName,
                new BankingProfileKey(0.0, 0.0, BankingProfileInterpolationMode.Linear),
                new BankingProfileKey(20.0, ToRadians(30.0), BankingProfileInterpolationMode.Constant),
                new BankingProfileKey(50.0, ToRadians(30.0), BankingProfileInterpolationMode.SmoothStep),
                new BankingProfileKey(80.0, ToRadians(-15.0), BankingProfileInterpolationMode.Linear),
                new BankingProfileKey(ProfileLength, ToRadians(20.0), BankingProfileInterpolationMode.Constant));
        }

        public static BankingProfileFixture UnwrappedOver360Roll()
        {
            return CreateFixture(
                UnwrappedOver360RollName,
                new BankingProfileKey(0.0, 0.0, BankingProfileInterpolationMode.Linear),
                new BankingProfileKey(ProfileLength, ToRadians(450.0), BankingProfileInterpolationMode.Constant));
        }

        private static BankingProfileFixture CreateFixture(
            string name,
            params BankingProfileKey[] keys)
        {
            return new BankingProfileFixture(
                name,
                new BankingProfile(keys),
                BuildUniformDistances(ProfileLength, DefaultSampleCount));
        }

        private static double[] BuildUniformDistances(double totalLength, int sampleCount)
        {
            if (!IsFinite(totalLength) || totalLength <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(totalLength),
                    totalLength,
                    "Fixture length must be finite and greater than zero.");
            }

            if (sampleCount < 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleCount),
                    sampleCount,
                    "Fixture sample count must be at least two.");
            }

            var distances = new double[sampleCount];
            double interval = totalLength / (sampleCount - 1);

            for (int i = 0; i < sampleCount; i++)
            {
                distances[i] = i * interval;
            }

            distances[sampleCount - 1] = totalLength;
            return distances;
        }

        private static double ToRadians(double degrees)
        {
            return degrees * DegreesToRadians;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
