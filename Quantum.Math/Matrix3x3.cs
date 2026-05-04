namespace Quantum.Math
{
    /// <summary>
    /// Minimal double-precision 3x3 matrix for basis transforms.
    /// </summary>
    public readonly struct Matrix3x3
    {
        public double M00 { get; }
        public double M01 { get; }
        public double M02 { get; }

        public double M10 { get; }
        public double M11 { get; }
        public double M12 { get; }

        public double M20 { get; }
        public double M21 { get; }
        public double M22 { get; }

        public Matrix3x3(
            double m00, double m01, double m02,
            double m10, double m11, double m12,
            double m20, double m21, double m22)
        {
            M00 = m00;
            M01 = m01;
            M02 = m02;
            M10 = m10;
            M11 = m11;
            M12 = m12;
            M20 = m20;
            M21 = m21;
            M22 = m22;
        }

        public static Matrix3x3 Identity => new Matrix3x3(
            1.0, 0.0, 0.0,
            0.0, 1.0, 0.0,
            0.0, 0.0, 1.0);

        /// <summary>
        /// Builds a matrix whose columns are the provided basis vectors.
        /// </summary>
        public static Matrix3x3 FromBasis(Vector3d x, Vector3d y, Vector3d z)
        {
            return new Matrix3x3(
                x.X, y.X, z.X,
                x.Y, y.Y, z.Y,
                x.Z, y.Z, z.Z);
        }

        /// <summary>
        /// Multiplies this matrix by a vector (M * v).
        /// </summary>
        public Vector3d Multiply(Vector3d vector)
        {
            return new Vector3d(
                (M00 * vector.X) + (M01 * vector.Y) + (M02 * vector.Z),
                (M10 * vector.X) + (M11 * vector.Y) + (M12 * vector.Z),
                (M20 * vector.X) + (M21 * vector.Y) + (M22 * vector.Z));
        }

        public Matrix3x3 Transpose()
        {
            return new Matrix3x3(
                M00, M10, M20,
                M01, M11, M21,
                M02, M12, M22);
        }
    }
}
