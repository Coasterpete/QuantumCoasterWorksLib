using System;
using System.Collections.Generic;
using Quantum.Core;
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
        private readonly ITrackFrameProvider? _trackFrameProvider;

        public TrainIntegrationMode IntegrationMode { get; }

        public TrainFollowerState Follower { get; }

        public double DeltaTime { get; }

        public double GravityMagnitude { get; }

        public double LinearDragCoefficient { get; }

        public double QuadraticDragCoefficient { get; }

        public double RollingResistance { get; }

        public ITrackFrameProvider? TrackFrameProvider => _trackFrameProvider;

        /// <summary>
        /// Optional scalar controlling how much curvature-normal acceleration magnitude influences 1D speed in
        /// <see cref="TrainIntegrationMode.TangentialProjected"/> mode. Default is 0.0 (disabled).
        /// </summary>
        public double CurvatureNormalSpeedInfluenceMultiplier { get; }

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
                integrationMode: TrainIntegrationMode.LegacyNormalComponent,
                trackFrameProvider: null)
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
                integrationMode: integrationMode,
                trackFrameProvider: null)
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
                integrationMode: TrainIntegrationMode.LegacyNormalComponent,
                trackFrameProvider: null)
        {
        }

        public TrainStepLoop(
            TrainFollowerState follower,
            double deltaTime,
            double gravityMagnitude,
            double linearDragCoefficient,
            double quadraticDragCoefficient,
            double rollingResistance,
            ITrackFrameProvider? trackFrameProvider)
            : this(
                follower,
                deltaTime,
                gravityMagnitude,
                linearDragCoefficient,
                quadraticDragCoefficient,
                rollingResistance,
                forceTargetProvider: null,
                integrationMode: TrainIntegrationMode.LegacyNormalComponent,
                trackFrameProvider: trackFrameProvider)
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
            ITrackFrameProvider? trackFrameProvider)
            : this(
                follower,
                deltaTime,
                gravityMagnitude,
                linearDragCoefficient,
                quadraticDragCoefficient,
                rollingResistance,
                forceTargetProvider,
                integrationMode: TrainIntegrationMode.LegacyNormalComponent,
                trackFrameProvider: trackFrameProvider)
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
            TrainIntegrationMode integrationMode,
            ITrackFrameProvider? trackFrameProvider = null,
            double curvatureNormalSpeedInfluenceMultiplier = 0.0)
        {
            Guard.RequireNonNegativeFinite(
                curvatureNormalSpeedInfluenceMultiplier,
                nameof(curvatureNormalSpeedInfluenceMultiplier),
                "Curvature normal speed influence multiplier must be a finite, non-negative value.");

            Follower = follower ?? throw new ArgumentNullException(nameof(follower));
            DeltaTime = deltaTime;
            GravityMagnitude = gravityMagnitude;
            LinearDragCoefficient = linearDragCoefficient;
            QuadraticDragCoefficient = quadraticDragCoefficient;
            RollingResistance = rollingResistance;
            _forceTargetProvider = forceTargetProvider;
            _trackFrameProvider = trackFrameProvider;
            IntegrationMode = integrationMode;
            CurvatureNormalSpeedInfluenceMultiplier = curvatureNormalSpeedInfluenceMultiplier;
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
                    StepTangentialProjected();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(IntegrationMode),
                        IntegrationMode,
                        "Unknown train integration mode.");
            }
        }

        private void StepLegacyNormalComponent()
        {
            double normalComponentAcceleration = 0.0;
            double? tangentialAcceleration = null;

            // Legacy mode does not publish normal/binormal/world diagnostics on the follower state.
            // Clear these fields each step so stale values cannot leak across mode transitions.
            Follower.ProjectedAcceleration = null;
            Follower.NormalAcceleration = null;
            Follower.NormalAccelerationVector = null;
            Follower.BinormalAcceleration = null;
            Follower.BinormalAccelerationVector = null;
            Follower.CombinedWorldAccelerationVector = null;

            if (TryGetProjectedAcceleration(Follower.Distance, Follower.Frame, out Vector3d projectedAcceleration))
            {
                AccelerationComponents components = AccelerationDecomposer.Decompose(projectedAcceleration, Follower.Frame);
                double a_t = components.Tangential;
                tangentialAcceleration = a_t;
                normalComponentAcceleration = components.Normal;
            }

            Follower.TangentialAcceleration = tangentialAcceleration;

            double halfStepVelocityKick = 0.5 * normalComponentAcceleration * DeltaTime;
            Follower.Speed += halfStepVelocityKick;

            Follower.UpdateWithGravity(
                DeltaTime,
                GravityMagnitude,
                LinearDragCoefficient,
                QuadraticDragCoefficient,
                RollingResistance);

            Follower.Speed += halfStepVelocityKick;
            Follower.Acceleration += normalComponentAcceleration;

            Tick++;
            ElapsedTimeSeconds += DeltaTime;
        }

        private void StepTangentialProjected()
        {
            if (!TryGetProjectedAcceleration(Follower.Distance, Follower.Frame, out Vector3d projectedAcceleration))
            {
                throw new NotSupportedException(
                    "Train integration mode TangentialProjected requires projected acceleration data.");
            }

            AccelerationComponents components = AccelerationDecomposer.Decompose(projectedAcceleration, Follower.Frame);
            double tangentialProjectedAcceleration = components.Tangential;
            Follower.TangentialAcceleration = tangentialProjectedAcceleration;
            Follower.ProjectedAcceleration = null;
            Follower.NormalAcceleration = null;
            Follower.NormalAccelerationVector = null;
            Follower.BinormalAcceleration = null;
            Follower.BinormalAccelerationVector = null;
            double curvatureSpeedInfluenceAcceleration = 0.0;

            if (TryGetCurvatureFromTrackFrameProvider(Follower.Distance, out double curvature))
            {
                if (TryGetCurvatureNormalAccelerationVector(
                    curvature,
                    Follower.Speed,
                    Follower.Frame,
                    out double normalAcceleration,
                    out Vector3d normalAccelerationVector))
                {
                    Follower.NormalAcceleration = normalAcceleration;
                    Follower.NormalAccelerationVector = normalAccelerationVector;
                    curvatureSpeedInfluenceAcceleration = ComputeCurvatureSpeedInfluenceAcceleration(
                        normalAccelerationVector,
                        Follower.Speed);
                }

                if (TryGetCurvatureBinormalAccelerationVector(
                    Follower.Distance,
                    curvature,
                    Follower.Speed,
                    Follower.Frame,
                    out double binormalAcceleration,
                    out Vector3d binormalAccelerationVector))
                {
                    Follower.BinormalAcceleration = binormalAcceleration;
                    Follower.BinormalAccelerationVector = binormalAccelerationVector;
                }
            }

            UpdateCombinedWorldAccelerationDiagnostic(Follower);

            double integratedTangentialAcceleration =
                tangentialProjectedAcceleration + curvatureSpeedInfluenceAcceleration;
            double halfStepVelocityKick = 0.5 * integratedTangentialAcceleration * DeltaTime;
            Follower.Speed += halfStepVelocityKick;

            if (TryGetGravityAccelerationFromTrackFrameProvider(Follower.Distance, out double gravityAccelerationAlongTrack))
            {
                Follower.UpdateWithResolvedGravityAcceleration(
                    DeltaTime,
                    gravityAccelerationAlongTrack,
                    LinearDragCoefficient,
                    QuadraticDragCoefficient,
                    RollingResistance);
            }
            else
            {
                Follower.UpdateWithGravity(
                    DeltaTime,
                    GravityMagnitude,
                    LinearDragCoefficient,
                    QuadraticDragCoefficient,
                    RollingResistance);
            }

            Follower.Speed += halfStepVelocityKick;
            Follower.Acceleration += integratedTangentialAcceleration;

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
                    // In tangential-projected mode, normal/binormal diagnostics are curvature contracts when present.
                    // Keep those scalar/vector values and only refresh projected/tangential diagnostics from sampled force targets.
                    bool preserveCurvatureNormalDiagnostic =
                        IntegrationMode == TrainIntegrationMode.TangentialProjected &&
                        snapshot.NormalAcceleration.HasValue;
                    bool preserveCurvatureBinormalDiagnostic =
                        IntegrationMode == TrainIntegrationMode.TangentialProjected;
                    ApplyProjectedAccelerationDiagnostics(
                        snapshot,
                        projectedAcceleration,
                        preserveCurvatureNormalDiagnostic,
                        preserveCurvatureBinormalDiagnostic);
                }

                if (IntegrationMode == TrainIntegrationMode.TangentialProjected)
                {
                    UpdateCombinedWorldAccelerationDiagnostic(snapshot);
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
            clone.NormalAccelerationVector = source.NormalAccelerationVector;
            clone.BinormalAcceleration = source.BinormalAcceleration;
            clone.BinormalAccelerationVector = source.BinormalAccelerationVector;
            clone.CombinedWorldAccelerationVector = source.CombinedWorldAccelerationVector;
            return clone;
        }

        private static void ApplyProjectedAccelerationDiagnostics(
            TrainFollowerState sample,
            Vector3d projectedAcceleration,
            bool preserveNormalAcceleration = false,
            bool preserveBinormalAcceleration = false)
        {
            AccelerationComponents components = AccelerationDecomposer.Decompose(projectedAcceleration, sample.Frame);
            sample.ProjectedAcceleration = projectedAcceleration;
            sample.TangentialAcceleration = components.Tangential;
            if (!preserveNormalAcceleration)
            {
                sample.NormalAcceleration = components.Normal;
            }

            if (!preserveBinormalAcceleration)
            {
                sample.BinormalAcceleration = components.Binormal;
                sample.BinormalAccelerationVector = components.Binormal * sample.Frame.Binormal;
            }
        }

        private static void UpdateCombinedWorldAccelerationDiagnostic(TrainFollowerState state)
        {
            if (!state.TangentialAcceleration.HasValue)
            {
                state.CombinedWorldAccelerationVector = null;
                return;
            }

            Vector3d combinedWorldAcceleration =
                state.TangentialAcceleration.Value * state.Frame.Tangent;

            if (state.NormalAccelerationVector.HasValue)
            {
                combinedWorldAcceleration += state.NormalAccelerationVector.Value;
            }

            state.CombinedWorldAccelerationVector = combinedWorldAcceleration;
        }

        private bool TryGetGravityAccelerationFromTrackFrameProvider(double distance, out double gravityAccelerationAlongTrack)
        {
            gravityAccelerationAlongTrack = 0.0;
            if (_trackFrameProvider is null)
            {
                return false;
            }

            if (!_trackFrameProvider.TryGetFrameAtDistance(distance, out TrackFrame frame))
            {
                return false;
            }

            Vector3d gravityVector = new Vector3d(0.0, -GravityMagnitude, 0.0);
            gravityAccelerationAlongTrack = Vector3d.Dot(gravityVector, frame.Tangent);
            return true;
        }

        private bool TryGetCurvatureFromTrackFrameProvider(double distance, out double curvature)
        {
            curvature = 0.0;
            if (_trackFrameProvider is null)
            {
                return false;
            }

            return _trackFrameProvider.TryGetCurvatureAtDistance(distance, out curvature);
        }

        private bool TryGetCurvatureNormalAccelerationVector(
            double curvature,
            double speed,
            TrackFrame frame,
            out double normalAcceleration,
            out Vector3d normalAccelerationVector)
        {
            normalAcceleration = 0.0;
            normalAccelerationVector = default;

            if (!TryComputeCurvatureAccelerationMagnitude(curvature, speed, out normalAcceleration))
            {
                normalAcceleration = 0.0;
                return false;
            }

            normalAccelerationVector = normalAcceleration * frame.Normal;
            if (!IsFiniteVector(normalAccelerationVector))
            {
                normalAcceleration = 0.0;
                normalAccelerationVector = default;
                return false;
            }

            return true;
        }

        private bool TryGetCurvatureBinormalAccelerationVector(
            double distance,
            double curvature,
            double speed,
            TrackFrame frame,
            out double binormalAcceleration,
            out Vector3d binormalAccelerationVector)
        {
            binormalAcceleration = 0.0;
            binormalAccelerationVector = default;

            if (!TryComputeCurvatureAccelerationMagnitude(
                curvature,
                speed,
                out double curvatureAccelerationMagnitude))
            {
                return false;
            }

            double orientationBinormalScale = 0.0;
            if (_trackFrameProvider != null &&
                _trackFrameProvider.TryGetFrameAtDistance(distance, out TrackFrame curvatureFrame))
            {
                orientationBinormalScale = Vector3d.Dot(curvatureFrame.Normal, frame.Binormal);
                if (!Numeric.IsFinite(orientationBinormalScale))
                {
                    return false;
                }
            }

            binormalAcceleration = curvatureAccelerationMagnitude * orientationBinormalScale;
            if (!Numeric.IsFinite(binormalAcceleration))
            {
                binormalAcceleration = 0.0;
                return false;
            }

            binormalAccelerationVector = binormalAcceleration * frame.Binormal;
            if (!IsFiniteVector(binormalAccelerationVector))
            {
                binormalAcceleration = 0.0;
                binormalAccelerationVector = default;
                return false;
            }

            return true;
        }

        private double ComputeCurvatureSpeedInfluenceAcceleration(
            Vector3d normalAccelerationVector,
            double speed)
        {
            if (CurvatureNormalSpeedInfluenceMultiplier <= 0.0)
            {
                return 0.0;
            }

            double speedDirection = System.Math.Sign(speed);
            if (speedDirection == 0.0)
            {
                return 0.0;
            }

            double normalAccelerationMagnitude = normalAccelerationVector.Length;
            if (!Numeric.IsFinite(normalAccelerationMagnitude))
            {
                return 0.0;
            }

            double speedInfluenceAcceleration =
                CurvatureNormalSpeedInfluenceMultiplier * normalAccelerationMagnitude * speedDirection;
            if (!Numeric.IsFinite(speedInfluenceAcceleration))
            {
                return 0.0;
            }

            return speedInfluenceAcceleration;
        }

        private static bool IsFiniteVector(Vector3d value)
        {
            return Numeric.IsFinite(value.X) &&
                   Numeric.IsFinite(value.Y) &&
                   Numeric.IsFinite(value.Z);
        }

        private static bool TryComputeCurvatureAccelerationMagnitude(
            double curvature,
            double speed,
            out double curvatureAccelerationMagnitude)
        {
            curvatureAccelerationMagnitude = 0.0;
            if (!Numeric.IsFinite(curvature) || !Numeric.IsFinite(speed))
            {
                return false;
            }

            double speedSquared = speed * speed;
            if (!Numeric.IsFinite(speedSquared))
            {
                return false;
            }

            curvatureAccelerationMagnitude = speedSquared * curvature;
            return Numeric.IsFinite(curvatureAccelerationMagnitude);
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
