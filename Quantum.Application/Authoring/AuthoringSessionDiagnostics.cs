using System;

namespace Quantum.Application.Authoring
{
    public enum AuthoringSessionDiagnosticCode
    {
        NoActiveTransaction = 0,
        ActiveTransactionExists = 1,
        TransactionRevisionMismatch = 2,
        CommittedRevisionMismatch = 3,
        CandidateRevisionMismatch = 4,
        CandidateRejected = 5,
        PersistencePreparationFailed = 6,
        TransactionActive = 7
    }

    public sealed class AuthoringSessionDiagnostic
    {
        public AuthoringSessionDiagnostic(
            AuthoringSessionDiagnosticCode code,
            string message)
        {
            Code = code;
            Message = string.IsNullOrWhiteSpace(message)
                ? throw new ArgumentException("A diagnostic message is required.", nameof(message))
                : message;
        }

        public AuthoringSessionDiagnosticCode Code { get; }

        public string Message { get; }
    }
}
