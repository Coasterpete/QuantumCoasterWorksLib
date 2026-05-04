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

        public TrainIntegrationMode IntegrationMode { get; }

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
                forceTargetProvider: null,
                integrationMode: TrainIntegrationMode.LegacyNormalComponent)
        {
        }

        public TrainStepLoop(
            TrainFollowerState follower,
            double deltaTime,
            double gravityMagnitude,
            double linearDragCoefficient,
            double quadraticDragCoefficient,
            double rollingResistance,
            TrainIntegrationMode integrationMode)
            : this(
                follower,
                deltaTime,
                gravityMagnitude,
                linearDragCoefficient,
                quadraticDragCoefficient,
                rollingResistance,
                forceTargetProvider: null,
                integrationMode: integrationMode)
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
            : this(
                follower,
                deltaTime,
                gravityMagnitude,
                linearDragCoefficient,
                quadraticDragCoefficient,
                rollingResistance,
                forceTargetProvider,
                integrationMode: TrainIntegrationMode.LegacyNormalComponent)
        {
        }

        public TrainStepLoop(
            TrainFollowerState follower,
            double deltaTime,
            double gravityMagnitude,
            double linearDragCoefficient,
            double quadraticDragCoefficient,
            double rollingResistance,
            IForceTargetProvider? forceTargetProvider,
            TrainIntegrationMode integrationMode)
        {
            Follower = follower ?? throw new ArgumentNullException(nameof(follower));
            DeltaTime = deltaTime;
            GravityMagnitude = gravityMagnitude;
            LinearDragCoefficient = linearDragCoefficient;
            QuadraticDragCoefficient = quadraticDragCoefficient;
            RollingResistance = rollingResistance;
            _forceTargetProvider = forceTargetProvider;
            IntegrationMode = integrationMode;
            Tick = 0;
            ElapsedTimeSeconds = 0.0;
        }

        public void Step()
        {
            switch (IntegrationMode)
            {
                case TrainIntegrationMode.LegacyNormalComponent:
                    StepLegacyNormalComponent();
                    break;
                case TrainIntegrationMode.TangentialProjected:
                    throw new NotSupportedException(
                        "Train integration mode TangentialProjected is not supported yet.");
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(IntegrationMode),
                        IntegrationMode,
                        "Unknown train integration mode.");
            }
        }

        private void StepLegacyNormalComponent()
        {
            double accelerationFromNormalG = 0.0;
            double? tangentialAcceleration = null;
            if (TryGetProjectedAcceleration(Follower.Distance, Follower.Frame, out Vector3d projectedAcceleration))
            {
                AccelerationComponents components = AccelerationDecomposer.Decompose(projectedAcceleration, Follower.Frame);
                double a_t = components.Tangential;
                tangentialAcceleration = a_t;
                accelerationFromNormalG = components.Normal;
            }

            Follower.TangentialAcceleration = tangentialAcceleration;

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
                    ApplyProjectedAccelerationDiagnostics(snapshot, projectedAcceleration);
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
            clone.TangentialAcceleration = source.TangentialAcceleration;
            clone.NormalAcceleration = source.NormalAcceleration;
            clone.BinormalAcceleration = source.BinormalAcceleration;
            return clone;
        }

        private static void ApplyProjectedAccelerationDiagnostics(TrainFollowerState sample, Vector3d projectedAcceleration)
        {
            AccelerationComponents components = AccelerationDecomposer.Decompose(projectedAcceleration, sample.Frame);
            sample.ProjectedAcceleration = projectedAcceleration;
            sample.TangentialAcceleration = components.Tangential;
            sample.NormalAcceleration = components.Normal;
            sample.BinormalAcceleration = components.Binormal;
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
