using System;
using System.Collections.Generic;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Immutable editor-facing graph snapshot for authored coaster sections.
    /// </summary>
    /// <remarks>
    /// The graph stores coaster-domain inputs only. It has no UI layout, renderer,
    /// serialization, or active-document ownership behavior.
    /// </remarks>
    public sealed class TrackAuthoringGraph
    {
        private readonly IReadOnlyList<TrackAuthoringGraphNode> _nodes;
        private readonly IReadOnlyList<TrackAuthoringGraphEdge> _edges;

        public TrackAuthoringGraph(
            IEnumerable<TrackAuthoringGraphNode> nodes,
            IEnumerable<TrackAuthoringGraphEdge> edges)
            : this(nodes, edges, TrackStartPose.Identity, null)
        {
        }

        public TrackAuthoringGraph(
            IEnumerable<TrackAuthoringGraphNode> nodes,
            IEnumerable<TrackAuthoringGraphEdge> edges,
            TrackStartPose startPose,
            TrackBankingDefinition? banking)
        {
            if (nodes is null)
            {
                throw new ArgumentNullException(nameof(nodes));
            }

            if (edges is null)
            {
                throw new ArgumentNullException(nameof(edges));
            }

            StartPose = startPose ?? throw new ArgumentNullException(nameof(startPose));

            var copiedNodes = new List<TrackAuthoringGraphNode>();
            foreach (TrackAuthoringGraphNode node in nodes)
            {
                if (node is null)
                {
                    throw new ArgumentException("Graph node entries cannot be null.", nameof(nodes));
                }

                copiedNodes.Add(node);
            }

            var copiedEdges = new List<TrackAuthoringGraphEdge>();
            foreach (TrackAuthoringGraphEdge edge in edges)
            {
                if (edge is null)
                {
                    throw new ArgumentException("Graph edge entries cannot be null.", nameof(edges));
                }

                copiedEdges.Add(edge);
            }

            _nodes = copiedNodes.AsReadOnly();
            _edges = copiedEdges.AsReadOnly();
            Banking = banking;
        }

        public IReadOnlyList<TrackAuthoringGraphNode> Nodes => _nodes;

        public IReadOnlyList<TrackAuthoringGraphEdge> Edges => _edges;

        public TrackStartPose StartPose { get; }

        public TrackBankingDefinition? Banking { get; }

        /// <summary>
        /// Returns a new graph with one node's immutable section definition replaced.
        /// </summary>
        public TrackAuthoringGraph WithSection(
            string nodeId,
            GeometricSectionDefinition replacement)
        {
            if (nodeId is null)
            {
                throw new ArgumentNullException(nameof(nodeId));
            }

            if (replacement is null)
            {
                throw new ArgumentNullException(nameof(replacement));
            }

            if (!string.Equals(nodeId, replacement.Id, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A replacement section must preserve the graph node ID.",
                    nameof(replacement));
            }

            var replacedNodes = new List<TrackAuthoringGraphNode>(_nodes.Count);
            int replacementCount = 0;
            for (int i = 0; i < _nodes.Count; i++)
            {
                TrackAuthoringGraphNode node = _nodes[i];
                if (string.Equals(node.Id, nodeId, StringComparison.Ordinal))
                {
                    replacedNodes.Add(new TrackAuthoringGraphNode(replacement));
                    replacementCount++;
                }
                else
                {
                    replacedNodes.Add(node);
                }
            }

            if (replacementCount == 0)
            {
                throw new ArgumentException(
                    $"Graph node ID '{nodeId}' was not found.",
                    nameof(nodeId));
            }

            if (replacementCount != 1)
            {
                throw new InvalidOperationException(
                    $"Graph node ID '{nodeId}' is duplicated and cannot be replaced unambiguously.");
            }

            return new TrackAuthoringGraph(replacedNodes, _edges, StartPose, Banking);
        }
    }
}
