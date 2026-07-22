using Quantum.Application.Authoring;
using Quantum.IO.TrackLayout.V2;
using Quantum.Track.Authoring;

namespace QuantumCoasterWorks.Tests.Application;

public sealed class TrackAuthoringEvaluationCoordinatorTests
{
    [Fact]
    public async Task SynchronousModeMatchesDirectSessionEvaluationAndCommit()
    {
        TrackAuthoringSession direct = CreateSession();
        TrackAuthoringSession scheduled = CreateSession();
        InteractiveAuthoringTransaction directTransaction = Begin(direct);
        InteractiveAuthoringTransaction scheduledTransaction = Begin(scheduled);
        CandidateUpdateResult directUpdate = direct.SubmitCandidate(
            directTransaction.Revision,
            RadiusOperation(35.0));
        using var coordinator = new TrackAuthoringEvaluationCoordinator(scheduled);

        AuthoringEvaluationSubmission submission = coordinator.SubmitProvisionalEdit(
            scheduledTransaction.Revision,
            RadiusOperation(35.0));
        AuthoringEvaluationOutcome outcome = await submission.Completion;
        AuthoringCommitResult directCommit = direct.Commit(directUpdate.Candidate!.Revision);
        AuthoringScheduledCommitResult scheduledCommit =
            await coordinator.CommitLatestAsync(scheduledTransaction.Revision);

        Assert.True(submission.Completion.IsCompleted);
        Assert.Equal(AuthoringEvaluationOutcomeStatus.Accepted, outcome.Status);
        Assert.True(directCommit.Succeeded);
        Assert.True(scheduledCommit.Succeeded);
        Assert.Equal(
            direct.CommittedState.CanonicalPackageJson,
            scheduled.CommittedState.CanonicalPackageJson);
        Assert.Equal(35.0, Radius(scheduled.CommittedState.SourceGraph));
    }

    [Fact]
    public async Task BackgroundModeKeepsOneRunningAndOnlyLatestPending()
    {
        TrackAuthoringSession session = CreateSession();
        InteractiveAuthoringTransaction transaction = Begin(session);
        var evaluator = new ControlledEvaluator();
        await using var coordinator = CreateBackground(session, evaluator);
        AuthoringEvaluationSubmission first = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            RadiusOperation(25.0));
        await evaluator.WaitForInvocationAsync(0);

