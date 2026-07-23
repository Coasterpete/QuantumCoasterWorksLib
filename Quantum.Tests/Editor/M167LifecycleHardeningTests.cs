using System.Diagnostics;
using Quantum.Application.Authoring;
using Quantum.Editor.Avalonia.Services;
using Quantum.Editor.Avalonia.Services.Documents;

namespace Quantum.Tests.Editor;

public sealed class M167LifecycleHardeningTests
{
    [Fact]
    public async Task ReplacementDiscardsRunningNonCooperativeCompletionWithoutBlocking()
    {
        var evaluator = new NonCooperativeEvaluator();
        TrackAuthoringEvaluationCoordinator? retiredCoordinator = null;
        TrackAuthoringSession? retiredSession = null;
        await using var workspace = CreateWorkspace(
            evaluator,
            (coordinator, session) =>
            {
                retiredCoordinator ??= coordinator;
                retiredSession ??= session;
            });
        TrackEditorDocument original = M167HardeningFoundationTests.CreateDocument("replace-old");
        workspace.Documents.SetActiveDocument(original);
        Assert.True(workspace.BeginStraightLengthEdit("launch"));
        AuthoringEvaluationSubmission submission = workspace.SubmitStraightLengthEdit(42.0);
        await evaluator.Started;

        TrackEditorDocument replacement =
            M167HardeningFoundationTests.CreateDocument("replace-new");
        var replacementElapsed = Stopwatch.StartNew();
        workspace.Documents.SetActiveDocument(replacement);
        replacementElapsed.Stop();

        Assert.True(replacementElapsed.Elapsed < TimeSpan.FromSeconds(1));
        Assert.Same(replacement, workspace.ActiveDocument);
        Assert.Null(workspace.StraightLengthEdit);
        evaluator.Release();
        AuthoringEvaluationOutcome completion = await submission.Completion;
        await workspace.WaitForLifecycleCompletionAsync();

        Assert.Equal(AuthoringEvaluationOutcomeStatus.Cancelled, completion.Status);
        Assert.False(workspace.PublishStraightLengthOutcome(completion));
        Assert.Equal(30.0, M167HardeningFoundationTests.StraightLength(original), 9);
        Assert.Equal(
            30.0,
            GetStraightLength(retiredSession!.CommittedState.PreparedState),
            9);
        EvaluationSchedulerSnapshot diagnostics = retiredCoordinator!.CaptureSnapshot();
        Assert.Equal(1, diagnostics.DiscardedPostLifecycleCompletions);
        Assert.Equal(0, diagnostics.Accepted);
        Assert.False(diagnostics.IsEvaluationRunning);
    }

    [Fact]
    public async Task CloseCancelsQueuedAndRunningWorkAndCompletesCleanly()
    {
        var evaluator = new NonCooperativeEvaluator();
        TrackAuthoringEvaluationCoordinator? retiredCoordinator = null;
        await using var workspace = CreateWorkspace(
            evaluator,
            (coordinator, _) => retiredCoordinator = coordinator);
        TrackEditorDocument document = M167HardeningFoundationTests.CreateDocument("close");
        workspace.Documents.SetActiveDocument(document);
        Assert.True(workspace.BeginStraightLengthEdit("launch"));
        AuthoringEvaluationSubmission running = workspace.SubmitStraightLengthEdit(31.0);
        await evaluator.Started;
        AuthoringEvaluationSubmission pending = workspace.SubmitStraightLengthEdit(32.0);

        var closeElapsed = Stopwatch.StartNew();
        workspace.Documents.CloseDocument(document);
        closeElapsed.Stop();
        Assert.True(closeElapsed.Elapsed < TimeSpan.FromSeconds(1));
        Assert.Null(workspace.ActiveDocument);

        AuthoringEvaluationOutcome pendingOutcome = await pending.Completion;
        evaluator.Release();
        AuthoringEvaluationOutcome runningOutcome = await running.Completion;
        await workspace.WaitForLifecycleCompletionAsync();

        Assert.Equal(AuthoringEvaluationOutcomeStatus.Cancelled, pendingOutcome.Status);
        Assert.Equal(AuthoringEvaluationOutcomeStatus.Cancelled, runningOutcome.Status);
        EvaluationSchedulerSnapshot diagnostics = retiredCoordinator!.CaptureSnapshot();
        Assert.Equal(2, diagnostics.Cancelled);
        Assert.Equal(1, diagnostics.DiscardedPostLifecycleCompletions);
    }

