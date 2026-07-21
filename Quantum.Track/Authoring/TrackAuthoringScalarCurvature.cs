using System;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Direction of signed scalar curvature in the established authoring frame.
    /// </summary>
    public enum TrackAuthoringCurvatureDirection
    {
        Negative = -1,
        Straight = 0,
        Positive = 1
    }

    /// <summary>
    /// Central signed scalar-curvature conventions for supported planar authoring sections.
    /// </summary>
    /// <remarks>
    /// Positive signed radius and curvature preserve the existing convention of turning
    /// toward positive Y from a positive-X heading. Spatial sections intentionally do not
    /// expose an approximated scalar curvature through this API.
    /// </remarks>
    public static class TrackAuthoringScalarCurvature
    {
        public static double FromSignedRadius(double signedRadius)
        {
            if (!AuthoringValidation.IsFinite(signedRadius) || signedRadius == 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(signedRadius),
                    signedRadius,
                    "Signed radius must be finite and non-zero.");
            }

            double curvature = 1.0 / signedRadius;
            if (!AuthoringValidation.IsFinite(curvature))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(signedRadius),
                    signedRadius,
                    "Signed radius must produce finite signed curvature.");
            }

            return curvature;
        }

        public static double ToSignedRadius(double signedCurvature)
        {
            if (!AuthoringValidation.IsFinite(signedCurvature) || signedCurvature == 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(signedCurvature),
                    signedCurvature,
                    "Signed curvature must be finite and non-zero to have a finite radius.");
            }

            double radius = 1.0 / signedCurvature;
            if (!AuthoringValidation.IsFinite(radius))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(signedCurvature),
                    signedCurvature,
                    "Signed curvature must produce a finite signed radius.");
            }

            return radius;
        }

        public static TrackAuthoringCurvatureDirection DirectionOf(double signedCurvature)
        {
            AuthoringValidation.RequireFinite(
                signedCurvature,
                nameof(signedCurvature),
                "Signed curvature");

            if (signedCurvature > 0.0)
            {
                return TrackAuthoringCurvatureDirection.Positive;
            }

            if (signedCurvature < 0.0)
            {
                return TrackAuthoringCurvatureDirection.Negative;
            }

            return TrackAuthoringCurvatureDirection.Straight;
        }

        public static double FromMagnitudeAndDirection(
            double magnitude,
            TrackAuthoringCurvatureDirection direction)
        {
            if (!AuthoringValidation.IsFinite(magnitude) || magnitude < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(magnitude),
                    magnitude,
                    "Curvature magnitude must be finite and non-negative.");
            }

            if (direction != TrackAuthoringCurvatureDirection.Negative &&
                direction != TrackAuthoringCurvatureDirection.Straight &&
                direction != TrackAuthoringCurvatureDirection.Positive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(direction),
                    direction,
                    "Unsupported scalar-curvature direction.");
            }

            if (direction == TrackAuthoringCurvatureDirection.Straight)
            {
                if (magnitude != 0.0)
                {
                    throw new ArgumentException(
                        "Straight curvature direction requires zero magnitude.",
                        nameof(magnitude));
                }

                return 0.0;
            }

            if (magnitude == 0.0)
            {
                throw new ArgumentException(
                    "Positive or negative curvature direction requires non-zero magnitude.",
                    nameof(magnitude));
            }

            return direction == TrackAuthoringCurvatureDirection.Positive
                ? magnitude
                : -magnitude;
        }

        public static bool TryGetStartCurvature(
            TrackAuthoringSectionDefinition section,
            out double signedCurvature)
        {
            if (section is null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            switch (section)
            {
                case StraightSectionDefinition _:
                    signedCurvature = 0.0;
                    return true;

                case ConstantCurvatureSectionDefinition constantCurvature:
                    signedCurvature = FromSignedRadius(constantCurvature.Radius);
                    return true;

                case CurvatureTransitionSectionDefinition transition:
                    signedCurvature = transition.StartCurvature;
                    return true;

                default:
                    signedCurvature = 0.0;
                    return false;
            }
        }

        public static bool TryGetEndCurvature(
            TrackAuthoringSectionDefinition section,
            out double signedCurvature)
        {
            if (section is null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            switch (section)
            {
                case StraightSectionDefinition _:
                    signedCurvature = 0.0;
                    return true;

                case ConstantCurvatureSectionDefinition constantCurvature:
                    signedCurvature = FromSignedRadius(constantCurvature.Radius);
                    return true;

                case CurvatureTransitionSectionDefinition transition:
                    signedCurvature = transition.EndCurvature;
                    return true;

                default:
                    signedCurvature = 0.0;
                    return false;
            }
        }
    }
}
