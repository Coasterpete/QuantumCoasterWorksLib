using Quantum.Math;
using Quantum.Splines;
using Quantum.Track.Internal;
using System.Collections.Generic;
using System.ComponentModel;
using SplineTrackFrame = Quantum.Splines.TrackFrame;

#pragma warning disable CS0618 // Legacy spline-frame compatibility surface.

namespace Quantum.Track
{
    /// <summary>
    /// Evaluates coaster track documents at segment-local positions or station distances.
    /// </summary>
    /// <remarks>
    /// The preferred coaster-facing lane is station-distance sampling from a
    /// bound <see cref="TrackDocument"/> or <see cref="CompiledTrackRuntime"/> into
    /// <see cref="Quantum.Track.TrackFrame"/>
    /// values whose <see cref="Quantum.Track.TrackFrame.Distance"/> is the
    /// clamped global station distance. Overloads that return
    /// <see cref="Quantum.Splines.TrackFrame"/> are support-layer compatibility
    /// APIs; their <c>S</c> values may follow lower-level support semantics rather
    /// than the public global station-distance contract.
    /// </remarks>
    public class TrackEvaluator
    {
        private const double MinimumVectorMagnitude = 1e-9;
        private const double ParallelAxisThreshold = 0.99;
        private readonly TrackDocument? _boundDocument;
        private readonly CompiledTrackSamplingContext? _boundSamplingContext;
        private readonly int _boundSegmentCount;

        /// <summary>
        /// Creates an evaluator for explicit-document evaluation calls.
        /// </summary>
        public TrackEvaluator()
        {
        }

        /// <summary>
        /// Creates an evaluator bound to one live track document for station-distance sampling.
        /// </summary>
        /// <remarks>
        /// Later mutations to the document are observed by subsequent evaluation calls.
        /// Use <see cref="CompiledTrackRuntime"/> for compile-once snapshot semantics.
        /// </remarks>
        public TrackEvaluator(TrackDocument document)
        {
            _boundDocument = document ?? throw new System.ArgumentNullException(nameof(document));
        }

        /// <summary>
        /// Creates an evaluator bound to compile-once sampling state.
        /// </summary>
        public TrackEvaluator(CompiledTrackRuntime runtime)
        {
            if (runtime is null)
            {
                throw new System.ArgumentNullException(nameof(runtime));
            }

            _boundSamplingContext = runtime.SamplingContext;
            _boundSegmentCount = runtime.SegmentCount;
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
            CurveFrame frame = EvaluateCurveFrame(doc, position);
            return Transform3d.FromTrackFrame(frame, frame.Position);
        }

        /// <summary>
        /// Preferred coaster-facing overload. Samples the bound track document at
        /// a station distance and returns the public coaster-domain frame contract.
        /// </summary>
        /// <param name="distance">Station distance along the bound track document. Current behavior clamps finite out-of-range values to the track extents.</param>
        /// <returns>A <see cref="Quantum.Track.TrackFrame"/> with finite, orthonormalized axes and <see cref="Quantum.Track.TrackFrame.Distance"/> equal to the requested clamped global station distance.</returns>
        public TrackFrame EvaluateFrameAtDistance(double distance)
        {
            if (_boundSamplingContext is null)
            {
                TrackDocument doc = ResolveBoundDocument();
                return EvaluateTrackFrameAtDistance(doc, distance);
            }

            ThrowIfDistanceNonFinite(distance);
            ThrowIfEmptyTrack(_boundSegmentCount, distance);
            return _boundSamplingContext.SampleCanonicalFrame(distance, ResolveRollRadians);
        }

        /// <summary>
        /// Samples a track document at a global station distance and returns the
        /// canonical coaster-domain frame.
        /// </summary>
        public TrackFrame EvaluateTrackFrameAtDistance(TrackDocument doc, double distance)
        {
            if (doc is null)
            {
                throw new System.ArgumentNullException(nameof(doc));
            }

            ThrowIfDistanceNonFinite(distance);
            CompiledTrackSamplingContext samplingContext = CompileForDistance(doc, distance);
            return samplingContext.SampleCanonicalFrame(distance, ResolveRollRadians);
        }

