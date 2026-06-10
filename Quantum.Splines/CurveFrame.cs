using Quantum.Math;

namespace Quantum.Splines
{
    /// <summary>
    /// Orthonormal frame sampled along a generic curve at arc-length coordinate <see cref="S"/>.
    /// </summary>
    public readonly struct CurveFrame : ITrackFrameBasis
    {
        public CurveFrame(
            double s,
            Vector3d position,
            Vector3d tangent,
            Vector3d normal,
            Vector3d binormal)
        {
            S = s;
            Position = position;
            Tangent = tangent;
            Normal = normal;
            Binormal = binormal;
        }

        public double S { get; }

        public Vector3d Position { get; }

        public Vector3d Tangent { get; }

        public Vector3d Normal { get; }

        public Vector3d Binormal { get; }

        public Vector3d Right => Binormal;

        public Vector3d Up => Normal;

        public override string ToString()
        {
            return $"s={S}, Pos={Position}, Tan={Tangent}, N={Normal}, B={Binormal}";
        }
    }
}
