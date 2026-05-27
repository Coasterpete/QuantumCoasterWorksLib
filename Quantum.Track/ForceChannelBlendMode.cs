namespace Quantum.Track
{
    /// <summary>
    /// Deterministic combination mode for non-empty plural force channel lists.
    /// </summary>
    public enum ForceChannelBlendMode
    {
        /// <summary>
        /// Sum every channel value in list order.
        /// </summary>
        Sum = 0,

        /// <summary>
        /// Use the largest evaluated channel value.
        /// </summary>
        Max = 1,

        /// <summary>
        /// Use the last channel in the list.
        /// </summary>
        Override = 2
    }
}
