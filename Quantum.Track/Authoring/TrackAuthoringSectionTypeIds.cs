namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Stable backend discriminators for built-in route section definitions.
    /// </summary>
    /// <remarks>
    /// These values are backend identities, not UI labels or serialized package
    /// vocabulary. Versioned IO adapters map them to their own contract values.
    /// </remarks>
    public static class TrackAuthoringSectionTypeIds
    {
        public const string Straight = "geometry.straight";

        public const string ConstantCurvature = "geometry.constantCurvature";

        public const string CurvatureTransition = "geometry.curvatureTransition";

        public const string Spatial = "geometry.spatial";
    }
}
