using System;
using Quantum.Math;
using Quantum.Splines;

namespace Quantum.Physics
{
    /// <summary>
    /// Minimal train follower state that advances along an arc-length curve.
    /// </summary>
    public sealed class TrainFollowerState
    {
        public IArcLengthCurve Track { get; }

        public double Distance { get; private set; }

        public double Speed { get; set; }

        public double Acceleration { get; set; }

        public bool LoopEnabled { get; set; }

        public Vector3d Position { get; private set; }

        public Vector3d Tangent { get; private set; }

        public TrackFrame Frame { get; private set; }

        /// <summary>
        /// Projects gravity (negative Y direction) onto the current track tangent.
        /// Negative means gravity opposes forward motion; positive means gravity assists it.
        /// </summary>
        public double GravityAccelerationAlongTrack(double gravityMagnitude = 9.81)
        {
            if (double.IsNaN(gravityMagnitude) || double.IsInfinity(gravityMagnitude))
                throw new ArgumentOutOfRangeException(
                    nameof(gravityMagnitude),
                    gravityMagnitude,
                    "Gravity magnitude must be a finite, non-negative value.");

            if (gravityMagnitude < 0.0)
                throw new ArgumentOutOfRangeException(
                    nameof(gravityMagnitude),
                    gravityMagnitude,
                    "Gravity magnitude must be a finite, non-negative value.");

            return gravityMagnitude * Vector3d.Dot(new Vector3d(0.0, -1.0, 0.0), Frame.Tangent);
        }

        public TrainFollowerState(
            IArcLengthCurve track,
            double initialDistance = 0.0,
            double speed = 0.0,
            bool loopEnabled = false)
        {
            Track = track ?? throw new ArgumentNullException(nameof(track));
            Speed = speed;
            LoopEnabled = loopEnabled;

            Distance = NormalizeDistance(initialDistance);
            SampleCurrentState();
        }

        public void Update(double deltaTime)
        {
            double nextDistance = Distance + (Speed * deltaTime) + (0.5 * Acceleration * deltaTime * deltaTime);
            Speed += Acceleration * deltaTime;
            Distance = NormalizeDistance(nextDistance);
            SampleCurrentState();
        }

        private double NormalizeDistance(double value)
        {
            double length = Track.Length;

            if (length <= MathUtil.Epsilon)
                return 0.0;

            if (LoopEnabled)
            {
                double wrapped = value % length;
                if (wrapped < 0.0)
                    wrapped += length;

                return wrapped;
            }

            return MathUtil.Clamp(value, 0.0, length);
        }

        private void SampleCurrentState()
        {
            Frame = TrackFrameSampler.SampleFrameByLength(Track, Distance, Vector3d.UnitY);
            Position = Frame.Position;
            Tangent = Frame.Tangent;
        }
    }
}
