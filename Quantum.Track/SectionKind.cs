namespace Quantum.Track
{
    /// <summary>
    /// High-level normalized section family.
    /// </summary>
    public enum SectionKind
    {
        /// <summary>
        /// Force target channels such as normal, lateral, longitudinal, and roll rate.
        /// </summary>
        Force = 0,

        /// <summary>
        /// Geometry channels such as curvature and roll.
        /// </summary>
        Geometry = 1
    }
}
