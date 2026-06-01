using System;
using System.Collections.Generic;
using System.Linq;

namespace Quantum.Track
{
    /// <summary>
    /// Coaster-domain roll-angle profile sampled by station distance.
    /// </summary>
    public sealed class BankingProfile
    {
        private readonly IReadOnlyList<BankingProfileKey> _keys;

        public BankingProfile(IEnumerable<BankingProfileKey> keys)
        {
            if (keys is null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            BankingProfileKey[] keyArray = keys.ToArray();
            ValidateKeys(keyArray);
            _keys = Array.AsReadOnly(keyArray);
        }

        public IReadOnlyList<BankingProfileKey> Keys => _keys;

        private static void ValidateKeys(IReadOnlyList<BankingProfileKey> keys)
        {
            if (keys.Count == 0)
            {
                throw new ArgumentException("BankingProfile requires at least one key.", nameof(keys));
            }

            double previousDistance = double.NegativeInfinity;
            for (int i = 0; i < keys.Count; i++)
            {
                BankingProfileKey key = keys[i];

                if (!IsFinite(key.Distance))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(keys),
                        key.Distance,
                        $"BankingProfile key distance at index {i} must be finite.");
                }

                if (!IsFinite(key.RollRadians))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(keys),
                        key.RollRadians,
                        $"BankingProfile key roll at index {i} must be finite.");
                }

                if (!IsValidInterpolationMode(key.InterpolationToNext))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(keys),
                        key.InterpolationToNext,
                        $"BankingProfile key interpolation mode at index {i} is not supported.");
                }

                if (key.Distance <= previousDistance)
                {
                    throw new ArgumentException(
                        $"BankingProfile key distances must be strictly increasing. Key at index {i} is not greater than the previous key.",
                        nameof(keys));
                }

                previousDistance = key.Distance;
            }
        }

        internal static bool IsValidInterpolationMode(BankingProfileInterpolationMode mode)
        {
            switch (mode)
            {
                case BankingProfileInterpolationMode.Constant:
                case BankingProfileInterpolationMode.Linear:
                case BankingProfileInterpolationMode.SmoothStep:
                    return true;

                default:
                    return false;
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
