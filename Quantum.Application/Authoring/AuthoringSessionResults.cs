using System;
using System.Collections.Generic;

namespace Quantum.Application.Authoring
{
    public sealed class CandidateUpdateResult
    {
        private readonly IReadOnlyList<AuthoringSessionDiagnostic> diagnostics;

        internal CandidateUpdateResult(
            bool wasEvaluated,
            EvaluatedTrackCandidate? candidate,
            IEnumerable<AuthoringSessionDiagnostic> diagnostics)
        {
            WasEvaluated = wasEvaluated;
            Candidate = candidate;
            this.diagnostics = new List<AuthoringSessionDiagnostic>(
                diagnostics ?? throw new ArgumentNullException(nameof(diagnostics))).AsReadOnly();
        }

        public bool WasEvaluated { get; }

        public EvaluatedTrackCandidate? Candidate { get; }

        public bool CandidateAccepted => Candidate?.IsCommitEligible == true;

        public IReadOnlyList<AuthoringSessionDiagnostic> Diagnostics => diagnostics;
    }

    public sealed class AuthoringCommitResult
    {
        private readonly IReadOnlyList<AuthoringSessionDiagnostic> diagnostics;

        internal AuthoringCommitResult(
            bool succeeded,
            bool changed,
            CommittedTrackState? committedState,
            IEnumerable<AuthoringSessionDiagnostic> diagnostics)
        {
            Succeeded = succeeded;
            Changed = changed;
            CommittedState = committedState;
            this.diagnostics = new List<AuthoringSessionDiagnostic>(
                diagnostics ?? throw new ArgumentNullException(nameof(diagnostics))).AsReadOnly();
        }

        public bool Succeeded { get; }

        public bool Changed { get; }

        public CommittedTrackState? CommittedState { get; }

        public IReadOnlyList<AuthoringSessionDiagnostic> Diagnostics => diagnostics;
    }

    public sealed class AuthoringOneShotResult
    {
        internal AuthoringOneShotResult(
            CandidateUpdateResult update,
            AuthoringCommitResult? commit)
        {
            Update = update ?? throw new ArgumentNullException(nameof(update));
            Commit = commit;
        }

        public CandidateUpdateResult Update { get; }

        public AuthoringCommitResult? Commit { get; }

        public bool Succeeded => Commit?.Succeeded == true;
    }
}
