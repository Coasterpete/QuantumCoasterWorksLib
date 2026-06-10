using Quantum.Math;
using Quantum.Track;

namespace Quantum.Physics
{
    /// <summary>
    /// Resolves an acceleration vector into scalar components of a track frame basis.
    /// </summary>
    public static class AccelerationDecomposer
    {
        public static AccelerationComponents Decompose(Vector3d acceleration, TrackFrame frame)
        {
            double tangential = Vector3d.Dot(acceleration, frame.Tangent);
            double normal = Vector3d.Dot(acceleration, frame.Normal);
            double binormal = Vector3d.Dot(acceleration, frame.Binormal);
            return new AccelerationComponents(tangential, normal, binormal);
        }
    }
}
