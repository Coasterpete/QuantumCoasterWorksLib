namespace Quantum.Track
{
    /// <summary>
    /// Canonical station-distance range where support anchor candidates should not be placed.
    /// </summary>
    public sealed class SupportAnchorExcludedRange
    {
        public SupportAnchorExcludedRange(double startDistance, double endDistance)
        {
            StartDistance = startDistance;
            EndDistance = endDistance;
        }

        public double StartDistance { get; }

        public double EndDistance { get; }
    }
}
