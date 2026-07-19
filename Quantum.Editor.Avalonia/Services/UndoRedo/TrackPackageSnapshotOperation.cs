using Quantum.Editor.Avalonia.Services.Documents;
using Quantum.IO.TrackLayout.V2;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Editor.Avalonia.Services.UndoRedo;

/// <summary>
/// Backward-compatible constructor surface that immediately converts package JSON
/// into validated immutable graph snapshots.
/// </summary>
[Obsolete("Use TrackGraphSnapshotOperation for graph-authoritative editor history.")]
public sealed class TrackPackageSnapshotOperation : IUndoableEditorOperation
{
    private readonly TrackGraphSnapshotOperation graphOperation;

    public TrackPackageSnapshotOperation(
        string description,
        TrackEditorDocument document,
        string beforeJson,
        string afterJson)
    {
        ArgumentNullException.ThrowIfNull(document);
        (TrackAuthoringGraph beforeGraph, TrackLayoutPackageV2GraphAncillaryState beforeAncillary) =
            ImportGraph(beforeJson, nameof(beforeJson));
        (TrackAuthoringGraph afterGraph, TrackLayoutPackageV2GraphAncillaryState afterAncillary) =
            ImportGraph(afterJson, nameof(afterJson));

        TrackLayoutPackageV2GraphAncillaryState activeAncillary = document.AncillaryState ??
            throw new InvalidOperationException(
                "The target document does not have Track Layout Package V2 ancillary state.");
        if (!AncillaryStateEquals(activeAncillary, beforeAncillary) ||
            !AncillaryStateEquals(activeAncillary, afterAncillary))
        {
            throw new NotSupportedException(
                "Package snapshot history cannot change metadata or heartline state in the graph-authoritative editor.");
        }

        graphOperation = new TrackGraphSnapshotOperation(
            description,
            document,
            beforeGraph,
            afterGraph);
    }

    public string Description => graphOperation.Description;

    public void Execute() => graphOperation.Execute();

    public void Undo() => graphOperation.Undo();

    private static (
        TrackAuthoringGraph Graph,
        TrackLayoutPackageV2GraphAncillaryState AncillaryState) ImportGraph(
        string json,
        string parameterName)
    {
        if (json is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        TrackLayoutPackageV2GraphImportResult import = TrackLayoutPackageV2GraphAdapter.Import(
            TrackLayoutPackageV2Json.Deserialize(json));
        if (!import.Success || import.Graph is null || import.AncillaryState is null)
        {
            string details = import.Diagnostics.Count == 0
                ? "Package snapshot import failed without diagnostics."
                : string.Join(
                    " ",
                    import.Diagnostics.Select(diagnostic =>
                        $"{diagnostic.Code} at {diagnostic.Path}: {diagnostic.Message}"));
            throw new ArgumentException(details, parameterName);
        }

        TrackAuthoringGraphCompileResult compilation =
            TrackAuthoringGraphCompiler.Compile(import.Graph);
        if (!compilation.Success || compilation.Compilation is null)
        {
            string details = compilation.Diagnostics.Count == 0
                ? "Graph snapshot compilation failed without diagnostics."
                : string.Join(
                    " ",
                    compilation.Diagnostics.Select(diagnostic =>
                        $"{diagnostic.Code}: {diagnostic.Message}"));
            throw new ArgumentException(details, parameterName);
        }

        return (import.Graph, import.AncillaryState);
    }

    private static bool AncillaryStateEquals(
        TrackLayoutPackageV2GraphAncillaryState first,
        TrackLayoutPackageV2GraphAncillaryState second)
    {
        return string.Equals(first.Contract, second.Contract, StringComparison.Ordinal) &&
               first.Version == second.Version &&
               string.Equals(first.Units, second.Units, StringComparison.Ordinal) &&
               string.Equals(first.SourceName, second.SourceName, StringComparison.Ordinal) &&
               string.Equals(first.LayoutId, second.LayoutId, StringComparison.Ordinal) &&
               HeartlineEquals(first.HeartlineOffset, second.HeartlineOffset);
    }

    private static bool HeartlineEquals(HeartlineOffset? first, HeartlineOffset? second)
    {
        if (!first.HasValue || !second.HasValue)
        {
            return first.HasValue == second.HasValue;
        }

        return first.Value.NormalOffsetMeters == second.Value.NormalOffsetMeters &&
               first.Value.LateralOffsetMeters == second.Value.LateralOffsetMeters;
    }
}
