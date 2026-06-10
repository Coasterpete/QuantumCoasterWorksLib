using System;
using Quantum.Track;

namespace Quantum.Physics
{
    /// <summary>
    /// Track-frame provider backed by a fixed track document.
    /// </summary>
    public sealed class TrackFrameProviderFromDocument : ITrackFrameProvider
    {
        private readonly TrackPhysicsAdapter _adapter;
        private readonly TrackDocument _document;

        public TrackFrameProviderFromDocument(TrackDocument document)
            : this(new TrackPhysicsAdapter(), document)
        {
        }

        public TrackFrameProviderFromDocument(TrackPhysicsAdapter adapter, TrackDocument document)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public bool TryGetFrameAtDistance(double distance, out TrackFrame frame)
        {
            if (double.IsNaN(distance) || double.IsInfinity(distance))
            {
                frame = default;
                return false;
            }

            try
            {
                frame = _adapter.GetFrameAtDistance(_document, distance);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                frame = default;
                return false;
            }
            catch (InvalidOperationException)
            {
                frame = default;
                return false;
            }
        }

        public bool TryGetCurvatureAtDistance(double distance, out double curvature)
        {
            if (double.IsNaN(distance) || double.IsInfinity(distance))
            {
                curvature = 0.0;
                return false;
            }

            return _adapter.TryGetCurvatureAtDistance(_document, distance, out curvature);
        }
    }
}
