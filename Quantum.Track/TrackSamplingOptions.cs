using System;

namespace Quantum.Track
{
    /// <summary>
    /// Immutable sampling controls used when compiling reusable track runtime state.
    /// </summary>
    public sealed class TrackSamplingOptions
    {
        public const int DefaultArcLengthSamples = 100;
        public const double DefaultArcLengthTolerance = 1e-4;
        public const int DefaultTransportSamplesPerSegment = 100;

        private static readonly TrackSamplingOptions DefaultOptions = new TrackSamplingOptions(
            DefaultArcLengthSamples,
            DefaultArcLengthTolerance,
            DefaultTransportSamplesPerSegment);

        public TrackSamplingOptions()
            : this(
                DefaultArcLengthSamples,
                DefaultArcLengthTolerance,
                DefaultTransportSamplesPerSegment)
        {
        }

        public TrackSamplingOptions(
            int arcLengthSamples,
            double arcLengthTolerance,
            int transportSamplesPerSegment)
        {
            if (arcLengthSamples < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(arcLengthSamples),
                    arcLengthSamples,
                    "Arc-length sample count must be at least 1.");
            }

            if (!IsFinite(arcLengthTolerance) || arcLengthTolerance <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(arcLengthTolerance),
                    arcLengthTolerance,
                    "Arc-length tolerance must be finite and greater than zero.");
            }

            if (transportSamplesPerSegment < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(transportSamplesPerSegment),
                    transportSamplesPerSegment,
                    "Transport sample count per segment must be at least 1.");
            }

            ArcLengthSamples = arcLengthSamples;
            ArcLengthTolerance = arcLengthTolerance;
            TransportSamplesPerSegment = transportSamplesPerSegment;
        }

        public static TrackSamplingOptions Default => DefaultOptions;

        public int ArcLengthSamples { get; }

        public int ArcLengthSampleCount => ArcLengthSamples;

        public double ArcLengthTolerance { get; }

        public int TransportSamplesPerSegment { get; }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
