using System;
using Quantum.Core;
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

        public Vector3d? ProjectedAcceleration { get; set; }

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
            Guard.RequireNonNegativeFinite(
                gravityMagnitude,
                nameof(gravityMagnitude),
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
            ClampSpeedNearZero();
            Distance = NormalizeDistance(nextDistance);
            SampleCurrentState();
        }

        public void UpdateWithGravity(double deltaTime, double gravityMagnitude = 9.81)
        {
            UpdateWithGravity(
                deltaTime,
                gravityMagnitude,
                linearDragCoefficient: 0.0,
                quadraticDragCoefficient: 0.0);
        }

        public void UpdateWithGravity(double deltaTime, double gravityMagnitude, double linearDragCoefficient = 0.0)
        {
            UpdateWithGravity(
                deltaTime,
                gravityMagnitude,
                linearDragCoefficient,
                quadraticDragCoefficient: 0.0);
        }

        public void UpdateWithGravity(
            double deltaTime,
            double gravityMagnitude,
            double linearDragCoefficient,
            double quadraticDragCoefficient = 0.0)
        {
            UpdateWithGravity(
                deltaTime,
                gravityMagnitude,
                linearDragCoefficient,
                quadraticDragCoefficient,
                rollingResistance: 0.0);
        }

        public void UpdateWithGravity(
            double deltaTime,
            double gravityMagnitude,
            double linearDragCoefficient,
            double quadraticDragCoefficient,
            double rollingResistance = 0.0)
        {
            double previousSpeed = Speed;

            Guard.RequireNonNegativeFinite(
                linearDragCoefficient,
                nameof(linearDragCoefficient),
                "Linear drag coefficient must be a finite, non-negative value.");

            Guard.RequireNonNegativeFinite(
                quadraticDragCoefficient,
                nameof(quadraticDragCoefficient),
                "Quadratic drag coefficient must be a finite, non-negative value.");

            Guard.RequireNonNegativeFinite(
                rollingResistance,
                nameof(rollingResistance),
                "Rolling resistance must be a finite, non-negative value.");

            double gravityAcceleration = GravityAccelerationAlongTrack(gravityMagnitude);

            Acceleration =
                gravityAcceleration
                - (linearDragCoefficient * Speed)
                - (quadraticDragCoefficient * Speed * System.Math.Abs(Speed));
            Acceleration -= rollingResistance * System.Math.Sign(Speed);

            double speedWithoutResistance = previousSpeed + (gravityAcceleration * deltaTime);
            Update(deltaTime);

            if (DidReverseDirection(previousSpeed, Speed) && !DidReverseDirection(previousSpeed, speedWithoutResistance))
            {
                Speed = 0.0;
            }
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

        private void ClampSpeedNearZero()
        {
            if (System.Math.Abs(Speed) < MathUtil.Epsilon)
            {
                Speed = 0.0;
            }
        }

        private static bool DidReverseDirection(double previousSpeed, double newSpeed)
        {
            if (previousSpeed > 0.0)
                return newSpeed < 0.0;

            if (previousSpeed < 0.0)
                return newSpeed > 0.0;

            return false;
        }
    }
}
