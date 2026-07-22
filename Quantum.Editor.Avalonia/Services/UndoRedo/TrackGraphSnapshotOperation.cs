using Quantum.Editor.Avalonia.Services.Documents;
using Quantum.Track.Authoring;

namespace Quantum.Editor.Avalonia.Services.UndoRedo;

/// <summary>
/// Atomic undo entry containing only two already-validated immutable graph snapshots.
/// </summary>
public sealed class TrackGraphSnapshotOperation : IUndoableEditorOperation
{
    private readonly TrackEditorDocument document;
    private readonly TrackAuthoringGraph? beforeGraph;
    private readonly TrackAuthoringGraph? afterGraph;
    private readonly TrackEditorGraphState? beforeState;
    private readonly TrackEditorGraphState? afterState;

    public TrackGraphSnapshotOperation(
        string description,
        TrackEditorDocument document,
        TrackAuthoringGraph beforeGraph,
        TrackAuthoringGraph afterGraph)
    {
        Description = string.IsNullOrWhiteSpace(description)
            ? throw new ArgumentException("Operation description is required.", nameof(description))
            : description;
        this.document = document ?? throw new ArgumentNullException(nameof(document));
        this.beforeGraph = beforeGraph ?? throw new ArgumentNullException(nameof(beforeGraph));
        this.afterGraph = afterGraph ?? throw new ArgumentNullException(nameof(afterGraph));
    }

    internal TrackGraphSnapshotOperation(
        string description,
        TrackEditorDocument document,
        TrackEditorGraphState beforeState,
        TrackEditorGraphState afterState)
    {
        Description = string.IsNullOrWhiteSpace(description)
            ? throw new ArgumentException("Operation description is required.", nameof(description))
            : description;
        this.document = document ?? throw new ArgumentNullException(nameof(document));
        this.beforeState = beforeState ?? throw new ArgumentNullException(nameof(beforeState));
        this.afterState = afterState ?? throw new ArgumentNullException(nameof(afterState));
    }

    public string Description { get; }

    public void Execute()
    {
        if (afterState is not null)
        {
            document.ReplaceGraphState(afterState);
            return;
        }

        document.ReplaceGraph(afterGraph!);
    }

    public void Undo()
    {
        if (beforeState is not null)
        {
            document.ReplaceGraphState(beforeState);
            return;
        }

        document.ReplaceGraph(beforeGraph!);
    }
}
