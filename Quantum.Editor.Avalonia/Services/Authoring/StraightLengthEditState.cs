using Quantum.Application.Authoring;
using Quantum.Track.Authoring;

namespace Quantum.Editor.Avalonia.Services.Authoring;

public enum StraightLengthEditStatus
{
    Ready,
    Evaluating,
    Accepted,
    Invalid
}

/// <summary>
/// Frontend-facing immutable state for the one M167.4 live parameter.
/// The application session remains authoritative for graph presentation and commit.
/// </summary>
public sealed record StraightLengthEditState(
    TransactionRevision TransactionRevision,
    PreparedTrackGraphState BeforeState,
    string NodeId,
    double CommittedLength,
    double RawLength,
    double? AcceptedPreviewLength,
    StraightLengthEditStatus Status,
    IReadOnlyList<string> Diagnostics,
    long RawPointerUpdates,
    long AcceptedPreviews,
    TimeSpan FinalCommitWait)
{
    public bool IsInvalid => Status == StraightLengthEditStatus.Invalid;

    public string StatusText => Status switch
    {
        StraightLengthEditStatus.Evaluating => "Evaluating preview…",
        StraightLengthEditStatus.Accepted => "Live preview",
        StraightLengthEditStatus.Invalid =>
            "Last valid preview — current value is invalid",
        _ => "Drag horizontally to edit length"
    };
}

public sealed record StraightLengthInteractionMetrics(
    long RawPointerUpdates,
    long SubmittedEvaluations,
    long CoalescedUpdates,
    long StartedEvaluations,
    long AcceptedPreviews,
    long StaleCompletions,
    TimeSpan SubmitToPresentLatency,
    TimeSpan FinalCommitWaitLatency,
    int CompilerInvocationCount);

/// <summary>
/// Defers construction of the immutable straight definition until candidate
/// evaluation so an invalid raw value still reserves a newer provisional revision.
/// </summary>
internal sealed class SetStraightLengthCandidateOperation : ITrackAuthoringCandidateOperation
{
    internal SetStraightLengthCandidateOperation(string nodeId, double absoluteLength)
    {
        NodeId = string.IsNullOrWhiteSpace(nodeId)
            ? throw new ArgumentException("A straight node ID is required.", nameof(nodeId))
            : nodeId;
        AbsoluteLength = absoluteLength;
    }

    public string OperationTypeId => "straight.setAbsoluteLength";

    internal string NodeId { get; }

    internal double AbsoluteLength { get; }

    public TrackAuthoringGraph Apply(TrackAuthoringGraph sourceGraph)
    {
        ArgumentNullException.ThrowIfNull(sourceGraph);
        TrackAuthoringGraphNode node = sourceGraph.Nodes.Single(candidate =>
            string.Equals(candidate.Id, NodeId, StringComparison.Ordinal));
        StraightSectionDefinition straight = node.Section as StraightSectionDefinition ??
            throw new InvalidOperationException(
                $"Graph node '{NodeId}' is not a straight section.");
        return TrackAuthoringGraphOperations.Replace(
            sourceGraph,
            NodeId,
            new StraightSectionDefinition(
                straight.Id,
                AbsoluteLength,
                straight.RollRadians));
    }
}
