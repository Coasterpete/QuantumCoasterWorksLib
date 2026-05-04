namespace Quantum.Track
{
    public readonly struct TrackPosition
    {
        public TrackPosition(int segmentIndex, double localT)
        {
            SegmentIndex = segmentIndex;
            LocalT = localT;
        }

        public int SegmentIndex { get; }

        public double LocalT { get; }
    }
}
