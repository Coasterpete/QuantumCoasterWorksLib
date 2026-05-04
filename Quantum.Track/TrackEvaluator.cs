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
    }
}