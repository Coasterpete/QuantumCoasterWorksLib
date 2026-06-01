using Quantum.Math;
using Quantum.Splines;
using Quantum.Track.Internal;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SplineTrackFrame = Quantum.Splines.TrackFrame;

namespace Quantum.Track
{
    /// <summary>
    /// Evaluates coaster track documents at segment-local positions or station distances.
    /// </summary>
    /// <remarks>
    /// The public coaster-domain lane is station-distance sampling from a
    /// <see cref="TrackDocument"/> into <see cref="TrackFrame"/> values.
    /// Spline-backed overloads remain implementation/support-layer compatibility
    /// points and should not be treated as the consumer-facing API boundary.
    /// </remarks>
    public class TrackEvaluator
    {
        private const double MinimumVectorMagnitude = 1e-9;
        private const double ParallelAxisThreshold = 0.99;
        private readonly TrackDocument? _boundDocument;

        /// <summary>
        /// Creates an evaluator for explicit-document evaluation calls.
        /// </summary>
        public TrackEvaluator()
        {
        }

        /// <summary>
        /// Creates an evaluator bound to one track document for station-distance frame sampling.
        /// </summary>
        public TrackEvaluator(TrackDocument document)
        {
            _boundDocument = document ?? throw new System.ArgumentNullException(nameof(document));
        }

        public TrackEvaluationResult Evaluate(TrackDocument document)
        {
            if (document is null)
            {
                throw new System.ArgumentNullException(nameof(document));
            }

            int evaluatedSegmentCount = 0;

            for (int i = 0; i < document.Segments.Count; i++)
            {
                if (document.Segments[i] is null)
                {
                    return new TrackEvaluationResult(
                        success: false,
                        evaluatedSegmentCount: evaluatedSegmentCount,
                        error: "TrackDocument contains a null segment entry.");
                }

                evaluatedSegmentCount++;
            }

            return new TrackEvaluationResult(
                success: true,
                evaluatedSegmentCount: evaluatedSegmentCount,
                message: "Track evaluation completed.");
        }

        public TrackEvaluationPoint EvaluateAt(TrackDocument doc, TrackPosition position)
        {
            if (doc is null)
            {
                throw new System.ArgumentNullException(nameof(doc));
            }

            if (position.SegmentIndex < 0 || position.SegmentIndex >= doc.Segments.Count)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(position.SegmentIndex),
                    position.SegmentIndex,
                    "SegmentIndex must reference an existing segment in the track document.");
            }

