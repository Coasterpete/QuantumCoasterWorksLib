using System;
using System.Numerics;
using Quantum.Math;

namespace Quantum.Track
{
    /// <summary>
    /// Opt-in heartline/rider-reference frame sampled in centerline station distance.
    /// </summary>
    /// <remarks>
    /// <see cref="Distance"/> remains the centerline station distance. The basis
    /// axes are inherited from the sampled source frame. In particular,
    /// <see cref="Tangent"/> is the sampled source-frame tangent, not the
    /// mathematical derivative of the offset curve when curvature or banking
    /// changes.
    /// </remarks>
    public readonly struct HeartlineFrame : ITrackFrameBasis
    {
        public HeartlineFrame(
            double distance,
            Vector3d centerlinePosition,
            Vector3d position,
            Vector3d tangent,
            Vector3d normal,
            Vector3d binormal)
        {
            if (double.IsNaN(distance) || double.IsInfinity(distance))
            {
                throw new ArgumentOutOfRangeException(nameof(distance), distance, "Distance must be finite.");
            }

            ValidateFinite(centerlinePosition, nameof(centerlinePosition));
            ValidateFinite(position, nameof(position));
            ValidateFinite(tangent, nameof(tangent));
            ValidateFinite(normal, nameof(normal));
            ValidateFinite(binormal, nameof(binormal));

            Distance = distance;
            CenterlinePosition = centerlinePosition;
            Position = position;
            Tangent = tangent;
            Normal = normal;
            Binormal = binormal;
        }

        /// <summary>
        /// Clamped centerline station distance associated with this sample.
        /// </summary>
        public double Distance { get; }

        /// <summary>
        /// Sampled centerline position before applying heartline offsets.
        /// </summary>
        public Vector3d CenterlinePosition { get; }

        /// <summary>
        /// Heartline/rider-reference position after applying normal and lateral offsets.
        /// </summary>
        public Vector3d Position { get; }

        /// <summary>
        /// Sampled source-frame tangent, not the mathematical derivative of the
        /// offset curve when curvature or banking changes.
        /// </summary>
        public Vector3d Tangent { get; }

        /// <summary>
        /// Normal axis inherited from the sampled source frame.
        /// </summary>
        public Vector3d Normal { get; }

        /// <summary>
        /// Binormal/lateral axis inherited from the sampled source frame.
        /// </summary>
        public Vector3d Binormal { get; }

        /// <summary>
        /// Canonical conversion from <see cref="HeartlineFrame"/> to <see cref="Matrix4x4"/>.
        /// Builds a matrix whose first three columns are Tangent/Normal/Binormal
        /// and whose fourth column is Position, matching <see cref="TrackFrame"/>.
        /// </summary>
        public Matrix4x4 ToMatrix4x4()
        {
            return TrackFrame.CreateFromFrame(Position, Tangent, Normal, Binormal);
        }

        private static void ValidateFinite(Vector3d vector, string paramName)
        {
            if (double.IsNaN(vector.X) || double.IsNaN(vector.Y) || double.IsNaN(vector.Z) ||
                double.IsInfinity(vector.X) || double.IsInfinity(vector.Y) || double.IsInfinity(vector.Z))
            {
                throw new ArgumentOutOfRangeException(paramName, "Vector must contain finite components.");
            }
        }
    }
}
