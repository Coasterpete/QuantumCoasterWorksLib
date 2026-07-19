namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Immutable directed connection between two authoring graph node IDs.
    /// </summary>
    public sealed class TrackAuthoringGraphEdge
    {
        public TrackAuthoringGraphEdge(string sourceNodeId, string targetNodeId)
        {
            SourceNodeId = sourceNodeId ?? string.Empty;
            TargetNodeId = targetNodeId ?? string.Empty;
        }

        public string SourceNodeId { get; }

        public string TargetNodeId { get; }
    }
}
