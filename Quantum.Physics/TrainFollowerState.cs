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
            Position = Track.EvaluateByLength(Distance);
            Tangent = Track.TangentByLength(Distance);
        }
    }
}
