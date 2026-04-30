using Quantum.Math;

namespace Quantum.Splines
{
    /// <summary>
    /// Orthonormal frame sampled along a track.
    /// </summary>
    public readonly struct TrackFrame
    {
        public double S { get; }

        public Vector3d Position { get; }

        public Vector3d Tangent { get; }

        public Vector3d Right { get; }

        public Vector3d Up { get; }

        public TrackFrame(double s, Vector3d position, Vector3d tangent, Vector3d right, Vector3d up)
        {
            S = s;
            Position = position;
            Tangent = tangent;
            Right = right;
            Up = up;
        }

        public override string ToString()
        {
            return $"s={S}, Pos={Position}, Tan={Tangent}, Right={Right}, Up={Up}";
        }
    }
}
