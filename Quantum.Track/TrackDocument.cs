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
    ///
    /// Documents are intentionally mutable during the current backend prototype.
    /// Evaluators read the current segment and section lists when an evaluation call
    /// starts; they do not take ownership of, or freeze, the document. Avoid mutating
    /// a document while it is being evaluated, and treat mutations as affecting later
    /// evaluations, including evaluations from an already-bound <see cref="TrackEvaluator"/>.
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
        /// <remarks>
        /// This list is mutable by design for authoring and prototype workflows.
        /// Mutating it changes the station-distance coordinate for future evaluation
        /// calls. The backend does not currently provide concurrent mutation safety.
        /// </remarks>
        public IList<TrackSegment> Segments { get; }

        /// <summary>
        /// Coaster-domain sections associated with the document.
        /// </summary>
        /// <remarks>
        /// This list is mutable by design. Section mutations are visible to future
        /// consumers that resolve force, metadata, or authoring information from the
        /// same document instance.
        /// </remarks>
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
