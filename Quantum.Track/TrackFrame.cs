using System;
using System.Numerics;
using Quantum.Math;

namespace Quantum.Track
{
    /// <summary>
    /// Public coaster-domain pose basis sampled from a track centerline.
    /// </summary>
    /// <remarks>
    /// <see cref="TrackFrame"/> is the stable backend contract for track-space
    /// orientation. It is intentionally defined in <c>Quantum.Track</c> so
    /// downstream train, export, Unity, and debug code can depend on coaster
    /// concepts instead of spline implementation types.
    /// </remarks>
    public readonly struct TrackFrame
    {
        /// <summary>
        /// Creates a track frame at the provided clamped global station distance.
        /// </summary>
        /// <param name="distance">Clamped global station distance associated with this frame.</param>
        /// <param name="position">Centerline position at the sampled station.</param>
        /// <param name="tangent">Forward axis. Producer output is expected to be unit length.</param>
        /// <param name="normal">Up axis. Producer output is expected to be unit length and orthogonal to <paramref name="tangent"/>.</param>
        /// <param name="binormal">Right/lateral axis. Producer output is expected to complete the orthonormal basis.</param>
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

        /// <summary>
        /// Clamped global station distance associated with this frame.
        /// </summary>
        public double Distance { get; }

        /// <summary>
        /// Centerline position in backend track-space coordinates.
        /// </summary>
        public Vector3d Position { get; }

        /// <summary>
        /// Forward axis of the sampled track frame.
        /// </summary>
        public Vector3d Tangent { get; }

        /// <summary>
        /// Up axis of the sampled track frame.
        /// </summary>
        public Vector3d Normal { get; }

        /// <summary>
        /// Right/lateral axis of the sampled track frame.
        /// </summary>
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
