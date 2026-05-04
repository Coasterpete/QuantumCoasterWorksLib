namespace Quantum.Track
{
    public sealed class TrackEvaluationResult
    {
        public TrackEvaluationResult(bool success, int evaluatedSegmentCount, string? message = null, string? error = null)
        {
            Success = success;
            Message = message;
            Error = error;
            EvaluatedSegmentCount = evaluatedSegmentCount;
        }

        public bool Success { get; }

        public string? Message { get; }

        public string? Error { get; }

        public int EvaluatedSegmentCount { get; }
    }
}