using System;
using System.Collections.Generic;
using System.Linq;

namespace Quantum.Track
{
    /// <summary>
    /// Runtime compilation outcome with a read-only deterministic diagnostic list.
    /// </summary>
    public sealed class TrackRuntimeCompileResult
    {
        private readonly IReadOnlyList<TrackRuntimeDiagnostic> _diagnostics;

        internal TrackRuntimeCompileResult(
            CompiledTrackRuntime? runtime,
            IEnumerable<TrackRuntimeDiagnostic> diagnostics)
        {
            if (diagnostics is null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            TrackRuntimeDiagnostic[] diagnosticArray = diagnostics.ToArray();
            for (int i = 0; i < diagnosticArray.Length; i++)
            {
                if (diagnosticArray[i] is null)
                {
                    throw new ArgumentException(
                        "Diagnostic collection cannot contain null entries.",
                        nameof(diagnostics));
                }
            }

            Runtime = runtime;
            _diagnostics = Array.AsReadOnly(diagnosticArray);
        }

        public bool Success => Runtime != null && !HasErrors;

        public bool HasErrors => Diagnostics.Any(
            diagnostic => diagnostic.Severity == TrackRuntimeDiagnosticSeverity.Error);

        public CompiledTrackRuntime? Runtime { get; }

        public IReadOnlyList<TrackRuntimeDiagnostic> Diagnostics => _diagnostics;
    }
}
