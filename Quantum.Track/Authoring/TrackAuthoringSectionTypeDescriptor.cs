using System;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Engine-agnostic identity for one section type available to authoring tools.
    /// </summary>
    public sealed class TrackAuthoringSectionTypeDescriptor
    {
        public TrackAuthoringSectionTypeDescriptor(
            string typeId,
            TrackAuthoringSectionFamily family)
        {
            TypeId = string.IsNullOrWhiteSpace(typeId)
                ? throw new ArgumentException(
                    "A section type discriminator is required.",
                    nameof(typeId))
                : typeId;
            if (family != TrackAuthoringSectionFamily.Geometry &&
                family != TrackAuthoringSectionFamily.Force)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(family),
                    family,
                    "Unsupported track authoring section family.");
            }

            Family = family;
        }

        public string TypeId { get; }

        public TrackAuthoringSectionFamily Family { get; }
    }
}
