using System;
using Quantum.Math;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Validated unbanked construction frame for the start of authored track geometry.
    /// </summary>
    public sealed class TrackStartPose
    {
        private const double MinimumAxisMagnitude = 1e-9;
        private const double UnitLengthTolerance = 1e-9;
        private const double OrthogonalityTolerance = 1e-9;
        private const double HandednessTolerance = 1e-9;

        private static readonly TrackStartPose IdentityPose = new TrackStartPose(
            Vector3d.Zero,
            Vector3d.UnitX,
            Vector3d.UnitY,
            Vector3d.UnitZ);

        public TrackStartPose(
            Vector3d position,
            Vector3d tangent,
            Vector3d normal,
            Vector3d binormal)
        {
            ValidateFinite(position, nameof(position));
            ValidateBasisAxis(tangent, nameof(tangent));
            ValidateBasisAxis(normal, nameof(normal));
            ValidateBasisAxis(binormal, nameof(binormal));

            ValidateOrthogonal(tangent, normal, nameof(normal));
            ValidateOrthogonal(tangent, binormal, nameof(binormal));
            ValidateOrthogonal(normal, binormal, nameof(binormal));

            Vector3d expectedBinormal = Vector3d.Cross(tangent, normal);
            if (Vector3d.Dot(expectedBinormal, binormal) < 1.0 - HandednessTolerance)
            {
                throw new ArgumentException(
                    "Tangent, normal, and binormal must form a consistent right-handed basis.",
                    nameof(binormal));
            }

            Position = position;
            Tangent = tangent;
            Normal = normal;
            Binormal = binormal;
        }

        /// <summary>
        /// Origin with positive-X tangent, positive-Y normal, and positive-Z binormal.
        /// </summary>
        public static TrackStartPose Identity => IdentityPose;

        public Vector3d Position { get; }

        public Vector3d Tangent { get; }

        public Vector3d Normal { get; }

        public Vector3d Binormal { get; }

        private static void ValidateBasisAxis(Vector3d axis, string paramName)
        {
            ValidateFinite(axis, paramName);

            double length = axis.Length;
            if (length <= MinimumAxisMagnitude)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    "Basis axes must not have near-zero magnitude.");
            }

            if (System.Math.Abs(length - 1.0) > UnitLengthTolerance)
            {
                throw new ArgumentException(
                    "Basis axes must be unit length; TrackStartPose does not normalize inputs.",
                    paramName);
            }
        }

        private static void ValidateOrthogonal(
            Vector3d first,
            Vector3d second,
            string paramName)
        {
            if (System.Math.Abs(Vector3d.Dot(first, second)) > OrthogonalityTolerance)
            {
                throw new ArgumentException(
                    "Tangent, normal, and binormal must be mutually orthogonal.",
                    paramName);
            }
        }

        private static void ValidateFinite(Vector3d vector, string paramName)
        {
            if (!IsFinite(vector.X) || !IsFinite(vector.Y) || !IsFinite(vector.Z))
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    "Vector components must be finite.");
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
