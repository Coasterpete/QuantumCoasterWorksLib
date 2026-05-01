using System;
using System.Collections.Generic;

namespace Quantum.FVD
{
    public sealed class FvdSectionDefinition
    {
        private readonly List<FvdSectionFunction> _functions;

        public FvdSectionKind Kind { get; }

        public FvdFunctionDomain Domain { get; }

        public double StartX { get; }

        public double EndX { get; }

        public IReadOnlyList<FvdSectionFunction> Functions => _functions;

        public FvdSectionDefinition(
            FvdSectionKind kind,
            FvdFunctionDomain domain,
            double startX,
            double endX,
            List<FvdSectionFunction> functions)
        {
            if (functions == null)
                throw new ArgumentNullException(nameof(functions));

            if (!IsFinite(startX))
                throw new ArgumentOutOfRangeException(nameof(startX), "StartX must be finite.");

            if (!IsFinite(endX))
                throw new ArgumentOutOfRangeException(nameof(endX), "EndX must be finite.");

            if (startX >= endX)
                throw new ArgumentException("StartX must be strictly less than EndX.");

            Kind = kind;
            Domain = domain;
            StartX = startX;
            EndX = endX;
            _functions = new List<FvdSectionFunction>(functions.Count);

            for (int i = 0; i < functions.Count; i++)
            {
                FvdSectionFunction function = functions[i] ?? throw new ArgumentException(
                    $"Section function at index {i} cannot be null.",
                    nameof(functions));

                ValidateChannelForKind(kind, function.Channel);
                ValidateSamples(function.Samples, startX, endX, i);
                _functions.Add(function);
            }
        }

        private static void ValidateChannelForKind(FvdSectionKind kind, FvdSectionChannel channel)
        {
            bool valid = kind switch
            {
                FvdSectionKind.Force => channel == FvdSectionChannel.NormalG
                                        || channel == FvdSectionChannel.LateralG
                                        || channel == FvdSectionChannel.RollRateDegPerSec,
                FvdSectionKind.Geometry => channel == FvdSectionChannel.Curvature
                                           || channel == FvdSectionChannel.RollAngleDeg,
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
            IReadOnlyList<FvdSectionSample> samples,
            double startX,
            double endX,
            int functionIndex)
        {
            if (samples == null)
            {
                throw new ArgumentException(
                    $"Samples for function index {functionIndex} cannot be null.",
                    nameof(samples));
            }

            double previousX = double.NegativeInfinity;
            bool hasPrevious = false;

            for (int i = 0; i < samples.Count; i++)
            {
                FvdSectionSample sample = samples[i];

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
