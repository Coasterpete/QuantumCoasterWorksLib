using System;
using System.Collections.Generic;
using Quantum.Math;

namespace Quantum.Splines
{
    public static class TrackFrameSampler
    {
        public static ArcLengthSample SampleByLength(IArcLengthCurve curve, double s)
        {
            if (curve == null)
                throw new ArgumentNullException(nameof(curve));

            double clampedS = ClampDistance(curve, s);
            Vector3d position = curve.EvaluateByLength(clampedS);
            Vector3d tangent = NormalizeOrThrow(curve.TangentByLength(clampedS), "tangent");

            return new ArcLengthSample(clampedS, position, tangent);
        }

        public static TrackFrame SampleFrameByLength(IArcLengthCurve curve, double s, Vector3d referenceUp)
        {
            ArcLengthSample sample = SampleByLength(curve, s);
            Vector3d tangent = sample.Tangent;

            Vector3d upHint = ResolveUpHint(referenceUp, tangent);
            Vector3d right = Vector3d.Cross(upHint, tangent);

            if (right.Length <= MathUtil.Epsilon)
            {
                Vector3d fallbackAxis = SelectFallbackAxis(tangent);
                right = Vector3d.Cross(fallbackAxis, tangent);
            }

            right = NormalizeOrThrow(right, "right");
            Vector3d up = NormalizeOrThrow(Vector3d.Cross(tangent, right), "up");

            return new TrackFrame(sample.S, sample.Position, tangent, right, up);
        }

        public static List<TrackFrame> SampleFramesUniform(IArcLengthCurve curve, double stepLength, Vector3d referenceUp)
        {
            if (curve == null)
                throw new ArgumentNullException(nameof(curve));

            if (double.IsNaN(stepLength) || double.IsInfinity(stepLength) || stepLength <= MathUtil.Epsilon)
                throw new ArgumentOutOfRangeException(nameof(stepLength), "Step length must be a positive finite value.");

            double length = curve.Length;
            var frames = new List<TrackFrame>();

            if (length <= MathUtil.Epsilon)
            {
                frames.Add(SampleFrameByLength(curve, 0.0, referenceUp));
                return frames;
            }

            double s = 0.0;

            while (s < length)
            {
                frames.Add(SampleFrameByLength(curve, s, referenceUp));
                s += stepLength;
            }

            // Ensure the terminal endpoint is always represented.
            frames.Add(SampleFrameByLength(curve, length, referenceUp));

            return frames;
        }

        private static double ClampDistance(IArcLengthCurve curve, double s)
        {
            double length = curve.Length;

            if (length <= MathUtil.Epsilon)
                return 0.0;

            return MathUtil.Clamp(s, 0.0, length);
        }

        private static Vector3d ResolveUpHint(Vector3d referenceUp, Vector3d tangent)
        {
            if (!IsFinite(referenceUp) || referenceUp.Length <= MathUtil.Epsilon)
                return SelectFallbackAxis(tangent);

            Vector3d normalizedUp = NormalizeOrThrow(referenceUp, "referenceUp");
            double alignment = System.Math.Abs(Vector3d.Dot(normalizedUp, tangent));

            if (alignment >= 1.0 - 1e-6)
                return SelectFallbackAxis(tangent);

            return normalizedUp;
        }

        private static Vector3d SelectFallbackAxis(Vector3d tangent)
        {
            Vector3d normalizedTangent = NormalizeOrThrow(tangent, "tangent");

            double xAlignment = System.Math.Abs(Vector3d.Dot(normalizedTangent, Vector3d.UnitX));
            double yAlignment = System.Math.Abs(Vector3d.Dot(normalizedTangent, Vector3d.UnitY));
            double zAlignment = System.Math.Abs(Vector3d.Dot(normalizedTangent, Vector3d.UnitZ));

            if (xAlignment <= yAlignment && xAlignment <= zAlignment)
                return Vector3d.UnitX;

            if (yAlignment <= xAlignment && yAlignment <= zAlignment)
                return Vector3d.UnitY;

            return Vector3d.UnitZ;
        }

        private static Vector3d NormalizeOrThrow(Vector3d vector, string label)
        {
            if (!IsFinite(vector))
                throw new InvalidOperationException($"Unable to normalize {label}: vector contains non-finite components.");

            double length = vector.Length;
            if (length <= MathUtil.Epsilon)
                throw new InvalidOperationException($"Unable to normalize {label}: vector magnitude is near zero.");

            return vector / length;
        }

        private static bool IsFinite(Vector3d vector)
        {
            return !(double.IsNaN(vector.X) || double.IsNaN(vector.Y) || double.IsNaN(vector.Z) ||
                     double.IsInfinity(vector.X) || double.IsInfinity(vector.Y) || double.IsInfinity(vector.Z));
        }
    }
}
