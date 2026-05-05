using Quantum.Splines;

namespace Quantum.Physics
{
    /// <summary>
    /// Read-only provider for track frames at an arc-length distance.
    /// </summary>
    public interface ITrackFrameProvider
    {
        bool TryGetFrameAtDistance(double distance, out TrackFrame frame);
    }
}
