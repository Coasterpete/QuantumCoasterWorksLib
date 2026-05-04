namespace Quantum.Math
{
    /// <summary>
    /// Minimal orthonormal basis contract used to build transforms from track frames
    /// without creating project reference cycles.
    /// </summary>
    public interface ITrackFrameBasis
    {
        Vector3d Tangent { get; }

        Vector3d Normal { get; }

        Vector3d Binormal { get; }
    }
}
