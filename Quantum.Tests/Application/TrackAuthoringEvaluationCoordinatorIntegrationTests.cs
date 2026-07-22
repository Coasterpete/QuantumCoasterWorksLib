using Quantum.Application.Authoring;
using Quantum.IO.TrackLayout.V2;
using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace QuantumCoasterWorks.Tests.Application;

public sealed class TrackAuthoringEvaluationCoordinatorIntegrationTests
{
    [Fact]
    public async Task RealStraightCandidateMatchesSynchronousAndCommitDoesNotRecompile()
    {
        PreparedTrackGraphState initial = CreateStraightState(length: 10.0, banking: null);
        var direct = new TrackAuthoringSession(initial);
        var background = new TrackAuthoringSession(initial);
        InteractiveAuthoringTransaction directTransaction = Begin(direct, "straight");
        InteractiveAuthoringTransaction backgroundTransaction = Begin(background, "straight");
        TrackAuthoringCandidateOperation operation = TrackAuthoringCandidateOperation.Replace(
            "straight",
            new StraightSectionDefinition("straight", 14.0));
        CandidateUpdateResult directResult = direct.SubmitCandidate(
            directTransaction.Revision,
            operation);
        await using var coordinator = new TrackAuthoringEvaluationCoordinator(
            background,
            BackgroundOptions());

        AuthoringEvaluationOutcome backgroundResult = await coordinator
            .SubmitProvisionalEdit(backgroundTransaction.Revision, operation)
            .Completion;

        Assert.Equal(AuthoringEvaluationOutcomeStatus.Accepted, backgroundResult.Status);
        Assert.Equal(1, backgroundResult.Timing.CompilerInvocationCount);
        Assert.Equal(
            directResult.Candidate!.PreparedState!.CanonicalPackageJson,
            backgroundResult.Candidate!.PreparedState!.CanonicalPackageJson);
        using TrackAuthoringPipelineMeasurement measurement =
            TrackAuthoringPipelineMeasurement.Begin();
        AuthoringScheduledCommitResult commit =
            await coordinator.CommitLatestAsync(backgroundTransaction.Revision);

        Assert.True(commit.Succeeded);
        Assert.Equal(0, measurement.GraphCompilerInvocationCount);
        Assert.Same(
            backgroundResult.Candidate.PreparedState,
            background.CommittedState.PreparedState);
        Assert.Equal(1, coordinator.CaptureSnapshot().CompilerInvocationCount);
        Assert.Equal(1, background.History.UndoCount);
    }

    [Fact]
    public async Task ExplicitBankingCompilationRejectionPreservesCommittedState()
    {
        var banking = new TrackBankingDefinition(new[]
        {
            new BankingProfileKey(0.0, 0.0),
            new BankingProfileKey(10.0, 0.0)
        });
        PreparedTrackGraphState initial = CreateStraightState(10.0, banking);
        var session = new TrackAuthoringSession(initial);
        InteractiveAuthoringTransaction transaction = Begin(session, "straight");
        await using var coordinator = new TrackAuthoringEvaluationCoordinator(
            session,
            BackgroundOptions());

        AuthoringEvaluationOutcome outcome = await coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            TrackAuthoringCandidateOperation.Append(
                new StraightSectionDefinition("extra", 5.0))).Completion;
        AuthoringScheduledCommitResult commit =
            await coordinator.CommitLatestAsync(transaction.Revision);

