using System;
using Quantum.Math;

namespace Quantum.Track.Internal
{
    internal static class RotationMinimizingFrameTransport
    {
        private const double MinimumVectorMagnitude = 1e-9;

        public static Vector3d TransportNormal(
            Vector3d previousNormal,
            Vector3d previousTangent,
            Vector3d currentTangent)
        {
            Vector3d normalizedPreviousTangent = NormalizeOrThrow(
                previousTangent,
                "previous tangent");
            Vector3d normalizedCurrentTangent = NormalizeOrThrow(
                currentTangent,
                "current tangent");
            Vector3d rotationAxis = Vector3d.Cross(
                normalizedPreviousTangent,
                normalizedCurrentTangent);
            Vector3d transportedNormal = previousNormal;
            double axisLength = rotationAxis.Length;

            if (axisLength > MinimumVectorMagnitude)
            {
                rotationAxis /= axisLength;
                double tangentDot = Clamp(
                    Vector3d.Dot(normalizedPreviousTangent, normalizedCurrentTangent),
                    -1.0,
                    1.0);
                transportedNormal = RotateAroundAxis(
                    previousNormal,
                    rotationAxis,
                    System.Math.Acos(tangentDot));
            }
            else if (Vector3d.Dot(normalizedPreviousTangent, normalizedCurrentTangent) < 0.0)
            {
                rotationAxis = ResolveProjectedNormal(
                    SelectFallbackAxis(normalizedPreviousTangent),
                    normalizedPreviousTangent);
                transportedNormal = RotateAroundAxis(
                    previousNormal,
                    rotationAxis,
                    System.Math.PI);
            }

            Vector3d normal = ResolveProjectedNormal(
                transportedNormal,
                normalizedCurrentTangent);
            if (Vector3d.Dot(normal, transportedNormal) < 0.0)
            {
                normal *= -1.0;
            }

            return normal;
        }

        private static Vector3d ResolveProjectedNormal(
            Vector3d candidateNormal,
            Vector3d tangent)
        {
            Vector3d normalizedTangent = NormalizeOrThrow(tangent, "tangent");
            Vector3d projected = candidateNormal -
                (normalizedTangent * Vector3d.Dot(candidateNormal, normalizedTangent));
            if (!IsFinite(projected) || projected.Length <= MinimumVectorMagnitude)
            {
                Vector3d fallbackAxis = SelectFallbackAxis(normalizedTangent);
                projected = fallbackAxis -
                    (normalizedTangent * Vector3d.Dot(fallbackAxis, normalizedTangent));
            }

            return NormalizeOrThrow(projected, "normal");
        }

        private static Vector3d SelectFallbackAxis(Vector3d tangent)
        {
            double xAlignment = System.Math.Abs(Vector3d.Dot(tangent, Vector3d.UnitX));
            double yAlignment = System.Math.Abs(Vector3d.Dot(tangent, Vector3d.UnitY));
            double zAlignment = System.Math.Abs(Vector3d.Dot(tangent, Vector3d.UnitZ));

            if (xAlignment <= yAlignment && xAlignment <= zAlignment)
            {
                return Vector3d.UnitX;
            }

            return yAlignment <= zAlignment ? Vector3d.UnitY : Vector3d.UnitZ;
        }

        private static Vector3d RotateAroundAxis(
            Vector3d vector,
            Vector3d axis,
            double angle)
        {
            Vector3d normalizedAxis = NormalizeOrThrow(axis, "rotation axis");
            double cos = System.Math.Cos(angle);
            double sin = System.Math.Sin(angle);
            return (vector * cos) +
                   (Vector3d.Cross(normalizedAxis, vector) * sin) +
                   (normalizedAxis * (Vector3d.Dot(normalizedAxis, vector) * (1.0 - cos)));
        }

        private static Vector3d NormalizeOrThrow(Vector3d vector, string label)
        {
            if (!IsFinite(vector))
            {
                throw new InvalidOperationException(
                    $"Unable to normalize {label}: vector contains non-finite components.");
            }

            double length = vector.Length;
            if (length <= MinimumVectorMagnitude)
            {
                throw new InvalidOperationException(
                    $"Unable to normalize {label}: vector magnitude is near zero.");
            }

            return vector / length;
        }

        private static bool IsFinite(Vector3d vector)
        {
            return IsFinite(vector.X) && IsFinite(vector.Y) && IsFinite(vector.Z);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static double Clamp(double value, double min, double max)
        {
            return System.Math.Max(min, System.Math.Min(value, max));
        }
    }
}
