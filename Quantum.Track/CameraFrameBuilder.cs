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
        private const double MaximumPitchRadians = (System.Math.PI * 0.5) - 1e-4;

        public static CameraTransform BuildRideCamera(TrackFrame frame, Vector3d offset)
        {
            ValidateFinite(offset, nameof(offset));

            Vector3d position = ComputeCameraPositionFromLocalOffset(frame, offset);

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

        public static CameraTransform BuildBRollCamera(
            TrackFrame targetFrame,
            Vector3d localOffset,
            double lookAheadDistance,
            TrackEvaluator evaluator,
            Vector3d upHint)
        {
            ValidateFinite(localOffset, nameof(localOffset));
            ValidateFinite(upHint, nameof(upHint));

            if (double.IsNaN(lookAheadDistance) || double.IsInfinity(lookAheadDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(lookAheadDistance), "Look-ahead distance must be finite.");
            }

            if (evaluator is null)
            {
                throw new ArgumentNullException(nameof(evaluator));
            }

            Vector3d cameraPosition = ComputeCameraPositionFromLocalOffset(targetFrame, localOffset);

            Vector3d targetPosition = targetFrame.Position;
            if (lookAheadDistance != 0.0)
            {
                double lookAheadSampleDistance = targetFrame.Distance + lookAheadDistance;
                if (double.IsNaN(lookAheadSampleDistance) || double.IsInfinity(lookAheadSampleDistance))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(lookAheadDistance),
                        "Target look-ahead sample distance must be finite.");
                }

                TrackFrame lookAheadFrame = evaluator.EvaluateFrameAtDistance(lookAheadSampleDistance);
                targetPosition = lookAheadFrame.Position;
            }

            return BuildTargetCamera(cameraPosition, targetPosition, upHint);
        }

        public static CameraTransform BuildFlyByCamera(
            Vector3d cameraPosition,
            TrackFrame targetFrame,
            Vector3d upHint)
        {
            return BuildTargetCamera(cameraPosition, targetFrame.Position, upHint);
        }

        public static CameraTransform BuildFlyViewCamera(FlyViewCameraState state)
        {
            ValidateFinite(state.Position, nameof(state.Position));
            ValidateFinite(state.YawRadians, nameof(state.YawRadians));
            ValidateFinite(state.PitchRadians, nameof(state.PitchRadians));
            ValidateFinite(state.RollRadians, nameof(state.RollRadians));

            double clampedPitch = MathUtil.Clamp(state.PitchRadians, -MaximumPitchRadians, MaximumPitchRadians);
            double cosYaw = System.Math.Cos(state.YawRadians);
            double sinYaw = System.Math.Sin(state.YawRadians);
            double cosPitch = System.Math.Cos(clampedPitch);
            double sinPitch = System.Math.Sin(clampedPitch);

            Vector3d forward = NormalizeOrThrow(
                new Vector3d(cosPitch * cosYaw, sinPitch, cosPitch * sinYaw),
                nameof(state),
                "Unable to construct a valid forward vector from fly-view angles.");
            Vector3d right = NormalizeOrThrow(
                Vector3d.Cross(forward, Vector3d.UnitY),
                nameof(state.PitchRadians),
                "Fly-view pitch is too close to straight up or down.");
            Vector3d up = NormalizeOrThrow(
                Vector3d.Cross(right, forward),
                nameof(state),
                "Unable to construct a valid up vector.");

            if (state.RollRadians != 0.0)
            {
                up = NormalizeOrThrow(
                    RotateAroundAxis(up, forward, state.RollRadians),
                    nameof(state.RollRadians),
                    "Unable to construct a valid up vector from fly-view roll.");
                right = NormalizeOrThrow(
                    Vector3d.Cross(forward, up),
                    nameof(state.RollRadians),
                    "Unable to construct a valid right vector from fly-view roll.");
                up = NormalizeOrThrow(
                    Vector3d.Cross(right, forward),
                    nameof(state.RollRadians),
                    "Unable to construct a valid up vector from fly-view roll.");
            }

            Matrix4x4 transform = TrackFrame.CreateFromFrame(state.Position, forward, up, right);
            return new CameraTransform(transform, state.Position, forward, up, right);
        }

        public static CameraTransform BuildWalkViewCamera(WalkViewCameraState state)
        {
            ValidateFinite(state.Position, nameof(state.Position));
            ValidateFinite(state.YawRadians, nameof(state.YawRadians));
            ValidateFinite(state.PitchRadians, nameof(state.PitchRadians));
            ValidateFinite(state.RollRadians, nameof(state.RollRadians));
            ValidateFinite(state.EyeHeight, nameof(state.EyeHeight));

            if (state.EyeHeight < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(state.EyeHeight), "Eye height must be non-negative.");
            }

            Vector3d cameraPosition = state.Position + (Vector3d.UnitY * state.EyeHeight);
            return BuildFlyViewCamera(
                new FlyViewCameraState(
                    position: cameraPosition,
                    yawRadians: state.YawRadians,
                    pitchRadians: state.PitchRadians,
                    rollRadians: state.RollRadians));
        }

        private static Vector3d ComputeCameraPositionFromLocalOffset(TrackFrame frame, Vector3d localOffset)
        {
            return
                frame.Position +
                (frame.Tangent * localOffset.X) +
                (frame.Normal * localOffset.Y) +
                (frame.Binormal * localOffset.Z);
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

        private static Vector3d RotateAroundAxis(Vector3d vector, Vector3d axis, double angle)
        {
            Vector3d normalizedAxis = NormalizeOrThrow(axis, nameof(axis), "Rotation axis must have non-zero length.");
            double cos = System.Math.Cos(angle);
            double sin = System.Math.Sin(angle);

            Vector3d scaledVector = vector * cos;
            Vector3d crossTerm = Vector3d.Cross(normalizedAxis, vector) * sin;
            Vector3d projectionTerm = normalizedAxis * (Vector3d.Dot(normalizedAxis, vector) * (1.0 - cos));
            return scaledVector + crossTerm + projectionTerm;
        }

        private static void ValidateFinite(Vector3d vector, string paramName)
        {
            if (double.IsNaN(vector.X) || double.IsNaN(vector.Y) || double.IsNaN(vector.Z) ||
                double.IsInfinity(vector.X) || double.IsInfinity(vector.Y) || double.IsInfinity(vector.Z))
            {
                throw new ArgumentOutOfRangeException(paramName, "Vector must contain finite components.");
            }
        }

        private static void ValidateFinite(double value, string paramName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(paramName, "Value must be finite.");
            }
        }
    }
}