        /// <summary>
        /// Returns the total station length of the bound track document.
        /// </summary>
        public double GetBoundTrackTotalLength()
        {
            if (_boundSamplingContext != null)
            {
                return _boundSamplingContext.TotalLength;
            }

            TrackDocument doc = ResolveBoundDocument();
            return CompiledTrackSamplingContext.Compile(doc).TotalLength;
        }

        /// <summary>
        /// Returns the measured geometric station length used by distance sampling.
        /// </summary>
        public double GetTrackTotalLength(TrackDocument doc)
        {
            if (doc is null)
            {
                throw new System.ArgumentNullException(nameof(doc));
            }

            return CompiledTrackSamplingContext.Compile(doc).TotalLength;
        }

        internal TrackDocument GetBoundTrackDocument()
        {
            return ResolveBoundDocument();
        }

        /// <summary>
        /// Explicit support-layer compatibility API for callers that still need
        /// the spline frame contract.
        /// </summary>
        /// <remarks>
        /// This method intentionally returns <see cref="Quantum.Splines.TrackFrame"/>.
        /// Its <c>S</c> value follows support-layer semantics, may be segment-local,
        /// and is not the public global station-distance contract. New
        /// coaster-facing code should prefer a bound evaluator and
        /// <see cref="EvaluateFrameAtDistance(double)"/>, which returns
        /// <see cref="Quantum.Track.TrackFrame"/>.
        /// </remarks>
        [System.Obsolete("Use EvaluateTrackFrameAtDistance for coaster-facing sampling. This method remains for spline-frame compatibility.")]
        public SplineTrackFrame EvaluateSplineFrameAtDistance(TrackDocument doc, double distance)
        {
            if (doc is null)
            {
                throw new System.ArgumentNullException(nameof(doc));
            }

            ThrowIfDistanceNonFinite(distance);
            CompiledTrackSamplingContext samplingContext = CompileForDistance(doc, distance);
            ResolvedTrackDistance resolvedDistance = samplingContext.Resolve(distance);
            TrackFrame frame = samplingContext.SampleCanonicalFrame(distance, ResolveRollRadians);
            return BuildSplineFrame(frame, resolvedDistance.LocalDistance);
        }

        /// <summary>
        /// Support-layer compatibility overload retained for existing spline-backed callers.
        /// </summary>
        /// <remarks>
        /// Prefer <see cref="EvaluateSplineFrameAtDistance(TrackDocument, double)"/>
        /// when the support-layer return type and possible <c>S</c> semantics are
        /// intentional, or bind the evaluator to a document and call
        /// <see cref="EvaluateFrameAtDistance(double)"/> for the preferred
        /// coaster-facing <see cref="Quantum.Track.TrackFrame"/> contract.
        /// </remarks>
        [System.Obsolete("Use EvaluateTrackFrameAtDistance for coaster-facing sampling. This overload remains for compatibility.")]
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

            CompiledTrackSamplingContext samplingContext = CompileForDistance(doc, distances[0]);
            var points = new TrackEvaluationPoint[distanceCount];

            for (int i = 0; i < distanceCount; i++)
            {
                ResolvedTrackDistance resolvedDistance = samplingContext.Resolve(distances[i]);
                points[i] = new TrackEvaluationPoint(resolvedDistance.Segment, resolvedDistance.LocalT);
            }

            return points;
        }

        /// <summary>
        /// Explicit support-layer compatibility API for callers that still need
        /// the spline frame contract.
        /// </summary>
        /// <remarks>
        /// This method intentionally returns <see cref="Quantum.Splines.TrackFrame"/>
        /// values. Their <c>S</c> values follow support-layer semantics and may be
        /// segment-local, and are not the public global station-distance contract.
        /// New coaster-facing code should prefer a bound evaluator and
        /// <see cref="EvaluateFramesAtDistances(IReadOnlyList{double})"/>, which
        /// returns <see cref="Quantum.Track.TrackFrame"/> values.
        /// </remarks>
        [System.Obsolete("Use EvaluateTrackFramesAtDistances for coaster-facing sampling. This method remains for spline-frame compatibility.")]
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

