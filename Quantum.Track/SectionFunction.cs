using System;
using System.Collections.Generic;

namespace Quantum.Track
{
    /// <summary>
    /// Normalized function for one section channel.
    /// </summary>
    public sealed class SectionFunction
    {
        private readonly Func<double, double>? _evaluateAt;
        private readonly List<SectionSample> _samples;
        private readonly IReadOnlyList<SectionSample> _samplesView;

        /// <summary>
        /// Initializes a sample-backed section function.
        /// </summary>
        public SectionFunction(SectionChannel channel, List<SectionSample> samples)
            : this(channel, samples, evaluateAt: null)
        {
        }

        internal SectionFunction(
            SectionChannel channel,
            IReadOnlyList<SectionSample> samples,
            Func<double, double>? evaluateAt)
        {
            if (samples is null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            Channel = channel;
            _samples = new List<SectionSample>(samples.Count);
            for (int i = 0; i < samples.Count; i++)
            {
                _samples.Add(samples[i]);
            }

            _samplesView = _samples.AsReadOnly();
            _evaluateAt = evaluateAt;
        }

        /// <summary>
        /// Channel evaluated by this function.
        /// </summary>
        public SectionChannel Channel { get; }

        /// <summary>
        /// Ordered sample points in the owning section domain.
        /// </summary>
        public IReadOnlyList<SectionSample> Samples => _samplesView;

        /// <summary>
        /// Evaluates the function at a coordinate in the owning section domain.
        /// </summary>
        public double EvaluateAt(double x)
        {
            if (!IsFinite(x))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(x),
                    x,
                    "Evaluation X must be finite.");
            }

            if (_evaluateAt != null)
            {
                double value = _evaluateAt(x);
                if (!IsFinite(value))
                {
                    throw new InvalidOperationException("Section function evaluation returned a non-finite value.");
                }

                return value;
            }

            if (_samples.Count == 0)
            {
                throw new InvalidOperationException("Cannot evaluate a section function with no samples.");
            }

            SectionSample first = _samples[0];
            if (x <= first.X)
            {
                return first.Value;
            }

            int lastIndex = _samples.Count - 1;
            SectionSample last = _samples[lastIndex];
            if (x >= last.X)
            {
                return last.Value;
            }

            for (int i = 1; i <= lastIndex; i++)
            {
                SectionSample right = _samples[i];
                if (x <= right.X)
                {
                    SectionSample left = _samples[i - 1];
                    double span = right.X - left.X;

                    if (span <= 0.0)
                    {
                        throw new InvalidOperationException(
                            "Section function samples must be strictly increasing by X.");
                    }

                    double t = (x - left.X) / span;
                    return Lerp(left.Value, right.Value, t);
                }
            }

            return last.Value;
        }

        private static double Lerp(double start, double end, double t)
        {
            return start + ((end - start) * t);
        }

        private static bool IsFinite(double value)
        {
            return !(double.IsNaN(value) || double.IsInfinity(value));
        }
    }
}
