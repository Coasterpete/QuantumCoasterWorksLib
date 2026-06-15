using System;
using System.Collections.Generic;
using Quantum.Math;
using Quantum.Splines;

namespace Quantum.Track.Internal
{
    internal sealed class CanonicalTransportedFrameSampler
    {
        private const double MinimumVectorMagnitude = 1e-9;
        private const double ParallelAxisThreshold = 0.99;

        private readonly CompiledTrackSamplingContext _samplingContext;
        private readonly TransportNode[] _nodes;
        private readonly Vector3d? _authoredStartNormal;

        public CanonicalTransportedFrameSampler(
            CompiledTrackSamplingContext samplingContext,
            IReadOnlyList<double> nodeDistances,
            Vector3d? authoredStartNormal)
        {
            _samplingContext = samplingContext ?? throw new ArgumentNullException(nameof(samplingContext));
            _authoredStartNormal = authoredStartNormal;
            if (nodeDistances is null)
            {
                throw new ArgumentNullException(nameof(nodeDistances));
            }

            if (nodeDistances.Count == 0)
            {
                throw new ArgumentException("Canonical transport history requires at least one sampling node.", nameof(nodeDistances));
            }

            _nodes = BuildTransportHistory(nodeDistances);
        }

        public TrackFrame Sample(
            double distance,
            Func<ResolvedTrackDistance, double> rollRadiansResolver)
        {
            if (rollRadiansResolver is null)
            {
                throw new ArgumentNullException(nameof(rollRadiansResolver));
            }

            ResolvedTrackDistance resolvedDistance = _samplingContext.Resolve(distance);
            CenterlineSample centerline = EvaluateCenterline(resolvedDistance);
            int nodeIndex = FindPrecedingNodeIndex(resolvedDistance.ClampedDistance);
            TransportNode node = _nodes[nodeIndex];

            Vector3d baseNormal = TransportNormal(node.BaseNormal, node.Tangent, centerline.Tangent);
            Vector3d normal = baseNormal;
            Vector3d binormal = NormalizeOrThrow(Vector3d.Cross(centerline.Tangent, baseNormal), "binormal");
            baseNormal = NormalizeOrThrow(Vector3d.Cross(binormal, centerline.Tangent), "normal");
            normal = baseNormal;

            double rollRadians = rollRadiansResolver(resolvedDistance);
            if (double.IsNaN(rollRadians) || double.IsInfinity(rollRadians))
            {
                throw new InvalidOperationException("Track roll must be finite.");
            }

            if (rollRadians != 0.0)
            {
                normal = RotateAroundAxis(normal, centerline.Tangent, rollRadians);
                binormal = RotateAroundAxis(binormal, centerline.Tangent, rollRadians);
            }

            OrthonormalizeBasis(centerline.Tangent, ref normal, ref binormal);

            return new TrackFrame(
                resolvedDistance.ClampedDistance,
                centerline.Position,
                centerline.Tangent,
                normal,
                binormal);
        }

        public TrackFrame[] SampleMany(
            IReadOnlyList<double> distances,
            Func<ResolvedTrackDistance, double> rollRadiansResolver)
        {
            if (distances is null)
            {
                throw new ArgumentNullException(nameof(distances));
            }

            var frames = new TrackFrame[distances.Count];
            for (int i = 0; i < distances.Count; i++)
            {
                frames[i] = Sample(distances[i], rollRadiansResolver);
            }

            return frames;
        }

        private TransportNode[] BuildTransportHistory(IReadOnlyList<double> nodeDistances)
        {
            var nodes = new TransportNode[nodeDistances.Count];
            ResolvedTrackDistance firstResolved = _samplingContext.Resolve(nodeDistances[0]);
            CenterlineSample firstSample = EvaluateCenterline(firstResolved);
            Vector3d previousTangent = firstSample.Tangent;
            Vector3d previousNormal = _authoredStartNormal.HasValue
                ? ResolveProjectedNormal(_authoredStartNormal.Value, previousTangent)
                : BuildInitialNormal(previousTangent);
            nodes[0] = new TransportNode(firstResolved.ClampedDistance, previousTangent, previousNormal);

            for (int i = 1; i < nodeDistances.Count; i++)
            {
                ResolvedTrackDistance resolved = _samplingContext.Resolve(nodeDistances[i]);
                CenterlineSample sample = EvaluateCenterline(resolved);
                previousNormal = TransportNormal(previousNormal, previousTangent, sample.Tangent);
                previousTangent = sample.Tangent;
                nodes[i] = new TransportNode(resolved.ClampedDistance, previousTangent, previousNormal);
            }

            return nodes;
        }

