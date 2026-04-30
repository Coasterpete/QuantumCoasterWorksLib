using Quantum.Math;

namespace Quantum.Splines
{
    /// <summary>
    /// Represents a sample on an arc-length parameterized curve.
    /// </summary>
    public readonly struct ArcLengthSample
    {
        public double S { get; }

        public Vector3d Position { get; }

        public Vector3d Tangent { get; }

        public ArcLengthSample(double s, Vector3d position, Vector3d tangent)
        {
            S = s;
            Position = position;
            Tangent = tangent;
        }

        public override string ToString()
        {
            return $"s={S}, Pos={Position}, Tan={Tangent}";
        }
    }
}
