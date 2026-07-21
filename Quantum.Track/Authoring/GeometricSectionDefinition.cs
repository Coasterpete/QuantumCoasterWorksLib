namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Base authoring definition for one ordered geometric track section.
    /// </summary>
    public abstract class GeometricSectionDefinition : TrackAuthoringSectionDefinition
    {
        protected GeometricSectionDefinition(
            string id,
            double length,
            double rollRadians)
            : base(id, TrackAuthoringSectionFamily.Geometry)
        {
            Length = AuthoringValidation.RequirePositiveFinite(length, nameof(length), "Section length");
            RollRadians = AuthoringValidation.RequireFinite(
                rollRadians,
                nameof(rollRadians),
                "Section roll");
        }

        /// <summary>
        /// Section length in station-distance units.
        /// </summary>
        public double Length { get; }

        /// <summary>
        /// Constant section roll around the centerline tangent, in radians.
        /// </summary>
        public double RollRadians { get; }
    }
}
