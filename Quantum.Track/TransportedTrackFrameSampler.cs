using System;
using System.Collections.Generic;
using Quantum.Math;
using SplineTrackFrame = Quantum.Splines.TrackFrame;
using SystemMath = System.Math;

namespace Quantum.Track
{
    /// <summary>
    /// Explicit station-distance sampler that transports the unrolled frame basis
    /// across an ordered sample sequence before applying existing segment roll.
    /// </summary>
    public static class TransportedTrackFrameSampler
    {
        private const double MinimumVectorMagnitude = 1e-9;

        /// <summary>
        /// Samples transported frames using a fresh evaluator bound to
        /// <paramref name="document"/>.
        /// </summary>
        /// <param name="document">Track document to sample.</param>
        /// <param name="distances">Finite station distances in non-decreasing order.</param>
        public static TrackFrame[] SampleFramesAtDistances(
            TrackDocument document,
            IReadOnlyList<double> distances)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            return SampleFramesAtDistances(document, new TrackEvaluator(document), distances);
        }

        /// <summary>
        /// Samples transported frames using the provided evaluator for centerline
        /// and compatibility frame evaluation.
        /// </summary>
        /// <param name="document">Track document to sample.</param>
        /// <param name="evaluator">Evaluator whose current frame behavior seeds the transported sampler.</param>
        /// <param name="distances">Finite station distances in non-decreasing order.</param>
        public static TrackFrame[] SampleFramesAtDistances(
            TrackDocument document,
            TrackEvaluator evaluator,
            IReadOnlyList<double> distances)
        {
            return SampleFramesAtDistancesCore(
                document,
                evaluator,
                distances,
                (_, evaluationPoint) => ResolveRollRadians(evaluationPoint));
        }

        internal static TrackFrame[] SampleFramesAtDistances(
            TrackDocument document,
            TrackEvaluator evaluator,
            IReadOnlyList<double> distances,
            Func<int, TrackEvaluationPoint, double> rollRadiansResolver)
        {
            if (rollRadiansResolver is null)
            {
                throw new ArgumentNullException(nameof(rollRadiansResolver));
            }

            return SampleFramesAtDistancesCore(
                document,
                evaluator,
                distances,
                rollRadiansResolver);
        }

        private static TrackFrame[] SampleFramesAtDistancesCore(
            TrackDocument document,
            TrackEvaluator evaluator,
            IReadOnlyList<double> distances,
            Func<int, TrackEvaluationPoint, double> rollRadiansResolver)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (evaluator is null)
            {
                throw new ArgumentNullException(nameof(evaluator));
            }

            if (distances is null)
            {
                throw new ArgumentNullException(nameof(distances));
            }

            ValidateOrderedDistances(distances);

            if (distances.Count == 0)
            {
                return Array.Empty<TrackFrame>();
            }

            TrackEvaluationPoint[] evaluationPoints = evaluator.EvaluateAtDistances(document, distances);
            SplineTrackFrame[] scalarFrames = evaluator.EvaluateSplineFramesAtDistances(document, distances);
            double[] stationDistances = ClampStationDistances(document, distances);
            var transportedFrames = new TrackFrame[scalarFrames.Length];

            TrackFrame firstFrame = BuildExportFrame(
                evaluator.EvaluateSplineFrameAtDistance(document, distances[0]),
                stationDistances[0]);
            Vector3d previousTangent = NormalizeOrThrow(firstFrame.Tangent, "tangent");
            double firstRollRadians = ResolveRollRadians(evaluationPoints[0]);
            Vector3d previousBaseNormal = ResolveInitialBaseNormal(firstFrame, previousTangent, firstRollRadians);

