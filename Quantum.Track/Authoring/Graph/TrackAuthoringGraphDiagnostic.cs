namespace Quantum.Track.Authoring
{
    public enum TrackAuthoringGraphDiagnosticCode
    {
        EmptyGraph = 0,
        DuplicateNodeId = 1,
        UnknownEdgeEndpoint = 2,
        DuplicateEdge = 3,
        MultipleIncomingEdges = 4,
        MultipleOutgoingEdges = 5,
        CycleDetected = 6,
        DisconnectedNode = 7,
        AuthoringCompilationFailed = 8,
        UnsupportedSectionFamily = 9,
        CandidateOperationFailed = 10
    }

    /// <summary>
    /// Structured graph validation or downstream authoring compilation diagnostic.
    /// </summary>
    public sealed class TrackAuthoringGraphDiagnostic
    {
        public TrackAuthoringGraphDiagnostic(
            TrackAuthoringGraphDiagnosticCode code,
            string message,
            string? nodeId = null,
            string? sourceNodeId = null,
            string? targetNodeId = null)
        {
            Code = code;
            Message = message ?? string.Empty;
            NodeId = nodeId;
            SourceNodeId = sourceNodeId;
            TargetNodeId = targetNodeId;
        }

        public TrackAuthoringGraphDiagnosticCode Code { get; }

        public string Message { get; }

        public string? NodeId { get; }

        public string? SourceNodeId { get; }

        public string? TargetNodeId { get; }
    }
}
