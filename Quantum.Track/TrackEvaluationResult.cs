namespace Quantum.Track
{
    /// <summary>
    /// Lightweight validation summary for evaluating a <see cref="TrackDocument"/>.
    /// </summary>
    public sealed class TrackEvaluationResult
    {
        /// <summary>
        /// Creates a track evaluation summary.
        /// </summary>
        /// <param name="success">Whether evaluation validation completed successfully.</param>
        /// <param name="evaluatedSegmentCount">Number of segments examined before completion.</param>
        /// <param name="message">Optional success or diagnostic message.</param>
        /// <param name="error">Optional error message when validation fails.</param>
        public TrackEvaluationResult(bool success, int evaluatedSegmentCount, string? message = null, string? error = null)
        {
            Success = success;
            Message = message;
            Error = error;
            EvaluatedSegmentCount = evaluatedSegmentCount;
        }

        /// <summary>
        /// Whether evaluation validation completed successfully.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Optional success or diagnostic message.
        /// </summary>
        public string? Message { get; }

        /// <summary>
        /// Optional error message when validation fails.
        /// </summary>
        public string? Error { get; }

        /// <summary>
        /// Number of segments examined before completion.
        /// </summary>
        public int EvaluatedSegmentCount { get; }
    }
}
