using Quantum.Math;

namespace Quantum.Track
{
    /// <summary>
    /// Data-only state for constructing a walk-view camera frame.
    /// </summary>
    public readonly struct WalkViewCameraState
    {
        public WalkViewCameraState(
            Vector3d position,
            double yawRadians,
            double pitchRadians,
            double eyeHeight,
            double rollRadians = 0.0)
        {
            Position = position;
            YawRadians = yawRadians;
            PitchRadians = pitchRadians;
            EyeHeight = eyeHeight;
            RollRadians = rollRadians;
        }

        public Vector3d Position { get; }

        public double YawRadians { get; }

        public double PitchRadians { get; }

        public double EyeHeight { get; }

        public double RollRadians { get; }
    }
}
