using System;
using System.Collections.Generic;
using System.Linq;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Produces deterministic neighbor-aware defaults for the initial geometric catalog.
    /// </summary>
    /// <remarks>
    /// Roll prefers the immediate upstream section, then the immediate downstream section,
    /// then zero. Constant curvature prefers a finite-radius non-zero upstream endpoint,
    /// then downstream, then the positive 25-unit radius fallback. Transitions bridge two
    /// supported endpoints, copy a sole supported endpoint, or fall back to zero-to-zero.
    /// Spatial and future non-geometric endpoints are deliberately unavailable rather than
    /// approximated; the result flags identify that condition and the fallback selected from
    /// the remaining context. Insertion station is nullable when a preceding timeline node
    /// does not yet expose a station extent through the current production contracts.
    /// </remarks>
    public static class TrackAuthoringSectionDefaultFactory
    {
        public const double DefaultLength = 10.0;
        public const double PositiveRadiusFallback = 25.0;

        public static TrackAuthoringSectionDefaults ForAppend(
            TrackAuthoringGraph graph,
            string sectionTypeId,
            string sectionId)
        {
            IReadOnlyList<TrackAuthoringGraphNode> orderedNodes = GetAuthoringRoute(graph);
            return Create(orderedNodes, orderedNodes.Count, replacing: false, sectionTypeId, sectionId);
        }

        public static TrackAuthoringSectionDefaults ForInsertBefore(
            TrackAuthoringGraph graph,
            string anchorNodeId,
            string sectionTypeId,
            string sectionId)
        {
            IReadOnlyList<TrackAuthoringGraphNode> orderedNodes = GetAuthoringRoute(graph);
            int anchorIndex = FindNodeIndex(orderedNodes, anchorNodeId, nameof(anchorNodeId));
            return Create(orderedNodes, anchorIndex, replacing: false, sectionTypeId, sectionId);
        }

        public static TrackAuthoringSectionDefaults ForInsertAfter(
            TrackAuthoringGraph graph,
            string anchorNodeId,
            string sectionTypeId,
            string sectionId)
        {
            IReadOnlyList<TrackAuthoringGraphNode> orderedNodes = GetAuthoringRoute(graph);
            int anchorIndex = FindNodeIndex(orderedNodes, anchorNodeId, nameof(anchorNodeId));
            return Create(orderedNodes, anchorIndex + 1, replacing: false, sectionTypeId, sectionId);
        }

        public static TrackAuthoringSectionDefaults ForReplacement(
            TrackAuthoringGraph graph,
            string nodeId,
            string sectionTypeId)
        {
            IReadOnlyList<TrackAuthoringGraphNode> orderedNodes = GetAuthoringRoute(graph);
            int nodeIndex = FindNodeIndex(orderedNodes, nodeId, nameof(nodeId));
            return Create(orderedNodes, nodeIndex, replacing: true, sectionTypeId, nodeId);
        }

        private static TrackAuthoringSectionDefaults Create(
            IReadOnlyList<TrackAuthoringGraphNode> orderedNodes,
            int positionIndex,
            bool replacing,
            string sectionTypeId,
            string sectionId)
        {
            if (sectionTypeId is null)
            {
                throw new ArgumentNullException(nameof(sectionTypeId));
            }

            AuthoringValidation.RequireId(sectionId);

            TrackAuthoringSectionDefaultFlags flags = TrackAuthoringSectionDefaultFlags.None;
            double? insertionStation = 0.0;
            for (int i = 0; i < positionIndex; i++)
            {
                if (orderedNodes[i].Section is GeometricSectionDefinition geometry &&
                    insertionStation.HasValue)
                {
                    double nextStation = insertionStation.Value + geometry.Length;
                    if (AuthoringValidation.IsFinite(nextStation))
                    {
                        insertionStation = nextStation;
                        continue;
                    }
                }

                insertionStation = null;
                flags |= TrackAuthoringSectionDefaultFlags.InsertionStationUnavailable;
            }

            TrackAuthoringSectionDefinition? upstream = positionIndex > 0
                ? orderedNodes[positionIndex - 1].Section
                : null;
            int downstreamIndex = replacing ? positionIndex + 1 : positionIndex;
            TrackAuthoringSectionDefinition? downstream = downstreamIndex < orderedNodes.Count
                ? orderedNodes[downstreamIndex].Section
                : null;

            double inheritedRoll;
            if (TryGetRoll(upstream, out double upstreamRoll))
            {
                inheritedRoll = upstreamRoll;
                flags |= TrackAuthoringSectionDefaultFlags.RollInheritedFromUpstream;
            }
            else if (TryGetRoll(downstream, out double downstreamRoll))
            {
                if (upstream != null)
                {
                    flags |= TrackAuthoringSectionDefaultFlags.UpstreamRollUnavailable;
                }

                inheritedRoll = downstreamRoll;
                flags |= TrackAuthoringSectionDefaultFlags.RollInheritedFromDownstream;
            }
            else
            {
                if (upstream != null)
                {
                    flags |= TrackAuthoringSectionDefaultFlags.UpstreamRollUnavailable;
                }

                if (downstream != null)
                {
                    flags |= TrackAuthoringSectionDefaultFlags.DownstreamRollUnavailable;
                }

                inheritedRoll = 0.0;
                flags |= TrackAuthoringSectionDefaultFlags.ZeroRollFallback;
            }

            double? upstreamEndCurvature = GetEndCurvature(upstream, ref flags);
            double? downstreamStartCurvature = GetStartCurvature(downstream, ref flags);
            TrackAuthoringSectionDefinition definition = CreateDefinition(
                sectionTypeId,
                sectionId,
                inheritedRoll,
                upstreamEndCurvature,
                downstreamStartCurvature,
                ref flags);

            return new TrackAuthoringSectionDefaults(
                definition,
                insertionStation,
                upstream?.Id,
                downstream?.Id,
                upstreamEndCurvature,
                downstreamStartCurvature,
                inheritedRoll,
                flags);
        }

        private static TrackAuthoringSectionDefinition CreateDefinition(
            string sectionTypeId,
            string sectionId,
            double rollRadians,
            double? upstreamEndCurvature,
            double? downstreamStartCurvature,
            ref TrackAuthoringSectionDefaultFlags flags)
        {
            switch (sectionTypeId)
            {
                case TrackAuthoringSectionTypeIds.Straight:
                    return new StraightSectionDefinition(sectionId, DefaultLength, rollRadians);

                case TrackAuthoringSectionTypeIds.ConstantCurvature:
                    if (TryUseFiniteRadius(upstreamEndCurvature, out double upstreamRadius))
                    {
                        flags |= TrackAuthoringSectionDefaultFlags.CurvatureInheritedFromUpstream;
                        return new ConstantCurvatureSectionDefinition(
                            sectionId,
                            DefaultLength,
                            upstreamRadius,
                            rollRadians);
                    }

                    if (TryUseFiniteRadius(downstreamStartCurvature, out double downstreamRadius))
                    {
                        flags |= TrackAuthoringSectionDefaultFlags.CurvatureInheritedFromDownstream;
                        return new ConstantCurvatureSectionDefinition(
                            sectionId,
                            DefaultLength,
                            downstreamRadius,
                            rollRadians);
                    }

                    flags |= TrackAuthoringSectionDefaultFlags.PositiveRadiusFallback;
                    return new ConstantCurvatureSectionDefinition(
                        sectionId,
                        DefaultLength,
                        PositiveRadiusFallback,
                        rollRadians);

                case TrackAuthoringSectionTypeIds.CurvatureTransition:
                    double startCurvature;
                    double endCurvature;
                    if (upstreamEndCurvature.HasValue && downstreamStartCurvature.HasValue)
                    {
                        startCurvature = upstreamEndCurvature.Value;
                        endCurvature = downstreamStartCurvature.Value;
                        flags |= TrackAuthoringSectionDefaultFlags.TransitionBridgesNeighbors;
                    }
                    else if (upstreamEndCurvature.HasValue)
                    {
                        startCurvature = upstreamEndCurvature.Value;
                        endCurvature = upstreamEndCurvature.Value;
                        flags |= TrackAuthoringSectionDefaultFlags.CurvatureInheritedFromUpstream;
                    }
                    else if (downstreamStartCurvature.HasValue)
                    {
                        startCurvature = downstreamStartCurvature.Value;
                        endCurvature = downstreamStartCurvature.Value;
                        flags |= TrackAuthoringSectionDefaultFlags.CurvatureInheritedFromDownstream;
                    }
                    else
                    {
                        startCurvature = 0.0;
                        endCurvature = 0.0;
                        flags |= TrackAuthoringSectionDefaultFlags.ZeroCurvatureFallback;
                    }

                    return new CurvatureTransitionSectionDefinition(
                        sectionId,
                        DefaultLength,
                        startCurvature,
                        endCurvature,
                        CurvatureTransitionInterpolationMode.Linear,
                        rollRadians);

                default:
                    throw new NotSupportedException(
                        $"Section type '{sectionTypeId}' does not have initial authoring defaults.");
            }
        }

        private static bool TryUseFiniteRadius(double? curvature, out double radius)
        {
            if (!curvature.HasValue || curvature.Value == 0.0)
            {
                radius = 0.0;
                return false;
            }

            try
            {
                radius = TrackAuthoringScalarCurvature.ToSignedRadius(curvature.Value);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                radius = 0.0;
                return false;
            }
        }

        private static bool TryGetRoll(
            TrackAuthoringSectionDefinition? section,
            out double rollRadians)
        {
            if (section is GeometricSectionDefinition geometry)
            {
                rollRadians = geometry.RollRadians;
                return true;
            }

            rollRadians = 0.0;
            return false;
        }

        private static double? GetEndCurvature(
            TrackAuthoringSectionDefinition? section,
            ref TrackAuthoringSectionDefaultFlags flags)
        {
            if (section is null)
            {
                return null;
            }

            if (TrackAuthoringScalarCurvature.TryGetEndCurvature(section, out double curvature))
            {
                return curvature;
            }

            flags |= TrackAuthoringSectionDefaultFlags.UpstreamScalarCurvatureUnavailable;
            return null;
        }

        private static double? GetStartCurvature(
            TrackAuthoringSectionDefinition? section,
            ref TrackAuthoringSectionDefaultFlags flags)
        {
            if (section is null)
            {
                return null;
            }

            if (TrackAuthoringScalarCurvature.TryGetStartCurvature(section, out double curvature))
            {
                return curvature;
            }

            flags |= TrackAuthoringSectionDefaultFlags.DownstreamScalarCurvatureUnavailable;
            return null;
        }

        private static IReadOnlyList<TrackAuthoringGraphNode> GetAuthoringRoute(
            TrackAuthoringGraph graph)
        {
            if (graph is null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            TrackAuthoringGraphRouteResult route = TrackAuthoringGraphRouteValidator.Validate(graph);
            if (!route.Success)
            {
                throw new InvalidOperationException(
                    "Section defaults require a deterministic linear authoring route: " +
                    string.Join(
                        " ",
                        route.Diagnostics.Select(diagnostic =>
                            $"{diagnostic.Code}: {diagnostic.Message}")));
            }

            return route.OrderedNodes;
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

            throw new ArgumentException($"Graph node ID '{nodeId}' was not found.", parameterName);
        }
    }
}
