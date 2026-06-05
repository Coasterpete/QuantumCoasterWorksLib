using System;
using System.Collections.Generic;

namespace Quantum.Math
{
    /// <summary>
    /// Engine-agnostic double-precision 3D vector used by the Quantum backend.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Vector3d"/> is intentionally a lightweight mutable value type:
    /// its components are public fields so coaster-domain code can pass and adjust
    /// points, directions, tangents, and frame bases without engine dependencies.
    /// </para>
    /// <para>
    /// This type is a backend math primitive, not a replacement for mature
    /// matrix, spline, interpolation, or geometry libraries.
    /// </para>
    /// </remarks>
    public struct Vector3d
    {
        /// <summary>
        /// Mutable X component.
        /// </summary>
        public double X;

        /// <summary>
        /// Mutable Y component.
        /// </summary>
        public double Y;

        /// <summary>
        /// Mutable Z component.
        /// </summary>
        public double Z;

        public Vector3d(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        // Common constants
        public static Vector3d Zero => new Vector3d(0.0, 0.0, 0.0);
        public static Vector3d UnitX => new Vector3d(1.0, 0.0, 0.0);
        public static Vector3d UnitY => new Vector3d(0.0, 1.0, 0.0);
        public static Vector3d UnitZ => new Vector3d(0.0, 0.0, 1.0);

        // Length
        public double Length => System.Math.Sqrt(X * X + Y * Y + Z * Z);
        public double LengthSquared => X * X + Y * Y + Z * Z;

        // Normalization
        public Vector3d Normalized()
        {
            double len = Length;
            if (len < 1e-9)
                return Zero;

            return new Vector3d(X / len, Y / len, Z / len);
        }

        // Static helpers
        public static double Dot(Vector3d a, Vector3d b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        public static Vector3d Cross(Vector3d a, Vector3d b)
        {
            return new Vector3d(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X
            );
        }

        // Operators
        public static Vector3d operator +(Vector3d a, Vector3d b)
            => new Vector3d(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Vector3d operator -(Vector3d a, Vector3d b)
            => new Vector3d(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Vector3d operator *(Vector3d v, double s)
            => new Vector3d(v.X * s, v.Y * s, v.Z * s);

        public static Vector3d operator *(double s, Vector3d v)
            => new Vector3d(v.X * s, v.Y * s, v.Z * s);

        public static Vector3d operator /(Vector3d v, double s)
            => new Vector3d(v.X / s, v.Y / s, v.Z / s);

        public override string ToString()
        {
            return $"({X}, {Y}, {Z})";
        }
    }
}
