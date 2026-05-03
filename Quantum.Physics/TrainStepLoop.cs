using System;

namespace Quantum.Physics
{
    /// <summary>
    /// Minimal deterministic fixed-step loop over a single train follower state.
    /// </summary>
    public sealed class TrainStepLoop
    {
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
        {
            Follower = follower ?? throw new ArgumentNullException(nameof(follower));
            DeltaTime = deltaTime;
            GravityMagnitude = gravityMagnitude;
            LinearDragCoefficient = linearDragCoefficient;
            QuadraticDragCoefficient = quadraticDragCoefficient;
            RollingResistance = rollingResistance;
            Tick = 0;
            ElapsedTimeSeconds = 0.0;
        }

        public void Step()
        {
            Follower.UpdateWithGravity(
                DeltaTime,
                GravityMagnitude,
                LinearDragCoefficient,
                QuadraticDragCoefficient,
                RollingResistance);

            Tick++;
            ElapsedTimeSeconds += DeltaTime;
        }

        public void Step(int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                Step();
            }
        }
    }
}
