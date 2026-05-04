namespace Quantum.Track
{
    public abstract class TrackSegment
    {
        protected TrackSegment(double length, string? id = null, string? forceSegmentReference = null)
        {
            Length = length;
            Id = id;
            ForceSegmentReference = forceSegmentReference;
        }

        public double Length { get; }

        public string? Id { get; }

        public string? ForceSegmentReference { get; }
    }
}