            if (double.IsNaN(position.LocalT) || double.IsInfinity(position.LocalT) || position.LocalT < 0.0 || position.LocalT > 1.0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(position.LocalT),
                    position.LocalT,
                    "LocalT must be finite and within [0.0, 1.0].");
            }

            TrackSegment segment = doc.Segments[position.SegmentIndex];

            if (segment is null)
            {
                throw new System.InvalidOperationException("TrackDocument contains a null segment entry.");
            }

            return new TrackEvaluationPoint(segment, position.LocalT);
        }

        public Transform3d EvaluateTransform(TrackDocument doc, TrackPosition position)
        {
            SplineTrackFrame frame = EvaluateFrame(doc, position);
            return Transform3d.FromTrackFrame(frame, frame.Position);
        }

        /// <summary>
        /// Samples the bound track document at a station distance and returns the
        /// public coaster-domain frame contract.
        /// </summary>
        /// <param name="distance">Station distance along the bound track document. Current behavior clamps finite out-of-range values to the track extents.</param>
        /// <returns>A <see cref="TrackFrame"/> with finite, orthonormalized axes and <see cref="TrackFrame.Distance"/> equal to the requested clamped global station distance.</returns>
        public TrackFrame EvaluateFrameAtDistance(double distance)
        {
            TrackDocument doc = ResolveBoundDocument();
            SplineTrackFrame splineFrame = EvaluateSplineFrameAtDistance(doc, distance);
            return BuildExportFrame(splineFrame, ClampStationDistance(doc, distance));
        }

        /// <summary>
        /// Returns the total station length of the bound track document.
        /// </summary>
        public double GetBoundTrackTotalLength()
        {
            TrackDocument doc = ResolveBoundDocument();
            return doc.TotalLength;
        }

        internal TrackDocument GetBoundTrackDocument()
        {
            return ResolveBoundDocument();
        }

        /// <summary>
        /// Explicit support-layer frame sampling method for callers that still need
        /// the spline frame contract.
        /// </summary>
        /// <remarks>
        /// This method intentionally returns <see cref="Quantum.Splines.TrackFrame"/>.
        /// Its <c>S</c> value follows support-layer semantics and may be
        /// segment-local. It is not the public global station-distance contract.
        /// New coaster-facing code should prefer a bound evaluator and
        /// <see cref="EvaluateFrameAtDistance(double)"/>, which returns
        /// <see cref="TrackFrame"/>.
        /// </remarks>
        public SplineTrackFrame EvaluateSplineFrameAtDistance(TrackDocument doc, double distance)
        {
            TrackEvaluationPoint evaluationPoint = EvaluateAtDistance(doc, distance);
            TrackPosition position = ResolveTrackPosition(doc, evaluationPoint);
            return EvaluateFrame(doc, position);
        }

        /// <summary>
        /// Compatibility overload retained for existing spline-backed callers.
        /// </summary>
        /// <remarks>
        /// Prefer <see cref="EvaluateSplineFrameAtDistance(TrackDocument, double)"/>
        /// when the support-layer return type is intentional, or bind the evaluator
        /// to a document and call <see cref="EvaluateFrameAtDistance(double)"/> for
        /// the public coaster-domain frame contract.
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public SplineTrackFrame EvaluateFrameAtDistance(TrackDocument doc, double distance)
        {
            return EvaluateSplineFrameAtDistance(doc, distance);
        }

        /// <summary>
        /// Resolves station distances to segment/local samples in a track document.
        /// </summary>
        /// <param name="doc">Track document whose ordered segments define the station coordinate.</param>
        /// <param name="distances">Finite station distances. Current behavior clamps finite out-of-range values to the track extents.</param>
        public TrackEvaluationPoint[] EvaluateAtDistances(
            TrackDocument doc,
            IReadOnlyList<double> distances)
        {
            if (doc is null)
            {
                throw new System.ArgumentNullException(nameof(doc));
            }

            if (distances is null)
            {
                throw new System.ArgumentNullException(nameof(distances));
            }

            int distanceCount = distances.Count;
            if (distanceCount == 0)
            {
                return System.Array.Empty<TrackEvaluationPoint>();
            }

            for (int i = 0; i < distanceCount; i++)
            {
                ThrowIfDistanceNonFinite(distances[i]);
            }

            if (doc.Segments.Count == 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    "distance",
                    distances[0],
                    "Distance cannot be evaluated for an empty track document.");
            }

            CompiledTrackSamplingContext samplingContext = CompiledTrackSamplingContext.Compile(doc);
            var points = new TrackEvaluationPoint[distanceCount];

            for (int i = 0; i < distanceCount; i++)
            {
                ResolvedTrackDistance resolvedDistance = samplingContext.Resolve(distances[i]);
                points[i] = new TrackEvaluationPoint(resolvedDistance.Segment, resolvedDistance.LocalT);
            }

            return points;
        }

        /// <summary>
        /// Explicit support-layer batch frame sampling method for callers that still
        /// need the spline frame contract.
        /// </summary>
        /// <remarks>
        /// This method intentionally returns <see cref="Quantum.Splines.TrackFrame"/>
        /// values. Their <c>S</c> values follow support-layer semantics and may be
        /// segment-local. New coaster-facing code should prefer a bound evaluator and
        /// <see cref="EvaluateFramesAtDistances(IReadOnlyList{double})"/>, which
        /// returns <see cref="TrackFrame"/> values.
        /// </remarks>
        public SplineTrackFrame[] EvaluateSplineFramesAtDistances(
            TrackDocument doc,
            IReadOnlyList<double> distances)
        {
            if (doc is null)
            {
                throw new System.ArgumentNullException(nameof(doc));
            }

            if (distances is null)
            {
                throw new System.ArgumentNullException(nameof(distances));
            }

            int distanceCount = distances.Count;
            if (distanceCount == 0)
            {
                return System.Array.Empty<SplineTrackFrame>();
            }

            for (int i = 0; i < distanceCount; i++)
            {
                ThrowIfDistanceNonFinite(distances[i]);
            }

            if (doc.Segments.Count == 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    "distance",
                    distances[0],
                    "Distance cannot be evaluated for an empty track document.");
            }

            CompiledTrackSamplingContext samplingContext = CompiledTrackSamplingContext.Compile(doc);
            Dictionary<TrackSegment, int>? firstSegmentIndicesByReference = null;
            var frames = new SplineTrackFrame[distanceCount];

            for (int i = 0; i < distanceCount; i++)
            {
                ResolvedTrackDistance resolvedDistance = samplingContext.Resolve(distances[i]);

                if (resolvedDistance.Segment.Spline is IParamCurve spline)
                {
                    ThrowIfFrameLocalTInvalid(resolvedDistance.LocalT);

                    var evaluationPoint = new TrackEvaluationPoint(
                        resolvedDistance.Segment,
                        resolvedDistance.LocalT);
                    double rollRadians = ResolveRollRadians(evaluationPoint);
                    Vector3d splinePosition = spline.Evaluate(resolvedDistance.LocalT);
                    Vector3d splineTangent = NormalizeOrThrow(spline.Tangent(resolvedDistance.LocalT), "tangent");
                    frames[i] = BuildTrackFrame(evaluationPoint, splinePosition, splineTangent, rollRadians);
                    continue;
                }

                firstSegmentIndicesByReference ??= BuildFirstSegmentIndicesByReference(doc);
                TrackPosition position = ResolveTrackPosition(firstSegmentIndicesByReference, resolvedDistance);
                frames[i] = EvaluateFrame(doc, position);
            }

            return frames;
        }

        /// <summary>
        /// Compatibility overload retained for existing spline-backed callers.
        /// </summary>
        /// <remarks>
        /// Prefer <see cref="EvaluateSplineFramesAtDistances(TrackDocument, IReadOnlyList{double})"/>
        /// when the support-layer return type is intentional, or bind the evaluator
        /// to a document and call <see cref="EvaluateFramesAtDistances(IReadOnlyList{double})"/>
        /// for the public coaster-domain frame contract.
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public SplineTrackFrame[] EvaluateFramesAtDistances(
            TrackDocument doc,
            IReadOnlyList<double> distances)
        {
            return EvaluateSplineFramesAtDistances(doc, distances);
        }

        /// <summary>
        /// Samples the bound track document at station distances and returns public
        /// coaster-domain frame contracts whose <see cref="TrackFrame.Distance"/>
        /// values are the requested clamped global station distances.
        /// </summary>
        /// <param name="distances">Finite station distances. Current behavior clamps finite out-of-range values to the track extents.</param>
        public TrackFrame[] EvaluateFramesAtDistances(IReadOnlyList<double> distances)
        {
            TrackDocument doc = ResolveBoundDocument();
            SplineTrackFrame[] splineFrames = EvaluateSplineFramesAtDistances(doc, distances);
            double[] stationDistances = ClampStationDistances(doc, distances);
            var exportFrames = new TrackFrame[splineFrames.Length];

            for (int i = 0; i < splineFrames.Length; i++)
            {
                exportFrames[i] = BuildExportFrame(splineFrames[i], stationDistances[i]);
            }

            return exportFrames;
        }

        public Transform3d EvaluateTransformAtDistance(TrackDocument doc, double distance)
        {
            TrackEvaluationPoint evaluationPoint = EvaluateAtDistance(doc, distance);
            TrackPosition position = ResolveTrackPosition(doc, evaluationPoint);
            return EvaluateTransform(doc, position);
        }

        public SplineTrackFrame EvaluateFrame(TrackDocument doc, TrackPosition position)
        {
            TrackEvaluationPoint evaluationPoint = EvaluateAt(doc, position);
            double rollRadians = ResolveRollRadians(evaluationPoint);

            if (evaluationPoint.Segment.Spline is IParamCurve spline)
            {
                Vector3d splinePosition = spline.Evaluate(evaluationPoint.LocalT);
                Vector3d splineTangent = NormalizeOrThrow(spline.Tangent(evaluationPoint.LocalT), "tangent");
                return BuildTrackFrame(evaluationPoint, splinePosition, splineTangent, rollRadians);
            }

            return EvaluateFallbackFrame(doc, position, evaluationPoint, rollRadians);
        }

        private static Transform3d EvaluateFallbackTransform(TrackDocument doc, TrackPosition position, TrackEvaluationPoint evaluationPoint)
        {
            double distanceAlongTrack = 0.0;

            for (int i = 0; i < position.SegmentIndex; i++)
            {
                TrackSegment priorSegment = doc.Segments[i];

                if (priorSegment is null)
                {
                    throw new System.InvalidOperationException("TrackDocument contains a null segment entry.");
                }

                distanceAlongTrack += priorSegment.Length;
            }

            distanceAlongTrack += evaluationPoint.LocalT * evaluationPoint.Segment.Length;

            return new Transform3d(
                Matrix3x3.Identity,
                new Vector3d(distanceAlongTrack, 0.0, 0.0));
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

        private static double ClampStationDistance(TrackDocument doc, double distance)
        {
            return System.Math.Max(0.0, System.Math.Min(distance, doc.TotalLength));
        }

        private static double[] ClampStationDistances(TrackDocument doc, IReadOnlyList<double> distances)
        {
            if (distances is null)
            {
                throw new System.ArgumentNullException(nameof(distances));
            }

            int distanceCount = distances.Count;
            if (distanceCount == 0)
            {
                return System.Array.Empty<double>();
            }

            double totalLength = doc.TotalLength;
            var stationDistances = new double[distanceCount];

            for (int i = 0; i < distanceCount; i++)
            {
                stationDistances[i] = System.Math.Max(0.0, System.Math.Min(distances[i], totalLength));
            }

            return stationDistances;
        }

        private static SplineTrackFrame EvaluateFallbackFrame(
            TrackDocument doc,
            TrackPosition position,
            TrackEvaluationPoint evaluationPoint,
            double rollRadians)
        {
            Transform3d fallbackTransform = EvaluateFallbackTransform(doc, position, evaluationPoint);
            return BuildTrackFrame(evaluationPoint, fallbackTransform.Position, Vector3d.UnitX, rollRadians);
        }

        private static SplineTrackFrame BuildTrackFrame(
            TrackEvaluationPoint evaluationPoint,
            Vector3d position,
            Vector3d tangent,
            double rollRadians)
        {
            Vector3d normalizedTangent = NormalizeOrThrow(tangent, "tangent");
            Vector3d referenceUp = SelectReferenceUp(normalizedTangent);
            Vector3d binormal = NormalizeOrThrow(Vector3d.Cross(normalizedTangent, referenceUp), "binormal");
            Vector3d normal = NormalizeOrThrow(Vector3d.Cross(binormal, normalizedTangent), "normal");

            if (rollRadians != 0.0)
            {
                normal = NormalizeOrThrow(RotateAroundAxis(normal, normalizedTangent, rollRadians), "normal");
                binormal = NormalizeOrThrow(RotateAroundAxis(binormal, normalizedTangent, rollRadians), "binormal");
            }

            double s = evaluationPoint.LocalT * evaluationPoint.Segment.Length;

            return new SplineTrackFrame(s, position, normalizedTangent, normal, binormal);
        }

        private static double ResolveRollRadians(TrackEvaluationPoint evaluationPoint)
        {
            double rollRadians = evaluationPoint.Segment.RollRadians;
            if (double.IsNaN(rollRadians) || double.IsInfinity(rollRadians))
            {
                throw new System.InvalidOperationException("Track segment roll must be finite.");
            }

            return rollRadians;
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

        private static Vector3d NormalizeOrThrow(Vector3d vector, string label)
        {
            if (!IsFinite(vector))
            {
                throw new System.InvalidOperationException($"Unable to normalize {label}: vector contains non-finite components.");
            }

            double length = vector.Length;
            if (length <= MinimumVectorMagnitude)
            {
                throw new System.InvalidOperationException($"Unable to normalize {label}: vector magnitude is near zero.");
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

        private static TrackPosition ResolveTrackPosition(TrackDocument doc, TrackEvaluationPoint evaluationPoint)
        {
            for (int i = 0; i < doc.Segments.Count; i++)
            {
                if (object.ReferenceEquals(doc.Segments[i], evaluationPoint.Segment))
                {
                    return new TrackPosition(i, evaluationPoint.LocalT);
                }
            }

            throw new System.InvalidOperationException("TrackDocument could not resolve the evaluated segment.");
        }

        private static TrackPosition ResolveTrackPosition(
            IReadOnlyDictionary<TrackSegment, int> firstSegmentIndicesByReference,
            ResolvedTrackDistance resolvedDistance)
        {
            if (!firstSegmentIndicesByReference.TryGetValue(resolvedDistance.Segment, out int segmentIndex))
            {
                throw new System.InvalidOperationException("TrackDocument could not resolve the evaluated segment.");
            }

            return new TrackPosition(segmentIndex, resolvedDistance.LocalT);
        }

        private static Dictionary<TrackSegment, int> BuildFirstSegmentIndicesByReference(TrackDocument doc)
        {
            var firstSegmentIndicesByReference = new Dictionary<TrackSegment, int>(
                doc.Segments.Count,
                ReferenceIdentityComparer<TrackSegment>.Instance);

            for (int i = 0; i < doc.Segments.Count; i++)
            {
                TrackSegment segment = doc.Segments[i];

                if (!firstSegmentIndicesByReference.ContainsKey(segment))
                {
                    firstSegmentIndicesByReference.Add(segment, i);
                }
            }

            return firstSegmentIndicesByReference;
        }

        private TrackDocument ResolveBoundDocument()
        {
            if (_boundDocument is null)
            {
                throw new System.InvalidOperationException(
                    "TrackEvaluator is not bound to a TrackDocument. Use the constructor overload that accepts TrackDocument or call the overload that accepts a TrackDocument argument.");
            }

            return _boundDocument;
        }

        /// <summary>
        /// Resolves a station distance into the segment and local parameter used for
        /// centerline evaluation.
        /// </summary>
        /// <remarks>
        /// This is the stable station-distance sampling contract. Finite distances
        /// outside the document range are clamped; non-finite distances and empty
        /// documents are rejected.
        /// </remarks>
        public TrackEvaluationPoint EvaluateAtDistance(TrackDocument doc, double distance)
        {
            if (doc is null)
            {
                throw new System.ArgumentNullException(nameof(doc));
            }

            if (double.IsNaN(distance) || double.IsInfinity(distance))
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(distance),
                    distance,
                    "Distance must be finite.");
            }

            if (doc.Segments.Count == 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(distance),
                    distance,
                    "Distance cannot be evaluated for an empty track document.");
            }

            CompiledTrackSamplingContext samplingContext = CompiledTrackSamplingContext.Compile(doc);
            ResolvedTrackDistance resolvedDistance = samplingContext.Resolve(distance);
            return new TrackEvaluationPoint(resolvedDistance.Segment, resolvedDistance.LocalT);
        }

        private static void ThrowIfDistanceNonFinite(double distance)
        {
            if (double.IsNaN(distance) || double.IsInfinity(distance))
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(distance),
                    distance,
                    "Distance must be finite.");
            }
        }

        private static void ThrowIfFrameLocalTInvalid(double localT)
        {
            if (double.IsNaN(localT) || double.IsInfinity(localT) || localT < 0.0 || localT > 1.0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(TrackPosition.LocalT),
                    localT,
                    "LocalT must be finite and within [0.0, 1.0].");
            }
        }

        private sealed class ReferenceIdentityComparer<TReference> : IEqualityComparer<TReference>
            where TReference : class
        {
            public static ReferenceIdentityComparer<TReference> Instance { get; } =
                new ReferenceIdentityComparer<TReference>();

            public bool Equals(TReference? x, TReference? y)
            {
                return object.ReferenceEquals(x, y);
            }

            public int GetHashCode(TReference obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
