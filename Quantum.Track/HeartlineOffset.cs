using System;

namespace Quantum.Track
{
    /// <summary>
    /// Opt-in rider-reference offset from a sampled track frame.
    /// </summary>
    /// <remarks>
    /// Offsets are expressed in meters along sampled frame axes: positive normal
    /// is along the frame <see cref="TrackFrame.Normal"/> axis and positive
    /// lateral is along the frame <see cref="TrackFrame.Binormal"/> axis.
    /// </remarks>
    public readonly struct HeartlineOffset
    {
        public HeartlineOffset(double normalOffsetMeters, double lateralOffsetMeters = 0.0)
        {
            ThrowIfNonFinite(normalOffsetMeters, nameof(normalOffsetMeters));
            ThrowIfNonFinite(lateralOffsetMeters, nameof(lateralOffsetMeters));

            NormalOffsetMeters = normalOffsetMeters;
            LateralOffsetMeters = lateralOffsetMeters;
        }

        public static HeartlineOffset Zero => new HeartlineOffset(0.0, 0.0);

        public double NormalOffsetMeters { get; }

        public double LateralOffsetMeters { get; }

        private static void ThrowIfNonFinite(double value, string paramName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, "Heartline offset must be finite.");
            }
        }
    }
}
