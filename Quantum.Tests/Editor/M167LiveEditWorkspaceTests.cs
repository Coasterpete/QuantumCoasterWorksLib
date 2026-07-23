using Quantum.Application.Authoring;
using Quantum.Editor.Avalonia.Services;
using Quantum.Editor.Avalonia.Services.Authoring;
using Quantum.Editor.Avalonia.Services.Documents;
using Quantum.IO.TrackLayout.V2;
using Quantum.Track.Authoring;

namespace Quantum.Tests.Editor;

public sealed class M167LiveEditWorkspaceTests
{
    [Fact]
    public void ActiveDocumentOwnsOneStableSessionAndCoordinator()
    {
        using var workspace = CreateWorkspace(out _);
        TrackAuthoringSession session = Assert.IsType<TrackAuthoringSession>(
            workspace.AuthoringSession);
        TrackAuthoringEvaluationCoordinator coordinator =
            Assert.IsType<TrackAuthoringEvaluationCoordinator>(workspace.EvaluationCoordinator);

        workspace.SetStatus("Refresh without replacing the document.");

        Assert.Same(session, workspace.AuthoringSession);
        Assert.Same(coordinator, workspace.EvaluationCoordinator);
        Assert.Equal(
            AuthoringEvaluationExecutionMode.SerializedBackground,
            coordinator.Options.ExecutionMode);
    }

    [Fact]
    public void DocumentReplacementCancelsTransactionAndDisposesCoordinator()
    {
        using var workspace = CreateWorkspace(out _);
        TrackAuthoringSession oldSession = workspace.AuthoringSession!;
        TrackAuthoringEvaluationCoordinator oldCoordinator = workspace.EvaluationCoordinator!;
        Assert.True(workspace.BeginStraightLengthEdit("launch"));
        TransactionRevision revision = workspace.StraightLengthEdit!.TransactionRevision;

        workspace.NewDocument();

        Assert.Null(oldSession.ActiveTransaction);
        Assert.False(workspace.IsInteractiveEditActive);
        Assert.NotSame(oldSession, workspace.AuthoringSession);
        Assert.NotSame(oldCoordinator, workspace.EvaluationCoordinator);
        Assert.Throws<ObjectDisposedException>(() => oldCoordinator.SubmitProvisionalEdit(
            revision,
            TrackAuthoringCandidateOperation.Replace(
                "launch",
                new StraightSectionDefinition("launch", 32.0))));
    }

    [Fact]
    public async Task AcceptedPresentedStateProjectsWithoutChangingCommittedDocument()
    {
        using var workspace = CreateWorkspace(out TrackEditorDocument document);
        TrackAuthoringGraph committedGraph = document.Graph!;
        TrackAuthoringCompilation committedCompilation = document.Compilation!;
        string committedJson = document.CapturePackageJson();
        bool dirty = document.IsDirty;
        Assert.True(workspace.BeginStraightLengthEdit("launch"));

        AuthoringEvaluationOutcome outcome = await workspace
            .SubmitStraightLengthEdit(36.0)
            .Completion;
        Assert.True(workspace.PublishStraightLengthOutcome(outcome));

        Assert.Equal(AuthoringEvaluationOutcomeStatus.Accepted, outcome.Status);
        Assert.Same(committedGraph, document.Graph);
        Assert.Same(committedCompilation, document.Compilation);
        Assert.Equal(committedJson, document.CapturePackageJson());
        Assert.Equal(dirty, document.IsDirty);
        Assert.Equal(36.0, workspace.EngineeringSnapshot!.TotalLength, 9);
        Assert.Same(outcome.Candidate!.PreparedState, workspace.PresentedState);
        Assert.False(workspace.UndoRedo.CanUndo);
    }

    [Fact]
    public async Task SuccessfulCommitAdoptsExactCandidateWithoutCompilationAndCreatesOneUndoEntry()
    {
        using var workspace = CreateWorkspace(out TrackEditorDocument document);
        PreparedTrackGraphState before = workspace.AuthoringSession!.CommittedState.PreparedState;
        Assert.True(workspace.BeginStraightLengthEdit("launch"));
        AuthoringEvaluationOutcome outcome = await workspace
            .SubmitStraightLengthEdit(36.0)
            .Completion;
        workspace.PublishStraightLengthOutcome(outcome);
        PreparedTrackGraphState accepted = outcome.Candidate!.PreparedState!;
        using TrackAuthoringPipelineMeasurement measurement =
            TrackAuthoringPipelineMeasurement.Begin();

        AuthoringScheduledCommitResult result =
            (await workspace.CommitStraightLengthEditAsync())!;

        Assert.True(result.Succeeded);
        Assert.True(result.CommitResult!.Changed);
        Assert.Equal(0, measurement.GraphCompilerInvocationCount);
        Assert.Same(accepted, workspace.AuthoringSession!.CommittedState.PreparedState);
        Assert.Same(accepted.Graph, document.Graph);
        Assert.Same(accepted.GraphCompileResult, document.GraphCompileResult);
        Assert.True(workspace.UndoRedo.CanUndo);
        Assert.False(workspace.UndoRedo.CanRedo);

        Assert.True(workspace.UndoLast());
        Assert.Same(before.Graph, document.Graph);
        Assert.True(workspace.RedoLast());
        Assert.Same(accepted.Graph, document.Graph);
        Assert.Same(accepted.GraphCompileResult, document.GraphCompileResult);
    }

