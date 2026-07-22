using System;
using System.Collections.Generic;
using Quantum.Track.Authoring;

namespace Quantum.Application.Authoring
{
    public sealed class EvaluatedTrackCandidate
    {
        private readonly IReadOnlyList<AuthoringSessionDiagnostic> applicationDiagnostics;

        internal EvaluatedTrackCandidate(
            EvaluatedCandidateRevision revision,
            TrackAuthoringCandidateEvaluation evaluation,
            PreparedTrackGraphState? preparedState,
            IEnumerable<AuthoringSessionDiagnostic> applicationDiagnostics)
        {
            Revision = revision;
            Evaluation = evaluation ?? throw new ArgumentNullException(nameof(evaluation));
            PreparedState = preparedState;
            this.applicationDiagnostics = new List<AuthoringSessionDiagnostic>(
                applicationDiagnostics ??
                throw new ArgumentNullException(nameof(applicationDiagnostics))).AsReadOnly();
        }

        public EvaluatedCandidateRevision Revision { get; }

        public TrackAuthoringCandidateEvaluation Evaluation { get; }

        public TrackAuthoringGraph? CandidateGraph => Evaluation.CandidateGraph;

        public PreparedTrackGraphState? PreparedState { get; }

        public IReadOnlyList<TrackAuthoringGraphDiagnostic> GraphDiagnostics =>
            Evaluation.Diagnostics;

        public IReadOnlyList<AuthoringSessionDiagnostic> ApplicationDiagnostics =>
            applicationDiagnostics;

        public bool IsCommitEligible => PreparedState != null;
    }
}
