using System.Numerics;

namespace Quantum.Track
{
    /// <summary>
    /// Transform data for a single train car body.
    /// </summary>
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

        /// <summary>
        /// Body matrix stored as <see cref="Matrix4x4"/> (single precision).
        /// Current precision policy is mixed: bogie, wheel, and articulated matrices remain
        /// <c>Matrix4x4d</c>.
        /// </summary>
        public Matrix4x4 Matrix { get; }
    }
}
