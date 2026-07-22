using System;
using System.Collections.Generic;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Immutable result returned by the pure graph validation/compilation service.
    /// </summary>
    public sealed class TrackAuthoringGraphCompileResult
    {
        private readonly IReadOnlyList<TrackAuthoringGraphNode> _orderedNodes;
        private readonly IReadOnlyList<TrackAuthoringGraphDiagnostic> _diagnostics;

        internal TrackAuthoringGraphCompileResult(
            TrackAuthoringGraph sourceGraph,
            bool success,
            IEnumerable<TrackAuthoringGraphNode> orderedNodes,
            TrackAuthoringDefinition? definition,
            TrackAuthoringCompilation? compilation,
            IEnumerable<TrackAuthoringGraphDiagnostic> diagnostics)
        {
            if (sourceGraph is null)
            {
                throw new ArgumentNullException(nameof(sourceGraph));
            }

            if (orderedNodes is null)
            {
                throw new ArgumentNullException(nameof(orderedNodes));
            }

            if (diagnostics is null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            SourceGraph = sourceGraph;
            Success = success;
            _orderedNodes = new List<TrackAuthoringGraphNode>(orderedNodes).AsReadOnly();
            Definition = definition;
            Compilation = compilation;
            _diagnostics = new List<TrackAuthoringGraphDiagnostic>(diagnostics).AsReadOnly();
        }

        public bool Success { get; }

        /// <summary>
        /// Gets the immutable graph instance evaluated to produce this result.
        /// </summary>
        public TrackAuthoringGraph SourceGraph { get; }

        public IReadOnlyList<TrackAuthoringGraphNode> OrderedNodes => _orderedNodes;

        public TrackAuthoringDefinition? Definition { get; }

        public TrackAuthoringCompilation? Compilation { get; }

        public IReadOnlyList<TrackAuthoringGraphDiagnostic> Diagnostics => _diagnostics;
    }
}
