using System.Collections.Generic;

namespace Quantum.Track
{
    public class TrackDocument
    {
        public TrackDocument(
            IEnumerable<TrackSegment>? segments = null,
            IEnumerable<TrackSection>? sections = null)
        {
            Segments = segments is null
                ? new List<TrackSegment>()
                : new List<TrackSegment>(segments);

            Sections = sections is null
                ? new List<TrackSection>()
                : new List<TrackSection>(sections);
        }

        public IList<TrackSegment> Segments { get; }

        public IList<TrackSection> Sections { get; }

        public double TotalLength
        {
            get
            {
                double totalLength = 0.0;

                for (int i = 0; i < Segments.Count; i++)
                {
                    TrackSegment segment = Segments[i];

                    if (segment is null)
                    {
                        throw new System.InvalidOperationException("TrackDocument contains a null segment entry.");
                    }

                    totalLength += segment.Length;
                }

                return totalLength;
            }
        }
    }
}
