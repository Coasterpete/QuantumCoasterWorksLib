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

        public double EvaluateAt(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(x),
                    x,
                    "Evaluation X must be a finite value.");
            }

            if (_samples.Count == 0)
                throw new InvalidOperationException("Cannot evaluate a section function with no samples.");

            FvdSectionSample first = _samples[0];
            if (x <= first.X)
                return first.Value;

            int lastIndex = _samples.Count - 1;
            FvdSectionSample last = _samples[lastIndex];
            if (x >= last.X)
                return last.Value;

            for (int i = 1; i <= lastIndex; i++)
            {
                FvdSectionSample right = _samples[i];
                if (x <= right.X)
                {
                    FvdSectionSample left = _samples[i - 1];
                    double span = right.X - left.X;

                    if (span <= 0.0)
                    {
                        throw new InvalidOperationException(
                            "Section function samples must be strictly increasing by X.");
                    }

                    double t = (x - left.X) / span;
                    return left.Value + ((right.Value - left.Value) * t);
                }
            }

            return last.Value;
        }
    }
}
