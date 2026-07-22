using Quantum.Track.Authoring;

namespace Quantum.Editor.Avalonia.Services.Documents;

/// <summary>
/// One immutable graph snapshot together with the exact successful compilation and
/// canonical package JSON derived from it. Empty graphs intentionally have neither.
/// </summary>
internal sealed class TrackEditorGraphState
{
    public TrackEditorGraphState(
        TrackAuthoringGraph graph,
        TrackAuthoringGraphCompileResult? graphCompileResult,
        string? packageJson)
    {
        Graph = graph ?? throw new ArgumentNullException(nameof(graph));

        if (graph.Nodes.Count == 0)
        {
            if (graphCompileResult is not null || packageJson is not null)
            {
                throw new ArgumentException(
                    "An empty graph cannot have a compilation or package snapshot.",
                    nameof(graphCompileResult));
            }
        }
        else if (graphCompileResult is null ||
                 !graphCompileResult.Success ||
                 graphCompileResult.Compilation is null ||
                 !ReferenceEquals(graphCompileResult.SourceGraph, graph) ||
                 packageJson is null)
        {
            throw new ArgumentException(
                "A non-empty graph state requires its exact successful compilation and package snapshot.",
                nameof(graphCompileResult));
        }

        GraphCompileResult = graphCompileResult;
        PackageJson = packageJson;
    }

    public TrackAuthoringGraph Graph { get; }

    public TrackAuthoringGraphCompileResult? GraphCompileResult { get; }

    public string? PackageJson { get; }
}
