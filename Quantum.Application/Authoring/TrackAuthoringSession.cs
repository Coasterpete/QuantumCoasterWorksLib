using System;
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
        private readonly object syncRoot = new object();
        private AuthoringSessionId sessionId;
        private long committedRevisionSequence;
        private long transactionRevisionSequence;
        private long synchronousRequestRevisionSequence;
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

        public AuthoringSessionId SessionId
        {
            get { lock (syncRoot) { return sessionId; } }
        }

        public CommittedTrackState CommittedState
        {
            get { lock (syncRoot) { return committedState; } }
        }

        public PreparedTrackGraphState PresentedState
        {
            get { lock (syncRoot) { return presentedState; } }
        }

        public InteractiveAuthoringTransaction? ActiveTransaction
        {
            get { lock (syncRoot) { return activeTransaction; } }
        }

        public AuthoringHistory History { get; }

        public bool CanUndo
        {
            get { lock (syncRoot) { return History.CanUndo; } }
        }

        public bool CanRedo
        {
            get { lock (syncRoot) { return History.CanRedo; } }
        }

        public string? UndoDescription
        {
            get { lock (syncRoot) { return History.UndoDescription; } }
        }

        public string? RedoDescription
        {
            get { lock (syncRoot) { return History.RedoDescription; } }
        }

        public bool HasCleanBaseline
        {
            get { lock (syncRoot) { return hasCleanBaseline; } }
        }

        public string? CleanCanonicalPackageBaseline
        {
            get { lock (syncRoot) { return cleanCanonicalPackageJson; } }
        }

        public string? PersistableCanonicalPackageJson
        {
            get { lock (syncRoot) { return committedState.CanonicalPackageJson; } }
        }

        public bool IsDirty
        {
            get { lock (syncRoot) { return IsDirtyCore(); } }
        }

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
            lock (syncRoot)
            {
                return new AuthoringSessionSnapshot(
                    sessionId,
                    committedState,
                    presentedState,
                    activeTransaction,
                    IsDirtyCore(),
                    History.UndoCount,
                    History.RedoCount,
                    History.RetainedPackageByteCount);
            }
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
            lock (syncRoot)
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
                    newestProvisionalRevision: null,
                    newestCandidate: null,
                    presentedCandidate: null);
                return activeTransaction;
            }
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

            AuthoringCandidateReservationResult reservation;
            lock (syncRoot)
            {
                synchronousRequestRevisionSequence++;
                reservation = ReserveCandidateCore(
                    transactionRevision,
                    operation,
                    new EvaluationRequestRevision(
                        sessionId,
                        synchronousRequestRevisionSequence),
                    DateTimeOffset.UtcNow);
            }

            if (reservation.Request is null)
            {
                return new CandidateUpdateResult(
                    wasEvaluated: false,
                    candidate: null,
                    reservation.Diagnostics);
            }

            TrackCandidateEvaluationProduct product =
                new ProductionTrackCandidateEvaluator()
                    .EvaluateAsync(reservation.Request, System.Threading.CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            AuthoringCandidatePublicationResult publication = PublishCandidate(product);
            return publication.UpdateResult;
        }

        internal AuthoringCandidateReservationResult ReserveCandidate(
            TransactionRevision transactionRevision,
            ITrackAuthoringCandidateOperation operation,
            EvaluationRequestRevision requestRevision,
            DateTimeOffset submittedAtUtc)
        {
            if (operation is null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            lock (syncRoot)
            {
                return ReserveCandidateCore(
                    transactionRevision,
                    operation,
                    requestRevision,
                    submittedAtUtc);
            }
        }

        internal bool IsEvaluationRequestCurrent(AuthoringEvaluationRequest request)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            lock (syncRoot)
            {
                return IsEvaluationRequestCurrentCore(request);
            }
        }

        internal AuthoringCandidatePublicationResult PublishCandidate(
            TrackCandidateEvaluationProduct product)
        {
            if (product is null)
            {
                throw new ArgumentNullException(nameof(product));
            }

            lock (syncRoot)
            {
                EvaluatedTrackCandidate candidate = product.Candidate;
                if (!IsCandidateRevisionCurrentCore(candidate.Revision))
                {
                    return new AuthoringCandidatePublicationResult(
                        acceptedBySession: false,
                        new CandidateUpdateResult(
                            wasEvaluated: true,
                            candidate: null,
                            new[]
                            {
                                new AuthoringSessionDiagnostic(
                                    AuthoringSessionDiagnosticCode.CandidateRevisionMismatch,
                                    "The evaluated candidate is not the active newest provisional revision.")
                            }));
                }

                activeTransaction = activeTransaction!.WithCandidate(candidate);
                if (candidate.IsCommitEligible)
                {
                    presentedState = candidate.PreparedState!;
                }

                return new AuthoringCandidatePublicationResult(
                    acceptedBySession: true,
                    new CandidateUpdateResult(
                        wasEvaluated: true,
                        candidate,
                        candidate.ApplicationDiagnostics));
            }
        }

        internal bool TryGetNewestCandidate(
            TransactionRevision transactionRevision,
            out EvaluatedCandidateRevision candidateRevision,
            out EvaluatedTrackCandidate? candidate)
        {
            lock (syncRoot)
            {
                if (activeTransaction is null ||
                    activeTransaction.Revision != transactionRevision ||
                    !activeTransaction.NewestProvisionalRevision.HasValue)
                {
                    candidateRevision = default;
                    candidate = null;
                    return false;
                }

                ProvisionalEditRevision provisionalRevision =
                    activeTransaction.NewestProvisionalRevision.Value;
                candidateRevision = new EvaluatedCandidateRevision(
                    activeTransaction.BaseCommittedRevision,
                    provisionalRevision);
                candidate = activeTransaction.NewestCandidate?.Revision.ProvisionalEditRevision ==
                    provisionalRevision
                    ? activeTransaction.NewestCandidate
                    : null;
                return true;
            }
        }

        public AuthoringCommitResult Commit(EvaluatedCandidateRevision candidateRevision)
        {
            lock (syncRoot)
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
                if (!activeTransaction.NewestProvisionalRevision.HasValue ||
                    candidateRevision.ProvisionalEditRevision !=
                        activeTransaction.NewestProvisionalRevision.Value ||
                    candidate is null ||
                    candidate.Revision != candidateRevision)
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
        }

        public bool Cancel(TransactionRevision transactionRevision)
        {
            lock (syncRoot)
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
        }

        public bool Undo()
        {
            lock (syncRoot)
            {
                if (activeTransaction != null || !History.TryUndo(out AuthoringHistoryEntry? entry))
                {
                    return false;
                }

                AdoptCommittedState(entry!.BeforeState);
                return true;
            }
        }

        public bool Redo()
        {
            lock (syncRoot)
            {
                if (activeTransaction != null || !History.TryRedo(out AuthoringHistoryEntry? entry))
                {
                    return false;
                }

                AdoptCommittedState(entry!.AfterState);
                return true;
            }
        }

        /// <summary>
        /// Adopts an already validated and persistence-ready state as one logical
        /// editor command. This is the bridge used by existing one-shot editor
        /// commands so interactive and non-interactive changes share this session's
        /// single authoritative history.
        /// </summary>
        public AuthoringCommitResult CommitPreparedEdit(
            string operationDescription,
            PreparedTrackGraphState preparedState)
        {
            if (string.IsNullOrWhiteSpace(operationDescription))
            {
                throw new ArgumentException(
                    "An operation description is required.",
                    nameof(operationDescription));
            }

            if (preparedState is null)
            {
                throw new ArgumentNullException(nameof(preparedState));
            }

            lock (syncRoot)
            {
                if (activeTransaction != null)
                {
                    return RejectedCommit(
                        AuthoringSessionDiagnosticCode.TransactionActive,
                        "A prepared editor command cannot commit during an active transaction.");
                }

                PreparedTrackGraphState beforeState = committedState.PreparedState;
                if (beforeState.HasSameCanonicalContent(preparedState))
                {
                    presentedState = beforeState;
                    return new AuthoringCommitResult(
                        succeeded: true,
                        changed: false,
                        committedState,
                        Array.Empty<AuthoringSessionDiagnostic>());
                }

                History.Record(new AuthoringHistoryEntry(
                    operationDescription,
                    beforeState,
                    preparedState));
                AdoptCommittedState(preparedState);
                return new AuthoringCommitResult(
                    succeeded: true,
                    changed: true,
                    committedState,
                    Array.Empty<AuthoringSessionDiagnostic>());
            }
        }

        public void ClearHistory()
        {
            lock (syncRoot)
            {
                if (activeTransaction != null)
                {
                    throw new InvalidOperationException(
                        "History cannot be cleared during an active transaction.");
                }

                History.Clear();
            }
        }

        public void MarkClean()
        {
            lock (syncRoot)
            {
                SetCleanBaseline(committedState.PreparedState, markClean: true);
            }
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

            lock (syncRoot)
            {
                sessionId = AuthoringSessionId.New();
                committedRevisionSequence = 0;
                transactionRevisionSequence = 0;
                synchronousRequestRevisionSequence = 0;
                committedState = new CommittedTrackState(
                    replacement,
                    new CommittedSourceRevision(sessionId, sequence: 0));
                presentedState = replacement;
                activeTransaction = null;
                History.Clear();
                SetCleanBaseline(replacement, markClean);
            }
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

        private AuthoringCandidateReservationResult ReserveCandidateCore(
            TransactionRevision transactionRevision,
            ITrackAuthoringCandidateOperation operation,
            EvaluationRequestRevision requestRevision,
            DateTimeOffset submittedAtUtc)
        {
            if (activeTransaction is null)
            {
                return RejectedReservation(
                    AuthoringSessionDiagnosticCode.NoActiveTransaction,
                    "There is no active authoring transaction.");
            }

            if (requestRevision.SessionId != sessionId ||
                activeTransaction.Revision != transactionRevision)
            {
                return RejectedReservation(
                    AuthoringSessionDiagnosticCode.TransactionRevisionMismatch,
                    "The submitted transaction revision does not match the active transaction.");
            }

            if (activeTransaction.BaseCommittedRevision != committedState.Revision)
            {
                return RejectedReservation(
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
            var request = new AuthoringEvaluationRequest(
                requestRevision,
                candidateRevision,
                activeTransaction.BeforeState.Graph,
                activeTransaction.BeforeState.AncillaryState,
                operation,
                submittedAtUtc);
            activeTransaction =
                activeTransaction.WithReservedProvisionalRevision(provisionalRevision);
            return new AuthoringCandidateReservationResult(
                request,
                Array.Empty<AuthoringSessionDiagnostic>());
        }

        private bool IsEvaluationRequestCurrentCore(AuthoringEvaluationRequest request)
        {
            return request.SessionId == sessionId &&
                ReferenceEquals(request.SourceGraph, activeTransaction?.BeforeState.Graph) &&
                ReferenceEquals(request.AncillaryState, activeTransaction?.BeforeState.AncillaryState) &&
                IsCandidateRevisionCurrentCore(request.CandidateRevision);
        }

        private bool IsCandidateRevisionCurrentCore(EvaluatedCandidateRevision revision)
        {
            return activeTransaction != null &&
                revision.BaseCommittedRevision == committedState.Revision &&
                revision.ProvisionalEditRevision.TransactionRevision ==
                    activeTransaction.Revision &&
                activeTransaction.BaseCommittedRevision == committedState.Revision &&
                activeTransaction.NewestProvisionalRevision.HasValue &&
                revision.ProvisionalEditRevision ==
                    activeTransaction.NewestProvisionalRevision.Value;
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

        private bool IsDirtyCore()
        {
            return !hasCleanBaseline ||
                !string.Equals(
                    cleanCanonicalPackageJson,
                    committedState.CanonicalPackageJson,
                    StringComparison.Ordinal);
        }

        private static AuthoringCandidateReservationResult RejectedReservation(
            AuthoringSessionDiagnosticCode code,
            string message)
        {
            return new AuthoringCandidateReservationResult(
                request: null,
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
