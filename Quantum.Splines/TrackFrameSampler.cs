using System;
using System.Collections.Generic;
using Quantum.Math;

namespace Quantum.Splines
{
    public sealed class TrackFrameSampler
    {
        private readonly IArcLengthCurve _curve;
        private readonly Vector3d _referenceNormal;

        public TrackFrameSampler(IArcLengthCurve curve)
            : this(curve, Vector3d.UnitY)
        {
        }

        public TrackFrameSampler(IArcLengthCurve curve, Vector3d referenceNormal)
        {
            _curve = curve ?? throw new ArgumentNullException(nameof(curve));
            _referenceNormal = referenceNormal;
        }

        public TrackFrame GetFrameAt(double s)
        {
            ArcLengthSample sample = SampleByLength(_curve, s);
            return BuildFrame(_curve, sample.S, sample.Position, sample.Tangent, _referenceNormal);
        }

        public static ArcLengthSample SampleByLength(IArcLengthCurve curve, double s)
        {
            if (curve == null)
                throw new ArgumentNullException(nameof(curve));

            double clampedS = ClampDistance(curve, s);
            Vector3d position = curve.EvaluateByLength(clampedS);
            Vector3d tangent = NormalizeOrThrow(curve.TangentByLength(clampedS), "tangent");

            return new ArcLengthSample(clampedS, position, tangent);
        }

        public static TrackFrame GetFrameAt(IArcLengthCurve curve, double s)
        {
            var sampler = new TrackFrameSampler(curve, Vector3d.UnitY);
            return sampler.GetFrameAt(s);
        }

        public static TrackFrame SampleFrameByLength(IArcLengthCurve curve, double s, Vector3d referenceUp)
        {
            var sampler = new TrackFrameSampler(curve, referenceUp);
            return sampler.GetFrameAt(s);
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

        private static TrackFrame BuildFrame(
            IArcLengthCurve curve,
            double s,
            Vector3d position,
            Vector3d tangent,
            Vector3d referenceNormal)
        {
            Vector3d normal = ComputeNormal(curve, s, tangent, referenceNormal);
            Vector3d binormal = NormalizeOrThrow(Vector3d.Cross(tangent, normal), "binormal");
            normal = NormalizeOrThrow(Vector3d.Cross(binormal, tangent), "normal");

            return new TrackFrame(s, position, tangent, normal, binormal);
        }

        private static Vector3d ComputeNormal(
            IArcLengthCurve curve,
            double s,
            Vector3d tangent,
            Vector3d referenceNormal)
        {
            double length = curve.Length;
            if (length <= MathUtil.Epsilon)
                return ResolveFallbackNormal(referenceNormal, tangent);

            double deltaS = System.Math.Max(length * 1e-3, 1e-4);
            double prevS = MathUtil.Clamp(s - deltaS, 0.0, length);
            double nextS = MathUtil.Clamp(s + deltaS, 0.0, length);

            if (nextS - prevS <= MathUtil.Epsilon)
                return ResolveFallbackNormal(referenceNormal, tangent);

            Vector3d prevTangent = NormalizeOrThrow(curve.TangentByLength(prevS), "tangent");
            Vector3d nextTangent = NormalizeOrThrow(curve.TangentByLength(nextS), "tangent");

            Vector3d normalCandidate = RejectAlong(nextTangent - prevTangent, tangent);
            if (normalCandidate.Length <= MathUtil.Epsilon)
                return ResolveFallbackNormal(referenceNormal, tangent);

            Vector3d normal = NormalizeOrThrow(normalCandidate, "normal");

            Vector3d projectedReference = RejectAlong(referenceNormal, tangent);
            if (IsFinite(projectedReference) && projectedReference.Length > MathUtil.Epsilon)
            {
                Vector3d alignedReference = NormalizeOrThrow(projectedReference, "referenceNormal");
                if (Vector3d.Dot(normal, alignedReference) < 0.0)
                    normal = normal * -1.0;
            }

            return normal;
        }

        private static double ClampDistance(IArcLengthCurve curve, double s)
        {
            double length = curve.Length;

            if (length <= MathUtil.Epsilon)
                return 0.0;

            return MathUtil.Clamp(s, 0.0, length);
        }

        private static Vector3d ResolveFallbackNormal(Vector3d referenceNormal, Vector3d tangent)
        {
            if (IsFinite(referenceNormal) && referenceNormal.Length > MathUtil.Epsilon)
            {
                Vector3d projectedReference = RejectAlong(referenceNormal, tangent);
                if (projectedReference.Length > MathUtil.Epsilon)
                    return NormalizeOrThrow(projectedReference, "normal");
            }

            Vector3d fallbackAxis = SelectFallbackAxis(tangent);
            Vector3d projectedAxis = RejectAlong(fallbackAxis, tangent);
            return NormalizeOrThrow(projectedAxis, "normal");
        }

        private static Vector3d RejectAlong(Vector3d vector, Vector3d axis)
        {
            return vector - (Vector3d.Dot(vector, axis) * axis);
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
