using System;
using System.Collections.Generic;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Authored absolute roll values over the track station-distance domain.
    /// </summary>
    public sealed class TrackBankingDefinition
    {
        private readonly IReadOnlyList<BankingProfileKey> _keys;

        public TrackBankingDefinition(IEnumerable<BankingProfileKey> keys)
        {
            if (keys is null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            var copiedKeys = new List<BankingProfileKey>(keys);
            if (copiedKeys.Count < 2)
            {
                throw new ArgumentException(
                    "Authored banking requires at least two keys.",
                    nameof(keys));
            }

            _keys = new BankingProfile(copiedKeys).Keys;
        }

        /// <summary>
        /// Ordered authored banking keys. The input sequence is copied at construction.
        /// </summary>
        public IReadOnlyList<BankingProfileKey> Keys => _keys;
    }
}
