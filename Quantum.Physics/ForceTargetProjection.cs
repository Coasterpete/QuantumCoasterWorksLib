using Quantum.Math;
using Quantum.Track;

namespace Quantum.Physics
{
    /// <summary>
    /// Pure math helpers for mapping force targets into world-space vectors.
    /// </summary>
    public static class ForceTargetProjection
    {
        private const double StandardGravityMetersPerSecondSquared = 9.81;

        public static Vector3d ComputeForceVector(ForceTargets targets, TrackFrame frame)
        {
            double normalAcceleration = targets.NormalG * StandardGravityMetersPerSecondSquared;
            double lateralAcceleration = targets.LateralG * StandardGravityMetersPerSecondSquared;

            return (frame.Normal * normalAcceleration) + (frame.Binormal * lateralAcceleration);
        }
    }
}
