namespace Quantum.Track
{
    /// <summary>
    /// Channels carried by normalized section definitions.
    /// </summary>
    public enum SectionChannel
    {
        /// <summary>
        /// Normal force target in G.
        /// </summary>
        NormalG = 0,

        /// <summary>
        /// Lateral force target in G.
        /// </summary>
        LateralG = 1,

        /// <summary>
        /// Longitudinal force target in G.
        /// </summary>
        LongitudinalG = 2,

        /// <summary>
        /// Roll-rate target in degrees per second.
        /// </summary>
        RollRateDegPerSec = 3,

        /// <summary>
        /// Geometric curvature target.
        /// </summary>
        Curvature = 4,

        /// <summary>
        /// Geometric roll angle in radians, matching current GeometricSection.Roll
        /// and TrackSegment.RollRadians usage.
        /// </summary>
        Roll = 5
    }
}
