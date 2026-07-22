using System;
using Quantum.Track.Authoring;

namespace Quantum.Application.Authoring
{
    public sealed class CommittedTrackState
    {
        internal CommittedTrackState(
            PreparedTrackGraphState preparedState,
            CommittedSourceRevision revision)
        {
            PreparedState = preparedState ??
                throw new ArgumentNullException(nameof(preparedState));
            Revision = revision;
        }

        public PreparedTrackGraphState PreparedState { get; }

        public CommittedSourceRevision Revision { get; }

        public TrackAuthoringGraph SourceGraph => PreparedState.Graph;

        public TrackAuthoringGraphCompileResult? Compilation =>
            PreparedState.GraphCompileResult;

        public string? CanonicalPackageJson => PreparedState.CanonicalPackageJson;
    }
}
