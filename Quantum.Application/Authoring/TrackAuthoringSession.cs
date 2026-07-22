using System;
using System.Collections.Generic;
using Quantum.IO.TrackLayout.V2;
using Quantum.Track.Authoring;

namespace Quantum.Application.Authoring
{
    /// <summary>
    /// Headless owner of committed, presented, transactional, history, and persistence
    /// state for one track-authoring session.
    /// </summary>
    public sealed class TrackAuthoringSession
    {
        private AuthoringSessionId sessionId;
        private long committedRevisionSequence;
        private long transactionRevisionSequence;
        private CommittedTrackState committedState;
        private PreparedTrackGraphState presentedState;
        private InteractiveAuthoringTransaction? activeTransaction;
        private bool hasCleanBaseline;
        private string? cleanCanonicalPackageJson;

        public TrackAuthoringSession(
            PreparedTrackGraphState initialState,
            bool markClean = true)
        {
            if (initialState is null)
            {
                throw new ArgumentNullException(nameof(initialState));
            }

            History = new AuthoringHistory();
            sessionId = AuthoringSessionId.New();
            committedState = new CommittedTrackState(
                initialState,
                new CommittedSourceRevision(sessionId, sequence: 0));
            presentedState = initialState;
            SetCleanBaseline(initialState, markClean);
        }

        public AuthoringSessionId SessionId => sessionId;

        public CommittedTrackState CommittedState => committedState;

        public PreparedTrackGraphState PresentedState => presentedState;

        public InteractiveAuthoringTransaction? ActiveTransaction => activeTransaction;

        public AuthoringHistory History { get; }

        public bool HasCleanBaseline => hasCleanBaseline;

        public string? CleanCanonicalPackageBaseline => cleanCanonicalPackageJson;

        public string? PersistableCanonicalPackageJson =>
            committedState.CanonicalPackageJson;

        public bool IsDirty =>
            !hasCleanBaseline ||
            !string.Equals(
                cleanCanonicalPackageJson,
                committedState.CanonicalPackageJson,
                StringComparison.Ordinal);

        public static TrackAuthoringSession Create(
            TrackAuthoringGraph graph,
            TrackLayoutPackageV2GraphAncillaryState ancillaryState,
            bool markClean = true)
        {
            return new TrackAuthoringSession(
                PreparedTrackGraphState.Prepare(graph, ancillaryState),
                markClean);
        }

        public AuthoringSessionSnapshot CaptureSnapshot()
        {
            return new AuthoringSessionSnapshot(
                sessionId,
                committedState,
                presentedState,
                activeTransaction,
                IsDirty,
                History.UndoCount,
                History.RedoCount,
                History.RetainedPackageByteCount);
        }

        public InteractiveAuthoringTransaction BeginTransaction(
            string targetSectionId,
            string operationDescription)
        {
            return BeginTransaction(
                targetSectionId,
                operationDescription,
                operationDescription);
        }

        public InteractiveAuthoringTransaction BeginTransaction(
            string targetSectionId,
            string parameterIdentity,
            string operationDescription)
        {
            if (activeTransaction != null)
            {
                throw new InvalidOperationException(
                    "Only one interactive authoring transaction may be active.");
            }

            transactionRevisionSequence++;
            activeTransaction = new InteractiveAuthoringTransaction(
                new TransactionRevision(sessionId, transactionRevisionSequence),
                committedState.Revision,
                committedState.PreparedState,
                targetSectionId,
                parameterIdentity,
                operationDescription,
                newestCandidate: null,
                presentedCandidate: null);
            return activeTransaction;
        }

        /// <summary>
        /// Applies and evaluates an immutable absolute candidate operation against the
        /// transaction's captured base graph. It never chains from a prior preview.
        /// </summary>
        public CandidateUpdateResult SubmitCandidate(
            TransactionRevision transactionRevision,
            ITrackAuthoringCandidateOperation operation)
        {
            if (operation is null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (activeTransaction is null)
            {
                return RejectedUpdate(
                    AuthoringSessionDiagnosticCode.NoActiveTransaction,
                    "There is no active authoring transaction.");
            }

            if (activeTransaction.Revision != transactionRevision)
            {
                return RejectedUpdate(
                    AuthoringSessionDiagnosticCode.TransactionRevisionMismatch,
                    "The submitted transaction revision does not match the active transaction.");
            }

            if (activeTransaction.BaseCommittedRevision != committedState.Revision)
            {
                return RejectedUpdate(
                    AuthoringSessionDiagnosticCode.CommittedRevisionMismatch,
                    "The transaction base no longer matches the committed source revision.");
            }

            long provisionalSequence =
                (activeTransaction.NewestProvisionalRevision?.Sequence ?? 0) + 1;
            var provisionalRevision = new ProvisionalEditRevision(
                activeTransaction.Revision,
                provisionalSequence);
            var candidateRevision = new EvaluatedCandidateRevision(
                activeTransaction.BaseCommittedRevision,
                provisionalRevision);

            TrackAuthoringCandidateEvaluation evaluation =
                TrackAuthoringCandidateEvaluator.Evaluate(
                    activeTransaction.BeforeState.Graph,
                    operation);
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
                        activeTransaction.BeforeState.AncillaryState,
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
            }

            var candidate = new EvaluatedTrackCandidate(
                candidateRevision,
                evaluation,
                preparedState,
                diagnostics);
            activeTransaction = activeTransaction.WithCandidate(candidate);
            if (candidate.IsCommitEligible)
            {
                presentedState = candidate.PreparedState!;
            }

            return new CandidateUpdateResult(
                wasEvaluated: true,
                candidate,
                diagnostics);
        }

