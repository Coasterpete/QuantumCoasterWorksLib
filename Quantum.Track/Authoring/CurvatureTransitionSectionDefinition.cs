using System;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Authoring definition for a planar distance-domain curvature transition.
    /// </summary>
    public sealed class CurvatureTransitionSectionDefinition : GeometricSectionDefinition
    {
        public CurvatureTransitionSectionDefinition(
            string id,
            double length,
            double startCurvature,
            double endCurvature,
            CurvatureTransitionInterpolationMode interpolationMode =
                CurvatureTransitionInterpolationMode.Linear,
            double rollRadians = 0.0)
            : base(id, length, rollRadians)
        {
            StartCurvature = AuthoringValidation.RequireFinite(
                startCurvature,
                nameof(startCurvature),
                "Start curvature");
            EndCurvature = AuthoringValidation.RequireFinite(
                endCurvature,
                nameof(endCurvature),
                "End curvature");

            if (interpolationMode != CurvatureTransitionInterpolationMode.Linear)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(interpolationMode),
                    interpolationMode,
                    "Only linear distance-domain curvature interpolation is supported.");
            }

            double curvatureDelta = endCurvature - startCurvature;
            if (!AuthoringValidation.IsFinite(curvatureDelta))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(endCurvature),
                    endCurvature,
                    "Curvature range must be finite.");
            }

            double headingSweep = length * (startCurvature + (0.5 * curvatureDelta));
            if (!AuthoringValidation.IsFinite(headingSweep))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(endCurvature),
                    endCurvature,
                    "Transition curvature must produce a finite heading sweep.");
            }

            InterpolationMode = interpolationMode;
        }

        /// <summary>
        /// Signed curvature at the start of the section, in inverse station-distance units.
        /// </summary>
        public double StartCurvature { get; }

        /// <summary>
        /// Signed curvature at the end of the section, in inverse station-distance units.
        /// </summary>
        public double EndCurvature { get; }

        /// <summary>
        /// Distance-domain interpolation applied between the endpoint curvatures.
        /// </summary>
        public CurvatureTransitionInterpolationMode InterpolationMode { get; }

        public override string TypeId => TrackAuthoringSectionTypeIds.CurvatureTransition;
    }
}
