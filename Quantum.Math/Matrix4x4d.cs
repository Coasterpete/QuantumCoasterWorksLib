using System.Numerics;

namespace Quantum.Math
{
    /// <summary>
    /// Minimal double-precision 4x4 matrix.
    /// </summary>
    public readonly struct Matrix4x4d
    {
        public Matrix4x4d(
            double m11, double m12, double m13, double m14,
            double m21, double m22, double m23, double m24,
            double m31, double m32, double m33, double m34,
            double m41, double m42, double m43, double m44)
        {
            M11 = m11;
            M12 = m12;
            M13 = m13;
            M14 = m14;
            M21 = m21;
            M22 = m22;
            M23 = m23;
            M24 = m24;
            M31 = m31;
            M32 = m32;
            M33 = m33;
            M34 = m34;
            M41 = m41;
            M42 = m42;
            M43 = m43;
            M44 = m44;
        }

        public double M11 { get; }
        public double M12 { get; }
        public double M13 { get; }
        public double M14 { get; }

        public double M21 { get; }
        public double M22 { get; }
        public double M23 { get; }
        public double M24 { get; }

        public double M31 { get; }
        public double M32 { get; }
        public double M33 { get; }
        public double M34 { get; }

        public double M41 { get; }
        public double M42 { get; }
        public double M43 { get; }
        public double M44 { get; }

        public static Matrix4x4d FromMatrix4x4(Matrix4x4 matrix)
        {
            return new Matrix4x4d(
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44);
        }
    }
}
