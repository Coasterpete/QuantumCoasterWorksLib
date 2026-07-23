using System;

namespace Quantum.Application.Authoring
{
    public readonly struct AuthoringSessionId : IEquatable<AuthoringSessionId>
    {
        public AuthoringSessionId(Guid value)
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException("A session ID cannot be empty.", nameof(value));
            }

            Value = value;
        }

        public Guid Value { get; }

        public static AuthoringSessionId New()
        {
            return new AuthoringSessionId(Guid.NewGuid());
        }

        public bool Equals(AuthoringSessionId other) => Value.Equals(other.Value);

        public override bool Equals(object? obj) =>
            obj is AuthoringSessionId && Equals((AuthoringSessionId)obj);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString("N");

        public static bool operator ==(AuthoringSessionId left, AuthoringSessionId right) =>
            left.Equals(right);

        public static bool operator !=(AuthoringSessionId left, AuthoringSessionId right) =>
            !left.Equals(right);
    }

    public readonly struct CommittedSourceRevision : IEquatable<CommittedSourceRevision>
    {
        public CommittedSourceRevision(AuthoringSessionId sessionId, long sequence)
        {
            if (sessionId.Value == Guid.Empty)
            {
                throw new ArgumentException("A committed revision requires a session ID.", nameof(sessionId));
            }

            if (sequence < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            SessionId = sessionId;
            Sequence = sequence;
        }

        public AuthoringSessionId SessionId { get; }

        public long Sequence { get; }

        public bool Equals(CommittedSourceRevision other) =>
            SessionId.Equals(other.SessionId) && Sequence == other.Sequence;

        public override bool Equals(object? obj) =>
            obj is CommittedSourceRevision && Equals((CommittedSourceRevision)obj);

        public override int GetHashCode() =>
            unchecked((SessionId.GetHashCode() * 397) ^ Sequence.GetHashCode());

        public override string ToString() => $"{SessionId}:commit:{Sequence}";

        public static bool operator ==(CommittedSourceRevision left, CommittedSourceRevision right) =>
            left.Equals(right);

        public static bool operator !=(CommittedSourceRevision left, CommittedSourceRevision right) =>
            !left.Equals(right);
    }

    public readonly struct TransactionRevision : IEquatable<TransactionRevision>
    {
        public TransactionRevision(AuthoringSessionId sessionId, long sequence)
        {
            if (sessionId.Value == Guid.Empty)
            {
                throw new ArgumentException("A transaction revision requires a session ID.", nameof(sessionId));
            }

            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            SessionId = sessionId;
            Sequence = sequence;
        }

        public AuthoringSessionId SessionId { get; }

        public long Sequence { get; }

        public bool Equals(TransactionRevision other) =>
            SessionId.Equals(other.SessionId) && Sequence == other.Sequence;

        public override bool Equals(object? obj) =>
            obj is TransactionRevision && Equals((TransactionRevision)obj);

        public override int GetHashCode() =>
            unchecked((SessionId.GetHashCode() * 397) ^ Sequence.GetHashCode());

        public override string ToString() => $"{SessionId}:transaction:{Sequence}";

        public static bool operator ==(TransactionRevision left, TransactionRevision right) =>
            left.Equals(right);

        public static bool operator !=(TransactionRevision left, TransactionRevision right) =>
            !left.Equals(right);
    }

    public readonly struct ProvisionalEditRevision : IEquatable<ProvisionalEditRevision>
    {
        public ProvisionalEditRevision(TransactionRevision transactionRevision, long sequence)
        {
            if (transactionRevision.SessionId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "A provisional revision requires a transaction revision.",
                    nameof(transactionRevision));
            }

            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            TransactionRevision = transactionRevision;
            Sequence = sequence;
        }

        public TransactionRevision TransactionRevision { get; }

        public long Sequence { get; }

        public bool Equals(ProvisionalEditRevision other) =>
            TransactionRevision.Equals(other.TransactionRevision) && Sequence == other.Sequence;

        public override bool Equals(object? obj) =>
            obj is ProvisionalEditRevision && Equals((ProvisionalEditRevision)obj);

        public override int GetHashCode() =>
            unchecked((TransactionRevision.GetHashCode() * 397) ^ Sequence.GetHashCode());

        public override string ToString() => $"{TransactionRevision}:provisional:{Sequence}";

        public static bool operator ==(ProvisionalEditRevision left, ProvisionalEditRevision right) =>
            left.Equals(right);

        public static bool operator !=(ProvisionalEditRevision left, ProvisionalEditRevision right) =>
            !left.Equals(right);
    }

    public readonly struct EvaluatedCandidateRevision : IEquatable<EvaluatedCandidateRevision>
    {
        public EvaluatedCandidateRevision(
            CommittedSourceRevision baseCommittedRevision,
            ProvisionalEditRevision provisionalEditRevision)
        {
            if (baseCommittedRevision.SessionId !=
                provisionalEditRevision.TransactionRevision.SessionId)
            {
                throw new ArgumentException(
                    "Candidate base and provisional revisions must belong to the same session.",
                    nameof(provisionalEditRevision));
            }

            BaseCommittedRevision = baseCommittedRevision;
            ProvisionalEditRevision = provisionalEditRevision;
        }

        public CommittedSourceRevision BaseCommittedRevision { get; }

        public ProvisionalEditRevision ProvisionalEditRevision { get; }

        public bool Equals(EvaluatedCandidateRevision other) =>
            BaseCommittedRevision.Equals(other.BaseCommittedRevision) &&
            ProvisionalEditRevision.Equals(other.ProvisionalEditRevision);

        public override bool Equals(object? obj) =>
            obj is EvaluatedCandidateRevision && Equals((EvaluatedCandidateRevision)obj);

        public override int GetHashCode() =>
            unchecked((BaseCommittedRevision.GetHashCode() * 397) ^
                ProvisionalEditRevision.GetHashCode());

        public override string ToString() =>
            $"{BaseCommittedRevision}|{ProvisionalEditRevision}";

        public static bool operator ==(
            EvaluatedCandidateRevision left,
            EvaluatedCandidateRevision right) => left.Equals(right);

        public static bool operator !=(
            EvaluatedCandidateRevision left,
            EvaluatedCandidateRevision right) => !left.Equals(right);
    }
}
