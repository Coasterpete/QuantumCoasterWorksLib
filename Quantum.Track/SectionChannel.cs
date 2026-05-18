namespace Quantum.Track
{
    /// <summary>
    /// Channels carried by normalized section definitions.
    /// </summary>
    public enum SectionChannel
    {
        NormalG = 0,
        LateralG = 1,
        LongitudinalG = 2,
        /// <summary>
        /// Roll-rate target in degrees per second.
        /// </summary>
        RollRateDegPerSec = 3,
        Curvature = 4,
        /// <summary>
        /// Geometric roll angle in radians, matching current GeometricSection.Roll
        /// and TrackSegment.RollRadians usage.
        /// </summary>
        Roll = 5
    }
}
