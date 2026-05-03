namespace Quantum.Physics
{
    /// <summary>
    /// Read-only provider for force targets at a track-space position.
    /// </summary>
    public interface IForceTargetProvider
    {
        bool TryGetForceTargets(double x, out ForceTargets targets);
    }
}
