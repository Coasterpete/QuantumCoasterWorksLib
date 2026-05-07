using System;
using System.Numerics;
using Quantum.Math;

namespace Quantum.Track
{
    /// <summary>
    /// Authoritative train pose basis for track-space transforms.
    /// Position plus Tangent/Normal/Binormal define the current frame contract,
    /// and Tangent/Normal/Binormal are expected to already be unit-length and orthogonal.
    /// </summary>
    public readonly struct TrackFrame
    {
        public TrackFrame(
            double distance,
            Vector3d position,
            Vector3d tangent,
            Vector3d normal,
            Vector3d binormal)
        {
            if (double.IsNaN(distance) || double.IsInfinity(distance))
            {
                throw new ArgumentOutOfRangeException(nameof(distance), "Distance must be finite.");
            }

            ValidateFinite(position, nameof(position));
            ValidateFinite(tangent, nameof(tangent));
            ValidateFinite(normal, nameof(normal));
            ValidateFinite(binormal, nameof(binormal));

            Distance = distance;
            Position = position;
            Tangent = tangent;
            Normal = normal;
            Binormal = binormal;
        }

        public TrackFrame(Vector3d position, Vector3d tangent, Vector3d normal, Vector3d binormal)
            : this(0.0, position, tangent, normal, binormal)
        {
        }

        public double Distance { get; }

        public Vector3d Position { get; }

        public Vector3d Tangent { get; }

        public Vector3d Normal { get; }

        public Vector3d Binormal { get; }

        /// <summary>
        /// Canonical conversion from <see cref="TrackFrame"/> to <see cref="Matrix4x4"/>.
        /// Builds a matrix whose first three columns are Tangent/Normal/Binormal
        /// and whose fourth column is Position (column-vector convention).
        /// </summary>
        public Matrix4x4 ToMatrix4x4()
        {
            return CreateFromFrame(Position, Tangent, Normal, Binormal);
        }

        /// <summary>
        /// Builds a matrix whose first three columns are the provided basis vectors
        /// and whose fourth column is the provided position (column-vector convention).
        /// </summary>
        public static Matrix4x4 CreateFromFrame(Vector3d position, Vector3d t, Vector3d n, Vector3d b)
        {
            ValidateFinite(position, nameof(position));
            ValidateFinite(t, nameof(t));
            ValidateFinite(n, nameof(n));
            ValidateFinite(b, nameof(b));

            return new Matrix4x4(
                (float)t.X, (float)n.X, (float)b.X, (float)position.X,
                (float)t.Y, (float)n.Y, (float)b.Y, (float)position.Y,
                (float)t.Z, (float)n.Z, (float)b.Z, (float)position.Z,
                0f, 0f, 0f, 1f);
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
