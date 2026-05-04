using System.Collections.Generic;

namespace Quantum.Track
{
    public class TrackDocument
    {
        public TrackDocument(IEnumerable<TrackSegment>? segments = null)
        {
            Segments = segments is null
                ? new List<TrackSegment>()
                : new List<TrackSegment>(segments);
        }

        public IList<TrackSegment> Segments { get; }
    }
}
