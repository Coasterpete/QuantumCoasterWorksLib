using System;
using System.Collections.Generic;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Immutable topology-validation result for one deterministic linear route.
    /// </summary>
    public sealed class TrackAuthoringGraphRouteResult
    {
        private readonly IReadOnlyList<TrackAuthoringGraphNode> _orderedNodes;
        private readonly IReadOnlyList<TrackAuthoringGraphDiagnostic> _diagnostics;

        internal TrackAuthoringGraphRouteResult(
            IEnumerable<TrackAuthoringGraphNode> orderedNodes,
            IEnumerable<TrackAuthoringGraphDiagnostic> diagnostics)
        {
            _orderedNodes = new List<TrackAuthoringGraphNode>(
                orderedNodes ?? throw new ArgumentNullException(nameof(orderedNodes))).AsReadOnly();
            _diagnostics = new List<TrackAuthoringGraphDiagnostic>(
                diagnostics ?? throw new ArgumentNullException(nameof(diagnostics))).AsReadOnly();
        }

        public bool Success => _diagnostics.Count == 0;

        public IReadOnlyList<TrackAuthoringGraphNode> OrderedNodes => _orderedNodes;

        public IReadOnlyList<TrackAuthoringGraphDiagnostic> Diagnostics => _diagnostics;
    }
}
