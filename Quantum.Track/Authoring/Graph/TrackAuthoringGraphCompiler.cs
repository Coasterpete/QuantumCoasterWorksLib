using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Pure service that validates, deterministically linearizes, and compiles an
    /// immutable authoring graph through the existing backend authoring pipeline.
    /// </summary>
    public static class TrackAuthoringGraphCompiler
    {
        public static TrackAuthoringGraphCompileResult Compile(TrackAuthoringGraph graph)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                return CompileCore(graph);
            }
            finally
            {
                stopwatch.Stop();
                TrackAuthoringPipelineMeasurement.RecordGraphCompilation(stopwatch.Elapsed);
            }
        }

        private static TrackAuthoringGraphCompileResult CompileCore(TrackAuthoringGraph graph)
        {
            if (graph is null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            var diagnostics = new List<TrackAuthoringGraphDiagnostic>();
            if (graph.Nodes.Count == 0)
            {
                diagnostics.Add(new TrackAuthoringGraphDiagnostic(
                    TrackAuthoringGraphDiagnosticCode.EmptyGraph,
                    "The authoring graph must contain at least one section node."));
                return Failure(diagnostics);
            }

            TrackAuthoringGraphRouteResult route =
                TrackAuthoringGraphRouteValidator.Validate(graph);
            if (!route.Success)
            {
                return Failure(route.Diagnostics, route.OrderedNodes);
            }

            var orderedNodes = new List<TrackAuthoringGraphNode>(route.OrderedNodes);
            for (int i = 0; i < orderedNodes.Count; i++)
            {
                TrackAuthoringGraphNode node = orderedNodes[i];
                if (node.Section is GeometricSectionDefinition)
                {
                    continue;
                }

                diagnostics.Add(new TrackAuthoringGraphDiagnostic(
                    TrackAuthoringGraphDiagnosticCode.UnsupportedSectionFamily,
                    $"Section type '{node.TypeId}' in family '{node.Family}' does not have a route compiler.",
                    nodeId: node.Id));
            }

            if (diagnostics.Count != 0)
            {
                return Failure(diagnostics, orderedNodes);
            }

            TrackAuthoringDefinition definition;
            try
            {
                GeometricSectionDefinition[] sections = orderedNodes
                    .Select(node => (GeometricSectionDefinition)node.Section)
                    .ToArray();
                definition = graph.Banking is null
                    ? new TrackAuthoringDefinition(sections, graph.StartPose)
                    : new TrackAuthoringDefinition(sections, graph.StartPose, graph.Banking);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is NotSupportedException)
            {
                diagnostics.Add(CreateCompilationDiagnostic(exception));
                return Failure(diagnostics, orderedNodes);
            }

            try
            {
                TrackAuthoringCompilation compilation =
                    TrackAuthoringDocumentBuilder.Compile(definition);
                return new TrackAuthoringGraphCompileResult(
                    true,
                    orderedNodes,
                    definition,
                    compilation,
                    Array.Empty<TrackAuthoringGraphDiagnostic>());
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is NotSupportedException)
            {
                diagnostics.Add(CreateCompilationDiagnostic(exception));
                return new TrackAuthoringGraphCompileResult(
                    false,
                    orderedNodes,
                    definition,
                    null,
                    diagnostics);
            }
        }

        private static TrackAuthoringGraphDiagnostic CreateCompilationDiagnostic(Exception exception)
        {
            return new TrackAuthoringGraphDiagnostic(
                TrackAuthoringGraphDiagnosticCode.AuthoringCompilationFailed,
                "The linear authoring graph could not be compiled by the existing backend pipeline: " +
                exception.Message);
        }

        private static TrackAuthoringGraphCompileResult Failure(
            IEnumerable<TrackAuthoringGraphDiagnostic> diagnostics,
            IEnumerable<TrackAuthoringGraphNode>? orderedNodes = null)
        {
            return new TrackAuthoringGraphCompileResult(
                false,
                orderedNodes ?? Array.Empty<TrackAuthoringGraphNode>(),
                null,
                null,
                diagnostics);
        }
    }
}
