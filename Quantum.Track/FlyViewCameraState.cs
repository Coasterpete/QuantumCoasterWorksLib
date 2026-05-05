using Quantum.Math;

namespace Quantum.Track
{
    /// <summary>
    /// Data-only state for constructing a fly-view camera frame.
    /// </summary>
    public readonly struct FlyViewCameraState
    {
        public FlyViewCameraState(
            Vector3d position,
            double yawRadians,
            double pitchRadians,
            double rollRadians = 0.0)
        {
            Position = position;
            YawRadians = yawRadians;
            PitchRadians = pitchRadians;
            RollRadians = rollRadians;
        }

        public Vector3d Position { get; }

        public double YawRadians { get; }

        public double PitchRadians { get; }

        public double RollRadians { get; }
    }
}
