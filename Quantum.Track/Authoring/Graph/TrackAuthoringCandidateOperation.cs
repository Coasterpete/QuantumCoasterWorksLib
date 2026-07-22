using System;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Pure provisional transformation of one immutable authoring graph snapshot.
    /// </summary>
    /// <remarks>
    /// Implementations may represent one section edit or a future compound timeline
    /// transformation. Candidate evaluation depends only on this contract, so adding
    /// trim, split, range, or multi-parameter operations does not require a new evaluator.
    /// </remarks>
    public interface ITrackAuthoringCandidateOperation
    {
        string OperationTypeId { get; }

        TrackAuthoringGraph Apply(TrackAuthoringGraph sourceGraph);
    }

    public enum TrackAuthoringCandidateOperationKind
    {
        Append = 0,
        InsertBefore = 1,
        InsertAfter = 2,
        Replace = 3
    }

    /// <summary>
    /// Typed immutable graph operation carrying an actual production section definition.
    /// </summary>
    public sealed class TrackAuthoringCandidateOperation : ITrackAuthoringCandidateOperation
    {
        public const string AppendOperationTypeId = "graph.appendSection";
        public const string InsertBeforeOperationTypeId = "graph.insertSectionBefore";
        public const string InsertAfterOperationTypeId = "graph.insertSectionAfter";
        public const string ReplaceOperationTypeId = "graph.replaceSection";

        private TrackAuthoringCandidateOperation(
            TrackAuthoringCandidateOperationKind kind,
            string? targetNodeId,
            TrackAuthoringSectionDefinition section)
        {
            Kind = kind;
            TargetNodeId = targetNodeId;
            Section = section ?? throw new ArgumentNullException(nameof(section));
        }

        public TrackAuthoringCandidateOperationKind Kind { get; }

        public string? TargetNodeId { get; }

        public TrackAuthoringSectionDefinition Section { get; }

        public string OperationTypeId
        {
            get
            {
                switch (Kind)
                {
                    case TrackAuthoringCandidateOperationKind.Append:
                        return AppendOperationTypeId;

                    case TrackAuthoringCandidateOperationKind.InsertBefore:
                        return InsertBeforeOperationTypeId;

                    case TrackAuthoringCandidateOperationKind.InsertAfter:
                        return InsertAfterOperationTypeId;

                    case TrackAuthoringCandidateOperationKind.Replace:
                        return ReplaceOperationTypeId;

                    default:
                        throw new NotSupportedException(
                            $"Candidate operation kind '{Kind}' is not supported.");
                }
            }
        }

        public TrackAuthoringGraph Apply(TrackAuthoringGraph sourceGraph)
        {
            switch (Kind)
            {
                case TrackAuthoringCandidateOperationKind.Append:
                    return TrackAuthoringGraphOperations.Append(sourceGraph, Section);

                case TrackAuthoringCandidateOperationKind.InsertBefore:
                    return TrackAuthoringGraphOperations.InsertBefore(
                        sourceGraph,
                        TargetNodeId!,
                        Section);

                case TrackAuthoringCandidateOperationKind.InsertAfter:
                    return TrackAuthoringGraphOperations.InsertAfter(
                        sourceGraph,
                        TargetNodeId!,
                        Section);

                case TrackAuthoringCandidateOperationKind.Replace:
                    return TrackAuthoringGraphOperations.Replace(
                        sourceGraph,
                        TargetNodeId!,
                        Section);

                default:
                    throw new NotSupportedException(
                        $"Candidate operation kind '{Kind}' is not supported.");
            }
        }

        public static TrackAuthoringCandidateOperation Append(
            TrackAuthoringSectionDefinition section)
        {
            return new TrackAuthoringCandidateOperation(
                TrackAuthoringCandidateOperationKind.Append,
                null,
                section);
        }

        public static TrackAuthoringCandidateOperation InsertBefore(
            string anchorNodeId,
            TrackAuthoringSectionDefinition section)
        {
            return new TrackAuthoringCandidateOperation(
                TrackAuthoringCandidateOperationKind.InsertBefore,
                RequireTargetNodeId(anchorNodeId, nameof(anchorNodeId)),
                section);
        }

        public static TrackAuthoringCandidateOperation InsertAfter(
            string anchorNodeId,
            TrackAuthoringSectionDefinition section)
        {
            return new TrackAuthoringCandidateOperation(
                TrackAuthoringCandidateOperationKind.InsertAfter,
                RequireTargetNodeId(anchorNodeId, nameof(anchorNodeId)),
                section);
        }

        public static TrackAuthoringCandidateOperation Replace(
            string nodeId,
            TrackAuthoringSectionDefinition section)
        {
            return new TrackAuthoringCandidateOperation(
                TrackAuthoringCandidateOperationKind.Replace,
                RequireTargetNodeId(nodeId, nameof(nodeId)),
                section);
        }

        private static string RequireTargetNodeId(string nodeId, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                throw new ArgumentException(
                    "A candidate operation target node ID is required.",
                    parameterName);
            }

            return nodeId;
        }
    }
}