    [Fact]
    public async Task InvalidLatestRevisionRestoresCommittedStateAndCreatesNoHistory()
    {
        using var workspace = CreateWorkspace(out TrackEditorDocument document);
        TrackAuthoringGraph committedGraph = document.Graph!;
        string committedJson = document.CapturePackageJson();
        bool dirty = document.IsDirty;
        Assert.True(workspace.BeginStraightLengthEdit("launch"));
        AuthoringEvaluationOutcome valid = await workspace
            .SubmitStraightLengthEdit(34.0)
            .Completion;
        workspace.PublishStraightLengthOutcome(valid);
        Assert.Equal(34.0, workspace.ViewportSnapshot.TotalLength, 9);

        AuthoringEvaluationOutcome invalid = await workspace
            .SubmitStraightLengthEdit(double.NaN)
            .Completion;
        Assert.True(workspace.PublishStraightLengthOutcome(invalid));
        Assert.Equal(StraightLengthEditStatus.Invalid, workspace.StraightLengthEdit!.Status);
        Assert.Contains("Last valid preview", workspace.StraightLengthEdit.StatusText);
        Assert.Equal(34.0, workspace.ViewportSnapshot.TotalLength, 9);

        AuthoringScheduledCommitResult result =
            (await workspace.CommitStraightLengthEditAsync())!;

        Assert.False(result.Succeeded);
        Assert.Same(committedGraph, document.Graph);
        Assert.Equal(committedJson, document.CapturePackageJson());
        Assert.Equal(dirty, document.IsDirty);
        Assert.Equal(30.0, workspace.ViewportSnapshot.TotalLength, 9);
        Assert.False(workspace.UndoRedo.CanUndo);
        Assert.Null(workspace.StraightLengthEdit);
    }

    [Fact]
    public async Task CancelRestoresCommittedPresentationAndCreatesNoHistory()
    {
        using var workspace = CreateWorkspace(out TrackEditorDocument document);
        TrackAuthoringGraph committedGraph = document.Graph!;
        Assert.True(workspace.BeginStraightLengthEdit("launch"));
        AuthoringEvaluationOutcome outcome = await workspace
            .SubmitStraightLengthEdit(36.0)
            .Completion;
        workspace.PublishStraightLengthOutcome(outcome);
        Assert.Equal(36.0, workspace.ViewportSnapshot.TotalLength, 9);

        Assert.True(workspace.CancelStraightLengthEdit());

        Assert.Same(committedGraph, document.Graph);
        Assert.Equal(30.0, workspace.ViewportSnapshot.TotalLength, 9);
        Assert.False(workspace.UndoRedo.CanUndo);
        Assert.Null(workspace.AuthoringSession!.ActiveTransaction);
    }

    [Fact]
    public async Task NoOpReleaseCreatesNoHistory()
    {
        using var workspace = CreateWorkspace(out TrackEditorDocument document);
        Assert.True(workspace.BeginStraightLengthEdit("launch"));
        AuthoringEvaluationOutcome outcome = await workspace
            .SubmitStraightLengthEdit(30.0)
            .Completion;
        workspace.PublishStraightLengthOutcome(outcome);

        AuthoringScheduledCommitResult result =
            (await workspace.CommitStraightLengthEditAsync())!;

        Assert.True(result.Succeeded);
        Assert.False(result.CommitResult!.Changed);
        Assert.False(workspace.UndoRedo.CanUndo);
        Assert.Equal(30.0, StraightLength(document.Graph!, "launch"), 9);
    }