        Assert.Equal(AuthoringEvaluationOutcomeStatus.Rejected, outcome.Status);
        Assert.Contains(
            outcome.Candidate!.GraphDiagnostics,
            diagnostic => diagnostic.Code ==
                TrackAuthoringGraphDiagnosticCode.AuthoringCompilationFailed);
        Assert.False(commit.Succeeded);
        Assert.Same(initial, session.CommittedState.PreparedState);
        Assert.Same(initial, session.PresentedState);
        Assert.Equal(0, session.History.UndoCount);
        Assert.False(session.IsDirty);
        Assert.Equal(1, coordinator.CaptureSnapshot().CompilerInvocationCount);
    }

    [Fact]
    public async Task SpatialGSharkCandidateHasSynchronousBackgroundParity()
    {
        SpatialSectionDefinition initialSection = CreateSpatial(
            "spatial",
            new[]
            {
                Vector3d.Zero,
                new Vector3d(2.0, 0.0, 0.0),
                new Vector3d(4.0, 1.4, 2.0),
                new Vector3d(6.0, 2.0, 3.5)
            });
        SpatialSectionDefinition replacement = CreateSpatial(
            "spatial",
            new[]
            {
                Vector3d.Zero,
                new Vector3d(1.5, 0.0, 0.0),
                new Vector3d(3.5, -1.0, 1.5),
                new Vector3d(6.5, -1.8, 2.5)
            });
        PreparedTrackGraphState initial = CreateSingleSectionState(initialSection);
        var direct = new TrackAuthoringSession(initial);
        var background = new TrackAuthoringSession(initial);
        InteractiveAuthoringTransaction directTransaction = Begin(direct, "spatial");
        InteractiveAuthoringTransaction backgroundTransaction = Begin(background, "spatial");
        TrackAuthoringCandidateOperation operation =
            TrackAuthoringCandidateOperation.Replace("spatial", replacement);
        CandidateUpdateResult directResult = direct.SubmitCandidate(
            directTransaction.Revision,
            operation);
        await using var coordinator = new TrackAuthoringEvaluationCoordinator(
            background,
            BackgroundOptions());

        AuthoringEvaluationOutcome first = await coordinator.SubmitProvisionalEdit(
            backgroundTransaction.Revision,
            operation).Completion;

        Assert.Equal(AuthoringEvaluationOutcomeStatus.Accepted, first.Status);
        Assert.Equal(
            directResult.Candidate!.PreparedState!.CanonicalPackageJson,
            first.Candidate!.PreparedState!.CanonicalPackageJson);
        Assert.Equal(1, first.Timing.CompilerInvocationCount);
        Assert.Equal(
            directResult.Candidate.Evaluation.Compilation!.Runtime.TotalLength,
            first.Candidate.Evaluation.Compilation!.Runtime.TotalLength,
            precision: 10);
    }

    [Fact]
    public async Task RepeatedBackgroundEvaluationIsDeterministic()
    {
        var session = new TrackAuthoringSession(
            CreateStraightState(length: 10.0, banking: null));
        InteractiveAuthoringTransaction transaction = Begin(session, "straight");
        TrackAuthoringCandidateOperation operation = TrackAuthoringCandidateOperation.Replace(
            "straight",
            new StraightSectionDefinition("straight", 13.0));
        await using var coordinator = new TrackAuthoringEvaluationCoordinator(
            session,
            BackgroundOptions());

        AuthoringEvaluationOutcome first = await coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            operation).Completion;
        AuthoringEvaluationOutcome second = await coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            operation).Completion;

        Assert.Equal(AuthoringEvaluationOutcomeStatus.Accepted, first.Status);
        Assert.Equal(AuthoringEvaluationOutcomeStatus.Accepted, second.Status);
        Assert.Equal(
            first.Candidate!.PreparedState!.CanonicalPackageJson,
            second.Candidate!.PreparedState!.CanonicalPackageJson);
        Assert.Equal(2, coordinator.CaptureSnapshot().Started);
        Assert.Equal(2, coordinator.CaptureSnapshot().CompilerInvocationCount);
    }

    [Fact]
    public async Task ProductionBackgroundPipelineNeverOverlapsCandidateWork()
    {
        var session = new TrackAuthoringSession(
            CreateStraightState(length: 10.0, banking: null));
        InteractiveAuthoringTransaction transaction = Begin(session, "straight");
        var concurrency = new OperationConcurrencyProbe();
        using var firstRelease = new ManualResetEventSlim(false);
        using var secondRelease = new ManualResetEventSlim(false);
        var firstOperation = new BlockingStraightOperation(
            12.0,
            concurrency,
            firstRelease);
        var secondOperation = new BlockingStraightOperation(
            16.0,
            concurrency,
            secondRelease);
        await using var coordinator = new TrackAuthoringEvaluationCoordinator(
            session,
            BackgroundOptions());

        AuthoringEvaluationSubmission first = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            firstOperation);
        Assert.True(firstOperation.Entered.Wait(TimeSpan.FromSeconds(10)));
        AuthoringEvaluationSubmission second = coordinator.SubmitProvisionalEdit(
            transaction.Revision,
            secondOperation);
        Assert.False(secondOperation.Entered.Wait(TimeSpan.FromMilliseconds(100)));
        firstRelease.Set();
        Assert.True(secondOperation.Entered.Wait(TimeSpan.FromSeconds(10)));
        secondRelease.Set();
        AuthoringEvaluationOutcome[] outcomes =
            await Task.WhenAll(first.Completion, second.Completion);

        Assert.Equal(1, concurrency.MaximumActive);
        Assert.Equal(2, coordinator.CaptureSnapshot().Started);
        Assert.Equal(2, coordinator.CaptureSnapshot().CompilerInvocationCount);
        Assert.Equal(AuthoringEvaluationOutcomeStatus.Stale, outcomes[0].Status);
        Assert.Equal(AuthoringEvaluationOutcomeStatus.Accepted, outcomes[1].Status);
    }

    [Fact]
    public void ApplicationSchedulingBoundaryHasNoFrontendOrRendererDependencies()
    {
        string[] forbiddenPrefixes =
        {
            "Avalonia",
            "Unity",
            "Unreal",
            "Silk",
            "OpenTK"
        };
        string[] references = typeof(TrackAuthoringEvaluationCoordinator).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(
            references,
            reference => forbiddenPrefixes.Any(prefix =>
                reference.StartsWith(prefix, StringComparison.Ordinal)));
    }

    private static AuthoringEvaluationSchedulerOptions BackgroundOptions() =>
        new AuthoringEvaluationSchedulerOptions(
            AuthoringEvaluationExecutionMode.SerializedBackground,
            TimeSpan.Zero);

    private static InteractiveAuthoringTransaction Begin(
        TrackAuthoringSession session,
        string sectionId) =>
        session.BeginTransaction(sectionId, "geometry", "Edit section geometry");

    private static PreparedTrackGraphState CreateStraightState(
        double length,
        TrackBankingDefinition? banking)
    {
        var graph = new TrackAuthoringGraph(
            new[]
            {
                new TrackAuthoringGraphNode(
                    new StraightSectionDefinition("straight", length))
            },
            Array.Empty<TrackAuthoringGraphEdge>(),
            TrackStartPose.Identity,
            banking);
        return Prepare(graph);
    }

    private static PreparedTrackGraphState CreateSingleSectionState(
        TrackAuthoringSectionDefinition definition)
    {
        return Prepare(new TrackAuthoringGraph(
            new[] { new TrackAuthoringGraphNode(definition) },
            Array.Empty<TrackAuthoringGraphEdge>()));
    }

    private static PreparedTrackGraphState Prepare(TrackAuthoringGraph graph) =>
        PreparedTrackGraphState.Prepare(
            graph,
            new TrackLayoutPackageV2GraphAncillaryState(
                TrackLayoutPackageV2Dto.ContractName,
                TrackLayoutPackageV2Dto.ContractVersion,
                "meters",
                "M167.3 self-authored integration test",
                "m167-3-integration-test",
                heartlineOffset: null));

    private static SpatialSectionDefinition CreateSpatial(
        string id,
        IReadOnlyList<Vector3d> controlPoints)
    {
        const int degree = 3;
        double[] weights = Enumerable.Repeat(1.0, controlPoints.Count).ToArray();
        var curve = new GSharkNurbsCurveAdapter(
            controlPoints.ToList(),
            weights.ToList(),
            degree);
        TrackSamplingOptions options = TrackSamplingOptions.Default;
        double length = new ArcLengthLUT(
            curve,
            options.ArcLengthSamples,
            options.ArcLengthTolerance).TotalLength;
        return new SpatialSectionDefinition(
            id,
            length,
            controlPoints,
            degree,
            weights);
    }

    private sealed class OperationConcurrencyProbe
    {
        private int active;
        private int maximumActive;

        internal int MaximumActive => Volatile.Read(ref maximumActive);

        internal void Enter()
        {
            int current = Interlocked.Increment(ref active);
            int observed;
            do
            {
                observed = Volatile.Read(ref maximumActive);
                if (observed >= current)
                {
                    break;
                }
            }
            while (Interlocked.CompareExchange(
                ref maximumActive,
                current,
                observed) != observed);
        }

        internal void Exit() => Interlocked.Decrement(ref active);
    }

    private sealed class BlockingStraightOperation : ITrackAuthoringCandidateOperation
    {
        private readonly double length;
        private readonly OperationConcurrencyProbe concurrency;
        private readonly ManualResetEventSlim release;

        internal BlockingStraightOperation(
            double length,
            OperationConcurrencyProbe concurrency,
            ManualResetEventSlim release)
        {
            this.length = length;
            this.concurrency = concurrency;
            this.release = release;
        }

        internal ManualResetEventSlim Entered { get; } = new ManualResetEventSlim(false);

        public string OperationTypeId => "test.blockingStraight";

        public TrackAuthoringGraph Apply(TrackAuthoringGraph sourceGraph)
        {
            concurrency.Enter();
            Entered.Set();
            try
            {
                if (!release.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("Timed out waiting to release candidate work.");
                }

                return TrackAuthoringGraphOperations.Replace(
                    sourceGraph,
                    "straight",
                    new StraightSectionDefinition("straight", length));
            }
            finally
            {
                concurrency.Exit();
            }
        }
    }
}
