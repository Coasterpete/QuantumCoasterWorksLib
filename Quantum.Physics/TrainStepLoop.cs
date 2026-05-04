using System;
using System.Collections.Generic;
using Quantum.Math;
using Quantum.Splines;

namespace Quantum.Physics
{
    /// <summary>
    /// Minimal deterministic fixed-step loop over a single train follower state.
    /// </summary>
    public sealed class TrainStepLoop
    {
        private readonly IForceTargetProvider? _forceTargetProvider;

        public TrainFollowerState Follower { get; }

        public double DeltaTime { get; }

        public double GravityMagnitude { get; }

        public double LinearDragCoefficient { get; }

        public double QuadraticDragCoefficient { get; }

        public double RollingResistance { get; }

        public long Tick { get; private set; }

        public double ElapsedTimeSeconds { get; private set; }

        public TrainStepLoop(
            TrainFollowerState follower,
            double deltaTime,
            double gravityMagnitude,
            double linearDragCoefficient,
            double quadraticDragCoefficient,
            double rollingResistance)
            : this(
                follower,
                deltaTime,
                gravityMagnitude,
                linearDragCoefficient,
                quadraticDragCoefficient,
                rollingResistance,
                forceTargetProvider: null)
        {
        }

        public TrainStepLoop(
            TrainFollowerState follower,
            double deltaTime,
            double gravityMagnitude,
            double linearDragCoefficient,
            double quadraticDragCoefficient,
            double rollingResistance,
            IForceTargetProvider? forceTargetProvider)
        {
            Follower = follower ?? throw new ArgumentNullException(nameof(follower));
            DeltaTime = deltaTime;
            GravityMagnitude = gravityMagnitude;
            LinearDragCoefficient = linearDragCoefficient;
            QuadraticDragCoefficient = quadraticDragCoefficient;
            RollingResistance = rollingResistance;
            _forceTargetProvider = forceTargetProvider;
            Tick = 0;
            ElapsedTimeSeconds = 0.0;
        }

        public void Step()
        {
            double accelerationFromNormalG = 0.0;
            if (TryGetProjectedAcceleration(Follower.Distance, Follower.Frame, out Vector3d projectedAcceleration))
            {
                accelerationFromNormalG = Vector3d.Dot(projectedAcceleration, Follower.Frame.Normal);
            }

            double halfStepVelocityKick = 0.5 * accelerationFromNormalG * DeltaTime;
            Follower.Speed += halfStepVelocityKick;

            Follower.UpdateWithGravity(
                DeltaTime,
                GravityMagnitude,
                LinearDragCoefficient,
                QuadraticDragCoefficient,
                RollingResistance);

            Follower.Speed += halfStepVelocityKick;
            Follower.Acceleration += accelerationFromNormalG;

            Tick++;
            ElapsedTimeSeconds += DeltaTime;
        }

        public void Step(int steps)
        {
            if (steps < 0)
                throw new ArgumentOutOfRangeException(nameof(steps), "Step count must be non-negative.");

            for (int i = 0; i < steps; i++)
            {
                Step();
            }
        }

        public IReadOnlyList<TrainFollowerState> Sample(int steps)
        {
            if (steps < 0)
                throw new ArgumentOutOfRangeException(nameof(steps), "Step count must be non-negative.");

            var snapshots = new List<TrainFollowerState>(steps);

            for (int i = 0; i < steps; i++)
            {
                Step();
                TrainFollowerState snapshot = CloneFollowerState(Follower);
                if (TryGetProjectedAcceleration(snapshot.Distance, snapshot.Frame, out Vector3d projectedAcceleration))
                {
                    snapshot.ProjectedAcceleration = projectedAcceleration;
                }

                snapshots.Add(snapshot);
            }

            return snapshots;
        }

        public IReadOnlyList<TrainFollowerState> SampleForDuration(double durationSeconds)
        {
            int steps = (int)System.Math.Floor(durationSeconds / DeltaTime);
            return Sample(steps);
        }

        private static TrainFollowerState CloneFollowerState(TrainFollowerState source)
        {
            var clone = new TrainFollowerState(
                source.Track,
                initialDistance: source.Distance,
                speed: source.Speed,
                loopEnabled: source.LoopEnabled);

            clone.Acceleration = source.Acceleration;
            clone.ProjectedAcceleration = source.ProjectedAcceleration;
            return clone;
        }

        private bool TryGetProjectedAcceleration(double distance, TrackFrame frame, out Vector3d projectedAcceleration)
        {
            if (_forceTargetProvider != null &&
                _forceTargetProvider.TryGetForceTargets(distance, out ForceTargets targets))
            {
                projectedAcceleration = ForceTargetProjection.ComputeForceVector(targets, frame);
                return true;
            }

            projectedAcceleration = default;
            return false;
        }
    }
}
