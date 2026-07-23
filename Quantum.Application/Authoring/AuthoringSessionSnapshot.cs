using System;

namespace Quantum.Application.Authoring
{
    public sealed class AuthoringSessionSnapshot
    {
        internal AuthoringSessionSnapshot(
            AuthoringSessionId sessionId,
            CommittedTrackState committedState,
            PreparedTrackGraphState presentedState,
            InteractiveAuthoringTransaction? activeTransaction,
            bool isDirty,
            int undoCount,
            int redoCount,
            long retainedHistoryPackageByteCount)
        {
            SessionId = sessionId;
            CommittedState = committedState ??
                throw new ArgumentNullException(nameof(committedState));
            PresentedState = presentedState ??
                throw new ArgumentNullException(nameof(presentedState));
            ActiveTransaction = activeTransaction;
            IsDirty = isDirty;
            UndoCount = undoCount;
            RedoCount = redoCount;
            RetainedHistoryPackageByteCount = retainedHistoryPackageByteCount;
        }

        public AuthoringSessionId SessionId { get; }

        public CommittedTrackState CommittedState { get; }

        public PreparedTrackGraphState PresentedState { get; }

        public InteractiveAuthoringTransaction? ActiveTransaction { get; }

        public bool IsPresentedCandidateCurrent =>
            ActiveTransaction?.IsPresentedCandidateCurrent == true;

        public bool IsDirty { get; }

        public int UndoCount { get; }

        public int RedoCount { get; }

        public long RetainedHistoryPackageByteCount { get; }

        /// <summary>
        /// Persistence always reads committed content, never provisional presentation.
        /// </summary>
        public string? PersistableCanonicalPackageJson =>
            CommittedState.CanonicalPackageJson;
    }
}
