using System;
using System.Numerics;
using Quantum.Math;

namespace Quantum.Track
{
    /// <summary>
    /// Camera transform representation derived from a track frame.
    /// </summary>
    public readonly struct CameraTransform
    {
        public CameraTransform(Matrix4x4 transform, Vector3d position, Vector3d forward, Vector3d up, Vector3d right)
        {
            ValidateFiniteMatrix(transform, nameof(transform));
            ValidateFinite(position, nameof(position));
            ValidateFinite(forward, nameof(forward));
            ValidateFinite(up, nameof(up));
            ValidateFinite(right, nameof(right));

            Transform = transform;
            Position = position;
            Forward = forward;
            Up = up;
            Right = right;
        }

        public Matrix4x4 Transform { get; }

        public Vector3d Position { get; }

        public Vector3d Forward { get; }

        public Vector3d Up { get; }

        public Vector3d Right { get; }

        private static void ValidateFiniteMatrix(Matrix4x4 matrix, string paramName)
        {
            if (!IsFinite(matrix.M11) || !IsFinite(matrix.M12) || !IsFinite(matrix.M13) || !IsFinite(matrix.M14) ||
                !IsFinite(matrix.M21) || !IsFinite(matrix.M22) || !IsFinite(matrix.M23) || !IsFinite(matrix.M24) ||
                !IsFinite(matrix.M31) || !IsFinite(matrix.M32) || !IsFinite(matrix.M33) || !IsFinite(matrix.M34) ||
                !IsFinite(matrix.M41) || !IsFinite(matrix.M42) || !IsFinite(matrix.M43) || !IsFinite(matrix.M44))
            {
                throw new ArgumentOutOfRangeException(paramName, "Matrix must contain finite components.");
            }
        }

        private static void ValidateFinite(Vector3d vector, string paramName)
        {
            if (!IsFinite(vector.X) || !IsFinite(vector.Y) || !IsFinite(vector.Z))
            {
                throw new ArgumentOutOfRangeException(paramName, "Vector must contain finite components.");
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
