using System.Numerics;

namespace Quantum.Track
{
    public readonly struct TrainCarTransform
    {
        public TrainCarTransform(int carIndex, double distance, TrackFrame frame, Matrix4x4 matrix)
        {
            CarIndex = carIndex;
            Distance = distance;
            Frame = frame;
            Matrix = matrix;
        }

        public int CarIndex { get; }

        public double Distance { get; }

        public TrackFrame Frame { get; }

        public Matrix4x4 Matrix { get; }
    }
}