    [Fact]
    public async Task ReplacementAbandonsFrozenFinalCommitAndNeverMutatesNewOwner()
    {
        var evaluator = new NonCooperativeEvaluator();
        TrackAuthoringEvaluationCoordinator? retiredCoordinator = null;
        var workspace = CreateWorkspace(
            evaluator,
            (coordinator, _) => retiredCoordinator ??= coordinator);
        try
        {
            TrackEditorDocument original =
                M167HardeningFoundationTests.CreateDocument("final-old");
            workspace.Documents.SetActiveDocument(original);
            Assert.True(workspace.BeginStraightLengthEdit("launch"));
            workspace.SubmitStraightLengthEdit(44.0);
            await evaluator.Started;
            Task<AuthoringScheduledCommitResult?> finalCommit =
                workspace.CommitStraightLengthEditAsync();

            TrackEditorDocument replacement =
                M167HardeningFoundationTests.CreateDocument("final-new");
            workspace.Documents.SetActiveDocument(replacement);
            evaluator.Release();
            AuthoringScheduledCommitResult result = (await finalCommit)!;
            await workspace.WaitForLifecycleCompletionAsync();

            Assert.False(result.Succeeded);
            Assert.Equal(AuthoringEvaluationOutcomeStatus.Cancelled, result.EvaluationStatus);
            Assert.Equal(30.0, M167HardeningFoundationTests.StraightLength(original), 9);
            Assert.Equal(30.0, M167HardeningFoundationTests.StraightLength(replacement), 9);
            EvaluationSchedulerSnapshot diagnostics = retiredCoordinator!.CaptureSnapshot();
            Assert.Equal(1, diagnostics.AbandonedFinalCommits);
            Assert.Equal(1, diagnostics.DiscardedPostLifecycleCompletions);
        }
        finally
        {
            await workspace.DisposeAsync();
        }
    }

    [Fact]
    public async Task ShutdownInitiationDoesNotBlockRunningCompilation()
    {
        var evaluator = new NonCooperativeEvaluator();
        TrackAuthoringEvaluationCoordinator? retiredCoordinator = null;
        var workspace = CreateWorkspace(
            evaluator,
            (coordinator, _) => retiredCoordinator = coordinator);
        TrackEditorDocument document = M167HardeningFoundationTests.CreateDocument("shutdown");
        workspace.Documents.SetActiveDocument(document);
        Assert.True(workspace.BeginStraightLengthEdit("launch"));
        AuthoringEvaluationSubmission submission = workspace.SubmitStraightLengthEdit(45.0);
        await evaluator.Started;
        Task<AuthoringScheduledCommitResult?> finalCommit =
            workspace.CommitStraightLengthEditAsync();

        var shutdownElapsed = Stopwatch.StartNew();
        workspace.Dispose();
        shutdownElapsed.Stop();
        Assert.True(shutdownElapsed.Elapsed < TimeSpan.FromSeconds(1));
        Assert.False(workspace.WaitForLifecycleCompletionAsync().IsCompleted);

        evaluator.Release();
        Assert.Equal(
            AuthoringEvaluationOutcomeStatus.Cancelled,
            (await submission.Completion).Status);
        Assert.Equal(
            AuthoringEvaluationOutcomeStatus.Cancelled,
            (await finalCommit)!.EvaluationStatus);
        await workspace.WaitForLifecycleCompletionAsync();
        EvaluationSchedulerSnapshot diagnostics = retiredCoordinator!.CaptureSnapshot();
        Assert.Equal(1, diagnostics.DiscardedPostLifecycleCompletions);
        Assert.Equal(1, diagnostics.AbandonedFinalCommits);
        Assert.Equal(30.0, M167HardeningFoundationTests.StraightLength(document), 9);
    }

