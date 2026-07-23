using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Pure production-backed evaluator for a single non-destructive graph candidate.
    /// </summary>
    public static class TrackAuthoringCandidateEvaluator
    {
        public static TrackAuthoringCandidateEvaluation Evaluate(
            TrackAuthoringGraph sourceGraph,
            ITrackAuthoringCandidateOperation operation)
        {
            return Evaluate(sourceGraph, operation, TrackAuthoringGraphCompiler.Compile);
        }

        internal static TrackAuthoringCandidateEvaluation Evaluate(
            TrackAuthoringGraph sourceGraph,
            ITrackAuthoringCandidateOperation operation,
            Func<TrackAuthoringGraph, TrackAuthoringGraphCompileResult> compiler)
        {
            if (sourceGraph is null)
            {
                throw new ArgumentNullException(nameof(sourceGraph));
            }

            if (operation is null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (compiler is null)
            {
                throw new ArgumentNullException(nameof(compiler));
            }

            TrackAuthoringGraph candidateGraph;
            var applicationStopwatch = Stopwatch.StartNew();
            try
            {
                candidateGraph = operation.Apply(sourceGraph) ??
                    throw new InvalidOperationException(
                        "A candidate operation cannot return a null graph.");
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is NotSupportedException)
            {
                applicationStopwatch.Stop();
                var operationDiagnostic = new TrackAuthoringGraphDiagnostic(
                    TrackAuthoringGraphDiagnosticCode.CandidateOperationFailed,
                    $"Candidate operation '{operation.OperationTypeId}' failed: {exception.Message}");
                return new TrackAuthoringCandidateEvaluation(
                    sourceGraph,
                    operation,
                    null,
                    null,
                    null,
                    new[] { operationDiagnostic },
                    applicationStopwatch.Elapsed,
                    TimeSpan.Zero);
            }

            applicationStopwatch.Stop();
            var validationStopwatch = Stopwatch.StartNew();

            TrackAuthoringGraphRouteResult routeResult =
                TrackAuthoringGraphRouteValidator.Validate(candidateGraph);
            if (!routeResult.Success)
            {
                validationStopwatch.Stop();
                return new TrackAuthoringCandidateEvaluation(
                    sourceGraph,
                    operation,
                    candidateGraph,
                    routeResult,
                    null,
                    routeResult.Diagnostics,
                    applicationStopwatch.Elapsed,
                    validationStopwatch.Elapsed);
            }

            // Empty is a valid editor/session state even though it is intentionally not
            // a compilable or persistable track package.
            if (candidateGraph.Nodes.Count == 0)
            {
                validationStopwatch.Stop();
                return new TrackAuthoringCandidateEvaluation(
                    sourceGraph,
                    operation,
                    candidateGraph,
                    routeResult,
                    null,
                    Array.Empty<TrackAuthoringGraphDiagnostic>(),
                    applicationStopwatch.Elapsed,
                    validationStopwatch.Elapsed);
            }

            TrackAuthoringGraphCompileResult compileResult = compiler(candidateGraph);
            validationStopwatch.Stop();
            IReadOnlyList<TrackAuthoringGraphDiagnostic> diagnostics = compileResult.Diagnostics;
            return new TrackAuthoringCandidateEvaluation(
                sourceGraph,
                operation,
                candidateGraph,
                routeResult,
                compileResult,
                diagnostics,
                applicationStopwatch.Elapsed,
                validationStopwatch.Elapsed);
        }
    }
}
