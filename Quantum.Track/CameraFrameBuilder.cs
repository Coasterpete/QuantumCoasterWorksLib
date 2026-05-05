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
        private const double MinimumVectorMagnitude = 1e-9;

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

        public static CameraTransform BuildTargetCamera(
            Vector3d cameraPosition,
            Vector3d targetPosition,
            Vector3d upHint)
        {
            ValidateFinite(cameraPosition, nameof(cameraPosition));
            ValidateFinite(targetPosition, nameof(targetPosition));
            ValidateFinite(upHint, nameof(upHint));

            Vector3d targetDirection = targetPosition - cameraPosition;
            Vector3d forward = NormalizeOrThrow(
                targetDirection,
                nameof(targetPosition),
                "Target direction must have non-zero length.");

            Vector3d projectedUp = upHint - (forward * Vector3d.Dot(upHint, forward));
            if (projectedUp.Length <= MinimumVectorMagnitude)
            {
                Vector3d fallbackUp = SelectFallbackUp(forward);
                projectedUp = fallbackUp - (forward * Vector3d.Dot(fallbackUp, forward));
            }

            Vector3d up = NormalizeOrThrow(
                projectedUp,
                nameof(upHint),
                "Up hint must not be parallel to forward direction.");
            Vector3d right = NormalizeOrThrow(
                Vector3d.Cross(forward, up),
                nameof(upHint),
                "Unable to construct a valid right vector.");
            up = NormalizeOrThrow(
                Vector3d.Cross(right, forward),
                nameof(upHint),
                "Unable to construct a valid up vector.");

            Matrix4x4 transform = TrackFrame.CreateFromFrame(cameraPosition, forward, up, right);
            return new CameraTransform(transform, cameraPosition, forward, up, right);
        }

        public static CameraTransform BuildFlyByCamera(
            Vector3d cameraPosition,
            TrackFrame targetFrame,
            Vector3d upHint)
        {
            return BuildTargetCamera(cameraPosition, targetFrame.Position, upHint);
        }

        private static Vector3d NormalizeOrThrow(Vector3d vector, string paramName, string message)
        {
            ValidateFinite(vector, paramName);

            double length = vector.Length;
            if (length <= MinimumVectorMagnitude)
            {
                throw new ArgumentOutOfRangeException(paramName, message);
            }

            return vector / length;
        }

        private static Vector3d SelectFallbackUp(Vector3d forward)
        {
            double absX = System.Math.Abs(forward.X);
            double absY = System.Math.Abs(forward.Y);
            double absZ = System.Math.Abs(forward.Z);

            if (absX <= absY && absX <= absZ)
            {
                return Vector3d.UnitX;
            }

            if (absY <= absZ)
            {
                return Vector3d.UnitY;
            }

            return Vector3d.UnitZ;
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
