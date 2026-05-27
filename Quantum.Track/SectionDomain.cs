namespace Quantum.Track
{
    /// <summary>
    /// Domain used by normalized section definitions.
    /// </summary>
    /// <remarks>
    /// Distance-domain definitions are currently the normalized evaluator's runtime path.
    /// Time-domain definitions can be represented and overlap-checked independently;
    /// elapsed-time force sampling is available through explicit force-target APIs rather
    /// than the distance-only normalized evaluator.
    /// </remarks>
    public enum SectionDomain
    {
        /// <summary>
        /// Section coordinates are resolved track distance.
        /// </summary>
        Distance = 0,

        /// <summary>
        /// Section coordinates are elapsed time.
        /// </summary>
        Time = 1
    }
}
