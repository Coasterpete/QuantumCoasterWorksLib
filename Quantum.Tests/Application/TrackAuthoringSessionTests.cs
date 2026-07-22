using Quantum.Application.Authoring;
using Quantum.IO.TrackLayout.V2;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class TrackAuthoringSessionTests
{
    [Fact]
    public void ProvisionalUpdatesKeepCommittedPersistenceDirtyAndHistoryUnchanged()
    {
        TrackAuthoringSession session = CreateSession();
        CommittedTrackState committedBefore = session.CommittedState;
        string persistedBefore = session.PersistableCanonicalPackageJson!;
        var sources = new List<TrackAuthoringGraph>();
        InteractiveAuthoringTransaction transaction = session.BeginTransaction(
            "turn",
            "radius",
            "Edit turn radius");

        CandidateUpdateResult first = session.SubmitCandidate(
            transaction.Revision,
            new AbsoluteRadiusOperation(30.0, sources));
        CandidateUpdateResult second = session.SubmitCandidate(
            transaction.Revision,
            new AbsoluteRadiusOperation(45.0, sources));

        Assert.True(first.CandidateAccepted);
        Assert.True(second.CandidateAccepted);
        Assert.Equal(1, first.Candidate!.Revision.ProvisionalEditRevision.Sequence);
        Assert.Equal(2, second.Candidate!.Revision.ProvisionalEditRevision.Sequence);
        Assert.All(sources, source => Assert.Same(committedBefore.SourceGraph, source));
        Assert.Same(committedBefore, session.CommittedState);
        Assert.Equal(20.0, Radius(session.CommittedState.SourceGraph));
        Assert.Equal(45.0, Radius(session.PresentedState.Graph));
        Assert.Same(second.Candidate.PreparedState, session.PresentedState);
        Assert.True(session.ActiveTransaction!.IsPresentedCandidateCurrent);
        Assert.False(session.IsDirty);
        Assert.Equal(0, session.History.UndoCount);
        Assert.Equal(0, session.History.RedoCount);
        Assert.Equal(persistedBefore, session.PersistableCanonicalPackageJson);
        Assert.NotEqual(
            persistedBefore,
            session.PresentedState.CanonicalPackageJson);
    }

    [Fact]
    public void InvalidNewestCandidateCannotCommitAndLeavesLastValidPreviewMarkedStale()
    {
        TrackAuthoringSession session = CreateSession();
        InteractiveAuthoringTransaction transaction = session.BeginTransaction(
            "turn",
            "radius",
            "Edit turn radius");
        CandidateUpdateResult valid = session.SubmitCandidate(
            transaction.Revision,
            RadiusOperation(30.0));
        PreparedTrackGraphState validPreview = session.PresentedState;
        string persistedBefore = session.PersistableCanonicalPackageJson!;

        CandidateUpdateResult invalid = session.SubmitCandidate(
            transaction.Revision,
            new DisconnectedRouteOperation());
        AuthoringCommitResult commit = session.Commit(invalid.Candidate!.Revision);

        Assert.True(invalid.WasEvaluated);
        Assert.False(invalid.CandidateAccepted);
        Assert.Equal(2, invalid.Candidate.Revision.ProvisionalEditRevision.Sequence);
        Assert.NotEmpty(invalid.Candidate.GraphDiagnostics);
        Assert.Contains(
            invalid.Diagnostics,
            diagnostic => diagnostic.Code == AuthoringSessionDiagnosticCode.CandidateRejected);
        Assert.Same(validPreview, session.PresentedState);
        Assert.Same(valid.Candidate!.PreparedState, session.ActiveTransaction!.PresentedCandidate!.PreparedState);
        Assert.False(session.ActiveTransaction.IsPresentedCandidateCurrent);
        Assert.False(commit.Succeeded);
        Assert.Contains(
            commit.Diagnostics,
            diagnostic => diagnostic.Code == AuthoringSessionDiagnosticCode.CandidateRejected);
        Assert.Equal(20.0, Radius(session.CommittedState.SourceGraph));
        Assert.Equal(persistedBefore, session.PersistableCanonicalPackageJson);
        Assert.False(session.IsDirty);
        Assert.Equal(0, session.History.UndoCount);
    }

    [Fact]
    public void CancelRestoresCommittedPresentationCreatesNoHistoryAndPreservesRedo()
    {
        TrackAuthoringSession session = CreateSession();
        CommitRadius(session, 30.0);
        Assert.True(session.Undo());
        Assert.Equal(1, session.History.RedoCount);
        bool dirtyBefore = session.IsDirty;
        InteractiveAuthoringTransaction transaction = session.BeginTransaction(
            "turn",
            "radius",
            "Preview turn radius");
        Assert.True(session.SubmitCandidate(
            transaction.Revision,
            RadiusOperation(50.0)).CandidateAccepted);

        Assert.True(session.Cancel(transaction.Revision));

        Assert.Null(session.ActiveTransaction);
        Assert.Same(session.CommittedState.PreparedState, session.PresentedState);
        Assert.Equal(20.0, Radius(session.PresentedState.Graph));
        Assert.Equal(0, session.History.UndoCount);
        Assert.Equal(1, session.History.RedoCount);
        Assert.Equal(dirtyBefore, session.IsDirty);
    }

    [Fact]
    public void NoOpCommitCreatesNoHistoryAndDoesNotClearRedo()
    {
        TrackAuthoringSession session = CreateSession();
        CommitRadius(session, 30.0);
        Assert.True(session.Undo());
        InteractiveAuthoringTransaction transaction = session.BeginTransaction(
            "turn",
            "radius",
            "No-op turn edit");
        CandidateUpdateResult update = session.SubmitCandidate(
            transaction.Revision,
            new ReturnSourceOperation());

        AuthoringCommitResult commit = session.Commit(update.Candidate!.Revision);

        Assert.True(commit.Succeeded);
        Assert.False(commit.Changed);
        Assert.Null(session.ActiveTransaction);
        Assert.Equal(0, session.History.UndoCount);
        Assert.Equal(1, session.History.RedoCount);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void ChangedCommitAdoptsExactEvaluatedStateOnceWithoutCompilingAgain()
    {
        TrackAuthoringSession session = CreateSession();
        InteractiveAuthoringTransaction transaction = session.BeginTransaction(
            "turn",
            "radius",
            "Edit turn radius");
        Assert.True(session.SubmitCandidate(
            transaction.Revision,
            RadiusOperation(25.0)).CandidateAccepted);
        CandidateUpdateResult update = session.SubmitCandidate(
            transaction.Revision,
            RadiusOperation(30.0));
        EvaluatedTrackCandidate candidate = update.Candidate!;

        using TrackAuthoringPipelineMeasurement measurement =
            TrackAuthoringPipelineMeasurement.Begin();
        AuthoringCommitResult commit = session.Commit(candidate.Revision);

        Assert.True(commit.Succeeded);
        Assert.True(commit.Changed);
        Assert.Equal(0, measurement.GraphCompilerInvocationCount);
        Assert.Equal(TimeSpan.Zero, measurement.GraphCompilerElapsed);
        Assert.Same(candidate.PreparedState, session.CommittedState.PreparedState);
        Assert.Same(
            candidate.Evaluation.CompileResult,
            session.CommittedState.Compilation);
        Assert.Same(
            candidate.Evaluation.CompileResult!.Compilation,
            session.CommittedState.Compilation!.Compilation);
        Assert.Same(
            candidate.PreparedState!.CanonicalPackageJson,
            session.CommittedState.CanonicalPackageJson);
        Assert.Equal(1, session.History.UndoCount);
        Assert.Equal(0, session.History.RedoCount);
        Assert.True(session.History.RetainedPackageByteCount > 0);
        Assert.True(session.IsDirty);
    }

    [Fact]
    public void UndoAndRedoRestoreExactPreparedStatesWithoutCompilationAndTrackSavepoint()
    {
        TrackAuthoringSession session = CreateSession();
        PreparedTrackGraphState before = session.CommittedState.PreparedState;
        EvaluatedTrackCandidate candidate = CommitRadius(session, 30.0);
        PreparedTrackGraphState after = candidate.PreparedState!;
        Assert.True(session.IsDirty);
        long commitRevision = session.CommittedState.Revision.Sequence;

        using (TrackAuthoringPipelineMeasurement undoMeasurement =
               TrackAuthoringPipelineMeasurement.Begin())
        {
            Assert.True(session.Undo());
            Assert.Equal(0, undoMeasurement.GraphCompilerInvocationCount);
        }

        Assert.Same(before, session.CommittedState.PreparedState);
        Assert.Equal(20.0, Radius(session.CommittedState.SourceGraph));
        Assert.False(session.IsDirty);
        Assert.True(session.CommittedState.Revision.Sequence > commitRevision);
        long undoRevision = session.CommittedState.Revision.Sequence;

        using (TrackAuthoringPipelineMeasurement redoMeasurement =
               TrackAuthoringPipelineMeasurement.Begin())
        {
            Assert.True(session.Redo());
            Assert.Equal(0, redoMeasurement.GraphCompilerInvocationCount);
        }

        Assert.Same(after, session.CommittedState.PreparedState);
        Assert.Equal(30.0, Radius(session.CommittedState.SourceGraph));
        Assert.True(session.IsDirty);
        Assert.True(session.CommittedState.Revision.Sequence > undoRevision);
    }

    [Fact]
    public void StaleAndMismatchedStructuralRevisionsAreRejectedSynchronously()
    {
        TrackAuthoringSession session = CreateSession();
        TrackAuthoringSession otherSession = CreateSession();
        InteractiveAuthoringTransaction transaction = session.BeginTransaction(
            "turn",
            "radius",
            "Edit turn radius");
        InteractiveAuthoringTransaction otherTransaction = otherSession.BeginTransaction(
            "turn",
            "radius",
            "Other edit");

        using (TrackAuthoringPipelineMeasurement measurement =
               TrackAuthoringPipelineMeasurement.Begin())
        {
            CandidateUpdateResult mismatch = session.SubmitCandidate(
                otherTransaction.Revision,
                RadiusOperation(25.0));
            Assert.False(mismatch.WasEvaluated);
            Assert.Equal(0, measurement.GraphCompilerInvocationCount);
            Assert.Contains(
                mismatch.Diagnostics,
                diagnostic => diagnostic.Code ==
                    AuthoringSessionDiagnosticCode.TransactionRevisionMismatch);
        }

        CandidateUpdateResult first = session.SubmitCandidate(
            transaction.Revision,
            RadiusOperation(30.0));
        CandidateUpdateResult newest = session.SubmitCandidate(
            transaction.Revision,
            RadiusOperation(40.0));
        AuthoringCommitResult staleCommit = session.Commit(first.Candidate!.Revision);

        Assert.False(staleCommit.Succeeded);
        Assert.Contains(
            staleCommit.Diagnostics,
            diagnostic => diagnostic.Code ==
                AuthoringSessionDiagnosticCode.CandidateRevisionMismatch);

        EvaluatedCandidateRevision structurallyEqual = new EvaluatedCandidateRevision(
            newest.Candidate!.Revision.BaseCommittedRevision,
            new ProvisionalEditRevision(
                new TransactionRevision(
                    session.SessionId,
                transaction.Revision.Sequence),
                newest.Candidate.Revision.ProvisionalEditRevision.Sequence));
        Assert.Equal(newest.Candidate.Revision, structurallyEqual);
        Assert.True(session.Commit(structurallyEqual).Succeeded);
        Assert.Equal(40.0, Radius(session.CommittedState.SourceGraph));
    }

    [Fact]
    public void OneShotPathIsHeadlessAndCreatesOneAtomicHistoryEntry()
    {
        TrackAuthoringSession session = CreateSession();

        AuthoringOneShotResult result = session.ApplyOneShot(
            "turn",
            "radius",
            "Edit turn radius",
            RadiusOperation(35.0));

        Assert.True(result.Succeeded);
        Assert.True(result.Commit!.Changed);
        Assert.Equal(35.0, Radius(session.CommittedState.SourceGraph));
        Assert.Equal(1, session.History.UndoCount);
        Assert.Null(session.ActiveTransaction);
        Assert.DoesNotContain(
            typeof(TrackAuthoringSession).Assembly.GetReferencedAssemblies(),
            reference => reference.Name != null &&
                reference.Name.StartsWith("Avalonia", StringComparison.Ordinal));
    }

    [Fact]
    public void MarkCleanAndSessionReplacementOwnDirtyAndRevisionBaselines()
    {
        TrackAuthoringSession session = CreateSession(markClean: false);
        AuthoringSessionId firstSessionId = session.SessionId;
        Assert.True(session.IsDirty);
        session.MarkClean();
        Assert.False(session.IsDirty);
        CommitRadius(session, 30.0);
        Assert.True(session.IsDirty);

        PreparedTrackGraphState replacement = CreatePreparedState(radius: 50.0);
        session.ReplaceSessionState(replacement, markClean: true);

        Assert.NotEqual(firstSessionId, session.SessionId);
        Assert.Same(replacement, session.CommittedState.PreparedState);
        Assert.Same(replacement, session.PresentedState);
        Assert.Equal(0, session.CommittedState.Revision.Sequence);
        Assert.Equal(0, session.History.UndoCount);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void TransactionAndProvisionalRevisionSequencesIncreaseMonotonically()
    {
        TrackAuthoringSession session = CreateSession();
        InteractiveAuthoringTransaction first = session.BeginTransaction(
            "turn",
            "radius",
            "First edit");
        CandidateUpdateResult invalid = session.SubmitCandidate(
            first.Revision,
            new DisconnectedRouteOperation());
        CandidateUpdateResult valid = session.SubmitCandidate(
            first.Revision,
            RadiusOperation(25.0));
        Assert.True(session.Cancel(first.Revision));
        InteractiveAuthoringTransaction second = session.BeginTransaction(
            "turn",
            "radius",
            "Second edit");

        Assert.True(second.Revision.Sequence > first.Revision.Sequence);
        Assert.Equal(1, invalid.Candidate!.Revision.ProvisionalEditRevision.Sequence);
        Assert.Equal(2, valid.Candidate!.Revision.ProvisionalEditRevision.Sequence);
    }

    private static TrackAuthoringSession CreateSession(bool markClean = true)
    {
        return new TrackAuthoringSession(CreatePreparedState(radius: 20.0), markClean);
    }

    private static PreparedTrackGraphState CreatePreparedState(double radius)
    {
        var graph = new TrackAuthoringGraph(
            new TrackAuthoringGraphNode[]
            {
                new TrackAuthoringGraphNode(
                    new StraightSectionDefinition("entry", 5.0)),
                new TrackAuthoringGraphNode(
                    new ConstantCurvatureSectionDefinition("turn", 8.0, radius))
            },
            new[] { new TrackAuthoringGraphEdge("entry", "turn") });
        var ancillary = new TrackLayoutPackageV2GraphAncillaryState(
            TrackLayoutPackageV2Dto.ContractName,
            TrackLayoutPackageV2Dto.ContractVersion,
            "meters",
            "M167.2 self-authored test",
            "m167-2-test",
            heartlineOffset: null);
        return PreparedTrackGraphState.Prepare(graph, ancillary);
    }

    private static TrackAuthoringCandidateOperation RadiusOperation(double radius)
    {
        return TrackAuthoringCandidateOperation.Replace(
            "turn",
            new ConstantCurvatureSectionDefinition("turn", 8.0, radius));
    }

    private static EvaluatedTrackCandidate CommitRadius(
        TrackAuthoringSession session,
        double radius)
    {
        InteractiveAuthoringTransaction transaction = session.BeginTransaction(
            "turn",
            "radius",
            "Edit turn radius");
        CandidateUpdateResult update = session.SubmitCandidate(
            transaction.Revision,
            RadiusOperation(radius));
        Assert.True(update.CandidateAccepted);
        Assert.True(session.Commit(update.Candidate!.Revision).Succeeded);
        return update.Candidate;
    }

    private static double Radius(TrackAuthoringGraph graph)
    {
        return Assert.IsType<ConstantCurvatureSectionDefinition>(
            graph.Nodes.Single(node => node.Id == "turn").Section).Radius;
    }

    private sealed class AbsoluteRadiusOperation : ITrackAuthoringCandidateOperation
    {
        private readonly double radius;
        private readonly ICollection<TrackAuthoringGraph> observedSources;

        public AbsoluteRadiusOperation(
            double radius,
            ICollection<TrackAuthoringGraph> observedSources)
        {
            this.radius = radius;
            this.observedSources = observedSources;
        }

        public string OperationTypeId => "test.absoluteRadius";

        public TrackAuthoringGraph Apply(TrackAuthoringGraph sourceGraph)
        {
            observedSources.Add(sourceGraph);
            return TrackAuthoringGraphOperations.Replace(
                sourceGraph,
                "turn",
                new ConstantCurvatureSectionDefinition("turn", 8.0, radius));
        }
    }

    private sealed class DisconnectedRouteOperation : ITrackAuthoringCandidateOperation
    {
        public string OperationTypeId => "test.disconnectRoute";

        public TrackAuthoringGraph Apply(TrackAuthoringGraph sourceGraph)
        {
            return new TrackAuthoringGraph(
                sourceGraph.Nodes,
                Array.Empty<TrackAuthoringGraphEdge>(),
                sourceGraph.StartPose,
                sourceGraph.Banking);
        }
    }

    private sealed class ReturnSourceOperation : ITrackAuthoringCandidateOperation
    {
        public string OperationTypeId => "test.returnSource";

        public TrackAuthoringGraph Apply(TrackAuthoringGraph sourceGraph) => sourceGraph;
    }
}
