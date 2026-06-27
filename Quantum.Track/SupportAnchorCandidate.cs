namespace Quantum.Track
{
    /// <summary>
    /// One generated support anchor candidate at a canonical station distance.
    /// </summary>
    public sealed class SupportAnchorCandidate
    {
        internal SupportAnchorCandidate(int index, double distance)
        {
            Index = index;
            Distance = distance;
        }

        public int Index { get; }

        public double Distance { get; }
    }
}