    [Fact]
    public async Task SessionLifecycleRevisionChangeRejectsStaleCompletion()
    {
        var evaluator = new NonCooperativeEvaluator();
        TrackEditorDocument document =
            M167HardeningFoundationTests.CreateDocument("stale-session");
        PreparedTrackGraphState state = document.CaptureGraphState();
        var session = new TrackAuthoringSession(state);
        var coordinator = new TrackAuthoringEvaluationCoordinator(
            session,
            new AuthoringEvaluationSchedulerOptions(
                AuthoringEvaluationExecutionMode.SerializedBackground,
                TimeSpan.Zero),
            evaluator,
            SystemAuthoringEvaluationClock.Instance);
        try
        {
            InteractiveAuthoringTransaction transaction = session.BeginTransaction(
                "launch",
                "StraightSectionDefinition.Length",
                "stale lifecycle");
            AuthoringEvaluationSubmission submission = coordinator.SubmitProvisionalEdit(
                transaction.Revision,
                new SetLengthOperation(41.0));
            await evaluator.Started;

            session.ReplaceSessionState(state, markClean: true);
            evaluator.Release();
            AuthoringEvaluationOutcome outcome = await submission.Completion;

            Assert.Equal(AuthoringEvaluationOutcomeStatus.Stale, outcome.Status);
            Assert.Equal(1, coordinator.CaptureSnapshot().RejectedStaleCompletions);
            Assert.Same(state, session.PresentedState);
            Assert.Same(state, session.CommittedState.PreparedState);
        }
        finally
        {
            await coordinator.DisposeAsync();
        }
    }

    private static EditorWorkspace CreateWorkspace(
        NonCooperativeEvaluator evaluator,
        Action<TrackAuthoringEvaluationCoordinator, TrackAuthoringSession> created)
    {
        return new EditorWorkspace(
            evaluationCoordinatorFactory: session =>
            {
                var coordinator = new TrackAuthoringEvaluationCoordinator(
                    session,
                    new AuthoringEvaluationSchedulerOptions(
                        AuthoringEvaluationExecutionMode.SerializedBackground,
                        TimeSpan.Zero),
                    evaluator,
                    SystemAuthoringEvaluationClock.Instance);
                created(coordinator, session);
                return coordinator;
            });
    }

    private static double GetStraightLength(PreparedTrackGraphState state)
    {
        return Assert.IsType<Quantum.Track.Authoring.StraightSectionDefinition>(
            state.Graph.Nodes.Single(node => node.Id == "launch").Section).Length;
    }

    private sealed class NonCooperativeEvaluator : ITrackCandidateEvaluator
    {
        private readonly TaskCompletionSource<bool> started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task Started => started.Task;

        internal void Release() => release.TrySetResult(true);

        public async Task<TrackCandidateEvaluationProduct> EvaluateAsync(
            AuthoringEvaluationRequest request,
            CancellationToken cancellationToken)
        {
            started.TrySetResult(true);
            await release.Task.ConfigureAwait(false);
            return await new ProductionTrackCandidateEvaluator().EvaluateAsync(
                request,
                CancellationToken.None);
        }
    }

    private sealed class SetLengthOperation : Quantum.Track.Authoring.ITrackAuthoringCandidateOperation
    {
        private readonly double length;

        internal SetLengthOperation(double length)
        {
            this.length = length;
        }

        public string OperationTypeId => "test.setStraightLength";

        public Quantum.Track.Authoring.TrackAuthoringGraph Apply(
            Quantum.Track.Authoring.TrackAuthoringGraph sourceGraph)
        {
            var straight = Assert.IsType<Quantum.Track.Authoring.StraightSectionDefinition>(
                sourceGraph.Nodes.Single(node => node.Id == "launch").Section);
            return Quantum.Track.Authoring.TrackAuthoringGraphOperations.Replace(
                sourceGraph,
                "launch",
                new Quantum.Track.Authoring.StraightSectionDefinition(
                    "launch",
                    length,
                    straight.RollRadians));
        }
    }
}
