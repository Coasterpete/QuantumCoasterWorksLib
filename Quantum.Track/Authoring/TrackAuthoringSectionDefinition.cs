using System;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Immutable typed definition owned by one route-authoring graph node.
    /// </summary>
    public abstract class TrackAuthoringSectionDefinition
    {
        protected TrackAuthoringSectionDefinition(
            string id,
            TrackAuthoringSectionFamily family)
        {
            if (family != TrackAuthoringSectionFamily.Geometry &&
                family != TrackAuthoringSectionFamily.Force)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(family),
                    family,
                    "Unsupported track authoring section family.");
            }

            Id = AuthoringValidation.RequireId(id);
            Family = family;
        }

        /// <summary>
        /// Stable route-node identity preserved across parameter edits and reordering.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// High-level section family used for compiler and editor catalog dispatch.
        /// </summary>
        public TrackAuthoringSectionFamily Family { get; }

        /// <summary>
        /// Stable backend section-type discriminator.
        /// </summary>
        public abstract string TypeId { get; }
    }
}