            CompiledTrackSamplingContext samplingContext = CompileForDistance(doc, distances[0]);
            TrackFrame[] canonicalFrames = samplingContext.SampleCanonicalFrames(
                distances,
                ResolveRollRadians);
            var frames = new SplineTrackFrame[distanceCount];

            for (int i = 0; i < distanceCount; i++)
            {
                ResolvedTrackDistance resolvedDistance = samplingContext.Resolve(distances[i]);
                frames[i] = BuildSplineFrame(canonicalFrames[i], resolvedDistance.LocalDistance);
            }

            return frames;
        }

        /// <summary>
        /// Support-layer compatibility overload retained for existing spline-backed callers.
        /// </summary>
        /// <remarks>
        /// Prefer <see cref="EvaluateSplineFramesAtDistances(TrackDocument, IReadOnlyList{double})"/>
        /// when the support-layer return type and possible <c>S</c> semantics are
        /// intentional, or bind the evaluator to a document and call
        /// <see cref="EvaluateFramesAtDistances(IReadOnlyList{double})"/> for the
        /// preferred coaster-facing <see cref="Quantum.Track.TrackFrame"/> contract.
        /// </remarks>
        [System.Obsolete("Use EvaluateTrackFramesAtDistances for coaster-facing sampling. This overload remains for compatibility.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public SplineTrackFrame[] EvaluateFramesAtDistances(
            TrackDocument doc,
            IReadOnlyList<double> distances)
        {
            return EvaluateSplineFramesAtDistances(doc, distances);
        }

        /// <summary>
        /// Preferred coaster-facing overload. Samples the bound track document at
        /// station distances and returns public coaster-domain frame contracts
        /// whose <see cref="Quantum.Track.TrackFrame.Distance"/> values are the
        /// requested clamped global station distances.
        /// </summary>
        /// <param name="distances">Finite station distances. Current behavior clamps finite out-of-range values to the track extents.</param>
        public TrackFrame[] EvaluateFramesAtDistances(IReadOnlyList<double> distances)
        {
            if (_boundSamplingContext is null)
            {
                TrackDocument doc = ResolveBoundDocument();
                return EvaluateTrackFramesAtDistances(doc, distances);
            }

            return EvaluateCanonicalFramesAtDistances(distances, ResolveRollRadians);
        }

        /// <summary>
        /// Samples a track document at global station distances and returns
        /// canonical coaster-domain frames in caller order.
        /// </summary>
        public TrackFrame[] EvaluateTrackFramesAtDistances(
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
                return System.Array.Empty<TrackFrame>();
            }

            for (int i = 0; i < distanceCount; i++)
            {
                ThrowIfDistanceNonFinite(distances[i]);
            }

            CompiledTrackSamplingContext samplingContext = CompileForDistance(doc, distances[0]);
            return samplingContext.SampleCanonicalFrames(distances, ResolveRollRadians);
        }

        public Transform3d EvaluateTransformAtDistance(TrackDocument doc, double distance)
        {
            TrackFrame frame = EvaluateTrackFrameAtDistance(doc, distance);
            return Transform3d.FromTrackFrame(frame, frame.Position);
        }

        /// <summary>
        /// Samples a transform from the bound document or compiled runtime.
        /// </summary>
        public Transform3d EvaluateTransformAtDistance(double distance)
        {
            TrackFrame frame = EvaluateFrameAtDistance(distance);
            return Transform3d.FromTrackFrame(frame, frame.Position);
        }

        /// <summary>
        /// Resolves a station distance against the bound document or compiled runtime.
        /// </summary>
        public TrackEvaluationPoint EvaluateAtDistance(double distance)
        {
            if (_boundSamplingContext is null)
            {
                return EvaluateAtDistance(ResolveBoundDocument(), distance);
            }

            ThrowIfDistanceNonFinite(distance);
            ThrowIfEmptyTrack(_boundSegmentCount, distance);
            return BuildEvaluationPoint(_boundSamplingContext.Resolve(distance));
        }

        /// <summary>
        /// Resolves station distances against the bound document or compiled runtime.
        /// </summary>
        public TrackEvaluationPoint[] EvaluateAtDistances(IReadOnlyList<double> distances)
        {
            if (_boundSamplingContext is null)
            {
                return EvaluateAtDistances(ResolveBoundDocument(), distances);
            }

            ValidateDistances(distances);
            if (distances.Count == 0)
            {
                return System.Array.Empty<TrackEvaluationPoint>();
            }

            ThrowIfEmptyTrack(_boundSegmentCount, distances[0]);
            var points = new TrackEvaluationPoint[distances.Count];
            for (int i = 0; i < distances.Count; i++)
            {
                points[i] = BuildEvaluationPoint(_boundSamplingContext.Resolve(distances[i]));
            }

            return points;
        }

        /// <summary>
        /// Attempts to sample unsigned centerline curvature from the bound document
        /// or compiled runtime.
        /// </summary>
        public bool TryGetCurvatureAtDistance(double distance, out double curvature)
        {
            curvature = 0.0;
            if (double.IsNaN(distance) || double.IsInfinity(distance))
            {
                return false;
            }

            CompiledTrackSamplingContext samplingContext;
            try
            {
                samplingContext = ResolveBoundSamplingContext(distance);
            }
            catch (System.ArgumentOutOfRangeException)
            {
                return false;
            }
            catch (System.InvalidOperationException)
            {
                return false;
            }

            return TryGetCurvatureAtDistance(samplingContext, distance, out curvature);
        }

        internal TrackFrame[] EvaluateCanonicalFramesAtDistances(
            IReadOnlyList<double> distances,
            System.Func<ResolvedTrackDistance, double> rollRadiansResolver)
        {
            if (_boundSamplingContext is null)
            {
                return EvaluateCanonicalFramesAtDistances(
                    ResolveBoundDocument(),
                    distances,
                    rollRadiansResolver);
            }

            if (rollRadiansResolver is null)
            {
                throw new System.ArgumentNullException(nameof(rollRadiansResolver));
            }

            ValidateDistances(distances);
            if (distances.Count == 0)
            {
                return System.Array.Empty<TrackFrame>();
            }

            ThrowIfEmptyTrack(_boundSegmentCount, distances[0]);
            return _boundSamplingContext.SampleCanonicalFrames(distances, rollRadiansResolver);
        }

        internal TrackFrame[] EvaluateCanonicalFramesAtDistances(
            TrackDocument doc,
            IReadOnlyList<double> distances,
            System.Func<ResolvedTrackDistance, double> rollRadiansResolver)
        {
            if (doc is null)
            {
                throw new System.ArgumentNullException(nameof(doc));
            }

            if (distances is null)
            {
                throw new System.ArgumentNullException(nameof(distances));
            }

            if (rollRadiansResolver is null)
            {
                throw new System.ArgumentNullException(nameof(rollRadiansResolver));
            }

            if (distances.Count == 0)
            {
                return System.Array.Empty<TrackFrame>();
            }

            for (int i = 0; i < distances.Count; i++)
            {
                ThrowIfDistanceNonFinite(distances[i]);
            }

            CompiledTrackSamplingContext samplingContext = CompileForDistance(doc, distances[0]);
            return samplingContext.SampleCanonicalFrames(distances, rollRadiansResolver);
        }

        internal CurveFrame[] EvaluateStatelessCurveFramesAtDistances(
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

            if (distances.Count == 0)
            {
                return System.Array.Empty<CurveFrame>();
            }

            for (int i = 0; i < distances.Count; i++)
            {
                ThrowIfDistanceNonFinite(distances[i]);
            }

            CompiledTrackSamplingContext samplingContext = CompileForDistance(doc, distances[0]);
            var frames = new CurveFrame[distances.Count];

            for (int i = 0; i < distances.Count; i++)
            {
                frames[i] = EvaluateResolvedCurveFrame(samplingContext.Resolve(distances[i]));
            }

            return frames;
        }

        [System.Obsolete("Use global station-distance TrackFrame APIs for coaster-facing sampling. This segment-local spline frame remains for compatibility.")]
        public SplineTrackFrame EvaluateFrame(TrackDocument doc, TrackPosition position)
        {
            CurveFrame frame = EvaluateCurveFrame(doc, position);
            return new SplineTrackFrame(
                frame.S,
                frame.Position,
                frame.Tangent,
                frame.Normal,
                frame.Binormal);
        }

        private CurveFrame EvaluateCurveFrame(TrackDocument doc, TrackPosition position)
        {
            if (doc is null)
            {
                throw new System.ArgumentNullException(nameof(doc));
            }

            TrackEvaluationPoint evaluationPoint = EvaluateAt(doc, position);
            double rollRadians = ResolveRollRadians(evaluationPoint);

            if (evaluationPoint.Segment.Spline is IParamCurve spline)
            {
                Vector3d splinePosition = spline.Evaluate(evaluationPoint.LocalT);
                Vector3d splineTangent = NormalizeOrThrow(spline.Tangent(evaluationPoint.LocalT), "tangent");
                return BuildCurveFrame(
                    splinePosition,
                    splineTangent,
                    rollRadians,
                    evaluationPoint.LocalT * evaluationPoint.Segment.Length);
            }

            return EvaluateFallbackCurveFrame(doc, position, evaluationPoint, rollRadians);
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

        private static SplineTrackFrame BuildSplineFrame(TrackFrame sourceFrame, double localDistance)
        {
            return new SplineTrackFrame(
                localDistance,
                sourceFrame.Position,
                sourceFrame.Tangent,
                sourceFrame.Normal,
                sourceFrame.Binormal);
        }

        private static CurveFrame EvaluateFallbackCurveFrame(
            TrackDocument doc,
            TrackPosition position,
            TrackEvaluationPoint evaluationPoint,
            double rollRadians)
        {
            Transform3d fallbackTransform = EvaluateFallbackTransform(doc, position, evaluationPoint);
            return BuildCurveFrame(
                fallbackTransform.Position,
                Vector3d.UnitX,
                rollRadians,
                evaluationPoint.LocalT * evaluationPoint.Segment.Length);
        }

        private static CurveFrame EvaluateResolvedCurveFrame(ResolvedTrackDistance resolvedDistance)
        {
            ThrowIfFrameLocalTInvalid(resolvedDistance.LocalT);

            var evaluationPoint = new TrackEvaluationPoint(
                resolvedDistance.Segment,
                resolvedDistance.LocalT);
            double rollRadians = ResolveRollRadians(evaluationPoint);

            if (resolvedDistance.Segment.Spline is IParamCurve spline)
            {
                Vector3d splinePosition;
                Vector3d splineTangent;

                if (spline is IArcLengthCurve arcLengthCurve)
                {
                    splinePosition = arcLengthCurve.EvaluateByLength(resolvedDistance.LocalDistance);
                    splineTangent = NormalizeOrThrow(
                        arcLengthCurve.TangentByLength(resolvedDistance.LocalDistance),
                        "tangent");
                }
                else
                {
                    splinePosition = spline.Evaluate(resolvedDistance.LocalT);
                    splineTangent = NormalizeOrThrow(
                        spline.Tangent(resolvedDistance.LocalT),
                        "tangent");
                }

                return BuildCurveFrame(
                    splinePosition,
                    splineTangent,
                    rollRadians,
                    resolvedDistance.LocalDistance);
            }

            return BuildCurveFrame(
                new Vector3d(resolvedDistance.ClampedDistance, 0.0, 0.0),
                Vector3d.UnitX,
                rollRadians,
                resolvedDistance.LocalDistance);
        }

        private static CurveFrame BuildCurveFrame(
            Vector3d position,
            Vector3d tangent,
            double rollRadians,
            double localDistance)
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

            return new CurveFrame(localDistance, position, normalizedTangent, normal, binormal);
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

        private static double ResolveRollRadians(ResolvedTrackDistance resolvedDistance)
        {
            return ResolveRollRadians(new TrackEvaluationPoint(
                resolvedDistance.Segment,
                resolvedDistance.LocalT));
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

        private TrackDocument ResolveBoundDocument()
        {
            if (_boundDocument is null)
            {
                throw new System.InvalidOperationException(
                    "TrackEvaluator is not bound to a TrackDocument. Use the constructor overload that accepts TrackDocument or call the overload that accepts a TrackDocument argument.");
            }

            return _boundDocument;
        }

        private CompiledTrackSamplingContext ResolveBoundSamplingContext(double distance)
        {
            if (_boundSamplingContext != null)
            {
                ThrowIfEmptyTrack(_boundSegmentCount, distance);
                return _boundSamplingContext;
            }

            return CompileForDistance(ResolveBoundDocument(), distance);
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

            CompiledTrackSamplingContext samplingContext = CompileForDistance(doc, distance);
            ResolvedTrackDistance resolvedDistance = samplingContext.Resolve(distance);
            return BuildEvaluationPoint(resolvedDistance);
        }

        private static CompiledTrackSamplingContext CompileForDistance(
            TrackDocument doc,
            double distance)
        {
            ThrowIfEmptyTrack(doc.Segments.Count, distance);

            return CompiledTrackSamplingContext.Compile(doc);
        }

        private static TrackEvaluationPoint BuildEvaluationPoint(
            ResolvedTrackDistance resolvedDistance)
        {
            return new TrackEvaluationPoint(
                resolvedDistance.Segment,
                resolvedDistance.LocalT);
        }

        private static void ValidateDistances(IReadOnlyList<double> distances)
        {
            if (distances is null)
            {
                throw new System.ArgumentNullException(nameof(distances));
            }

            for (int i = 0; i < distances.Count; i++)
            {
                ThrowIfDistanceNonFinite(distances[i]);
            }
        }

        private static void ThrowIfEmptyTrack(int segmentCount, double distance)
        {
            if (segmentCount == 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(distance),
                    distance,
                    "Distance cannot be evaluated for an empty track document.");
            }
        }

        private static bool TryGetCurvatureAtDistance(
            CompiledTrackSamplingContext samplingContext,
            double distance,
            out double curvature)
        {
            curvature = 0.0;
            ResolvedTrackDistance resolvedDistance = samplingContext.Resolve(distance);

            if (resolvedDistance.Segment.Spline is IParamCurveCurvature curvatureCurve &&
                curvatureCurve.TryGetCurvature(resolvedDistance.LocalT, out double splineCurvature) &&
                !double.IsNaN(splineCurvature) &&
                !double.IsInfinity(splineCurvature))
            {
                curvature = System.Math.Abs(splineCurvature);
                return true;
            }

            double totalLength = samplingContext.TotalLength;
            if (totalLength <= MathUtil.Epsilon)
            {
                return true;
            }

            double clampedDistance = MathUtil.Clamp(distance, 0.0, totalLength);
            double deltaS = System.Math.Max(totalLength * 1e-3, 1e-4);
            double previousDistance = MathUtil.Clamp(clampedDistance - deltaS, 0.0, totalLength);
            double nextDistance = MathUtil.Clamp(clampedDistance + deltaS, 0.0, totalLength);
            double span = nextDistance - previousDistance;
            if (span <= MathUtil.Epsilon)
            {
                return true;
            }

            TrackFrame previousFrame = samplingContext.SampleCanonicalFrame(
                previousDistance,
                ResolveRollRadians);
            TrackFrame nextFrame = samplingContext.SampleCanonicalFrame(
                nextDistance,
                ResolveRollRadians);
            curvature = (nextFrame.Tangent - previousFrame.Tangent).Length / span;

            if (double.IsNaN(curvature) || double.IsInfinity(curvature))
            {
                curvature = 0.0;
                return false;
            }

            return true;
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

    }
}

#pragma warning restore CS0618
