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
            ProvisionalEditRevision? newestProvisionalRevision,
            EvaluatedTrackCandidate? newestCandidate,
            EvaluatedTrackCandidate? presentedCandidate)
        {
            Revision = revision;
            BaseCommittedRevision = baseCommittedRevision;
            BeforeState = beforeState ?? throw new ArgumentNullException(nameof(beforeState));
            TargetSectionId = RequireText(targetSectionId, nameof(targetSectionId));
            ParameterIdentity = RequireText(parameterIdentity, nameof(parameterIdentity));
            Description = RequireText(description, nameof(description));
            NewestProvisionalRevision = newestProvisionalRevision;
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

        public ProvisionalEditRevision? NewestProvisionalRevision { get; }

        /// <summary>
        /// Last valid candidate retained for presentation. It may be older than the
        /// newest rejected update.
        /// </summary>
        public EvaluatedTrackCandidate? PresentedCandidate { get; }

        public bool IsPresentedCandidateCurrent =>
            PresentedCandidate != null &&
            NewestCandidate != null &&
            NewestProvisionalRevision.HasValue &&
            NewestCandidate.IsCommitEligible &&
            NewestCandidate.Revision.ProvisionalEditRevision == NewestProvisionalRevision.Value &&
            PresentedCandidate.Revision == NewestCandidate.Revision;

        internal InteractiveAuthoringTransaction WithReservedProvisionalRevision(
            ProvisionalEditRevision provisionalRevision)
        {
            if (provisionalRevision.TransactionRevision != Revision)
            {
                throw new ArgumentException(
                    "A provisional revision must belong to this transaction.",
                    nameof(provisionalRevision));
            }

            return new InteractiveAuthoringTransaction(
                Revision,
                BaseCommittedRevision,
                BeforeState,
                TargetSectionId,
                ParameterIdentity,
                Description,
                provisionalRevision,
                NewestCandidate,
                PresentedCandidate);
        }

        internal InteractiveAuthoringTransaction WithCandidate(
            EvaluatedTrackCandidate candidate)
        {
            if (candidate is null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (!NewestProvisionalRevision.HasValue ||
                candidate.Revision.ProvisionalEditRevision != NewestProvisionalRevision.Value)
            {
                throw new InvalidOperationException(
                    "Only the newest reserved provisional revision may be adopted.");
            }

            return new InteractiveAuthoringTransaction(
                Revision,
                BaseCommittedRevision,
                BeforeState,
                TargetSectionId,
                ParameterIdentity,
                Description,
                NewestProvisionalRevision,
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
