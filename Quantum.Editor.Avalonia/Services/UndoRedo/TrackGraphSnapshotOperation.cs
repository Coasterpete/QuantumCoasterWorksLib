using Quantum.Editor.Avalonia.Services.Documents;
using Quantum.Track.Authoring;

namespace Quantum.Editor.Avalonia.Services.UndoRedo;

/// <summary>
/// Atomic undo entry containing only two already-validated immutable graph snapshots.
/// </summary>
public sealed class TrackGraphSnapshotOperation : IUndoableEditorOperation
{
    private readonly TrackEditorDocument document;
    private readonly TrackAuthoringGraph beforeGraph;
    private readonly TrackAuthoringGraph afterGraph;

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

    public string Description { get; }

    public void Execute() => document.ReplaceGraph(afterGraph);

    public void Undo() => document.ReplaceGraph(beforeGraph);
}