        AuthoringEvaluationSubmission second = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            RadiusOperation(30.0));
        AuthoringEvaluationSubmission third = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            RadiusOperation(35.0));
        AuthoringEvaluationSubmission fourth = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            RadiusOperation(40.0));

        Assert.Equal(
            AuthoringEvaluationOutcomeStatus.Coalesced,
            (await second.Completion).Status);
        Assert.Equal(
            AuthoringEvaluationOutcomeStatus.Coalesced,
            (await third.Completion).Status);
        evaluator.Release(0);
        await evaluator.WaitForInvocationAsync(1);
        Assert.Equal(fourth.Request.RequestRevision, evaluator.RequestAt(1).RequestRevision);
        evaluator.Release(1);

        Assert.Equal(AuthoringEvaluationOutcomeStatus.Stale, (await first.Completion).Status);
        Assert.Equal(AuthoringEvaluationOutcomeStatus.Accepted, (await fourth.Completion).Status);
        EvaluationSchedulerSnapshot snapshot = coordinator.CaptureSnapshot();
        Assert.Equal(4, snapshot.Submitted);
        Assert.Equal(2, snapshot.Started);
        Assert.Equal(2, snapshot.Completed);
        Assert.Equal(2, snapshot.Coalesced);
        Assert.Equal(2, snapshot.CancelledBeforeStart);
        Assert.Equal(1, snapshot.MaximumPendingDepth);
        Assert.Equal(1, evaluator.MaximumConcurrentEvaluations);
        Assert.Equal(40.0, Radius(session.PresentedState.Graph));
    }

    [Fact]
    public async Task OlderCompletionAfterNewerAcceptedResultCannotBeAdopted()
    {
        TrackAuthoringSession session = CreateSession();
        InteractiveAuthoringTransaction transaction = Begin(session);
        var evaluator = new ControlledEvaluator();
        await using var coordinator = CreateBackground(session, evaluator);
        AuthoringEvaluationSubmission older = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            RadiusOperation(30.0));
        await evaluator.WaitForInvocationAsync(0);

        CandidateUpdateResult newer = session.SubmitCandidate(
            transaction.Revision,
            RadiusOperation(45.0));
        PreparedTrackGraphState newerPresentation = session.PresentedState;
        evaluator.Release(0);
        AuthoringEvaluationOutcome outcome = await older.Completion;

        Assert.True(newer.CandidateAccepted);
        Assert.Equal(AuthoringEvaluationOutcomeStatus.Stale, outcome.Status);
        Assert.Same(newerPresentation, session.PresentedState);
        Assert.Equal(45.0, Radius(session.PresentedState.Graph));
    }

    [Fact]
    public async Task StaleValidResultCannotReplaceNewerInvalidState()
    {
        TrackAuthoringSession session = CreateSession();
        PreparedTrackGraphState committedPresentation = session.PresentedState;
        InteractiveAuthoringTransaction transaction = Begin(session);
        var evaluator = new ControlledEvaluator();
        await using var coordinator = CreateBackground(session, evaluator);
        AuthoringEvaluationSubmission olderValid = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            RadiusOperation(30.0));
        await evaluator.WaitForInvocationAsync(0);

        CandidateUpdateResult newerInvalid = session.SubmitCandidate(
            transaction.Revision,
            new DisconnectedRouteOperation());
        evaluator.Release(0);
        AuthoringEvaluationOutcome outcome = await olderValid.Completion;

        Assert.False(newerInvalid.CandidateAccepted);
        Assert.Equal(AuthoringEvaluationOutcomeStatus.Stale, outcome.Status);
        Assert.Same(committedPresentation, session.PresentedState);
        Assert.Same(newerInvalid.Candidate, session.ActiveTransaction!.NewestCandidate);
        Assert.NotEmpty(session.ActiveTransaction.NewestCandidate!.GraphDiagnostics);
    }

    [Fact]
    public async Task StaleInvalidResultCannotReplaceNewerValidDiagnostics()
    {
        TrackAuthoringSession session = CreateSession();
        InteractiveAuthoringTransaction transaction = Begin(session);
        var evaluator = new ControlledEvaluator();
        await using var coordinator = CreateBackground(session, evaluator);
        AuthoringEvaluationSubmission olderInvalid = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            new DisconnectedRouteOperation());
        await evaluator.WaitForInvocationAsync(0);

        CandidateUpdateResult newerValid = session.SubmitCandidate(
            transaction.Revision,
            RadiusOperation(42.0));
        evaluator.Release(0);
        AuthoringEvaluationOutcome outcome = await olderInvalid.Completion;

        Assert.Equal(AuthoringEvaluationOutcomeStatus.Stale, outcome.Status);
        Assert.Same(newerValid.Candidate, session.ActiveTransaction!.NewestCandidate);
        Assert.Empty(session.ActiveTransaction.NewestCandidate!.GraphDiagnostics);
        Assert.Equal(42.0, Radius(session.PresentedState.Graph));
    }

    [Fact]
    public async Task TransactionCancellationPreventsPendingStartAndRunningAdoption()
    {
        TrackAuthoringSession session = CreateSession();
        InteractiveAuthoringTransaction transaction = Begin(session);
        var evaluator = new ControlledEvaluator();
        await using var coordinator = CreateBackground(session, evaluator);
        AuthoringEvaluationSubmission running = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            RadiusOperation(30.0));
        await evaluator.WaitForInvocationAsync(0);
        AuthoringEvaluationSubmission pending = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            RadiusOperation(40.0));

        Assert.True(coordinator.CancelTransaction(transaction.Revision));
        Assert.Equal(
            AuthoringEvaluationOutcomeStatus.Cancelled,
            (await pending.Completion).Status);
        evaluator.Release(0);
        Assert.Equal(
            AuthoringEvaluationOutcomeStatus.Cancelled,
            (await running.Completion).Status);

        Assert.Null(session.ActiveTransaction);
        Assert.Same(session.CommittedState.PreparedState, session.PresentedState);
        Assert.Equal(0, session.History.UndoCount);
        Assert.Equal(1, evaluator.InvocationCount);
        Assert.Equal(1, coordinator.CaptureSnapshot().CancelledBeforeStart);
    }

    [Fact]
    public async Task CancellationDuringNonCancellableEvaluationPreventsAdoption()
    {
        TrackAuthoringSession session = CreateSession();
        InteractiveAuthoringTransaction transaction = Begin(session);
        var evaluator = new ControlledEvaluator();
        await using var coordinator = CreateBackground(session, evaluator);
        using var cancellation = new CancellationTokenSource();
        AuthoringEvaluationSubmission submission = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            RadiusOperation(35.0),
            cancellation.Token);
        await evaluator.WaitForInvocationAsync(0);

        cancellation.Cancel();
        evaluator.Release(0);
        AuthoringEvaluationOutcome outcome = await submission.Completion;

        Assert.Equal(AuthoringEvaluationOutcomeStatus.Cancelled, outcome.Status);
        Assert.Equal(20.0, Radius(session.PresentedState.Graph));
        Assert.Null(session.ActiveTransaction!.NewestCandidate);
        Assert.Equal(1, coordinator.CaptureSnapshot().Cancelled);
    }

    [Fact]
    public async Task SessionReplacementInvalidatesRunningAndPendingRequests()
    {
        TrackAuthoringSession session = CreateSession();
        InteractiveAuthoringTransaction transaction = Begin(session);
        var evaluator = new ControlledEvaluator();
        await using var coordinator = CreateBackground(session, evaluator);
        AuthoringEvaluationSubmission running = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            RadiusOperation(30.0));
        await evaluator.WaitForInvocationAsync(0);
        AuthoringEvaluationSubmission pending = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            RadiusOperation(40.0));
        PreparedTrackGraphState replacement = CreatePreparedState(60.0);

        session.ReplaceSessionState(replacement, markClean: true);
        evaluator.Release(0);

        Assert.Equal(AuthoringEvaluationOutcomeStatus.Stale, (await running.Completion).Status);
        Assert.Equal(AuthoringEvaluationOutcomeStatus.Stale, (await pending.Completion).Status);
        Assert.Equal(1, evaluator.InvocationCount);
        Assert.Same(replacement, session.CommittedState.PreparedState);
        Assert.Same(replacement, session.PresentedState);
        Assert.Equal(0, session.History.UndoCount);
    }

    [Fact]
    public async Task EvaluatorFaultLeavesCommittedSessionUsable()
    {
        TrackAuthoringSession session = CreateSession();
        InteractiveAuthoringTransaction transaction = Begin(session);
        var evaluator = new ControlledEvaluator(faultedInvocationIndexes: new[] { 0 });
        await using var coordinator = CreateBackground(session, evaluator);
        AuthoringEvaluationSubmission faulted = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            RadiusOperation(30.0));
        await evaluator.WaitForInvocationAsync(0);
        evaluator.Release(0);

        AuthoringEvaluationOutcome faultOutcome = await faulted.Completion;
        Assert.Equal(AuthoringEvaluationOutcomeStatus.Faulted, faultOutcome.Status);
        Assert.NotNull(faultOutcome.Fault);
        Assert.Equal(AuthoringEvaluationPhase.CandidateEvaluation, faultOutcome.Fault!.Phase);
        Assert.Equal(20.0, Radius(session.CommittedState.SourceGraph));
        Assert.NotNull(session.ActiveTransaction);

        AuthoringEvaluationSubmission recovery = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            RadiusOperation(32.0));
        await evaluator.WaitForInvocationAsync(1);
        evaluator.Release(1);
        Assert.Equal(
            AuthoringEvaluationOutcomeStatus.Accepted,
            (await recovery.Completion).Status);
        Assert.True((await coordinator.CommitLatestAsync(transaction.Revision)).Succeeded);
        Assert.Equal(32.0, Radius(session.CommittedState.SourceGraph));
    }

    [Fact]
    public async Task CommitWaitsForExactNewestRevision()
    {
        TrackAuthoringSession session = CreateSession();
        InteractiveAuthoringTransaction transaction = Begin(session);
        var evaluator = new ControlledEvaluator();
        await using var coordinator = CreateBackground(session, evaluator);
        AuthoringEvaluationSubmission older = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            RadiusOperation(30.0));
        await evaluator.WaitForInvocationAsync(0);
        AuthoringEvaluationSubmission newest = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            RadiusOperation(40.0));

        Task<AuthoringScheduledCommitResult> commitTask =
            coordinator.CommitLatestAsync(transaction.Revision);
        Assert.False(commitTask.IsCompleted);
        evaluator.Release(0);
        await evaluator.WaitForInvocationAsync(1);
        Assert.Equal(newest.Request.CandidateRevision, evaluator.RequestAt(1).CandidateRevision);
        evaluator.Release(1);
        AuthoringScheduledCommitResult commit = await commitTask;

        Assert.Equal(AuthoringEvaluationOutcomeStatus.Stale, (await older.Completion).Status);
        Assert.Equal(AuthoringEvaluationOutcomeStatus.Accepted, (await newest.Completion).Status);
        Assert.True(commit.Succeeded);
        Assert.Equal(newest.Request.CandidateRevision, commit.CandidateRevision);
        Assert.Equal(40.0, Radius(session.CommittedState.SourceGraph));
        Assert.Equal(1, session.History.UndoCount);
    }

    [Fact]
    public async Task TransactionCancellationWhileCommitWaitsCannotCommit()
    {
        TrackAuthoringSession session = CreateSession();
        InteractiveAuthoringTransaction transaction = Begin(session);
        var evaluator = new ControlledEvaluator();
        await using var coordinator = CreateBackground(session, evaluator);
        AuthoringEvaluationSubmission submission = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            RadiusOperation(36.0));
        await evaluator.WaitForInvocationAsync(0);
        Task<AuthoringScheduledCommitResult> commitTask =
            coordinator.CommitLatestAsync(transaction.Revision);

        Assert.True(coordinator.CancelTransaction(transaction.Revision));
        evaluator.Release(0);
        AuthoringScheduledCommitResult commit = await commitTask;

        Assert.Equal(AuthoringEvaluationOutcomeStatus.Cancelled, commit.EvaluationStatus);
        Assert.Null(commit.CommitResult);
        Assert.Equal(
            AuthoringEvaluationOutcomeStatus.Cancelled,
            (await submission.Completion).Status);
        Assert.Equal(20.0, Radius(session.CommittedState.SourceGraph));
        Assert.Equal(0, session.History.UndoCount);
        Assert.Null(session.ActiveTransaction);
    }

    [Fact]
    public async Task CommitNeverAdoptsOlderLastGoodPreviewWhenNewestIsInvalid()
    {
        TrackAuthoringSession session = CreateSession();
        InteractiveAuthoringTransaction transaction = Begin(session);
        var evaluator = new ControlledEvaluator();
        await using var coordinator = CreateBackground(session, evaluator);
        AuthoringEvaluationSubmission valid = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            RadiusOperation(30.0));
        await evaluator.WaitForInvocationAsync(0);
        evaluator.Release(0);
        Assert.Equal(AuthoringEvaluationOutcomeStatus.Accepted, (await valid.Completion).Status);
        Assert.Equal(30.0, Radius(session.PresentedState.Graph));

        AuthoringEvaluationSubmission invalid = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            new DisconnectedRouteOperation());
        await evaluator.WaitForInvocationAsync(1);
        Task<AuthoringScheduledCommitResult> commitTask =
            coordinator.CommitLatestAsync(transaction.Revision);
        evaluator.Release(1);
        AuthoringScheduledCommitResult commit = await commitTask;

        Assert.Equal(AuthoringEvaluationOutcomeStatus.Rejected, (await invalid.Completion).Status);
        Assert.False(commit.Succeeded);
        Assert.Contains(
            commit.CommitResult!.Diagnostics,
            diagnostic => diagnostic.Code == AuthoringSessionDiagnosticCode.CandidateRejected);
        Assert.Equal(20.0, Radius(session.CommittedState.SourceGraph));
        Assert.Equal(30.0, Radius(session.PresentedState.Graph));
        Assert.Equal(0, session.History.UndoCount);
    }

    [Fact]
    public async Task FinalCommitRevisionBypassesNormalThrottle()
    {
        TrackAuthoringSession session = CreateSession();
        InteractiveAuthoringTransaction transaction = Begin(session);
        var evaluator = new ControlledEvaluator();
        var clock = new FakeEvaluationClock();
        var options = new AuthoringEvaluationSchedulerOptions(
            AuthoringEvaluationExecutionMode.SerializedBackground,
            TimeSpan.FromHours(1));
        await using var coordinator = new TrackAuthoringEvaluationCoordinator(
            session,
            options,
            evaluator,
            clock);
        AuthoringEvaluationSubmission first = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            RadiusOperation(30.0));
        await evaluator.WaitForInvocationAsync(0);
        evaluator.Release(0);
        await first.Completion;
        AuthoringEvaluationSubmission final = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            RadiusOperation(38.0));
        await clock.WaitForDelayAsync();

        Task<AuthoringScheduledCommitResult> commitTask =
            coordinator.CommitLatestAsync(transaction.Revision);
        await evaluator.WaitForInvocationAsync(1);
        evaluator.Release(1);
        AuthoringScheduledCommitResult commit = await commitTask;

        Assert.True(commit.Succeeded);
        Assert.Equal(AuthoringEvaluationOutcomeStatus.Accepted, (await final.Completion).Status);
        Assert.Equal(38.0, Radius(session.CommittedState.SourceGraph));
        Assert.Equal(DateTimeOffset.UnixEpoch, clock.UtcNow);
    }

    [Fact]
    public async Task CountersAndTimingsFollowFakeClockAndEvaluator()
    {
        TrackAuthoringSession session = CreateSession();
        InteractiveAuthoringTransaction transaction = Begin(session);
        var evaluator = new ControlledEvaluator();
        var clock = new FakeEvaluationClock();
        await using var coordinator = CreateBackground(session, evaluator, clock);
        AuthoringEvaluationSubmission first = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            RadiusOperation(30.0));
        await evaluator.WaitForInvocationAsync(0);
        AuthoringEvaluationSubmission second = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            RadiusOperation(40.0));
        clock.Advance(TimeSpan.FromMilliseconds(10));
        evaluator.Release(0);
        await evaluator.WaitForInvocationAsync(1);
        clock.Advance(TimeSpan.FromMilliseconds(5));
        evaluator.Release(1);
        await Task.WhenAll(first.Completion, second.Completion);

        EvaluationSchedulerSnapshot snapshot = coordinator.CaptureSnapshot();
        Assert.Equal(2, snapshot.Submitted);
        Assert.Equal(2, snapshot.Started);
        Assert.Equal(2, snapshot.Completed);
        Assert.Equal(1, snapshot.Accepted);
        Assert.Equal(1, snapshot.Stale);
        Assert.Equal(TimeSpan.FromMilliseconds(10), snapshot.QueueWaitTime);
        Assert.Equal(TimeSpan.FromMilliseconds(2), snapshot.CandidateApplicationTime);
        Assert.Equal(TimeSpan.FromMilliseconds(4), snapshot.ValidationAndCompilationTime);
        Assert.Equal(TimeSpan.FromMilliseconds(6), snapshot.PackagePreparationTime);
        Assert.Equal(TimeSpan.FromMilliseconds(12), snapshot.TotalEvaluationTime);
        Assert.Equal(TimeSpan.FromMilliseconds(25), snapshot.SubmitToResultTime);
        Assert.Equal(TimeSpan.FromMilliseconds(15), snapshot.SubmitToPresentTime);
        Assert.Equal(2, snapshot.CompilerInvocationCount);
        Assert.Equal(1, evaluator.MaximumConcurrentEvaluations);
    }

    private static TrackAuthoringEvaluationCoordinator CreateBackground(
        TrackAuthoringSession session,
        ControlledEvaluator evaluator,
        IAuthoringEvaluationClock? clock = null)
    {
        return new TrackAuthoringEvaluationCoordinator(
            session,
            new AuthoringEvaluationSchedulerOptions(
                AuthoringEvaluationExecutionMode.SerializedBackground,
                TimeSpan.Zero),
            evaluator,
            clock ?? new FakeEvaluationClock());
    }

    private static TrackAuthoringSession CreateSession() =>
        new TrackAuthoringSession(CreatePreparedState(20.0));

    private static PreparedTrackGraphState CreatePreparedState(double radius)
    {
        var graph = new TrackAuthoringGraph(
            new TrackAuthoringGraphNode[]
            {
                new TrackAuthoringGraphNode(new StraightSectionDefinition("entry", 5.0)),
                new TrackAuthoringGraphNode(
                    new ConstantCurvatureSectionDefinition("turn", 8.0, radius))
            },
            new[] { new TrackAuthoringGraphEdge("entry", "turn") });
        return PreparedTrackGraphState.Prepare(
            graph,
            new TrackLayoutPackageV2GraphAncillaryState(
                TrackLayoutPackageV2Dto.ContractName,
                TrackLayoutPackageV2Dto.ContractVersion,
                "meters",
                "M167.3 self-authored scheduler test",
                "m167-3-scheduler-test",
                heartlineOffset: null));
    }

    private static InteractiveAuthoringTransaction Begin(TrackAuthoringSession session) =>
        session.BeginTransaction("turn", "radius", "Edit turn radius");

    private static TrackAuthoringCandidateOperation RadiusOperation(double radius) =>
        TrackAuthoringCandidateOperation.Replace(
            "turn",
            new ConstantCurvatureSectionDefinition("turn", 8.0, radius));

    private static double Radius(TrackAuthoringGraph graph) =>
        Assert.IsType<ConstantCurvatureSectionDefinition>(
            graph.Nodes.Single(node => node.Id == "turn").Section).Radius;

    private sealed class DisconnectedRouteOperation : ITrackAuthoringCandidateOperation
    {
        public string OperationTypeId => "test.disconnectRoute";

        public TrackAuthoringGraph Apply(TrackAuthoringGraph sourceGraph) =>
            new TrackAuthoringGraph(
                sourceGraph.Nodes,
                Array.Empty<TrackAuthoringGraphEdge>(),
                sourceGraph.StartPose,
                sourceGraph.Banking);
    }

    private sealed class ControlledEvaluator : ITrackCandidateEvaluator
    {
        private readonly object gate = new object();
        private readonly List<Invocation> invocations = new List<Invocation>();
        private readonly HashSet<int> faultedInvocationIndexes;
        private readonly SemaphoreSlim invocationSignal = new SemaphoreSlim(0);
        private int activeEvaluations;

        internal ControlledEvaluator(IEnumerable<int>? faultedInvocationIndexes = null)
        {
            this.faultedInvocationIndexes = faultedInvocationIndexes is null
                ? new HashSet<int>()
                : new HashSet<int>(faultedInvocationIndexes);
        }

        internal int InvocationCount
        {
            get { lock (gate) { return invocations.Count; } }
        }

        internal int MaximumConcurrentEvaluations { get; private set; }

        public async Task<TrackCandidateEvaluationProduct> EvaluateAsync(
            AuthoringEvaluationRequest request,
            CancellationToken cancellationToken)
        {
            Invocation invocation;
            int index;
            lock (gate)
            {
                index = invocations.Count;
                invocation = new Invocation(request);
                invocations.Add(invocation);
                activeEvaluations++;
                MaximumConcurrentEvaluations =
                    System.Math.Max(MaximumConcurrentEvaluations, activeEvaluations);
            }

            invocationSignal.Release();
            try
            {
                // Intentionally ignores cancellation while held. This models the
                // current non-cooperative compiler boundary.
                await invocation.Release.Task.ConfigureAwait(false);
                if (faultedInvocationIndexes.Contains(index))
                {
                    throw new InvalidOperationException("Controlled evaluator fault.");
                }

                TrackCandidateEvaluationProduct production =
                    await new ProductionTrackCandidateEvaluator().EvaluateAsync(
                        request,
                        CancellationToken.None).ConfigureAwait(false);
                return new TrackCandidateEvaluationProduct(
                    production.Candidate,
                    TimeSpan.FromMilliseconds(1),
                    TimeSpan.FromMilliseconds(2),
                    TimeSpan.FromMilliseconds(3),
                    TimeSpan.FromMilliseconds(6),
                    production.CompilerInvocationCount);
            }
            finally
            {
                lock (gate)
                {
                    activeEvaluations--;
                }
            }
        }

        internal AuthoringEvaluationRequest RequestAt(int index)
        {
            lock (gate)
            {
                return invocations[index].Request;
            }
        }

        internal void Release(int index)
        {
            lock (gate)
            {
                invocations[index].Release.TrySetResult(true);
            }
        }

        internal async Task WaitForInvocationAsync(int index)
        {
            while (true)
            {
                lock (gate)
                {
                    if (invocations.Count > index)
                    {
                        return;
                    }
                }

                bool signalled = await invocationSignal.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.True(signalled, $"Timed out waiting for evaluator invocation {index}.");
            }
        }

        private sealed class Invocation
        {
            internal Invocation(AuthoringEvaluationRequest request)
            {
                Request = request;
                Release = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }

            internal AuthoringEvaluationRequest Request { get; }

            internal TaskCompletionSource<bool> Release { get; }
        }
    }

    private sealed class FakeEvaluationClock : IAuthoringEvaluationClock
    {
        private readonly object gate = new object();
        private readonly List<ScheduledDelay> delays = new List<ScheduledDelay>();
        private readonly SemaphoreSlim delaySignal = new SemaphoreSlim(0);
        private DateTimeOffset utcNow = DateTimeOffset.UnixEpoch;

        public DateTimeOffset UtcNow
        {
            get { lock (gate) { return utcNow; } }
        }

        public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            if (delay <= TimeSpan.Zero)
            {
                return Task.CompletedTask;
            }

            ScheduledDelay scheduled;
            lock (gate)
            {
                scheduled = new ScheduledDelay(utcNow + delay, cancellationToken);
                delays.Add(scheduled);
            }

            delaySignal.Release();
            return scheduled.Completion.Task;
        }

        internal void Advance(TimeSpan elapsed)
        {
            List<ScheduledDelay> due;
            lock (gate)
            {
                utcNow += elapsed;
                due = delays.Where(delay => delay.DueAtUtc <= utcNow).ToList();
                delays.RemoveAll(delay => delay.DueAtUtc <= utcNow);
            }

            foreach (ScheduledDelay delay in due)
            {
                delay.Completion.TrySetResult(true);
            }
        }

        internal async Task WaitForDelayAsync()
        {
            lock (gate)
            {
                if (delays.Count != 0)
                {
                    return;
                }
            }

            bool signalled = await delaySignal.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(signalled, "Timed out waiting for a throttle delay.");
        }

        private sealed class ScheduledDelay
        {
            internal ScheduledDelay(
                DateTimeOffset dueAtUtc,
                CancellationToken cancellationToken)
            {
                DueAtUtc = dueAtUtc;
                Completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                if (cancellationToken.CanBeCanceled)
                {
                    cancellationToken.Register(
                        state => ((TaskCompletionSource<bool>)state!).TrySetCanceled(),
                        Completion);
                }
            }

            internal DateTimeOffset DueAtUtc { get; }

            internal TaskCompletionSource<bool> Completion { get; }
        }
    }
}
