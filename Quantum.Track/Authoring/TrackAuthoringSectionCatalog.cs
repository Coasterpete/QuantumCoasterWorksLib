using System;
using System.Collections.Generic;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Built-in backend section types that currently have authoring definitions.
    /// </summary>
    public static class TrackAuthoringSectionCatalog
    {
        private static readonly IReadOnlyList<TrackAuthoringSectionTypeDescriptor> TypesValue =
            Array.AsReadOnly(new[]
            {
                Geometry(TrackAuthoringSectionTypeIds.Straight),
                Geometry(TrackAuthoringSectionTypeIds.ConstantCurvature),
                Geometry(TrackAuthoringSectionTypeIds.CurvatureTransition),
                Geometry(TrackAuthoringSectionTypeIds.Spatial)
            });

        public static IReadOnlyList<TrackAuthoringSectionTypeDescriptor> Types => TypesValue;

        private static TrackAuthoringSectionTypeDescriptor Geometry(string typeId)
        {
            return new TrackAuthoringSectionTypeDescriptor(
                typeId,
                TrackAuthoringSectionFamily.Geometry);
        }
    }
}