            for (int i = 0; i < scalarFrames.Length; i++)
            {
                TrackFrame scalarFrame = BuildExportFrame(scalarFrames[i], stationDistances[i]);
                Vector3d tangent = NormalizeOrThrow(scalarFrame.Tangent, "tangent");

                if (i > 0)
                {
                    previousBaseNormal = TransportNormal(previousBaseNormal, previousTangent, tangent);
                }

                Vector3d baseNormal = previousBaseNormal;
                Vector3d binormal = NormalizeOrThrow(Vector3d.Cross(tangent, baseNormal), "binormal");
                baseNormal = NormalizeOrThrow(Vector3d.Cross(binormal, tangent), "normal");
                Vector3d normal = baseNormal;

                double rollRadians = ResolveRollRadians(rollRadiansResolver(i, evaluationPoints[i]));
                if (rollRadians != 0.0)
                {
                    normal = RotateAroundAxis(normal, tangent, rollRadians);
                    binormal = RotateAroundAxis(binormal, tangent, rollRadians);
                }

                OrthonormalizeBasis(tangent, ref normal, ref binormal);

                transportedFrames[i] = new TrackFrame(
                    scalarFrame.Distance,
                    scalarFrame.Position,
                    tangent,
                    normal,
                    binormal);

                previousBaseNormal = baseNormal;
                previousTangent = tangent;
            }

