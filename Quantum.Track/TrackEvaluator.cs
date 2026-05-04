namespace Quantum.Track
{
    public class TrackEvaluator
    {
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
