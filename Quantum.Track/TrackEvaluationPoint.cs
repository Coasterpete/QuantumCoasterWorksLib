namespace Quantum.Track
{
    public readonly struct TrackEvaluationPoint
    {
        public TrackEvaluationPoint(TrackSegment segment, double localT)
        {
            if (segment is null)
            {
                throw new System.ArgumentNullException(nameof(segment));
            }

            Segment = segment;
            LocalT = localT;
        }

        public TrackSegment Segment { get; }

        public double LocalT { get; }
    }
}
