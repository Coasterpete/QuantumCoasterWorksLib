using System;
using Quantum.Core;
using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using TrackFrame = Quantum.Track.TrackFrame;

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

        /// <summary>
        /// World-space projected acceleration vector resolved from force targets.
        /// Populated by diagnostic sampling paths.
        /// </summary>
        public Vector3d? ProjectedAcceleration { get; set; }

        /// <summary>
        /// Tangential acceleration diagnostic along <see cref="Frame.Tangent"/>.
        /// </summary>
        public double? TangentialAcceleration { get; set; }

        /// <summary>
        /// Normal acceleration diagnostic scalar.
        /// In tangential-projected mode with curvature data this represents v^2 * curvature.
        /// In projection diagnostics it represents the normal component of projected acceleration.
        /// When tangential-projected samples include both curvature and projected diagnostics,
        /// curvature-derived values take precedence.
        /// </summary>
        public double? NormalAcceleration { get; set; }

        /// <summary>
        /// Normal acceleration diagnostic vector, aligned with <see cref="Frame.Normal"/> when present.
        /// </summary>
        public Vector3d? NormalAccelerationVector { get; set; }

        /// <summary>
        /// Binormal acceleration diagnostic along <see cref="Frame.Binormal"/>.
        /// </summary>
        public double? BinormalAcceleration { get; set; }

        /// <summary>
        /// Binormal acceleration diagnostic vector, aligned with <see cref="Frame.Binormal"/> when present.
        /// </summary>
        public Vector3d? BinormalAccelerationVector { get; set; }

        /// <summary>
        /// Combined world-space acceleration diagnostic built from tangential plus normal-vector terms.
        /// Binormal terms are intentionally excluded to preserve existing consumer expectations.
        /// </summary>
        public Vector3d? CombinedWorldAccelerationVector { get; set; }

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
            double gravityAcceleration = GravityAccelerationAlongTrack(gravityMagnitude);
            UpdateWithResolvedGravityAcceleration(
                deltaTime,
                gravityAcceleration,
                linearDragCoefficient,
                quadraticDragCoefficient,
                rollingResistance);
        }

        internal void UpdateWithResolvedGravityAcceleration(
            double deltaTime,
            double gravityAcceleration,
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
            CurveFrame curveFrame = CurveFrameSampler.SampleFrameByLength(Track, Distance, Vector3d.UnitY);
            Frame = new TrackFrame(
                Distance,
                curveFrame.Position,
                curveFrame.Tangent,
                curveFrame.Normal,
                curveFrame.Binormal);
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
