namespace Quantum.Track
{
    public sealed class CurvedSegment : TrackSegment
    {
        public CurvedSegment(double length, string? id = null, string? forceSegmentReference = null)
            : base(length, id, forceSegmentReference)
        {
        }
    }
}
