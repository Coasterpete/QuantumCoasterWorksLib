using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Quantum.IO.TrackLayout.V2;
using Quantum.Track.Authoring;

namespace Quantum.Application.Authoring
{
    public enum AuthoringEvaluationExecutionMode
    {
        Synchronous = 0,
        SerializedBackground = 1
    }

    public enum AuthoringEvaluationOutcomeStatus
    {
        Accepted = 0,
        Rejected = 1,
        Stale = 2,
        Coalesced = 3,
        Cancelled = 4,
        Faulted = 5
    }

    public enum AuthoringEvaluationPhase
    {
        CandidateConstruction = 0,
        CandidateEvaluation = 1,
        PackagePreparation = 2,
        Publication = 3,
        Scheduling = 4
    }

    public readonly struct EvaluationRequestRevision :
        IEquatable<EvaluationRequestRevision>
    {
        public EvaluationRequestRevision(AuthoringSessionId sessionId, long sequence)
        {
            if (sessionId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "An evaluation request revision requires a session ID.",
                    nameof(sessionId));
            }

            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            SessionId = sessionId;
            Sequence = sequence;
        }

        public AuthoringSessionId SessionId { get; }

        public long Sequence { get; }

        public bool Equals(EvaluationRequestRevision other) =>
            SessionId.Equals(other.SessionId) && Sequence == other.Sequence;

        public override bool Equals(object? obj) =>
            obj is EvaluationRequestRevision && Equals((EvaluationRequestRevision)obj);

        public override int GetHashCode() =>
            unchecked((SessionId.GetHashCode() * 397) ^ Sequence.GetHashCode());

        public override string ToString() => $"{SessionId}:evaluation:{Sequence}";

        public static bool operator ==(
            EvaluationRequestRevision left,
            EvaluationRequestRevision right) => left.Equals(right);

