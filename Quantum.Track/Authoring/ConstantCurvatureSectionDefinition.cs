using System;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Authoring definition for a planar constant-curvature arc.
    /// </summary>
    /// <remarks>
    /// Radius is signed: positive values turn toward positive Y from the initial
    /// positive-X heading, while negative values turn toward negative Y.
    /// </remarks>
    public sealed class ConstantCurvatureSectionDefinition : GeometricSectionDefinition
    {
        public ConstantCurvatureSectionDefinition(
            string id,
            double length,
            double radius,
            double rollRadians = 0.0)
            : base(id, length, rollRadians)
        {
            double curvature = TrackAuthoringScalarCurvature.FromSignedRadius(radius);
            double sweepRadians = length * curvature;

            if (!AuthoringValidation.IsFinite(sweepRadians))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(radius),
                    radius,
                    "Section radius must produce finite curvature and sweep values.");
            }

            Radius = radius;
        }

        /// <summary>
        /// Signed constant radius in station-distance units.
        /// </summary>
        public double Radius { get; }

        /// <summary>
        /// Alias that makes the signed-radius convention explicit.
        /// </summary>
        public double SignedRadius => Radius;

        public override string TypeId => TrackAuthoringSectionTypeIds.ConstantCurvature;
    }
}
