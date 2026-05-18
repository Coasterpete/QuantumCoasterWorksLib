using System;
using System.Collections.Generic;
using GShark.Geometry;
using Quantum.Math;

namespace Quantum.Splines
{
    /// <summary>
    /// Conversion helpers between Quantum and G-Shark vector/point primitives.
    /// </summary>
    public static class GSharkVector3dConversions
    {
        public static Point3 ToGSharkPoint3(this Vector3d value)
        {
            return new Point3(value.X, value.Y, value.Z);
        }

        public static Vector3 ToGSharkVector3(this Vector3d value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }

        public static Vector3d ToQuantumVector3d(this Point3 value)
        {
            return new Vector3d(value.X, value.Y, value.Z);
        }

        public static Vector3d ToQuantumVector3d(this Vector3 value)
        {
            return new Vector3d(value.X, value.Y, value.Z);
        }

        public static List<Point3> ToGSharkPoint3List(IReadOnlyList<Vector3d> points)
        {
            if (points == null)
                throw new ArgumentNullException(nameof(points));

            var converted = new List<Point3>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                converted.Add(points[i].ToGSharkPoint3());
            }

            return converted;
        }
    }
}
