using System;
using System.Numerics;
using Quantum.Math;

namespace Quantum.Track
{
    /// <summary>
    /// Builds ride camera transforms from track-frame basis and local offsets.
    /// </summary>
    public static class CameraFrameBuilder
    {
        public static CameraTransform BuildRideCamera(TrackFrame frame, Vector3d offset)
        {
            ValidateFinite(offset, nameof(offset));

            Vector3d position =
                frame.Position +
                (frame.Tangent * offset.X) +
                (frame.Normal * offset.Y) +
                (frame.Binormal * offset.Z);

            Vector3d forward = frame.Tangent;
            Vector3d up = frame.Normal;
            Vector3d right = frame.Binormal;

            Matrix4x4 transform = TrackFrame.CreateFromFrame(position, forward, up, right);
            return new CameraTransform(transform, position, forward, up, right);
        }

        private static void ValidateFinite(Vector3d vector, string paramName)
        {
            if (double.IsNaN(vector.X) || double.IsNaN(vector.Y) || double.IsNaN(vector.Z) ||
                double.IsInfinity(vector.X) || double.IsInfinity(vector.Y) || double.IsInfinity(vector.Z))
            {
                throw new ArgumentOutOfRangeException(paramName, "Vector must contain finite components.");
            }
        }
    }
}