    [Fact]
    public async Task SaveIsDisabledAndCannotPersistProvisionalContent()
    {
        using var workspace = CreateWorkspace(out TrackEditorDocument document);
        string committedJson = document.CapturePackageJson();
        Assert.True(workspace.BeginStraightLengthEdit("launch"));
        AuthoringEvaluationOutcome outcome = await workspace
            .SubmitStraightLengthEdit(36.0)
            .Completion;
        workspace.PublishStraightLengthOutcome(outcome);

        Assert.False(workspace.Commands.CanExecute(
            Quantum.Editor.Avalonia.Services.Commands.EditorCommandIds.SaveDocument));
        string provisionalSavePath = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N") + ".qcwtrack.json");
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => workspace.SaveDocument(provisionalSavePath));
        Assert.Contains("disabled", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(provisionalSavePath));
        Assert.Equal(committedJson, document.CapturePackageJson());
    }

    [Fact]
    public async Task PreviewRemapsStationCursorByStationInsteadOfResettingToZero()
    {
        using var workspace = CreateWorkspace(out _);
        workspace.SelectStationAt(24.0);
        double previousStation = workspace.StationCursor!.Value.Station;
        Assert.True(previousStation > 20.0);
        Assert.True(workspace.BeginStraightLengthEdit("launch"));

        AuthoringEvaluationOutcome outcome = await workspace
            .SubmitStraightLengthEdit(12.0)
            .Completion;
        workspace.PublishStraightLengthOutcome(outcome);

        Assert.Equal(12.0, workspace.ViewportSnapshot.TotalLength, 9);
        Assert.Equal(12.0, workspace.StationCursor!.Value.Station, 9);
        Assert.NotEqual(0, workspace.StationCursor.Value.SampleIndex);
    }

    [Fact]
    public void ProjectionRemapsSectionRouteIndexFromStableNodeId()
    {
        using var workspace = new EditorWorkspace();
        TrackEditorDocument document = TrackEditorDocument.Create(
            new TrackLayoutPackageV2Dto
            {
                Metadata = new TrackLayoutMetadataV2Dto
                {
                    Units = "meters",
                    SourceName = "M167.4 selection fixture"
                },
                Sections = new[]
                {
                    new TrackLayoutSectionV2Dto
                    {
                        Kind = TrackLayoutPackageV2Vocabulary.StraightSectionKind,
                        Id = "first",
                        Length = 10.0
                    },
                    new TrackLayoutSectionV2Dto
                    {
                        Kind = TrackLayoutPackageV2Vocabulary.StraightSectionKind,
                        Id = "second",
                        Length = 10.0
                    }
                }
            },
            "M167.4 selection fixture");
        workspace.Documents.SetActiveDocument(document);
        workspace.Select(workspace.GraphNodes.Single(node => node.NodeId == "second").Selection);

        Assert.True(workspace.ApplyGraphEdit(
            "Move selected section",
            graph => TrackAuthoringGraphOperations.MoveBefore(graph, "second", "first")));

        Assert.Equal("second", workspace.CurrentSelection!.NodeId);
        Assert.Equal(0, workspace.CurrentSelection.SectionIndex);
        Assert.Equal(0, workspace.GraphNodes.Single(node => node.NodeId == "second").RouteIndex);
    }

    [Fact]
    public async Task ProductionInteractionDragUndoRedoAndSaveReopenPreserveExactCommittedCandidate()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "QuantumCoasterWorks.M167.4",
            Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "live-edit.qcwtrack.json");
        Directory.CreateDirectory(directory);
        try
        {
            using var workspace = CreateWorkspace(out TrackEditorDocument document);
            Assert.True(workspace.BeginStraightLengthEdit("launch"));
            AuthoringEvaluationOutcome first = await workspace
                .SubmitStraightLengthEdit(32.0)
                .Completion;
            Assert.True(workspace.PublishStraightLengthOutcome(first));
            AuthoringEvaluationOutcome second = await workspace
                .SubmitStraightLengthEdit(35.0)
                .Completion;
            Assert.True(workspace.PublishStraightLengthOutcome(second));
            AuthoringEvaluationOutcome final = await workspace
                .SubmitStraightLengthEdit(37.0)
                .Completion;
            Assert.True(workspace.PublishStraightLengthOutcome(final));
            PreparedTrackGraphState accepted = final.Candidate!.PreparedState!;

            AuthoringScheduledCommitResult commit =
                (await workspace.CommitStraightLengthEditAsync())!;

            Assert.True(commit.Succeeded);
            Assert.Equal(37.0, StraightLength(document.Graph!, "launch"), 9);
            Assert.Equal(3, workspace.CaptureStraightLengthMetrics().AcceptedPreviews);
            Assert.True(workspace.UndoLast());
            Assert.Equal(30.0, StraightLength(document.Graph!, "launch"), 9);
            Assert.True(workspace.RedoLast());
            Assert.Same(accepted.Graph, document.Graph);
            Assert.Same(accepted.GraphCompileResult, document.GraphCompileResult);
            Assert.Equal(37.0, StraightLength(document.Graph!, "launch"), 9);

            workspace.SaveDocument(path);
            TrackEditorDocument reopened = workspace.OpenDocument(path);

            Assert.Equal(37.0, StraightLength(reopened.Graph!, "launch"), 9);
            Assert.False(reopened.IsDirty);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static EditorWorkspace CreateWorkspace(out TrackEditorDocument document)
    {
        var workspace = new EditorWorkspace();
        document = TrackEditorDocument.Create(
            new TrackLayoutPackageV2Dto
            {
                Metadata = new TrackLayoutMetadataV2Dto
                {
                    Units = "meters",
                    SourceName = "M167.4 self-authored straight fixture",
                    LayoutId = "m167-4-straight"
                },
                Sections = new[]
                {
                    new TrackLayoutSectionV2Dto
                    {
                        Kind = TrackLayoutPackageV2Vocabulary.StraightSectionKind,
                        Id = "launch",
                        Length = 30.0
                    }
                }
            },
            "M167.4 straight fixture");
        workspace.Documents.SetActiveDocument(document);
        return workspace;
    }

    private static double StraightLength(TrackAuthoringGraph graph, string nodeId)
    {
        return Assert.IsType<StraightSectionDefinition>(
            graph.Nodes.Single(node => node.Id == nodeId).Section).Length;
    }
}
