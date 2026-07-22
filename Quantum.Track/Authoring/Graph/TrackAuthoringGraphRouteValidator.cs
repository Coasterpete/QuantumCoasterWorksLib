using System;
using System.Collections.Generic;
using System.Linq;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Validates and deterministically linearizes graph topology without compiling
    /// any section payloads.
    /// </summary>
    /// <remarks>
    /// An empty graph is a valid authoring route here. Consumers that require
    /// compiled geometry, including <see cref="TrackAuthoringGraphCompiler"/>,
    /// apply their own non-empty requirement.
    /// </remarks>
    public static class TrackAuthoringGraphRouteValidator
    {
        public static TrackAuthoringGraphRouteResult Validate(TrackAuthoringGraph graph)
        {
            if (graph is null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            if (graph.Nodes.Count == 0)
            {
                return graph.Edges.Count == 0
                    ? Success(Array.Empty<TrackAuthoringGraphNode>())
                    : Failure(new[]
                    {
                        new TrackAuthoringGraphDiagnostic(
                            TrackAuthoringGraphDiagnosticCode.UnknownEdgeEndpoint,
                            "An empty authoring graph cannot contain edges.")
                    });
            }

            var diagnostics = new List<TrackAuthoringGraphDiagnostic>();
            Dictionary<string, TrackAuthoringGraphNode>? nodesById = BuildNodeIndex(
                graph.Nodes,
                diagnostics);
            if (nodesById is null)
            {
                return Failure(diagnostics);
            }

            var incoming = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var outgoing = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                string nodeId = graph.Nodes[i].Id;
                incoming.Add(nodeId, new List<string>());
                outgoing.Add(nodeId, new List<string>());
            }

            AddEdges(graph.Edges, nodesById, incoming, outgoing, diagnostics);
            if (diagnostics.Count != 0)
            {
                return Failure(diagnostics);
            }

            ValidateDegree(graph.Nodes, incoming, outgoing, diagnostics);
            if (diagnostics.Count != 0)
            {
                return Failure(diagnostics);
            }

            if (HasCycle(graph.Nodes, incoming, outgoing, out IReadOnlyList<string> cycleNodeIds))
            {
                diagnostics.Add(new TrackAuthoringGraphDiagnostic(
                    TrackAuthoringGraphDiagnosticCode.CycleDetected,
                    "The authoring graph contains a cycle involving node IDs: " +
                    string.Join(", ", cycleNodeIds) + "."));
                return Failure(diagnostics);
            }

            List<TrackAuthoringGraphNode> orderedNodes = BuildSingleRoute(
                graph.Nodes,
                nodesById,
                incoming,
                outgoing,
                diagnostics);
            return diagnostics.Count == 0
                ? Success(orderedNodes)
                : Failure(diagnostics, orderedNodes);
        }

        private static Dictionary<string, TrackAuthoringGraphNode>? BuildNodeIndex(
            IReadOnlyList<TrackAuthoringGraphNode> nodes,
            ICollection<TrackAuthoringGraphDiagnostic> diagnostics)
        {
            var result = new Dictionary<string, TrackAuthoringGraphNode>(StringComparer.Ordinal);
            for (int i = 0; i < nodes.Count; i++)
            {
                TrackAuthoringGraphNode node = nodes[i];
                if (!result.ContainsKey(node.Id))
                {
                    result.Add(node.Id, node);
                    continue;
                }

                diagnostics.Add(new TrackAuthoringGraphDiagnostic(
                    TrackAuthoringGraphDiagnosticCode.DuplicateNodeId,
                    $"Graph node ID '{node.Id}' is duplicated.",
                    nodeId: node.Id));
            }

            return diagnostics.Count == 0 ? result : null;
        }

        private static void AddEdges(
            IReadOnlyList<TrackAuthoringGraphEdge> edges,
            IReadOnlyDictionary<string, TrackAuthoringGraphNode> nodesById,
            IDictionary<string, List<string>> incoming,
            IDictionary<string, List<string>> outgoing,
            ICollection<TrackAuthoringGraphDiagnostic> diagnostics)
        {
            var seenEdges = new HashSet<(string Source, string Target)>();
            for (int i = 0; i < edges.Count; i++)
            {
                TrackAuthoringGraphEdge edge = edges[i];
                bool sourceExists = nodesById.ContainsKey(edge.SourceNodeId);
                bool targetExists = nodesById.ContainsKey(edge.TargetNodeId);

                if (!sourceExists || !targetExists)
                {
                    string missing = !sourceExists && !targetExists
                        ? "source and target node IDs"
                        : !sourceExists ? "source node ID" : "target node ID";
                    diagnostics.Add(new TrackAuthoringGraphDiagnostic(
                        TrackAuthoringGraphDiagnosticCode.UnknownEdgeEndpoint,
                        $"Graph edge '{edge.SourceNodeId}' -> '{edge.TargetNodeId}' has unknown {missing}.",
                        sourceNodeId: edge.SourceNodeId,
                        targetNodeId: edge.TargetNodeId));
                    continue;
                }

                if (!seenEdges.Add((edge.SourceNodeId, edge.TargetNodeId)))
                {
                    diagnostics.Add(new TrackAuthoringGraphDiagnostic(
                        TrackAuthoringGraphDiagnosticCode.DuplicateEdge,
                        $"Graph edge '{edge.SourceNodeId}' -> '{edge.TargetNodeId}' is duplicated.",
                        sourceNodeId: edge.SourceNodeId,
                        targetNodeId: edge.TargetNodeId));
                    continue;
                }

                outgoing[edge.SourceNodeId].Add(edge.TargetNodeId);
                incoming[edge.TargetNodeId].Add(edge.SourceNodeId);
            }
        }

        private static void ValidateDegree(
            IReadOnlyList<TrackAuthoringGraphNode> nodes,
            IReadOnlyDictionary<string, List<string>> incoming,
            IReadOnlyDictionary<string, List<string>> outgoing,
            ICollection<TrackAuthoringGraphDiagnostic> diagnostics)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                string nodeId = nodes[i].Id;
                if (incoming[nodeId].Count > 1)
                {
                    diagnostics.Add(new TrackAuthoringGraphDiagnostic(
                        TrackAuthoringGraphDiagnosticCode.MultipleIncomingEdges,
                        $"Graph node ID '{nodeId}' has {incoming[nodeId].Count} incoming edges; merging is not supported.",
                        nodeId: nodeId));
                }

                if (outgoing[nodeId].Count > 1)
                {
                    diagnostics.Add(new TrackAuthoringGraphDiagnostic(
                        TrackAuthoringGraphDiagnosticCode.MultipleOutgoingEdges,
                        $"Graph node ID '{nodeId}' has {outgoing[nodeId].Count} outgoing edges; branching is not supported.",
                        nodeId: nodeId));
                }
            }
        }

        private static bool HasCycle(
            IReadOnlyList<TrackAuthoringGraphNode> nodes,
            IReadOnlyDictionary<string, List<string>> incoming,
            IReadOnlyDictionary<string, List<string>> outgoing,
            out IReadOnlyList<string> cycleNodeIds)
        {
            var remainingIncoming = new Dictionary<string, int>(StringComparer.Ordinal);
            var ready = new Queue<string>();
            for (int i = 0; i < nodes.Count; i++)
            {
                string nodeId = nodes[i].Id;
                int count = incoming[nodeId].Count;
                remainingIncoming.Add(nodeId, count);
                if (count == 0)
                {
                    ready.Enqueue(nodeId);
                }
            }

            int visitedCount = 0;
            while (ready.Count != 0)
            {
                string nodeId = ready.Dequeue();
                visitedCount++;
                List<string> successors = outgoing[nodeId];
                for (int i = 0; i < successors.Count; i++)
                {
                    string successor = successors[i];
                    remainingIncoming[successor]--;
                    if (remainingIncoming[successor] == 0)
                    {
                        ready.Enqueue(successor);
                    }
                }
            }

            if (visitedCount == nodes.Count)
            {
                cycleNodeIds = Array.Empty<string>();
                return false;
            }

            cycleNodeIds = nodes
                .Where(node => remainingIncoming[node.Id] > 0)
                .Select(node => node.Id)
                .ToArray();
            return true;
        }

        private static List<TrackAuthoringGraphNode> BuildSingleRoute(
            IReadOnlyList<TrackAuthoringGraphNode> nodes,
            IReadOnlyDictionary<string, TrackAuthoringGraphNode> nodesById,
            IReadOnlyDictionary<string, List<string>> incoming,
            IReadOnlyDictionary<string, List<string>> outgoing,
            ICollection<TrackAuthoringGraphDiagnostic> diagnostics)
        {
            string[] startNodeIds = nodes
                .Where(node => incoming[node.Id].Count == 0)
                .Select(node => node.Id)
                .ToArray();
            string[] endNodeIds = nodes
                .Where(node => outgoing[node.Id].Count == 0)
                .Select(node => node.Id)
                .ToArray();

            if (startNodeIds.Length != 1 || endNodeIds.Length != 1)
            {
                diagnostics.Add(new TrackAuthoringGraphDiagnostic(
                    TrackAuthoringGraphDiagnosticCode.DisconnectedNode,
                    "A deterministic linear graph must have exactly one start and one end node; " +
                    $"found starts [{string.Join(", ", startNodeIds)}] and " +
                    $"ends [{string.Join(", ", endNodeIds)}]."));
                return new List<TrackAuthoringGraphNode>();
            }

            var orderedNodes = new List<TrackAuthoringGraphNode>(nodes.Count);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            string currentNodeId = startNodeIds[0];
            while (visited.Add(currentNodeId))
            {
                orderedNodes.Add(nodesById[currentNodeId]);
                List<string> successors = outgoing[currentNodeId];
                if (successors.Count == 0)
                {
                    break;
                }

                currentNodeId = successors[0];
            }

            if (orderedNodes.Count == nodes.Count)
            {
                return orderedNodes;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                TrackAuthoringGraphNode node = nodes[i];
                if (!visited.Contains(node.Id))
                {
                    diagnostics.Add(new TrackAuthoringGraphDiagnostic(
                        TrackAuthoringGraphDiagnosticCode.DisconnectedNode,
                        $"Graph node ID '{node.Id}' is disconnected from the single compiled route.",
                        nodeId: node.Id));
                }
            }

            return orderedNodes;
        }

        private static TrackAuthoringGraphRouteResult Success(
            IEnumerable<TrackAuthoringGraphNode> orderedNodes)
        {
            return new TrackAuthoringGraphRouteResult(
                orderedNodes,
                Array.Empty<TrackAuthoringGraphDiagnostic>());
        }

        private static TrackAuthoringGraphRouteResult Failure(
            IEnumerable<TrackAuthoringGraphDiagnostic> diagnostics,
            IEnumerable<TrackAuthoringGraphNode>? orderedNodes = null)
        {
            return new TrackAuthoringGraphRouteResult(
                orderedNodes ?? Array.Empty<TrackAuthoringGraphNode>(),
                diagnostics);
        }
    }
}
