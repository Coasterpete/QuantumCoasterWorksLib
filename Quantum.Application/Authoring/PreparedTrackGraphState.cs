using System;
using System.Linq;
using System.Text;
using Quantum.IO.TrackLayout.V2;
using Quantum.Track.Authoring;

namespace Quantum.Application.Authoring
{
    /// <summary>
    /// One immutable source graph together with its exact successful compilation and
    /// canonical Track Layout Package V2 JSON. Empty graphs intentionally have neither
    /// a compilation nor persistable package JSON.
    /// </summary>
    public sealed class PreparedTrackGraphState
    {
        private PreparedTrackGraphState(
            TrackAuthoringGraph graph,
            TrackLayoutPackageV2GraphAncillaryState ancillaryState,
            TrackAuthoringGraphCompileResult? graphCompileResult,
            string? canonicalPackageJson)
        {
            Graph = graph ?? throw new ArgumentNullException(nameof(graph));
            AncillaryState = ancillaryState ??
                throw new ArgumentNullException(nameof(ancillaryState));

            if (graph.Nodes.Count == 0)
            {
                if (graphCompileResult != null || canonicalPackageJson != null)
                {
                    throw new ArgumentException(
                        "An empty graph cannot have a compilation or package snapshot.",
                        nameof(graphCompileResult));
                }
            }
            else if (graphCompileResult is null ||
                     !graphCompileResult.Success ||
                     graphCompileResult.Compilation is null ||
                     !ReferenceEquals(graphCompileResult.SourceGraph, graph) ||
                     canonicalPackageJson is null)
            {
                throw new ArgumentException(
                    "A non-empty graph state requires its exact successful compilation and package snapshot.",
                    nameof(graphCompileResult));
            }

            GraphCompileResult = graphCompileResult;
            CanonicalPackageJson = canonicalPackageJson;
            RetainedPackageByteCount = canonicalPackageJson is null
                ? 0
                : Encoding.UTF8.GetByteCount(canonicalPackageJson);
        }

        public TrackAuthoringGraph Graph { get; }

        public TrackLayoutPackageV2GraphAncillaryState AncillaryState { get; }

        public TrackAuthoringGraphCompileResult? GraphCompileResult { get; }

        public string? CanonicalPackageJson { get; }

        /// <summary>
        /// UTF-8 size of the retained canonical package. This intentionally does not
        /// estimate the graph or compilation object graph.
        /// </summary>
        public int RetainedPackageByteCount { get; }

        public static PreparedTrackGraphState Prepare(
            TrackAuthoringGraph graph,
            TrackLayoutPackageV2GraphAncillaryState ancillaryState)
        {
            if (graph is null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            if (graph.Nodes.Count == 0)
            {
                return FromEvaluation(graph, ancillaryState, graphCompileResult: null);
            }

            TrackAuthoringGraphCompileResult compilation =
                TrackAuthoringGraphCompiler.Compile(graph);
            return FromEvaluation(graph, ancillaryState, compilation);
        }

        /// <summary>
        /// Prepares persistence from an already evaluated graph without compiling it
        /// again.
        /// </summary>
        public static PreparedTrackGraphState FromEvaluation(
            TrackAuthoringGraph graph,
            TrackLayoutPackageV2GraphAncillaryState ancillaryState,
            TrackAuthoringGraphCompileResult? graphCompileResult)
        {
            if (graph is null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            if (ancillaryState is null)
            {
                throw new ArgumentNullException(nameof(ancillaryState));
            }

            if (graph.Nodes.Count == 0)
            {
                if (graphCompileResult != null)
                {
                    throw new ArgumentException(
                        "An empty graph cannot have a compilation.",
                        nameof(graphCompileResult));
                }

                return FromPreparedData(
                    graph,
                    ancillaryState,
                    graphCompileResult: null,
                    canonicalPackageJson: null);
            }

            if (graphCompileResult is null ||
                !graphCompileResult.Success ||
                graphCompileResult.Compilation is null)
            {
                throw new ArgumentException(
                    "A non-empty graph requires a successful compilation.",
                    nameof(graphCompileResult));
            }

            TrackLayoutPackageV2GraphExportResult export =
                TrackLayoutPackageV2GraphAdapter.Export(
                    graph,
                    ancillaryState,
                    graphCompileResult);
            if (!export.Success || export.Package is null)
            {
                string details = export.GraphDiagnostics.Count != 0
                    ? string.Join(
                        " ",
                        export.GraphDiagnostics.Select(diagnostic =>
                            $"{diagnostic.Code}: {diagnostic.Message}"))
                    : string.Join(
                        " ",
                        export.PackageDiagnostics.Select(diagnostic =>
                            $"{diagnostic.Code} at {diagnostic.Path}: {diagnostic.Message}"));
                throw new InvalidOperationException(
                    string.IsNullOrEmpty(details)
                        ? "The Track Layout Package V2 export failed without diagnostics."
                        : "The authoring graph could not be prepared for persistence: " + details);
            }

            string canonicalPackageJson = TrackLayoutPackageV2Json.Serialize(
                export.Package,
                indented: true);
            return FromPreparedData(
                graph,
                ancillaryState,
                graphCompileResult,
                canonicalPackageJson);
        }

        /// <summary>
        /// Rehydrates an already prepared state without repeating compilation, export,
        /// or serialization. The caller must supply the canonical JSON originally
        /// prepared for this exact graph and compilation.
        /// </summary>
        public static PreparedTrackGraphState FromPreparedData(
            TrackAuthoringGraph graph,
            TrackLayoutPackageV2GraphAncillaryState ancillaryState,
            TrackAuthoringGraphCompileResult? graphCompileResult,
            string? canonicalPackageJson)
        {
            return new PreparedTrackGraphState(
                graph,
                ancillaryState,
                graphCompileResult,
                canonicalPackageJson);
        }

        public bool HasSameCanonicalContent(PreparedTrackGraphState other)
        {
            if (other is null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return string.Equals(
                CanonicalPackageJson,
                other.CanonicalPackageJson,
                StringComparison.Ordinal);
        }
    }
}
