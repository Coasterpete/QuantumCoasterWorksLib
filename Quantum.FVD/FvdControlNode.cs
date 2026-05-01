using Quantum.Math;

namespace Quantum.FVD
{
    /// <summary>
    /// FVD control node keyed by normalized station U.
    /// </summary>
    public readonly struct FvdControlNode
    {
        public double U { get; }

        public Vector3d Position { get; }

        public double Weight { get; }

        public FvdControlNode(double u, Vector3d position, double weight)
        {
            U = u;
            Position = position;
            Weight = weight;
        }
    }
}
