using System;

namespace Quantum.Application.Authoring
{
    public sealed class InteractiveAuthoringTransaction
    {
        internal InteractiveAuthoringTransaction(
            TransactionRevision revision,
            CommittedSourceRevision baseCommittedRevision,
            PreparedTrackGraphState beforeState,
            string targetSectionId,
            string parameterIdentity,
            string description,
            EvaluatedTrackCandidate? newestCandidate,
            EvaluatedTrackCandidate? presentedCandidate)
        {
            Revision = revision;
            BaseCommittedRevision = baseCommittedRevision;
            BeforeState = beforeState ?? throw new ArgumentNullException(nameof(beforeState));
            TargetSectionId = RequireText(targetSectionId, nameof(targetSectionId));
            ParameterIdentity = RequireText(parameterIdentity, nameof(parameterIdentity));
            Description = RequireText(description, nameof(description));
            NewestCandidate = newestCandidate;
            PresentedCandidate = presentedCandidate;
        }

        public TransactionRevision Revision { get; }

        public CommittedSourceRevision BaseCommittedRevision { get; }

        public PreparedTrackGraphState BeforeState { get; }

        public string TargetSectionId { get; }

        public string ParameterIdentity { get; }

        public string Description { get; }

        public EvaluatedTrackCandidate? NewestCandidate { get; }

        public ProvisionalEditRevision? NewestProvisionalRevision =>
            NewestCandidate?.Revision.ProvisionalEditRevision;

        /// <summary>
        /// Last valid candidate retained for presentation. It may be older than the
        /// newest rejected update.
        /// </summary>
        public EvaluatedTrackCandidate? PresentedCandidate { get; }

        public bool IsPresentedCandidateCurrent =>
            PresentedCandidate != null &&
            NewestCandidate != null &&
            NewestCandidate.IsCommitEligible &&
            PresentedCandidate.Revision == NewestCandidate.Revision;

        internal InteractiveAuthoringTransaction WithCandidate(
            EvaluatedTrackCandidate candidate)
        {
            if (candidate is null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            return new InteractiveAuthoringTransaction(
                Revision,
                BaseCommittedRevision,
                BeforeState,
                TargetSectionId,
                ParameterIdentity,
                Description,
                candidate,
                candidate.IsCommitEligible ? candidate : PresentedCandidate);
        }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("An authoring transaction value is required.", parameterName);
            }

            return value;
        }
    }
}
