using System.Collections.Generic;
using Quantum.Track.Authoring;

namespace Quantum.Track
{
    /// <summary>
    /// Mutable coaster track document used as the source of truth for centerline sampling.
    /// </summary>
    /// <remarks>
    /// Segment order defines the station-distance coordinate consumed by
    /// <see cref="TrackEvaluator"/>. Sections carry coaster-domain metadata and force
    /// inputs; spline/math details are support-layer implementation choices behind
    /// segment evaluation.
    ///
    /// Documents are intentionally mutable during authoring and the current backend
    /// prototype. The public lists are live authoring collections rather than
    /// immutable views or snapshots. A <see cref="TrackEvaluator"/> bound directly
    /// to a document reads the current segment list when each evaluation call starts,
    /// so later document mutations are observed by that evaluator.
    ///
    /// <see cref="CompiledTrackRuntime"/> provides the explicit sampling-snapshot
    /// boundary. It captures the current ordered segment list and compiled sampling
    /// state once; recompile a new runtime after authoring or document mutation.
    /// Avoid mutating a document while an evaluation call is in progress.
    /// </remarks>
    public class TrackDocument
    {
        /// <summary>
        /// Creates a track document by copying ordered segments and optional sections
        /// into mutable authoring lists.
        /// </summary>
        public TrackDocument(
            IEnumerable<TrackSegment>? segments = null,
            IEnumerable<TrackSection>? sections = null)
            : this(segments, sections, startPose: null)
        {
        }

        internal TrackDocument(
            IEnumerable<TrackSegment>? segments,
            IEnumerable<TrackSection>? sections,
            TrackStartPose? startPose)
        {
            Segments = segments is null
                ? new List<TrackSegment>()
                : new List<TrackSegment>(segments);

            Sections = sections is null
                ? new List<TrackSection>()
                : new List<TrackSection>(sections);

            StartPose = startPose;
        }

        /// <summary>
        /// Optional authored unbanked construction frame. Manually constructed
        /// documents retain null and use legacy canonical frame seeding.
        /// </summary>
        public TrackStartPose? StartPose { get; }

        /// <summary>
        /// Live ordered centerline segments whose lengths define station-distance sampling.
        /// </summary>
        /// <remarks>
        /// This list is mutable by design for authoring and prototype workflows.
        /// Callers may add, remove, replace, or reorder entries before evaluation.
        /// Mutating it changes <see cref="TotalLength"/> and the station-distance
        /// coordinate for future document-bound evaluation calls. Existing
        /// <see cref="CompiledTrackRuntime"/> instances remain snapshots and must be
        /// recompiled to observe the change. The backend does not currently provide
        /// concurrent mutation safety.
        /// </remarks>
        public IList<TrackSegment> Segments { get; }

        /// <summary>
        /// Live coaster-domain sections associated with the document.
        /// </summary>
        /// <remarks>
        /// This list is mutable by design for authoring and prototype workflows.
        /// Callers may add, remove, replace, or reorder entries before section-aware
        /// consumers read the document. Sections carry force, metadata, or authoring
        /// information; centerline distance sampling is defined by
        /// <see cref="Segments"/>.
        /// </remarks>
        public IList<TrackSection> Sections { get; }

        /// <summary>
        /// Sum of declared segment lengths in authoring station-distance units.
        /// </summary>
        /// <remarks>
        /// Sampling validates spline-backed declarations against measured geometric
        /// lengths and uses the measured values through <see cref="TrackEvaluator"/>.
        /// </remarks>
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
