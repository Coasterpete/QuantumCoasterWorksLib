using System.Collections.Generic;

namespace Quantum.Track
{
    public class TrackDocument
    {
        public TrackDocument()
        {
            Segments = new List<TrackSegment>();
        }

        public IList<TrackSegment> Segments { get; }
    }
}
