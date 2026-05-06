using Quantum.Math;

namespace Quantum.Track
{
    /// <summary>
    /// Transform data for a bogie attached to a train car body.
    /// </summary>
    public readonly struct BogieTransform
    {
        public BogieTransform(int carIndex, int bogieIndex, double distance, TrackFrame frame, Matrix4x4d matrix)
        {
            CarIndex = carIndex;
            BogieIndex = bogieIndex;
            Distance = distance;
            Frame = frame;
            Matrix = matrix;
        }

        public int CarIndex { get; }

        public int BogieIndex { get; }

        public double Distance { get; }

        public TrackFrame Frame { get; }

        /// <summary>
        /// Bogie matrix stored as <see cref="Matrix4x4d"/> (double precision).
        /// </summary>
        public Matrix4x4d Matrix { get; }
    }
}
