using System;
using System.Collections.Generic;
using System.Linq;

namespace Quantum.IO.GeometryInterchange
{
    public enum GeometryInterchangeResultStatus
    {
        Success = 0,
        Unsupported = 1,
        Failed = 2
    }

    public enum GeometryInterchangeDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public enum GeometryInterchangeDiagnosticCode
    {
        UnsupportedAdapter = 0,
        InvalidPayload = 1,
        InvalidDocument = 2
    }

    public sealed class GeometryInterchangeDiagnostic
    {
        public GeometryInterchangeDiagnostic(
            GeometryInterchangeDiagnosticCode code,
            GeometryInterchangeDiagnosticSeverity severity,
            string adapterName,
            string operation,
            string message,
            string? path = null)
        {
            Code = code;
            Severity = severity;
            AdapterName = adapterName ?? string.Empty;
            Operation = operation ?? string.Empty;
            Message = message ?? string.Empty;
            Path = path;
        }

        public GeometryInterchangeDiagnosticCode Code { get; }

        public GeometryInterchangeDiagnosticSeverity Severity { get; }

        public string AdapterName { get; }

        public string Operation { get; }

        public string Message { get; }

        public string? Path { get; }
    }

    public sealed class GeometryImportResult
    {
        private readonly IReadOnlyList<GeometryInterchangeDiagnostic> _diagnostics;

        public GeometryImportResult(
            GeometryInterchangeResultStatus status,
            ExternalCurveDocument? document,
            IEnumerable<GeometryInterchangeDiagnostic> diagnostics)
        {
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            Status = status;
            Document = document;
            GeometryInterchangeDiagnostic[] diagnosticArray = diagnostics.ToArray();

            for (int i = 0; i < diagnosticArray.Length; i++)
            {
                if (diagnosticArray[i] == null)
                {
                    throw new ArgumentException("Diagnostic collection cannot contain null entries.", nameof(diagnostics));
                }
            }

            _diagnostics = Array.AsReadOnly(diagnosticArray);
        }

        public GeometryInterchangeResultStatus Status { get; }

        public bool Success => Status == GeometryInterchangeResultStatus.Success;

        public ExternalCurveDocument? Document { get; }

        public IReadOnlyList<GeometryInterchangeDiagnostic> Diagnostics => _diagnostics;

        public static GeometryImportResult Unsupported(GeometryInterchangeDiagnostic diagnostic)
        {
            if (diagnostic == null)
            {
                throw new ArgumentNullException(nameof(diagnostic));
            }

            return new GeometryImportResult(
                GeometryInterchangeResultStatus.Unsupported,
                document: null,
                diagnostics: new[] { diagnostic });
        }
    }

    public sealed class GeometryExportResult
    {
        private readonly byte[] _payload;
        private readonly IReadOnlyList<GeometryInterchangeDiagnostic> _diagnostics;

        public GeometryExportResult(
            GeometryInterchangeResultStatus status,
            byte[]? payload,
            IEnumerable<GeometryInterchangeDiagnostic> diagnostics)
        {
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            Status = status;
            _payload = payload == null ? Array.Empty<byte>() : (byte[])payload.Clone();
            GeometryInterchangeDiagnostic[] diagnosticArray = diagnostics.ToArray();

            for (int i = 0; i < diagnosticArray.Length; i++)
            {
                if (diagnosticArray[i] == null)
                {
                    throw new ArgumentException("Diagnostic collection cannot contain null entries.", nameof(diagnostics));
                }
            }

            _diagnostics = Array.AsReadOnly(diagnosticArray);
        }

        public GeometryInterchangeResultStatus Status { get; }

        public bool Success => Status == GeometryInterchangeResultStatus.Success;

        public byte[] Payload => (byte[])_payload.Clone();

        public IReadOnlyList<GeometryInterchangeDiagnostic> Diagnostics => _diagnostics;

        public static GeometryExportResult Unsupported(GeometryInterchangeDiagnostic diagnostic)
        {
            if (diagnostic == null)
            {
                throw new ArgumentNullException(nameof(diagnostic));
            }

            return new GeometryExportResult(
                GeometryInterchangeResultStatus.Unsupported,
                payload: Array.Empty<byte>(),
                diagnostics: new[] { diagnostic });
        }
    }
}
