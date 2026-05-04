using Quantum.Math;
using Quantum.Splines;

namespace Quantum.Track
{
    public class TrackEvaluator
    {
        private const double MinimumVectorMagnitude = 1e-9;
        private const double ParallelAxisThreshold = 0.99;

        public TrackEvaluator()
        {
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
            TrackFrame frame = EvaluateFrame(doc, position);
            return Transform3d.FromTrackFrame(frame, frame.Position);
        }

        public TrackFrame EvaluateFrame(TrackDocument doc, TrackPosition position)
        {
            TrackEvaluationPoint evaluationPoint = EvaluateAt(doc, position);

            if (evaluationPoint.Segment.Spline is IParamCurve spline)
            {
                Vector3d splinePosition = spline.Evaluate(evaluationPoint.LocalT);
                Vector3d splineTangent = NormalizeOrThrow(spline.Tangent(evaluationPoint.LocalT), "tangent");
                return BuildTrackFrame(evaluationPoint, splinePosition, splineTangent);
            }

            return EvaluateFallbackFrame(doc, position, evaluationPoint);
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

        private static TrackFrame EvaluateFallbackFrame(TrackDocument doc, TrackPosition position, TrackEvaluationPoint evaluationPoint)
        {
            Transform3d fallbackTransform = EvaluateFallbackTransform(doc, position, evaluationPoint);
            return BuildTrackFrame(evaluationPoint, fallbackTransform.Position, Vector3d.UnitX);
        }

        private static TrackFrame BuildTrackFrame(TrackEvaluationPoint evaluationPoint, Vector3d position, Vector3d tangent)
        {
            Vector3d normalizedTangent = NormalizeOrThrow(tangent, "tangent");
            Vector3d referenceUp = SelectReferenceUp(normalizedTangent);
            Vector3d binormal = NormalizeOrThrow(Vector3d.Cross(normalizedTangent, referenceUp), "binormal");
            Vector3d normal = NormalizeOrThrow(Vector3d.Cross(binormal, normalizedTangent), "normal");
            double s = evaluationPoint.LocalT * evaluationPoint.Segment.Length;

            return new TrackFrame(s, position, normalizedTangent, normal, binormal);
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

            double totalLength = doc.TotalLength;
            double clampedDistance = System.Math.Max(0.0, System.Math.Min(distance, totalLength));
            double segmentStart = 0.0;

            for (int i = 0; i < doc.Segments.Count; i++)
            {
                TrackSegment segment = doc.Segments[i];

                if (segment is null)
                {
                    throw new System.InvalidOperationException("TrackDocument contains a null segment entry.");
                }

                double segmentEnd = segmentStart + segment.Length;
                bool isLastSegment = i == doc.Segments.Count - 1;

                if (clampedDistance < segmentEnd || isLastSegment)
                {
                    if (segment.Length <= 0.0)
                    {
                        return new TrackEvaluationPoint(segment, 0.0);
                    }

                    double localT = (clampedDistance - segmentStart) / segment.Length;
                    localT = System.Math.Max(0.0, System.Math.Min(localT, 1.0));
                    return new TrackEvaluationPoint(segment, localT);
                }

                segmentStart = segmentEnd;
            }

            throw new System.InvalidOperationException("TrackDocument could not be evaluated at the specified distance.");
        }
    }
}
