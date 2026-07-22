using System;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Immutable graph node containing one existing coaster section definition.
    /// </summary>
    public sealed class TrackAuthoringGraphNode
    {
        public TrackAuthoringGraphNode(TrackAuthoringSectionDefinition section)
        {
            Section = section ?? throw new ArgumentNullException(nameof(section));
            if (string.IsNullOrWhiteSpace(section.TypeId))
            {
                throw new ArgumentException(
                    "A graph section definition must provide a stable type discriminator.",
                    nameof(section));
            }
        }

        /// <summary>
        /// Stable graph identity shared by route operations, history, and persistence.
        /// </summary>
        public string Id => Section.Id;

        /// <summary>
        /// Immutable backend section definition owned by this node snapshot.
        /// </summary>
        public TrackAuthoringSectionDefinition Section { get; }

        public TrackAuthoringSectionFamily Family => Section.Family;

        public string TypeId => Section.TypeId;
    }
}
