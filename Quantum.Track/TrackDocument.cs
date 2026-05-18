using System.Collections.Generic;

namespace Quantum.Track
{
    /// <summary>
    /// Coaster track document used as the source of truth for centerline sampling.
    /// </summary>
    /// <remarks>
    /// Segment order defines the station-distance coordinate consumed by
    /// <see cref="TrackEvaluator"/>. Sections carry coaster-domain metadata and force
    /// inputs; spline/math details are support-layer implementation choices behind
    /// segment evaluation.
    /// </remarks>
    public class TrackDocument
    {
        /// <summary>
        /// Creates a track document from ordered segments and optional sections.
        /// </summary>
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

        /// <summary>
        /// Ordered centerline segments. Their lengths define station-distance sampling.
        /// </summary>
        public IList<TrackSegment> Segments { get; }

        /// <summary>
        /// Coaster-domain sections associated with the document.
        /// </summary>
        public IList<TrackSection> Sections { get; }

        /// <summary>
        /// Sum of segment lengths in station-distance units.
        /// </summary>
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
