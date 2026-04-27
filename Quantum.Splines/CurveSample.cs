using Quantum.Math;

namespace Quantum.Splines
{
    /// <summary>
    /// Represents a sampled point on a parametric curve.
    /// </summary>
    public struct CurveSample
    {
        /// <summary>
        /// Curve parameter in [0, 1].
        /// </summary>
        public double T;

        /// <summary>
        /// Position on the curve.
        /// </summary>
        public Vector3d Position;

        /// <summary>
        /// Tangent direction at the sample point.
        /// Expected to be normalized.
        /// </summary>
        public Vector3d Tangent;

        public CurveSample(double t, Vector3d position, Vector3d tangent)
        {
            T = t;
            Position = position;
            Tangent = tangent;
        }

        public override string ToString()
        {
            return $"t={T}, Pos={Position}, Tan={Tangent}";
        }
    }
}
