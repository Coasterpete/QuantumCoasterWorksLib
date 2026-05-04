namespace Quantum.Track
{
    public sealed class StraightSegment : TrackSegment
    {
        public StraightSegment(double length, string? id = null, string? forceSegmentReference = null)
            : base(length, id, forceSegmentReference)
        {
        }
    }
}
