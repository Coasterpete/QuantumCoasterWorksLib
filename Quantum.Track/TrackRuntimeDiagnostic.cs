namespace Quantum.Track
{
    public enum TrackRuntimeDiagnosticSeverity
    {
        Warning = 0,
        Error = 1
    }

    public enum TrackRuntimeDiagnosticCode
    {
        EmptyTrack = 0,
        NullSegment = 1,
        InvalidDeclaredLength = 2,
        InvalidRoll = 3,
        SplineMeasurementFailed = 4,
        InvalidMeasuredLength = 5,
        ReportedArcLengthMismatch = 6,
        DeclaredLengthMismatch = 7,
        SplineTangentEvaluationFailed = 8,
        InvalidSplineTangent = 9,
        SamplingCapacityExceeded = 10
    }

    /// <summary>
    /// One deterministic validation or compilation diagnostic for a track runtime.
    /// </summary>
    public sealed class TrackRuntimeDiagnostic
    {
        internal TrackRuntimeDiagnostic(
            TrackRuntimeDiagnosticCode code,
            TrackRuntimeDiagnosticSeverity severity,
            string message,
            int? segmentIndex = null,
            string? segmentId = null,
            double? splineParameter = null)
        {
            Code = code;
            Severity = severity;
            Message = message;
            SegmentIndex = segmentIndex;
            SegmentId = segmentId;
            SplineParameter = splineParameter;
        }

        public TrackRuntimeDiagnosticCode Code { get; }

        public TrackRuntimeDiagnosticSeverity Severity { get; }

        public string Message { get; }

        public int? SegmentIndex { get; }

        public string? SegmentId { get; }

        public double? SplineParameter { get; }

        public double? Parameter => SplineParameter;

        public double? LocalT => SplineParameter;
    }
}
