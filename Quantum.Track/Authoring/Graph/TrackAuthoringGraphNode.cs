using System;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Immutable graph node containing one existing coaster section definition.
    /// </summary>
    public sealed class TrackAuthoringGraphNode
    {
        public TrackAuthoringGraphNode(GeometricSectionDefinition section)
        {
            Section = section ?? throw new ArgumentNullException(nameof(section));
        }

        /// <summary>
        /// Stable graph identity. M157 deliberately reuses the existing section ID.
        /// </summary>
        public string Id => Section.Id;

        /// <summary>
        /// Immutable backend section definition owned by this node snapshot.
        /// </summary>
        public GeometricSectionDefinition Section { get; }
    }
}
