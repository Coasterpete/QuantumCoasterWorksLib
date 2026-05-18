using System;
using System.Collections.Generic;
using GShark.Fitting;
using GShark.Geometry;
using Quantum.Math;
using Quantum.Splines;
using ExportTrackFrame = Quantum.Track.TrackFrame;
using GSharkNurbsBase = GShark.Geometry.NurbsBase;

namespace Quantum.Track
{
    /// <summary>
    /// Deterministic debug-only sampler that approximates segmented track geometry
    /// with a single continuous curve for smoother frame diagnostics/rendering.
    /// </summary>
    public static class DebugTrackContinuousSampler
    {
        private const int SplineDegree = 3;
        private const double MinimumDistance = 1e-9;
        private const double MinimumVectorMagnitude = 1e-9;

        public static ExportTrackFrame[] SampleContinuousFrames(
            TrackDocument document,
            TrackEvaluator evaluator,
            IReadOnlyList<double> distances,
            int controlPointSampleCount,
            int arcLengthSampleCount,
            double rollBlendDistance)
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

            if (controlPointSampleCount < SplineDegree + 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(controlPointSampleCount),
                    controlPointSampleCount,
                    $"Control point sample count must be at least {SplineDegree + 1}.");
            }

            if (arcLengthSampleCount < 8)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(arcLengthSampleCount),
                    arcLengthSampleCount,
                    "Arc-length sample count must be at least 8.");
            }

            if (double.IsNaN(rollBlendDistance) || double.IsInfinity(rollBlendDistance) || rollBlendDistance < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rollBlendDistance),
                    rollBlendDistance,
                    "Roll blend distance must be finite and non-negative.");
            }

            if (distances.Count == 0)
            {
                return Array.Empty<ExportTrackFrame>();
            }

            for (int i = 0; i < distances.Count; i++)
            {
                if (double.IsNaN(distances[i]) || double.IsInfinity(distances[i]))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(distances),
                        $"Distance at index {i} must be finite.");
                }
            }

            double totalLength = document.TotalLength;
            if (totalLength <= MinimumDistance)
            {
                return evaluator.EvaluateFramesAtDistances(distances);
            }

            IArcLengthCurve curve = BuildContinuousCurve(evaluator, totalLength, controlPointSampleCount, arcLengthSampleCount);
            RollProfile rollProfile = BuildRollProfile(document);

            var clampedDistances = new double[distances.Count];
            var sampledPositions = new Vector3d[distances.Count];
            var sampledTangents = new Vector3d[distances.Count];

            for (int i = 0; i < distances.Count; i++)
            {
                double clampedDistance = Clamp(distances[i], 0.0, totalLength);
                double normalizedDistance = clampedDistance / totalLength;
                double smoothDistance = normalizedDistance * curve.Length;

                clampedDistances[i] = clampedDistance;
                sampledPositions[i] = curve.EvaluateByLength(smoothDistance);
                sampledTangents[i] = NormalizeOrThrow(curve.TangentByLength(smoothDistance), "tangent");
            }

            var frames = new ExportTrackFrame[distances.Count];
            Vector3d transportedNormal = ResolveInitialNormal(
                evaluator.EvaluateFrameAtDistance(clampedDistances[0]).Normal,
                sampledTangents[0]);

            for (int i = 0; i < distances.Count; i++)
            {
                Vector3d tangent = sampledTangents[i];
                if (i > 0)
                {
                    transportedNormal = TransportNormal(transportedNormal, tangent);
                }

                Vector3d normal = transportedNormal;
                Vector3d binormal = NormalizeOrThrow(Vector3d.Cross(tangent, normal), "binormal");
                normal = NormalizeOrThrow(Vector3d.Cross(binormal, tangent), "normal");

                double clampedDistance = clampedDistances[i];
                double rollRadians = ResolveEasedRollRadians(rollProfile, clampedDistance, rollBlendDistance);
                if (System.Math.Abs(rollRadians) > MinimumDistance)
                {
                    normal = RotateAroundAxis(normal, tangent, rollRadians);
                    binormal = RotateAroundAxis(binormal, tangent, rollRadians);
                }

                OrthonormalizeBasis(tangent, ref normal, ref binormal);

                if (i > 0)
                {
                    AlignFrameSigns(frames[i - 1], ref tangent, ref normal, ref binormal);
                }

                frames[i] = new ExportTrackFrame(
                    distance: clampedDistance,
                    position: sampledPositions[i],
                    tangent: tangent,
                    normal: normal,
                    binormal: binormal);
            }

            return frames;
        }

        private static IArcLengthCurve BuildContinuousCurve(
            TrackEvaluator evaluator,
            double totalLength,
            int controlPointSampleCount,
            int arcLengthSampleCount)
        {
            double[] controlDistances = TrackFrameDebugGizmoBuilder.BuildUniformFrameDistances(
                totalLength,
                controlPointSampleCount);
            ExportTrackFrame[] controlFrames = evaluator.EvaluateFramesAtDistances(controlDistances);

            var controlPoints = new List<Vector3d>(controlFrames.Length);
            for (int i = 0; i < controlFrames.Length; i++)
            {
                controlPoints.Add(controlFrames[i].Position);
            }

            Vector3 startTangent = controlFrames[0].Tangent.ToGSharkVector3();
            Vector3 endTangent = controlFrames[controlFrames.Length - 1].Tangent.ToGSharkVector3();
            GSharkNurbsBase interpolated = Curve.Interpolated(
                GSharkVector3dConversions.ToGSharkPoint3List(controlPoints),
                degree: SplineDegree,
                startTangent: startTangent,
                endTangent: endTangent,
                centripetal: true);

            IParamCurve smoothCurve = new InterpolatedNurbsCurveAdapter(interpolated);
            return new ArcLengthCurveAdapter(smoothCurve, arcLengthSampleCount);
        }

        private static RollProfile BuildRollProfile(TrackDocument document)
        {
            int segmentCount = document.Segments.Count;
            if (segmentCount == 0)
            {
                return new RollProfile(Array.Empty<double>(), Array.Empty<double>());
            }

            var boundaryDistances = new double[System.Math.Max(0, segmentCount - 1)];
            var segmentRolls = new double[segmentCount];

            double accumulatedDistance = 0.0;
            for (int i = 0; i < segmentCount; i++)
            {
                TrackSegment segment = document.Segments[i] ??
                    throw new InvalidOperationException("TrackDocument contains a null segment entry.");

                double roll = segment.RollRadians;
                if (double.IsNaN(roll) || double.IsInfinity(roll))
                {
                    throw new InvalidOperationException("Track segment roll must be finite.");
                }

                segmentRolls[i] = roll;

                if (i < segmentCount - 1)
                {
                    accumulatedDistance += segment.Length;
                    boundaryDistances[i] = accumulatedDistance;
                }
            }

            return new RollProfile(boundaryDistances, segmentRolls);
        }

        private static double ResolveEasedRollRadians(
            RollProfile profile,
            double distance,
            double blendDistance)
        {
            if (profile.SegmentRolls.Length == 0)
            {
                return 0.0;
            }

            int segmentIndex = ResolveSegmentIndex(profile.BoundaryDistances, distance);
            double baseRoll = profile.SegmentRolls[segmentIndex];

            if (blendDistance <= MinimumDistance || profile.BoundaryDistances.Length == 0)
            {
                return baseRoll;
            }

            int boundaryIndex = ResolveClosestBoundaryIndex(profile.BoundaryDistances, distance, out double boundaryDelta);
            if (boundaryIndex < 0 || boundaryDelta > blendDistance)
            {
                return baseRoll;
            }

            double boundaryDistance = profile.BoundaryDistances[boundaryIndex];
            double blendStart = boundaryDistance - blendDistance;
            double blendEnd = boundaryDistance + blendDistance;
            double t = Clamp01((distance - blendStart) / (blendEnd - blendStart));
            double easedT = SmoothStep(t);

            double leftRoll = profile.SegmentRolls[boundaryIndex];
            double rightRoll = profile.SegmentRolls[boundaryIndex + 1];
            return Lerp(leftRoll, rightRoll, easedT);
        }

        private static int ResolveClosestBoundaryIndex(
            IReadOnlyList<double> boundaries,
            double distance,
            out double smallestDelta)
        {
            int bestIndex = -1;
            smallestDelta = double.PositiveInfinity;

            for (int i = 0; i < boundaries.Count; i++)
            {
                double delta = System.Math.Abs(distance - boundaries[i]);
                if (delta < smallestDelta)
                {
                    smallestDelta = delta;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static int ResolveSegmentIndex(IReadOnlyList<double> boundaries, double distance)
        {
            for (int i = 0; i < boundaries.Count; i++)
            {
                if (distance <= boundaries[i])
                {
                    return i;
                }
            }

            return boundaries.Count;
        }

        private static Vector3d ResolveInitialNormal(Vector3d candidateNormal, Vector3d tangent)
        {
            Vector3d projectedCandidate = candidateNormal - (tangent * Vector3d.Dot(candidateNormal, tangent));
            if (projectedCandidate.Length > MinimumVectorMagnitude && IsFinite(projectedCandidate))
            {
                return NormalizeOrThrow(projectedCandidate, "normal");
            }

            return BuildFallbackNormal(tangent);
        }

        private static Vector3d TransportNormal(Vector3d previousNormal, Vector3d currentTangent)
        {
            Vector3d projected = previousNormal - (currentTangent * Vector3d.Dot(previousNormal, currentTangent));
            if (!IsFinite(projected) || projected.Length <= MinimumVectorMagnitude)
            {
                projected = BuildFallbackNormal(currentTangent);
            }

            Vector3d normal = NormalizeOrThrow(projected, "normal");
            if (Vector3d.Dot(normal, previousNormal) < 0.0)
            {
                normal = normal * -1.0;
            }

            return normal;
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

            double xAlignment = System.Math.Abs(Vector3d.Dot(normalizedTangent, Vector3d.UnitX));
            double yAlignment = System.Math.Abs(Vector3d.Dot(normalizedTangent, Vector3d.UnitY));
            double zAlignment = System.Math.Abs(Vector3d.Dot(normalizedTangent, Vector3d.UnitZ));

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

        private static void AlignFrameSigns(
            ExportTrackFrame previousFrame,
            ref Vector3d tangent,
            ref Vector3d normal,
            ref Vector3d binormal)
        {
            if (Vector3d.Dot(tangent, previousFrame.Tangent) < 0.0)
            {
                tangent = tangent * -1.0;
                normal = normal * -1.0;
                binormal = binormal * -1.0;
            }

            if (Vector3d.Dot(normal, previousFrame.Normal) < 0.0 &&
                Vector3d.Dot(binormal, previousFrame.Binormal) < 0.0)
            {
                normal = normal * -1.0;
                binormal = binormal * -1.0;
            }

            OrthonormalizeBasis(tangent, ref normal, ref binormal);
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
            double cos = System.Math.Cos(angle);
            double sin = System.Math.Sin(angle);

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

        private static double Clamp01(double value)
        {
            if (value < 0.0)
            {
                return 0.0;
            }

            if (value > 1.0)
            {
                return 1.0;
            }

            return value;
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

        private static double SmoothStep(double t)
        {
            return t * t * (3.0 - (2.0 * t));
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + ((b - a) * t);
        }

        private readonly struct RollProfile
        {
            public RollProfile(double[] boundaryDistances, double[] segmentRolls)
            {
                BoundaryDistances = boundaryDistances;
                SegmentRolls = segmentRolls;
            }

            public double[] BoundaryDistances { get; }

            public double[] SegmentRolls { get; }
        }

        private sealed class InterpolatedNurbsCurveAdapter : IParamCurve
        {
            private readonly GSharkNurbsBase _curve;

            public InterpolatedNurbsCurveAdapter(GSharkNurbsBase curve)
            {
                _curve = curve ?? throw new ArgumentNullException(nameof(curve));
            }

            public Vector3d Evaluate(double t)
            {
                double clampedT = Clamp01(t);
                return _curve.PointAt(clampedT).ToQuantumVector3d();
            }

            public Vector3d Tangent(double t)
            {
                double clampedT = Clamp01(t);
                Vector3d tangent = _curve.TangentAt(clampedT).ToQuantumVector3d();
                return NormalizeOrThrow(tangent, "tangent");
            }
        }
    }
}
