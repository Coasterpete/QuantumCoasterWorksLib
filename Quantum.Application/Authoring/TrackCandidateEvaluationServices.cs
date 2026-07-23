using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Quantum.Track.Authoring;

namespace Quantum.Application.Authoring
{
    internal sealed class AuthoringCandidateReservationResult
    {
        internal AuthoringCandidateReservationResult(
            AuthoringEvaluationRequest? request,
            IReadOnlyList<AuthoringSessionDiagnostic> diagnostics)
        {
            Request = request;
            Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        internal AuthoringEvaluationRequest? Request { get; }

        internal IReadOnlyList<AuthoringSessionDiagnostic> Diagnostics { get; }
    }

    internal sealed class AuthoringCandidatePublicationResult
    {
        internal AuthoringCandidatePublicationResult(
            bool acceptedBySession,
            CandidateUpdateResult updateResult)
        {
            AcceptedBySession = acceptedBySession;
            UpdateResult = updateResult ?? throw new ArgumentNullException(nameof(updateResult));
        }

        internal bool AcceptedBySession { get; }

        internal CandidateUpdateResult UpdateResult { get; }
    }

    internal interface ITrackCandidateEvaluator
    {
        Task<TrackCandidateEvaluationProduct> EvaluateAsync(
            AuthoringEvaluationRequest request,
            CancellationToken cancellationToken);
    }

    internal interface IAuthoringEvaluationClock
    {
        DateTimeOffset UtcNow { get; }

        Task Delay(TimeSpan delay, CancellationToken cancellationToken);
    }

    internal sealed class SystemAuthoringEvaluationClock : IAuthoringEvaluationClock
    {
        public static SystemAuthoringEvaluationClock Instance { get; } =
            new SystemAuthoringEvaluationClock();

        private SystemAuthoringEvaluationClock()
        {
        }

        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        public Task Delay(TimeSpan delay, CancellationToken cancellationToken) =>
            Task.Delay(delay, cancellationToken);
    }

    internal sealed class TrackCandidateEvaluationProduct
    {
        internal TrackCandidateEvaluationProduct(
            EvaluatedTrackCandidate candidate,
            TimeSpan candidateApplicationTime,
            TimeSpan validationAndCompilationTime,
            TimeSpan packagePreparationTime,
            TimeSpan totalEvaluationTime,
            int compilerInvocationCount)
        {
            Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
            CandidateApplicationTime = candidateApplicationTime;
            ValidationAndCompilationTime = validationAndCompilationTime;
            PackagePreparationTime = packagePreparationTime;
            TotalEvaluationTime = totalEvaluationTime;
            CompilerInvocationCount = compilerInvocationCount;
        }

        internal EvaluatedTrackCandidate Candidate { get; }

        internal TimeSpan CandidateApplicationTime { get; }

        internal TimeSpan ValidationAndCompilationTime { get; }

        internal TimeSpan PackagePreparationTime { get; }

        internal TimeSpan TotalEvaluationTime { get; }

        internal int CompilerInvocationCount { get; }
    }

    internal sealed class AuthoringEvaluationException : Exception
    {
        internal AuthoringEvaluationException(
            AuthoringEvaluationPhase phase,
            Exception innerException)
            : base(innerException.Message, innerException)
        {
            Phase = phase;
        }

        internal AuthoringEvaluationPhase Phase { get; }
    }

    internal sealed class ProductionTrackCandidateEvaluator : ITrackCandidateEvaluator
    {
        public Task<TrackCandidateEvaluationProduct> EvaluateAsync(
            AuthoringEvaluationRequest request,
            CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var totalStopwatch = Stopwatch.StartNew();
            TrackAuthoringCandidateEvaluation evaluation;
            try
            {
                evaluation = TrackAuthoringCandidateEvaluator.Evaluate(
                    request.SourceGraph,
                    request.Operation);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new AuthoringEvaluationException(
                    AuthoringEvaluationPhase.CandidateEvaluation,
                    exception);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var packageStopwatch = Stopwatch.StartNew();
            var diagnostics = new List<AuthoringSessionDiagnostic>();
            PreparedTrackGraphState? preparedState = null;
            if (!evaluation.CommitEligible || evaluation.CandidateGraph is null)
            {
                diagnostics.Add(new AuthoringSessionDiagnostic(
                    AuthoringSessionDiagnosticCode.CandidateRejected,
                    "The candidate operation did not produce a valid authoring state."));
            }
            else
            {
                try
                {
                    preparedState = PreparedTrackGraphState.FromEvaluation(
                        evaluation.CandidateGraph,
                        request.AncillaryState,
                        evaluation.CompileResult);
                }
                catch (Exception exception) when (
                    exception is ArgumentException ||
                    exception is InvalidOperationException ||
                    exception is NotSupportedException)
                {
                    diagnostics.Add(new AuthoringSessionDiagnostic(
                        AuthoringSessionDiagnosticCode.PersistencePreparationFailed,
                        exception.Message));
                }
                catch (Exception exception)
                {
                    throw new AuthoringEvaluationException(
                        AuthoringEvaluationPhase.PackagePreparation,
                        exception);
                }
            }

            packageStopwatch.Stop();
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = new EvaluatedTrackCandidate(
                request.CandidateRevision,
                evaluation,
                preparedState,
                diagnostics);
            totalStopwatch.Stop();
            int compilerInvocationCount = evaluation.CompileResult is null ? 0 : 1;
            return Task.FromResult(new TrackCandidateEvaluationProduct(
                candidate,
                evaluation.CandidateApplicationElapsed,
                evaluation.ValidationAndCompilationElapsed,
                packageStopwatch.Elapsed,
                totalStopwatch.Elapsed,
                compilerInvocationCount));
        }
    }
}
