using System;
using System.Collections.Generic;
using Quantum.Track.Internal;

namespace Quantum.Track
{
    /// <summary>
    /// Opt-in non-throwing compiler for reusable track runtime snapshots.
    /// </summary>
    public static class TrackRuntimeCompiler
    {
        public static TrackRuntimeCompileResult Compile(TrackDocument document)
        {
            return Compile(document, TrackSamplingOptions.Default);
        }

        public static TrackRuntimeCompileResult Compile(
            TrackDocument document,
            TrackSamplingOptions options)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var diagnostics = new List<TrackRuntimeDiagnostic>();
            CompiledTrackSamplingContext? samplingContext =
                CompiledTrackSamplingContext.TryCompile(
                    document,
                    options,
                    diagnostics,
                    out _);

            if (samplingContext is null)
            {
                return new TrackRuntimeCompileResult(runtime: null, diagnostics);
            }

            var runtime = new CompiledTrackRuntime(
                samplingContext,
                document.Segments.Count,
                options);
            return new TrackRuntimeCompileResult(runtime, diagnostics);
        }
    }
}
