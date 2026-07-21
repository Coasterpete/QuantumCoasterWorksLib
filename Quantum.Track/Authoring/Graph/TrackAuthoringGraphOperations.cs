using System;
using System.Collections.Generic;
using System.Linq;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Pure immutable operations for one deterministic linear authoring route.
    /// </summary>
    public static class TrackAuthoringGraphOperations
    {
        public static TrackAuthoringGraph Append(
            TrackAuthoringGraph graph,
            TrackAuthoringSectionDefinition section)
        {
            IReadOnlyList<TrackAuthoringGraphNode> orderedNodes = GetOrderedNodes(graph);
            ValidateNewSection(section, orderedNodes);

            var replacement = new List<TrackAuthoringGraphNode>(orderedNodes)
            {
                new TrackAuthoringGraphNode(section)
            };
            return Rebuild(graph, replacement);
        }

        public static TrackAuthoringGraph InsertBefore(
            TrackAuthoringGraph graph,
            string anchorNodeId,
            TrackAuthoringSectionDefinition section)
        {
            return Insert(graph, anchorNodeId, section, insertAfter: false);
        }

        public static TrackAuthoringGraph InsertAfter(
            TrackAuthoringGraph graph,
            string anchorNodeId,
            TrackAuthoringSectionDefinition section)
        {
            return Insert(graph, anchorNodeId, section, insertAfter: true);
        }

        public static TrackAuthoringGraph Delete(
            TrackAuthoringGraph graph,
            string nodeId)
        {
            IReadOnlyList<TrackAuthoringGraphNode> orderedNodes = GetOrderedNodes(graph);
            int index = FindNodeIndex(orderedNodes, nodeId, nameof(nodeId));
            var replacement = new List<TrackAuthoringGraphNode>(orderedNodes);
            replacement.RemoveAt(index);
            return Rebuild(graph, replacement);
        }

        public static TrackAuthoringGraph MoveBefore(
            TrackAuthoringGraph graph,
            string nodeId,
            string anchorNodeId)
        {
            return Move(graph, nodeId, anchorNodeId, moveAfter: false);
        }

        public static TrackAuthoringGraph MoveAfter(
            TrackAuthoringGraph graph,
            string nodeId,
            string anchorNodeId)
        {
            return Move(graph, nodeId, anchorNodeId, moveAfter: true);
        }

        public static TrackAuthoringGraph Replace(
            TrackAuthoringGraph graph,
            string nodeId,
            TrackAuthoringSectionDefinition replacement)
        {
            if (graph is null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            return graph.WithSection(nodeId, replacement);
        }

        private static TrackAuthoringGraph Insert(
            TrackAuthoringGraph graph,
            string anchorNodeId,
            TrackAuthoringSectionDefinition section,
            bool insertAfter)
        {
            IReadOnlyList<TrackAuthoringGraphNode> orderedNodes = GetOrderedNodes(graph);
            ValidateNewSection(section, orderedNodes);
            int anchorIndex = FindNodeIndex(orderedNodes, anchorNodeId, nameof(anchorNodeId));

            var replacement = new List<TrackAuthoringGraphNode>(orderedNodes);
            replacement.Insert(
                insertAfter ? anchorIndex + 1 : anchorIndex,
                new TrackAuthoringGraphNode(section));
            return Rebuild(graph, replacement);
        }

        private static TrackAuthoringGraph Move(
            TrackAuthoringGraph graph,
            string nodeId,
            string anchorNodeId,
            bool moveAfter)
        {
            IReadOnlyList<TrackAuthoringGraphNode> orderedNodes = GetOrderedNodes(graph);
            int movingIndex = FindNodeIndex(orderedNodes, nodeId, nameof(nodeId));
            FindNodeIndex(orderedNodes, anchorNodeId, nameof(anchorNodeId));

            if (string.Equals(nodeId, anchorNodeId, StringComparison.Ordinal))
            {
                return graph;
            }

            var replacement = new List<TrackAuthoringGraphNode>(orderedNodes);
            TrackAuthoringGraphNode movingNode = replacement[movingIndex];
            replacement.RemoveAt(movingIndex);
            int adjustedAnchorIndex = replacement.FindIndex(node =>
                string.Equals(node.Id, anchorNodeId, StringComparison.Ordinal));
            replacement.Insert(
                moveAfter ? adjustedAnchorIndex + 1 : adjustedAnchorIndex,
                movingNode);

            return SameOrder(orderedNodes, replacement)
                ? graph
                : Rebuild(graph, replacement);
        }

        private static IReadOnlyList<TrackAuthoringGraphNode> GetOrderedNodes(
            TrackAuthoringGraph graph)
        {
            if (graph is null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            TrackAuthoringGraphRouteResult route =
                TrackAuthoringGraphRouteValidator.Validate(graph);
            if (route.Success)
            {
                return route.OrderedNodes;
            }

            throw new InvalidOperationException(
                "The source authoring graph is not a deterministic linear route: " +
                string.Join(
                    " ",
                    route.Diagnostics.Select(diagnostic =>
                        $"{diagnostic.Code}: {diagnostic.Message}")));
        }

        private static void ValidateNewSection(
            TrackAuthoringSectionDefinition section,
            IReadOnlyList<TrackAuthoringGraphNode> orderedNodes)
        {
            if (section is null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            if (orderedNodes.Any(node =>
                    string.Equals(node.Id, section.Id, StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    $"Graph node ID '{section.Id}' already exists.",
                    nameof(section));
            }
        }

        private static int FindNodeIndex(
            IReadOnlyList<TrackAuthoringGraphNode> orderedNodes,
            string nodeId,
            string parameterName)
        {
            if (nodeId is null)
            {
                throw new ArgumentNullException(parameterName);
            }

            for (int i = 0; i < orderedNodes.Count; i++)
            {
                if (string.Equals(orderedNodes[i].Id, nodeId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            throw new ArgumentException(
                $"Graph node ID '{nodeId}' was not found.",
                parameterName);
        }

        private static TrackAuthoringGraph Rebuild(
            TrackAuthoringGraph source,
            IReadOnlyList<TrackAuthoringGraphNode> orderedNodes)
        {
            var edges = new TrackAuthoringGraphEdge[System.Math.Max(0, orderedNodes.Count - 1)];
            for (int i = 1; i < orderedNodes.Count; i++)
            {
                edges[i - 1] = new TrackAuthoringGraphEdge(
                    orderedNodes[i - 1].Id,
                    orderedNodes[i].Id);
            }

            return new TrackAuthoringGraph(
                orderedNodes,
                edges,
                source.StartPose,
                source.Banking);
        }

        private static bool SameOrder(
            IReadOnlyList<TrackAuthoringGraphNode> first,
            IReadOnlyList<TrackAuthoringGraphNode> second)
        {
            if (first.Count != second.Count)
            {
                return false;
            }

            for (int i = 0; i < first.Count; i++)
            {
                if (!string.Equals(first[i].Id, second[i].Id, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