        private int FindPrecedingNodeIndex(double distance)
        {
            int low = 0;
            int high = _nodes.Length - 1;

            while (low < high)
            {
                int mid = low + ((high - low + 1) / 2);
                if (_nodes[mid].Distance <= distance)
                {
                    low = mid;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return low;
        }

        private static CenterlineSample EvaluateCenterline(ResolvedTrackDistance resolvedDistance)
        {
            if (resolvedDistance.Segment.Spline is IParamCurve spline)
            {
                Vector3d position;
                Vector3d tangent;

                if (spline is IArcLengthCurve arcLengthCurve)
                {
                    position = arcLengthCurve.EvaluateByLength(resolvedDistance.LocalDistance);
                    tangent = arcLengthCurve.TangentByLength(resolvedDistance.LocalDistance);
                }
                else
                {
                    position = spline.Evaluate(resolvedDistance.LocalT);
                    tangent = spline.Tangent(resolvedDistance.LocalT);
                }

                return new CenterlineSample(position, NormalizeOrThrow(tangent, "tangent"));
            }

            return new CenterlineSample(
                new Vector3d(resolvedDistance.ClampedDistance, 0.0, 0.0),
                Vector3d.UnitX);
        }

        private static Vector3d BuildInitialNormal(Vector3d tangent)
        {
            Vector3d normalizedTangent = NormalizeOrThrow(tangent, "tangent");
            Vector3d referenceUp = SelectReferenceUp(normalizedTangent);
            Vector3d binormal = NormalizeOrThrow(Vector3d.Cross(normalizedTangent, referenceUp), "binormal");
            return NormalizeOrThrow(Vector3d.Cross(binormal, normalizedTangent), "normal");
        }

        private static Vector3d SelectReferenceUp(Vector3d tangent)
        {
            if (System.Math.Abs(Vector3d.Dot(tangent, Vector3d.UnitY)) < ParallelAxisThreshold)
            {
                return Vector3d.UnitY;
            }

            if (System.Math.Abs(Vector3d.Dot(tangent, Vector3d.UnitZ)) < ParallelAxisThreshold)
            {
                return Vector3d.UnitZ;
            }

            return Vector3d.UnitX;
        }

        private static Vector3d TransportNormal(
            Vector3d previousNormal,
            Vector3d previousTangent,
            Vector3d currentTangent)
        {
            return RotationMinimizingFrameTransport.TransportNormal(
                previousNormal,
                previousTangent,
                currentTangent);
        }

        private static Vector3d ResolveProjectedNormal(Vector3d candidateNormal, Vector3d tangent)
        {
            Vector3d normalizedTangent = NormalizeOrThrow(tangent, "tangent");
            Vector3d projected = candidateNormal - (normalizedTangent * Vector3d.Dot(candidateNormal, normalizedTangent));
            if (!IsFinite(projected) || projected.Length <= MinimumVectorMagnitude)
            {
                Vector3d fallbackAxis = SelectFallbackAxis(normalizedTangent);
                projected = fallbackAxis - (normalizedTangent * Vector3d.Dot(fallbackAxis, normalizedTangent));
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

        private static void OrthonormalizeBasis(
            Vector3d tangent,
            ref Vector3d normal,
            ref Vector3d binormal)
        {
            Vector3d projectedNormal = normal - (tangent * Vector3d.Dot(normal, tangent));
            normal = NormalizeOrThrow(projectedNormal, "normal");
            binormal = NormalizeOrThrow(Vector3d.Cross(tangent, normal), "binormal");
            normal = NormalizeOrThrow(Vector3d.Cross(binormal, tangent), "normal");
        }

        private static Vector3d RotateAroundAxis(Vector3d vector, Vector3d axis, double angle)
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
                throw new InvalidOperationException($"Unable to normalize {label}: vector contains non-finite components.");
            }

            double length = vector.Length;
            if (length <= MinimumVectorMagnitude)
            {
                throw new InvalidOperationException($"Unable to normalize {label}: vector magnitude is near zero.");
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

        private readonly struct CenterlineSample
        {
            public CenterlineSample(Vector3d position, Vector3d tangent)
            {
                Position = position;
                Tangent = tangent;
            }

            public Vector3d Position { get; }

            public Vector3d Tangent { get; }
        }

        private readonly struct TransportNode
        {
            public TransportNode(double distance, Vector3d tangent, Vector3d baseNormal)
            {
                Distance = distance;
                Tangent = tangent;
                BaseNormal = baseNormal;
            }

            public double Distance { get; }

            public Vector3d Tangent { get; }

            public Vector3d BaseNormal { get; }
        }
    }
}
