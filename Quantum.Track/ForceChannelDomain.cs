namespace Quantum.Track
{
    /// <summary>
    /// Sampling domain for force section channel evaluation.
    /// </summary>
    public enum ForceChannelDomain
    {
        /// <summary>
        /// Channels are evaluated from resolved track distance within the section interval.
        /// </summary>
        Distance = 0,

        /// <summary>
        /// Channels are evaluated from elapsed time when the caller uses an elapsed-time
        /// sampling API; distance-only APIs preserve legacy distance-normalized behavior.
        /// </summary>
        Time = 1
    }
}
