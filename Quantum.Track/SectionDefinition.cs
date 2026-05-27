using System;
using System.Collections.Generic;

namespace Quantum.Track
{
    /// <summary>
    /// Normalized, resolved section interval with one function per channel.
    /// </summary>
    /// <remarks>
    /// A section definition is the engine-agnostic contract produced by shorthand section
    /// types. Intervals use half-open coverage <c>[StartX, EndX)</c> during evaluator
    /// lookup, with the final section endpoint handled by <see cref="NormalizedSectionEvaluator"/>.
    /// Functions must be unique by channel within a section.
    /// </remarks>
    public sealed class SectionDefinition
    {
        private readonly List<SectionFunction> _functions;
        private readonly IReadOnlyList<SectionFunction> _functionsView;

        /// <summary>
        /// Initializes a normalized section definition.
        /// </summary>
        public SectionDefinition(
            SectionKind kind,
            SectionDomain domain,
            double startX,
            double endX,
            List<SectionFunction> functions)
        {
            if (functions is null)
            {
                throw new ArgumentNullException(nameof(functions));
            }

            ValidateKind(kind);
            ValidateDomain(domain);

            if (!IsFinite(startX))
            {
                throw new ArgumentOutOfRangeException(nameof(startX), startX, "StartX must be finite.");
            }

            if (!IsFinite(endX))
            {
                throw new ArgumentOutOfRangeException(nameof(endX), endX, "EndX must be finite.");
            }

            if (startX >= endX)
            {
                throw new ArgumentException("StartX must be strictly less than EndX.");
            }

            Kind = kind;
            Domain = domain;
            StartX = startX;
            EndX = endX;

            _functions = new List<SectionFunction>(functions.Count);
            var seenChannels = new HashSet<SectionChannel>();

            for (int i = 0; i < functions.Count; i++)
            {
                SectionFunction function = functions[i] ?? throw new ArgumentException(
                    $"Section function at index {i} cannot be null.",
                    nameof(functions));

                ValidateChannelForKind(kind, function.Channel);
                if (!seenChannels.Add(function.Channel))
                {
                    throw new ArgumentException(
                        $"Duplicate channel '{function.Channel}' is not allowed within a single section.",
                        nameof(functions));
                }

                ValidateSamples(function.Samples, startX, endX, i);
                _functions.Add(function);
            }

            _functionsView = _functions.AsReadOnly();
        }

        /// <summary>
        /// Section family that determines which channels are valid.
        /// </summary>
        public SectionKind Kind { get; }

        /// <summary>
        /// Coordinate domain for <see cref="StartX"/>, <see cref="EndX"/>, and function samples.
        /// </summary>
        public SectionDomain Domain { get; }

        /// <summary>
        /// Inclusive start coordinate in the section domain.
        /// </summary>
        public double StartX { get; }

        /// <summary>
        /// Exclusive end coordinate during normal lookup. The last section endpoint can
        /// be included by evaluator boundary rules.
        /// </summary>
        public double EndX { get; }

        /// <summary>
        /// Channel functions carried by this section.
        /// </summary>
        public IReadOnlyList<SectionFunction> Functions => _functionsView;

        /// <summary>
        /// Evaluates a channel at the requested coordinate.
        /// </summary>
        public double EvaluateAt(SectionChannel channel, double x)
        {
            for (int i = 0; i < _functions.Count; i++)
            {
                SectionFunction function = _functions[i];
                if (function.Channel == channel)
                {
                    return function.EvaluateAt(x);
                }
            }

            throw new InvalidOperationException(
                $"Section does not contain a function for channel '{channel}'.");
        }

        /// <summary>
        /// Evaluates all channels at the requested coordinate in <see cref="SectionChannel"/> order.
        /// </summary>
        public IReadOnlyList<SectionChannelEvaluation> EvaluateAllAt(double x)
        {
            var evaluations = new List<SectionChannelEvaluation>(_functions.Count);
            var channelOrder = (SectionChannel[])Enum.GetValues(typeof(SectionChannel));

            for (int channelIndex = 0; channelIndex < channelOrder.Length; channelIndex++)
            {
                SectionChannel channel = channelOrder[channelIndex];

                for (int functionIndex = 0; functionIndex < _functions.Count; functionIndex++)
                {
                    SectionFunction function = _functions[functionIndex];
                    if (function.Channel != channel)
                    {
                        continue;
                    }

                    evaluations.Add(new SectionChannelEvaluation(channel, function.EvaluateAt(x)));
                }
            }

            return evaluations;
        }

        private static void ValidateKind(SectionKind kind)
        {
            if (kind != SectionKind.Force && kind != SectionKind.Geometry)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported section kind.");
            }
        }

        private static void ValidateDomain(SectionDomain domain)
        {
            if (domain != SectionDomain.Distance && domain != SectionDomain.Time)
            {
                throw new ArgumentOutOfRangeException(nameof(domain), domain, "Unsupported section domain.");
            }
        }

        private static void ValidateChannelForKind(SectionKind kind, SectionChannel channel)
        {
            bool valid = kind switch
            {
                SectionKind.Force => channel == SectionChannel.NormalG
                                     || channel == SectionChannel.LateralG
                                     || channel == SectionChannel.LongitudinalG
                                     || channel == SectionChannel.RollRateDegPerSec,
                SectionKind.Geometry => channel == SectionChannel.Curvature
                                        || channel == SectionChannel.Roll,
                _ => false
            };

            if (!valid)
            {
                throw new ArgumentException(
                    $"Channel '{channel}' is not valid for section kind '{kind}'.",
                    nameof(channel));
            }
        }

        private static void ValidateSamples(
            IReadOnlyList<SectionSample> samples,
            double startX,
            double endX,
            int functionIndex)
        {
            if (samples is null)
            {
                throw new ArgumentException(
                    $"Samples for function index {functionIndex} cannot be null.",
                    nameof(samples));
            }

            double previousX = double.NegativeInfinity;
            bool hasPrevious = false;

            for (int i = 0; i < samples.Count; i++)
            {
                SectionSample sample = samples[i];

                if (!IsFinite(sample.X))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(sample.X),
                        $"Sample X at function index {functionIndex}, sample index {i} must be finite.");
                }

                if (sample.X < startX || sample.X > endX)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(sample.X),
                        $"Sample X at function index {functionIndex}, sample index {i} must be within [StartX, EndX].");
                }

                if (!IsFinite(sample.Value))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(sample.Value),
                        $"Sample Value at function index {functionIndex}, sample index {i} must be finite.");
                }

                if (hasPrevious && sample.X <= previousX)
                {
                    throw new ArgumentException(
                        $"Samples for function index {functionIndex} must be strictly increasing by X.",
                        nameof(samples));
                }

                previousX = sample.X;
                hasPrevious = true;
            }
        }

        private static bool IsFinite(double value)
        {
            return !(double.IsNaN(value) || double.IsInfinity(value));
        }
    }
}