        public static bool operator !=(
            EvaluationRequestRevision left,
            EvaluationRequestRevision right) => !left.Equals(right);
    }

    public sealed class AuthoringEvaluationSchedulerOptions
    {
        public AuthoringEvaluationSchedulerOptions(
            AuthoringEvaluationExecutionMode executionMode,
            TimeSpan minimumEvaluationInterval)
        {
            if (minimumEvaluationInterval < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumEvaluationInterval));
            }

            ExecutionMode = executionMode;
            MinimumEvaluationInterval = minimumEvaluationInterval;
        }

        public AuthoringEvaluationExecutionMode ExecutionMode { get; }

        public TimeSpan MinimumEvaluationInterval { get; }

        public static AuthoringEvaluationSchedulerOptions Synchronous { get; } =
            new AuthoringEvaluationSchedulerOptions(
                AuthoringEvaluationExecutionMode.Synchronous,
                TimeSpan.Zero);

        public static AuthoringEvaluationSchedulerOptions SerializedBackground { get; } =
            new AuthoringEvaluationSchedulerOptions(
                AuthoringEvaluationExecutionMode.SerializedBackground,
                TimeSpan.FromSeconds(1.0 / 30.0));
    }

    public sealed class AuthoringEvaluationRequest
    {
        internal AuthoringEvaluationRequest(
            EvaluationRequestRevision requestRevision,
            EvaluatedCandidateRevision candidateRevision,
            TrackAuthoringGraph sourceGraph,
            TrackLayoutPackageV2GraphAncillaryState ancillaryState,
            ITrackAuthoringCandidateOperation operation,
            DateTimeOffset submittedAtUtc)
        {
            RequestRevision = requestRevision;
            CandidateRevision = candidateRevision;
            SourceGraph = sourceGraph ?? throw new ArgumentNullException(nameof(sourceGraph));
            AncillaryState = ancillaryState ??
                throw new ArgumentNullException(nameof(ancillaryState));
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            SubmittedAtUtc = submittedAtUtc;
        }

        public EvaluationRequestRevision RequestRevision { get; }

        public EvaluatedCandidateRevision CandidateRevision { get; }

        public AuthoringSessionId SessionId => RequestRevision.SessionId;

        public CommittedSourceRevision BaseCommittedRevision =>
            CandidateRevision.BaseCommittedRevision;

        public TransactionRevision TransactionRevision =>
            CandidateRevision.ProvisionalEditRevision.TransactionRevision;

        public ProvisionalEditRevision ProvisionalEditRevision =>
            CandidateRevision.ProvisionalEditRevision;

        public TrackAuthoringGraph SourceGraph { get; }

        public TrackLayoutPackageV2GraphAncillaryState AncillaryState { get; }

        public ITrackAuthoringCandidateOperation Operation { get; }

        public DateTimeOffset SubmittedAtUtc { get; }
    }

    public sealed class AuthoringEvaluationTiming
    {
        internal AuthoringEvaluationTiming(
            TimeSpan queueWaitTime,
            TimeSpan candidateApplicationTime,
            TimeSpan validationAndCompilationTime,
            TimeSpan packagePreparationTime,
            TimeSpan totalEvaluationTime,
            TimeSpan submitToResultTime,
            TimeSpan? submitToPresentTime,
            int compilerInvocationCount)
        {
            QueueWaitTime = queueWaitTime;
            CandidateApplicationTime = candidateApplicationTime;
            ValidationAndCompilationTime = validationAndCompilationTime;
            PackagePreparationTime = packagePreparationTime;
            TotalEvaluationTime = totalEvaluationTime;
            SubmitToResultTime = submitToResultTime;
            SubmitToPresentTime = submitToPresentTime;
            CompilerInvocationCount = compilerInvocationCount;
        }

        public TimeSpan QueueWaitTime { get; }

        public TimeSpan CandidateApplicationTime { get; }

        public TimeSpan ValidationAndCompilationTime { get; }

        public TimeSpan PackagePreparationTime { get; }

        public TimeSpan TotalEvaluationTime { get; }

        public TimeSpan SubmitToResultTime { get; }

        public TimeSpan? SubmitToPresentTime { get; }

        public int CompilerInvocationCount { get; }
    }

    public sealed class AuthoringEvaluationFault
    {
        internal AuthoringEvaluationFault(
            AuthoringEvaluationPhase phase,
            Exception exception)
        {
            Phase = phase;
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name;
            Message = exception.Message;
        }

        public AuthoringEvaluationPhase Phase { get; }

        public string ExceptionType { get; }

        public string Message { get; }
    }

    public sealed class AuthoringEvaluationOutcome
    {
        private readonly IReadOnlyList<AuthoringSessionDiagnostic> diagnostics;

        internal AuthoringEvaluationOutcome(
            AuthoringEvaluationRequest request,
            AuthoringEvaluationOutcomeStatus status,
            EvaluatedTrackCandidate? candidate,
            IEnumerable<AuthoringSessionDiagnostic> diagnostics,
            AuthoringEvaluationTiming timing,
            AuthoringEvaluationFault? fault)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Status = status;
            Candidate = candidate;
            this.diagnostics = new List<AuthoringSessionDiagnostic>(
                diagnostics ?? throw new ArgumentNullException(nameof(diagnostics))).AsReadOnly();
            Timing = timing ?? throw new ArgumentNullException(nameof(timing));
            Fault = fault;
        }

        public AuthoringEvaluationRequest Request { get; }

        public AuthoringEvaluationOutcomeStatus Status { get; }

        public EvaluatedTrackCandidate? Candidate { get; }

        public IReadOnlyList<AuthoringSessionDiagnostic> Diagnostics => diagnostics;

        public AuthoringEvaluationTiming Timing { get; }

        public AuthoringEvaluationFault? Fault { get; }

        public bool WasPresented => Timing.SubmitToPresentTime.HasValue;
    }

    public sealed class AuthoringEvaluationSubmission
    {
        internal AuthoringEvaluationSubmission(
            AuthoringEvaluationRequest request,
            Task<AuthoringEvaluationOutcome> completion)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Completion = completion ?? throw new ArgumentNullException(nameof(completion));
        }

        public AuthoringEvaluationRequest Request { get; }

        public Task<AuthoringEvaluationOutcome> Completion { get; }
    }

    public sealed class AuthoringScheduledCommitResult
    {
        internal AuthoringScheduledCommitResult(
            EvaluatedCandidateRevision candidateRevision,
            AuthoringEvaluationOutcomeStatus evaluationStatus,
            AuthoringCommitResult? commitResult)
        {
            CandidateRevision = candidateRevision;
            EvaluationStatus = evaluationStatus;
            CommitResult = commitResult;
        }

        public EvaluatedCandidateRevision CandidateRevision { get; }

        public AuthoringEvaluationOutcomeStatus EvaluationStatus { get; }

        public AuthoringCommitResult? CommitResult { get; }

        public bool Succeeded => CommitResult?.Succeeded == true;
    }

    public sealed class EvaluationSchedulerSnapshot
    {
        internal EvaluationSchedulerSnapshot(
            long submitted,
            long started,
            long completed,
            long accepted,
            long rejected,
            long stale,
            long coalesced,
            long cancelledBeforeStart,
            long cancelled,
            long faulted,
            int maximumPendingDepth,
            int pendingDepth,
            bool isEvaluationRunning,
            TimeSpan queueWaitTime,
            TimeSpan candidateApplicationTime,
            TimeSpan validationAndCompilationTime,
            TimeSpan packagePreparationTime,
            TimeSpan totalEvaluationTime,
            TimeSpan submitToResultTime,
            TimeSpan submitToPresentTime,
            int compilerInvocationCount)
        {
            Submitted = submitted;
            Started = started;
            Completed = completed;
            Accepted = accepted;
            Rejected = rejected;
            Stale = stale;
            Coalesced = coalesced;
            CancelledBeforeStart = cancelledBeforeStart;
            Cancelled = cancelled;
            Faulted = faulted;
            MaximumPendingDepth = maximumPendingDepth;
            PendingDepth = pendingDepth;
            IsEvaluationRunning = isEvaluationRunning;
            QueueWaitTime = queueWaitTime;
            CandidateApplicationTime = candidateApplicationTime;
            ValidationAndCompilationTime = validationAndCompilationTime;
            PackagePreparationTime = packagePreparationTime;
            TotalEvaluationTime = totalEvaluationTime;
            SubmitToResultTime = submitToResultTime;
            SubmitToPresentTime = submitToPresentTime;
            CompilerInvocationCount = compilerInvocationCount;
        }

        public long Submitted { get; }
        public long Started { get; }
        public long Completed { get; }
        public long Accepted { get; }
        public long Rejected { get; }
        public long Stale { get; }
        public long Coalesced { get; }
        public long CancelledBeforeStart { get; }
        public long Cancelled { get; }
        public long Faulted { get; }
        public int MaximumPendingDepth { get; }
        public int PendingDepth { get; }
        public bool IsEvaluationRunning { get; }
        public TimeSpan QueueWaitTime { get; }
        public TimeSpan CandidateApplicationTime { get; }
        public TimeSpan ValidationAndCompilationTime { get; }
        public TimeSpan PackagePreparationTime { get; }
        public TimeSpan TotalEvaluationTime { get; }
        public TimeSpan SubmitToResultTime { get; }
        public TimeSpan SubmitToPresentTime { get; }
        public int CompilerInvocationCount { get; }
    }
}
