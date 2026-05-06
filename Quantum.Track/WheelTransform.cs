using Quantum.Math;

namespace Quantum.Track
{
    /// <summary>
    /// Transform data for a wheel sampled from a bogie frame.
    /// </summary>
    public readonly struct WheelTransform
    {
        public WheelTransform(
            int carIndex,
            int bogieIndex,
            int wheelIndex,
            double localOffsetX,
            double localOffsetY,
            double localOffsetZ,
            TrackFrame frame,
            Matrix4x4d matrix)
        {
            CarIndex = carIndex;
            BogieIndex = bogieIndex;
            WheelIndex = wheelIndex;
            LocalOffsetX = localOffsetX;
            LocalOffsetY = localOffsetY;
            LocalOffsetZ = localOffsetZ;
            Frame = frame;
            Matrix = matrix;
        }

        public int CarIndex { get; }

        public int BogieIndex { get; }

        public int WheelIndex { get; }

        public double LocalOffsetX { get; }

        public double LocalOffsetY { get; }

        public double LocalOffsetZ { get; }

        public TrackFrame Frame { get; }

        /// <summary>
        /// Wheel matrix stored as <see cref="Matrix4x4d"/> (double precision).
        /// </summary>
        public Matrix4x4d Matrix { get; }
    }
}
