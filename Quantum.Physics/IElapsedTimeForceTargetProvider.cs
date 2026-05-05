namespace Quantum.Physics
{
    /// <summary>
    /// Optional extension contract for force target providers that support elapsed-time-aware sampling.
    /// </summary>
    public interface IElapsedTimeForceTargetProvider : IForceTargetProvider
    {
        bool TryGetForceTargets(double distance, double elapsedTime, out ForceTargets targets);
    }
}