        public AuthoringCommitResult Commit(EvaluatedCandidateRevision candidateRevision)
        {
            if (activeTransaction is null)
            {
                return RejectedCommit(
                    AuthoringSessionDiagnosticCode.NoActiveTransaction,
                    "There is no active authoring transaction.");
            }

            if (activeTransaction.BaseCommittedRevision != committedState.Revision)
            {
                return RejectedCommit(
                    AuthoringSessionDiagnosticCode.CommittedRevisionMismatch,
                    "The transaction base no longer matches the committed source revision.");
            }

            EvaluatedTrackCandidate? candidate = activeTransaction.NewestCandidate;
            if (candidate is null || candidate.Revision != candidateRevision)
            {
                return RejectedCommit(
                    AuthoringSessionDiagnosticCode.CandidateRevisionMismatch,
                    "Only the exact newest evaluated candidate revision may be committed.");
            }

            if (!candidate.IsCommitEligible || candidate.PreparedState is null)
            {
                return RejectedCommit(
                    AuthoringSessionDiagnosticCode.CandidateRejected,
                    "The newest evaluated candidate is invalid and cannot be committed.");
            }

            PreparedTrackGraphState beforeState = activeTransaction.BeforeState;
            PreparedTrackGraphState afterState = candidate.PreparedState;
            if (beforeState.HasSameCanonicalContent(afterState))
            {
                activeTransaction = null;
                presentedState = committedState.PreparedState;
                return new AuthoringCommitResult(
                    succeeded: true,
                    changed: false,
                    committedState,
                    Array.Empty<AuthoringSessionDiagnostic>());
            }

            History.Record(new AuthoringHistoryEntry(
                activeTransaction.Description,
                beforeState,
                afterState));
            AdoptCommittedState(afterState);
            activeTransaction = null;
            return new AuthoringCommitResult(
                succeeded: true,
                changed: true,
                committedState,
                Array.Empty<AuthoringSessionDiagnostic>());
        }

        public bool Cancel(TransactionRevision transactionRevision)
        {
            if (activeTransaction is null ||
                activeTransaction.Revision != transactionRevision)
            {
                return false;
            }

            activeTransaction = null;
            presentedState = committedState.PreparedState;
            return true;
        }

        public bool Undo()
        {
            if (activeTransaction != null || !History.TryUndo(out AuthoringHistoryEntry? entry))
            {
                return false;
            }

            AdoptCommittedState(entry!.BeforeState);
            return true;
        }

        public bool Redo()
        {
            if (activeTransaction != null || !History.TryRedo(out AuthoringHistoryEntry? entry))
            {
                return false;
            }

            AdoptCommittedState(entry!.AfterState);
            return true;
        }

        public void MarkClean()
        {
            SetCleanBaseline(committedState.PreparedState, markClean: true);
        }

        /// <summary>
        /// Replaces the open/new session content, invalidating all prior structural
        /// revisions and clearing transaction and history state.
        /// </summary>
        public void ReplaceSessionState(
            PreparedTrackGraphState replacement,
            bool markClean)
        {
            if (replacement is null)
            {
                throw new ArgumentNullException(nameof(replacement));
            }

            sessionId = AuthoringSessionId.New();
            committedRevisionSequence = 0;
            transactionRevisionSequence = 0;
            committedState = new CommittedTrackState(
                replacement,
                new CommittedSourceRevision(sessionId, sequence: 0));
            presentedState = replacement;
            activeTransaction = null;
            History.Clear();
            SetCleanBaseline(replacement, markClean);
        }

        public AuthoringOneShotResult ApplyOneShot(
            string targetSectionId,
            string parameterIdentity,
            string operationDescription,
            ITrackAuthoringCandidateOperation operation)
        {
            InteractiveAuthoringTransaction transaction = BeginTransaction(
                targetSectionId,
                parameterIdentity,
                operationDescription);
            CandidateUpdateResult update = SubmitCandidate(transaction.Revision, operation);
            if (!update.CandidateAccepted || update.Candidate is null)
            {
                Cancel(transaction.Revision);
                return new AuthoringOneShotResult(update, commit: null);
            }

            AuthoringCommitResult commit = Commit(update.Candidate.Revision);
            return new AuthoringOneShotResult(update, commit);
        }

        private void AdoptCommittedState(PreparedTrackGraphState preparedState)
        {
            committedRevisionSequence++;
            committedState = new CommittedTrackState(
                preparedState,
                new CommittedSourceRevision(sessionId, committedRevisionSequence));
            presentedState = preparedState;
        }

        private void SetCleanBaseline(
            PreparedTrackGraphState preparedState,
            bool markClean)
        {
            hasCleanBaseline = markClean;
            cleanCanonicalPackageJson = markClean
                ? preparedState.CanonicalPackageJson
                : null;
        }

        private static CandidateUpdateResult RejectedUpdate(
            AuthoringSessionDiagnosticCode code,
            string message)
        {
            return new CandidateUpdateResult(
                wasEvaluated: false,
                candidate: null,
                new[] { new AuthoringSessionDiagnostic(code, message) });
        }

        private static AuthoringCommitResult RejectedCommit(
            AuthoringSessionDiagnosticCode code,
            string message)
        {
            return new AuthoringCommitResult(
                succeeded: false,
                changed: false,
                committedState: null,
                new[] { new AuthoringSessionDiagnostic(code, message) });
        }
    }
}
