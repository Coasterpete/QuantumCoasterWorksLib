using Quantum.Editor.Avalonia.Services.Documents;

namespace Quantum.Editor.Avalonia.Services.UndoRedo;

public sealed class TrackPackageSnapshotOperation : IUndoableEditorOperation
{
    private readonly TrackEditorDocument document;
    private readonly string beforeJson;
    private readonly string afterJson;

    public TrackPackageSnapshotOperation(
        string description,
        TrackEditorDocument document,
        string beforeJson,
        string afterJson)
    {
        Description = string.IsNullOrWhiteSpace(description)
            ? throw new ArgumentException("Operation description is required.", nameof(description))
            : description;
        this.document = document ?? throw new ArgumentNullException(nameof(document));
        this.beforeJson = beforeJson ?? throw new ArgumentNullException(nameof(beforeJson));
        this.afterJson = afterJson ?? throw new ArgumentNullException(nameof(afterJson));
    }

    public string Description { get; }

    public void Execute() => document.ReplacePackageJson(afterJson, markDirty: true);

    public void Undo() => document.ReplacePackageJson(beforeJson, markDirty: true);
}
