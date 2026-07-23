using System;
using System.Collections.Generic;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Immutable result of applying, validating, and compiling one candidate operation.
    /// </summary>
    public sealed class TrackAuthoringCandidateEvaluation
    {
        private readonly IReadOnlyList<TrackAuthoringGraphDiagnostic> _diagnostics;

        internal TrackAuthoringCandidateEvaluation(
            TrackAuthoringGraph sourceGraph,
            ITrackAuthoringCandidateOperation operation,
            TrackAuthoringGraph? candidateGraph,
            TrackAuthoringGraphRouteResult? routeResult,
            TrackAuthoringGraphCompileResult? compileResult,
            IEnumerable<TrackAuthoringGraphDiagnostic> diagnostics,
            TimeSpan candidateApplicationElapsed,
            TimeSpan validationAndCompilationElapsed)
        {
            SourceGraph = sourceGraph ?? throw new ArgumentNullException(nameof(sourceGraph));
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            CandidateGraph = candidateGraph;
            RouteResult = routeResult;
            CompileResult = compileResult;
            Compilation = compileResult?.Compilation;
            CandidateApplicationElapsed = candidateApplicationElapsed;
            ValidationAndCompilationElapsed = validationAndCompilationElapsed;
            _diagnostics = new List<TrackAuthoringGraphDiagnostic>(
                diagnostics ?? throw new ArgumentNullException(nameof(diagnostics))).AsReadOnly();
        }

        public TrackAuthoringGraph SourceGraph { get; }

        public ITrackAuthoringCandidateOperation Operation { get; }

        public TrackAuthoringGraph? CandidateGraph { get; }

        public TrackAuthoringGraphRouteResult? RouteResult { get; }

        public TrackAuthoringGraphCompileResult? CompileResult { get; }

        public TrackAuthoringCompilation? Compilation { get; }

        public IReadOnlyList<TrackAuthoringGraphDiagnostic> Diagnostics => _diagnostics;

        public TimeSpan CandidateApplicationElapsed { get; }

        public TimeSpan ValidationAndCompilationElapsed { get; }

        public bool CommitEligible =>
            CandidateGraph != null &&
            RouteResult?.Success == true &&
            (CandidateGraph.Nodes.Count == 0 ||
             (CompileResult?.Success == true && Compilation != null));

        /// <summary>
        /// Conservatively detects whether the active immutable graph snapshot changed
        /// since this result was evaluated.
        /// </summary>
        public bool IsStaleComparedTo(TrackAuthoringGraph currentGraph)
        {
            if (currentGraph is null)
            {
                throw new ArgumentNullException(nameof(currentGraph));
            }

            return !ReferenceEquals(SourceGraph, currentGraph);
        }
    }
}
