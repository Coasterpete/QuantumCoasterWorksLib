using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Quantum.Application.Authoring
{
    /// <summary>
    /// Frontend-neutral one-running/one-latest-pending evaluation coordinator.
    /// TrackAuthoringSession remains the final authority for candidate publication
    /// and commit.
    /// </summary>
    public sealed class TrackAuthoringEvaluationCoordinator : IAsyncDisposable, IDisposable
    {
        private readonly object gate = new object();
        private readonly TrackAuthoringSession session;
        private readonly AuthoringEvaluationSchedulerOptions options;
        private readonly ITrackCandidateEvaluator evaluator;
        private readonly IAuthoringEvaluationClock clock;
        private readonly SemaphoreSlim workSignal = new SemaphoreSlim(0);
        private readonly CancellationTokenSource shutdown = new CancellationTokenSource();
        private readonly Dictionary<ProvisionalEditRevision, WorkItem> outstanding =
            new Dictionary<ProvisionalEditRevision, WorkItem>();
        private readonly Task? workerTask;

        private WorkItem? pending;
        private WorkItem? running;
        private DateTimeOffset? lastEvaluationStartedAtUtc;
        private long requestSequence;
        private bool disposed;
        private TransactionRevision? frozenCommitTransaction;
        private ProvisionalEditRevision? frozenCommitRevision;

        private long submitted;
        private long started;
        private long completed;
        private long accepted;
        private long rejected;
        private long stale;
        private long coalesced;
        private long cancelledBeforeStart;
        private long cancelled;
        private long faulted;
        private int maximumPendingDepth;
        private TimeSpan queueWaitTime;
        private TimeSpan candidateApplicationTime;
        private TimeSpan validationAndCompilationTime;
        private TimeSpan packagePreparationTime;
        private TimeSpan totalEvaluationTime;
        private TimeSpan submitToResultTime;
        private TimeSpan submitToPresentTime;
        private int compilerInvocationCount;

        public TrackAuthoringEvaluationCoordinator(TrackAuthoringSession session)
            : this(
                session,
                AuthoringEvaluationSchedulerOptions.Synchronous,
                new ProductionTrackCandidateEvaluator(),
                SystemAuthoringEvaluationClock.Instance)
        {
        }

        public TrackAuthoringEvaluationCoordinator(
            TrackAuthoringSession session,
            AuthoringEvaluationSchedulerOptions options)
            : this(
                session,
                options,
                new ProductionTrackCandidateEvaluator(),
                SystemAuthoringEvaluationClock.Instance)
        {
        }

        internal TrackAuthoringEvaluationCoordinator(
            TrackAuthoringSession session,
            AuthoringEvaluationSchedulerOptions options,
            ITrackCandidateEvaluator evaluator,
            IAuthoringEvaluationClock clock)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));

            if (options.ExecutionMode ==
                AuthoringEvaluationExecutionMode.SerializedBackground)
            {
                workerTask = Task.Run(WorkerLoopAsync);
            }
        }

        public AuthoringEvaluationSchedulerOptions Options => options;

        public AuthoringEvaluationSubmission SubmitProvisionalEdit(
            TransactionRevision transactionRevision,
            Quantum.Track.Authoring.ITrackAuthoringCandidateOperation operation,
            CancellationToken cancellationToken = default)
        {
            if (operation is null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            cancellationToken.ThrowIfCancellationRequested();
            WorkItem item;
            lock (gate)
            {
                ThrowIfDisposed();
                if (frozenCommitTransaction.HasValue &&
                    frozenCommitTransaction.Value == transactionRevision)
                {
                    throw new InvalidOperationException(
                        "The newest provisional revision is frozen for commit.");
                }

                requestSequence++;
                AuthoringSessionId currentSessionId = session.SessionId;
                var requestRevision = new EvaluationRequestRevision(
                    currentSessionId,
                    requestSequence);
                AuthoringCandidateReservationResult reservation =
                    session.ReserveCandidate(
                        transactionRevision,
                        operation,
                        requestRevision,
                        clock.UtcNow);
                if (reservation.Request is null)
                {
                    string message = reservation.Diagnostics.Count == 0
                        ? "The provisional edit could not be reserved."
                        : reservation.Diagnostics[0].Message;
                    throw new InvalidOperationException(message);
                }

                item = new WorkItem(
                    reservation.Request,
                    CancellationTokenSource.CreateLinkedTokenSource(
                        shutdown.Token,
                        cancellationToken));
                submitted++;
                outstanding.Add(item.Request.ProvisionalEditRevision, item);

                if (options.ExecutionMode ==
                    AuthoringEvaluationExecutionMode.SerializedBackground)
                {
                    if (pending != null)
                    {
                        WorkItem replaced = pending;
                        pending = null;
                        outstanding.Remove(replaced.Request.ProvisionalEditRevision);
                        replaced.Cancellation.Cancel();
                        CompleteWithoutStartCore(
                            replaced,
                            AuthoringEvaluationOutcomeStatus.Coalesced,
                            countAsCoalesced: true);
                    }

                    pending = item;
                    maximumPendingDepth = System.Math.Max(maximumPendingDepth, 1);
                    workSignal.Release();
                }
                else
                {
                    running = item;
                    started++;
                    item.StartedAtUtc = clock.UtcNow;
                    lastEvaluationStartedAtUtc = item.StartedAtUtc;
                }
            }

            if (options.ExecutionMode == AuthoringEvaluationExecutionMode.Synchronous)
            {
                EvaluateAndPublishAsync(item).GetAwaiter().GetResult();
                lock (gate)
                {
                    if (ReferenceEquals(running, item))
                    {
                        running = null;
                    }

                    outstanding.Remove(item.Request.ProvisionalEditRevision);
                }
            }

            return new AuthoringEvaluationSubmission(item.Request, item.Completion.Task);
        }

        public async Task<AuthoringScheduledCommitResult> CommitLatestAsync(
            TransactionRevision transactionRevision,
            CancellationToken cancellationToken = default)
        {
            EvaluatedCandidateRevision newestCandidateRevision;
            ProvisionalEditRevision provisionalRevision;
            EvaluatedTrackCandidate? candidate;
            WorkItem? work = null;
            lock (gate)
            {
                ThrowIfDisposed();
                if (frozenCommitTransaction.HasValue)
                {
                    throw new InvalidOperationException(
                        "A final provisional revision is already waiting to commit.");
                }

                if (!session.TryGetNewestCandidate(
                    transactionRevision,
                    out newestCandidateRevision,
                    out candidate))
                {
                    throw new InvalidOperationException(
                        "The active transaction has no provisional revision to commit.");
                }

                provisionalRevision = newestCandidateRevision.ProvisionalEditRevision;

                frozenCommitTransaction = transactionRevision;
                frozenCommitRevision = provisionalRevision;
                if (candidate is null)
                {
                    if (!outstanding.TryGetValue(provisionalRevision, out work))
                    {
                        frozenCommitTransaction = null;
                        frozenCommitRevision = null;
                        return new AuthoringScheduledCommitResult(
                            newestCandidateRevision,
                            AuthoringEvaluationOutcomeStatus.Stale,
                            commitResult: null);
                    }

                    work.FinalCommitPriority = true;
                    workSignal.Release();
                }
            }

            try
            {
                EvaluatedCandidateRevision candidateRevision;
                AuthoringEvaluationOutcomeStatus evaluationStatus;
                if (candidate != null)
                {
                    candidateRevision = candidate.Revision;
                    evaluationStatus = candidate.IsCommitEligible
                        ? AuthoringEvaluationOutcomeStatus.Accepted
                        : AuthoringEvaluationOutcomeStatus.Rejected;
                }
                else
                {
                    AuthoringEvaluationOutcome outcome;
                    try
                    {
                        outcome = await WaitWithCancellationAsync(
                            work!.Completion.Task,
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        work!.Cancellation.Cancel();
                        return new AuthoringScheduledCommitResult(
                            work.Request.CandidateRevision,
                            AuthoringEvaluationOutcomeStatus.Cancelled,
                            commitResult: null);
                    }

                    candidateRevision = outcome.Request.CandidateRevision;
                    evaluationStatus = outcome.Status;
                    if (outcome.Status != AuthoringEvaluationOutcomeStatus.Accepted &&
                        outcome.Status != AuthoringEvaluationOutcomeStatus.Rejected)
                    {
                        return new AuthoringScheduledCommitResult(
                            candidateRevision,
                            outcome.Status,
                            commitResult: null);
                    }
                }

                AuthoringCommitResult commitResult = session.Commit(candidateRevision);
                return new AuthoringScheduledCommitResult(
                    candidateRevision,
                    evaluationStatus,
                    commitResult);
            }
            finally
            {
                lock (gate)
                {
                    if (frozenCommitTransaction == transactionRevision &&
                        frozenCommitRevision == provisionalRevision)
                    {
                        frozenCommitTransaction = null;
                        frozenCommitRevision = null;
                    }
                }
            }
        }

        public bool CancelTransaction(TransactionRevision transactionRevision)
        {
            WorkItem? pendingToComplete = null;
            lock (gate)
            {
                ThrowIfDisposed();
                if (pending != null &&
                    pending.Request.TransactionRevision == transactionRevision)
                {
                    pendingToComplete = pending;
                    pending = null;
                    outstanding.Remove(
                        pendingToComplete.Request.ProvisionalEditRevision);
                    pendingToComplete.Cancellation.Cancel();
                    CompleteWithoutStartCore(
                        pendingToComplete,
                        AuthoringEvaluationOutcomeStatus.Cancelled,
                        countAsCoalesced: false);
                }

                if (running != null &&
                    running.Request.TransactionRevision == transactionRevision)
                {
                    running.Cancellation.Cancel();
                }
            }

            return session.Cancel(transactionRevision);
        }

        public EvaluationSchedulerSnapshot CaptureSnapshot()
        {
            lock (gate)
            {
                return new EvaluationSchedulerSnapshot(
                    submitted,
                    started,
                    completed,
                    accepted,
                    rejected,
                    stale,
                    coalesced,
                    cancelledBeforeStart,
                    cancelled,
                    faulted,
                    maximumPendingDepth,
                    pending is null ? 0 : 1,
                    running != null,
                    queueWaitTime,
                    candidateApplicationTime,
                    validationAndCompilationTime,
                    packagePreparationTime,
                    totalEvaluationTime,
                    submitToResultTime,
                    submitToPresentTime,
                    compilerInvocationCount);
            }
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            WorkItem? pendingToComplete;
            Task? worker;
            lock (gate)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                shutdown.Cancel();
                pendingToComplete = pending;
                pending = null;
                if (pendingToComplete != null)
                {
                    outstanding.Remove(
                        pendingToComplete.Request.ProvisionalEditRevision);
                    pendingToComplete.Cancellation.Cancel();
                    CompleteWithoutStartCore(
                        pendingToComplete,
                        AuthoringEvaluationOutcomeStatus.Cancelled,
                        countAsCoalesced: false);
                }

                running?.Cancellation.Cancel();
                worker = workerTask;
                workSignal.Release();
            }

            if (worker != null)
            {
                try
                {
                    await worker.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            workSignal.Dispose();
            shutdown.Dispose();
        }

        private async Task WorkerLoopAsync()
        {
            while (!shutdown.IsCancellationRequested)
            {
                try
                {
                    await workSignal.WaitAsync(shutdown.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                while (!shutdown.IsCancellationRequested)
                {
                    WorkItem? item;
                    TimeSpan delay;
                    lock (gate)
                    {
                        item = pending;
                        if (item is null)
                        {
                            break;
                        }

                        if (item.Cancellation.IsCancellationRequested)
                        {
                            pending = null;
                            outstanding.Remove(item.Request.ProvisionalEditRevision);
                            CompleteWithoutStartCore(
                                item,
                                AuthoringEvaluationOutcomeStatus.Cancelled,
                                countAsCoalesced: false);
                            continue;
                        }

                        if (!session.IsEvaluationRequestCurrent(item.Request))
                        {
                            pending = null;
                            outstanding.Remove(item.Request.ProvisionalEditRevision);
                            CompleteWithoutStartCore(
                                item,
                                AuthoringEvaluationOutcomeStatus.Stale,
                                countAsCoalesced: false);
                            continue;
                        }

                        delay = item.FinalCommitPriority
                            ? TimeSpan.Zero
                            : RemainingThrottleDelayCore(clock.UtcNow);
                        if (delay == TimeSpan.Zero)
                        {
                            pending = null;
                            running = item;
                            item.StartedAtUtc = clock.UtcNow;
                            lastEvaluationStartedAtUtc = item.StartedAtUtc;
                            started++;
                        }
                    }

                    if (delay > TimeSpan.Zero)
                    {
                        Task delayTask = clock.Delay(delay, shutdown.Token);
                        Task signalTask = workSignal.WaitAsync(shutdown.Token);
                        try
                        {
                            await Task.WhenAny(delayTask, signalTask).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }

                        continue;
                    }

                    await EvaluateAndPublishAsync(item!).ConfigureAwait(false);
                    lock (gate)
                    {
                        if (ReferenceEquals(running, item))
                        {
                            running = null;
                        }

                        outstanding.Remove(item!.Request.ProvisionalEditRevision);
                        if (pending != null)
                        {
                            workSignal.Release();
                        }
                    }
                }
            }
        }

        private async Task EvaluateAndPublishAsync(WorkItem item)
        {
            TrackCandidateEvaluationProduct? product = null;
            AuthoringCandidatePublicationResult? publication = null;
            AuthoringEvaluationFault? evaluationFault = null;
            AuthoringEvaluationOutcomeStatus status;
            try
            {
                item.Cancellation.Token.ThrowIfCancellationRequested();
                product = await evaluator.EvaluateAsync(
                    item.Request,
                    item.Cancellation.Token).ConfigureAwait(false);
                item.Cancellation.Token.ThrowIfCancellationRequested();

                if (!session.IsEvaluationRequestCurrent(item.Request))
                {
                    status = AuthoringEvaluationOutcomeStatus.Stale;
                }
                else
                {
                    try
                    {
                        publication = session.PublishCandidate(product);
                    }
                    catch (Exception exception)
                    {
                        evaluationFault = new AuthoringEvaluationFault(
                            AuthoringEvaluationPhase.Publication,
                            exception);
                    }

                    if (evaluationFault != null)
                    {
                        status = AuthoringEvaluationOutcomeStatus.Faulted;
                    }
                    else if (!publication!.AcceptedBySession)
                    {
                        status = AuthoringEvaluationOutcomeStatus.Stale;
                    }
                    else
                    {
                        status = product.Candidate.IsCommitEligible
                            ? AuthoringEvaluationOutcomeStatus.Accepted
                            : AuthoringEvaluationOutcomeStatus.Rejected;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                status = AuthoringEvaluationOutcomeStatus.Cancelled;
            }
            catch (AuthoringEvaluationException exception)
            {
                evaluationFault = new AuthoringEvaluationFault(
                    exception.Phase,
                    exception.InnerException ?? exception);
                status = AuthoringEvaluationOutcomeStatus.Faulted;
            }
            catch (Exception exception)
            {
                evaluationFault = new AuthoringEvaluationFault(
                    AuthoringEvaluationPhase.CandidateEvaluation,
                    exception);
                status = AuthoringEvaluationOutcomeStatus.Faulted;
            }

            DateTimeOffset resultAtUtc = clock.UtcNow;
            TimeSpan queueWait = item.StartedAtUtc.HasValue
                ? NonNegative(item.StartedAtUtc.Value - item.Request.SubmittedAtUtc)
                : TimeSpan.Zero;
            TimeSpan submitToResult =
                NonNegative(resultAtUtc - item.Request.SubmittedAtUtc);
            TimeSpan? submitToPresent =
                status == AuthoringEvaluationOutcomeStatus.Accepted
                    ? submitToResult
                    : (TimeSpan?)null;
            var timing = new AuthoringEvaluationTiming(
                queueWait,
                product?.CandidateApplicationTime ?? TimeSpan.Zero,
                product?.ValidationAndCompilationTime ?? TimeSpan.Zero,
                product?.PackagePreparationTime ?? TimeSpan.Zero,
                product?.TotalEvaluationTime ?? TimeSpan.Zero,
                submitToResult,
                submitToPresent,
                product?.CompilerInvocationCount ?? 0);
            IReadOnlyList<AuthoringSessionDiagnostic> diagnostics =
                publication?.UpdateResult.Diagnostics ??
                product?.Candidate.ApplicationDiagnostics ??
                Array.Empty<AuthoringSessionDiagnostic>();
            var outcome = new AuthoringEvaluationOutcome(
                item.Request,
                status,
                product?.Candidate,
                diagnostics,
                timing,
                evaluationFault);

            lock (gate)
            {
                completed++;
                RecordOutcomeCore(outcome);
            }

            item.Completion.TrySetResult(outcome);
            item.Cancellation.Dispose();
        }

        private void CompleteWithoutStartCore(
            WorkItem item,
            AuthoringEvaluationOutcomeStatus status,
            bool countAsCoalesced)
        {
            DateTimeOffset resultAtUtc = clock.UtcNow;
            TimeSpan submitToResult =
                NonNegative(resultAtUtc - item.Request.SubmittedAtUtc);
            var timing = new AuthoringEvaluationTiming(
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero,
                submitToResult,
                submitToPresentTime: null,
                compilerInvocationCount: 0);
            var outcome = new AuthoringEvaluationOutcome(
                item.Request,
                status,
                candidate: null,
                Array.Empty<AuthoringSessionDiagnostic>(),
                timing,
                fault: null);
            if (status == AuthoringEvaluationOutcomeStatus.Cancelled ||
                status == AuthoringEvaluationOutcomeStatus.Coalesced)
            {
                cancelledBeforeStart++;
            }
            if (countAsCoalesced)
            {
                coalesced++;
            }

            RecordOutcomeCore(outcome);
            item.Completion.TrySetResult(outcome);
            item.Cancellation.Dispose();
        }

        private void RecordOutcomeCore(AuthoringEvaluationOutcome outcome)
        {
            switch (outcome.Status)
            {
                case AuthoringEvaluationOutcomeStatus.Accepted:
                    accepted++;
                    break;
                case AuthoringEvaluationOutcomeStatus.Rejected:
                    rejected++;
                    break;
                case AuthoringEvaluationOutcomeStatus.Stale:
                    stale++;
                    break;
                case AuthoringEvaluationOutcomeStatus.Cancelled:
                    cancelled++;
                    break;
                case AuthoringEvaluationOutcomeStatus.Faulted:
                    faulted++;
                    break;
            }

            queueWaitTime += outcome.Timing.QueueWaitTime;
            candidateApplicationTime += outcome.Timing.CandidateApplicationTime;
            validationAndCompilationTime +=
                outcome.Timing.ValidationAndCompilationTime;
            packagePreparationTime += outcome.Timing.PackagePreparationTime;
            totalEvaluationTime += outcome.Timing.TotalEvaluationTime;
            submitToResultTime += outcome.Timing.SubmitToResultTime;
            submitToPresentTime += outcome.Timing.SubmitToPresentTime ?? TimeSpan.Zero;
            compilerInvocationCount += outcome.Timing.CompilerInvocationCount;
        }

        private TimeSpan RemainingThrottleDelayCore(DateTimeOffset nowUtc)
        {
            if (!lastEvaluationStartedAtUtc.HasValue ||
                options.MinimumEvaluationInterval == TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }

            TimeSpan elapsed = NonNegative(nowUtc - lastEvaluationStartedAtUtc.Value);
            return elapsed >= options.MinimumEvaluationInterval
                ? TimeSpan.Zero
                : options.MinimumEvaluationInterval - elapsed;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(
                    nameof(TrackAuthoringEvaluationCoordinator));
            }
        }

        private static TimeSpan NonNegative(TimeSpan value) =>
            value < TimeSpan.Zero ? TimeSpan.Zero : value;

        private static async Task<T> WaitWithCancellationAsync<T>(
            Task<T> task,
            CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled)
            {
                return await task.ConfigureAwait(false);
            }

            var cancellationCompletion =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(
                state => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
                cancellationCompletion))
            {
                Task completedTask = await Task.WhenAny(
                    task,
                    cancellationCompletion.Task).ConfigureAwait(false);
                if (!ReferenceEquals(completedTask, task))
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            return await task.ConfigureAwait(false);
        }

        private sealed class WorkItem
        {
            internal WorkItem(
                AuthoringEvaluationRequest request,
                CancellationTokenSource cancellation)
            {
                Request = request;
                Cancellation = cancellation;
                Completion = new TaskCompletionSource<AuthoringEvaluationOutcome>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }

            internal AuthoringEvaluationRequest Request { get; }

            internal CancellationTokenSource Cancellation { get; }

            internal TaskCompletionSource<AuthoringEvaluationOutcome> Completion { get; }

            internal DateTimeOffset? StartedAtUtc { get; set; }

            internal bool FinalCommitPriority { get; set; }
        }
    }
}