            return transportedFrames;
        }

        private static void ValidateOrderedDistances(IReadOnlyList<double> distances)
        {
            double previousDistance = double.NegativeInfinity;

            for (int i = 0; i < distances.Count; i++)
            {
                double distance = distances[i];
                if (double.IsNaN(distance) || double.IsInfinity(distance))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(distances),
                        distance,
                        $"Distance at index {i} must be finite.");
                }

                if (distance < previousDistance)
                {
                    throw new ArgumentException(
                        $"Distances must be in non-decreasing station order. Distance at index {i} is less than the previous distance.",
                        nameof(distances));
                }

                previousDistance = distance;
            }
        }

        private static TrackFrame BuildExportFrame(SplineTrackFrame sourceFrame, double stationDistance)
        {
            Vector3d tangent = NormalizeOrThrow(sourceFrame.Tangent, "tangent");
            Vector3d projectedNormal = sourceFrame.Normal - (tangent * Vector3d.Dot(sourceFrame.Normal, tangent));
            Vector3d normal = NormalizeOrThrow(projectedNormal, "normal");
            Vector3d binormal = NormalizeOrThrow(Vector3d.Cross(tangent, normal), "binormal");
            normal = NormalizeOrThrow(Vector3d.Cross(binormal, tangent), "normal");

            return new TrackFrame(stationDistance, sourceFrame.Position, tangent, normal, binormal);
        }

        private static double[] ClampStationDistances(TrackDocument document, IReadOnlyList<double> distances)
        {
            int distanceCount = distances.Count;
            if (distanceCount == 0)
            {
                return Array.Empty<double>();
            }

            double totalLength = document.TotalLength;
            var stationDistances = new double[distanceCount];

            for (int i = 0; i < distanceCount; i++)
            {
                stationDistances[i] = SystemMath.Max(0.0, SystemMath.Min(distances[i], totalLength));
            }

            return stationDistances;
        }

        private static Vector3d ResolveInitialBaseNormal(
            TrackFrame firstFrame,
            Vector3d tangent,
            double rollRadians)
        {
            Vector3d candidateNormal = firstFrame.Normal;
            if (rollRadians != 0.0)
            {
                candidateNormal = RotateAroundAxis(candidateNormal, tangent, -rollRadians);
            }

            return ResolveProjectedNormal(candidateNormal, tangent);
        }

        private static Vector3d TransportNormal(
            Vector3d previousNormal,
            Vector3d previousTangent,
            Vector3d currentTangent)
        {
            Vector3d normalizedPreviousTangent = NormalizeOrThrow(previousTangent, "previous tangent");
            Vector3d normalizedCurrentTangent = NormalizeOrThrow(currentTangent, "current tangent");
            Vector3d rotationAxis = Vector3d.Cross(normalizedPreviousTangent, normalizedCurrentTangent);
            Vector3d transportedNormal = previousNormal;
            double axisLength = rotationAxis.Length;

            if (axisLength > MinimumVectorMagnitude)
            {
                rotationAxis /= axisLength;
                double tangentDot = Clamp(
                    Vector3d.Dot(normalizedPreviousTangent, normalizedCurrentTangent),
                    -1.0,
                    1.0);
                transportedNormal = RotateAroundAxis(previousNormal, rotationAxis, SystemMath.Acos(tangentDot));
            }
            else if (Vector3d.Dot(normalizedPreviousTangent, normalizedCurrentTangent) < 0.0)
            {
                rotationAxis = SelectFallbackAxis(normalizedPreviousTangent);
                rotationAxis = ResolveProjectedNormal(rotationAxis, normalizedPreviousTangent);
                transportedNormal = RotateAroundAxis(previousNormal, rotationAxis, SystemMath.PI);
            }

            Vector3d normal = ResolveProjectedNormal(transportedNormal, normalizedCurrentTangent);
            if (Vector3d.Dot(normal, transportedNormal) < 0.0)
            {
                normal *= -1.0;
            }

            return normal;
        }

        private static Vector3d ResolveProjectedNormal(Vector3d candidateNormal, Vector3d tangent)
        {
            Vector3d normalizedTangent = NormalizeOrThrow(tangent, "tangent");
            Vector3d projected = candidateNormal - (normalizedTangent * Vector3d.Dot(candidateNormal, normalizedTangent));
            if (!IsFinite(projected) || projected.Length <= MinimumVectorMagnitude)
            {
                projected = BuildFallbackNormal(normalizedTangent);
            }

            return NormalizeOrThrow(projected, "normal");
        }

        private static Vector3d BuildFallbackNormal(Vector3d tangent)
        {
            Vector3d fallbackAxis = SelectFallbackAxis(tangent);
            Vector3d projected = fallbackAxis - (tangent * Vector3d.Dot(fallbackAxis, tangent));
            return NormalizeOrThrow(projected, "normal");
        }

        private static Vector3d SelectFallbackAxis(Vector3d tangent)
        {
            Vector3d normalizedTangent = NormalizeOrThrow(tangent, "tangent");

            double xAlignment = SystemMath.Abs(Vector3d.Dot(normalizedTangent, Vector3d.UnitX));
            double yAlignment = SystemMath.Abs(Vector3d.Dot(normalizedTangent, Vector3d.UnitY));
            double zAlignment = SystemMath.Abs(Vector3d.Dot(normalizedTangent, Vector3d.UnitZ));

            if (xAlignment <= yAlignment && xAlignment <= zAlignment)
            {
                return Vector3d.UnitX;
            }

            if (yAlignment <= xAlignment && yAlignment <= zAlignment)
            {
                return Vector3d.UnitY;
            }

            return Vector3d.UnitZ;
        }

        private static double ResolveRollRadians(TrackEvaluationPoint evaluationPoint)
        {
            return ResolveRollRadians(evaluationPoint.Segment.RollRadians);
        }

        private static double ResolveRollRadians(double rollRadians)
        {
            if (double.IsNaN(rollRadians) || double.IsInfinity(rollRadians))
            {
                throw new InvalidOperationException("Track segment roll must be finite.");
            }

            return rollRadians;
        }

        private static void OrthonormalizeBasis(
            Vector3d tangent,
            ref Vector3d normal,
            ref Vector3d binormal)
        {
            Vector3d normalizedTangent = NormalizeOrThrow(tangent, "tangent");
            Vector3d projectedNormal = normal - (normalizedTangent * Vector3d.Dot(normal, normalizedTangent));
            normal = NormalizeOrThrow(projectedNormal, "normal");
            binormal = NormalizeOrThrow(Vector3d.Cross(normalizedTangent, normal), "binormal");
            normal = NormalizeOrThrow(Vector3d.Cross(binormal, normalizedTangent), "normal");
        }

        private static Vector3d RotateAroundAxis(Vector3d vector, Vector3d axis, double angle)
        {
            Vector3d normalizedAxis = NormalizeOrThrow(axis, "rotation axis");
            double cos = SystemMath.Cos(angle);
            double sin = SystemMath.Sin(angle);

            Vector3d scaledVector = vector * cos;
            Vector3d crossTerm = Vector3d.Cross(normalizedAxis, vector) * sin;
            Vector3d projectionTerm = normalizedAxis * (Vector3d.Dot(normalizedAxis, vector) * (1.0 - cos));
            return scaledVector + crossTerm + projectionTerm;
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
            return !(double.IsNaN(vector.X) ||
                     double.IsNaN(vector.Y) ||
                     double.IsNaN(vector.Z) ||
                     double.IsInfinity(vector.X) ||
                     double.IsInfinity(vector.Y) ||
                     double.IsInfinity(vector.Z));
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }
    }
}
