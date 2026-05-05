using Quantum.Splines;

namespace Quantum.Physics
{
    /// <summary>
    /// Read-only provider for track frames at an arc-length distance.
    /// </summary>
    public interface ITrackFrameProvider
    {
        bool TryGetFrameAtDistance(double distance, out TrackFrame frame);

        /// <summary>
        /// Optional curvature lookup at arc-length distance.
        /// Implementers may return false when curvature data is not available.
        /// </summary>
        bool TryGetCurvatureAtDistance(double distance, out double curvature)
        {
            curvature = 0.0;
            return false;
        }
    }
}
