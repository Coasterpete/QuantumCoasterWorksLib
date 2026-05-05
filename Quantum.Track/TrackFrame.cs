using System;
using System.Numerics;
using Quantum.Math;

namespace Quantum.Track
{
    /// <summary>
    /// Geometry frame representation for exporting track basis/position data.
    /// Tangent/Normal/Binormal are expected to already be unit-length and orthogonal.
    /// </summary>
    public readonly struct TrackFrame
    {
        public TrackFrame(Vector3d position, Vector3d tangent, Vector3d normal, Vector3d binormal)
        {
            ValidateFinite(position, nameof(position));
            ValidateFinite(tangent, nameof(tangent));
            ValidateFinite(normal, nameof(normal));
            ValidateFinite(binormal, nameof(binormal));

            Position = position;
            Tangent = tangent;
            Normal = normal;
            Binormal = binormal;
        }

        public Vector3d Position { get; }

        public Vector3d Tangent { get; }

        public Vector3d Normal { get; }

        public Vector3d Binormal { get; }

        /// <summary>
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
