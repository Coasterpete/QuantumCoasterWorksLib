using System;
using System.Collections.Generic;

namespace Quantum.FVD
{
    public sealed class FvdSectionFunction
    {
        private readonly List<FvdSectionSample> _samples;

        public FvdSectionChannel Channel { get; }

        public IReadOnlyList<FvdSectionSample> Samples => _samples;

        public FvdSectionFunction(FvdSectionChannel channel, List<FvdSectionSample> samples)
        {
            if (samples == null)
                throw new ArgumentNullException(nameof(samples));

            Channel = channel;
            _samples = new List<FvdSectionSample>(samples);
        }
    }
}
