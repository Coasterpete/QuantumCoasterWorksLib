using Quantum.Math;

namespace Quantum.Track
{
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

        public Matrix4x4d Matrix { get; }
    }
}
