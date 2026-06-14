using System;
using Quantum.Track.Internal;

namespace Quantum.Track
{
    /// <summary>
    /// Compiled, reusable sampling state for one track document snapshot.
    /// </summary>
    /// <remarks>
    /// Construction compiles the document once so evaluators bound to this runtime
    /// can sample repeatedly without rebuilding distance and frame state. Segment
    /// list membership, order, measured lengths, and roll values are captured at
    /// construction time. Later mutations to <see cref="TrackDocument.Segments"/>
    /// are not observed; compile a new runtime after authoring changes.
    ///
    /// Curve objects are retained by reference rather than deeply cloned. Mutating
    /// a curve object after compilation can therefore invalidate the compiled
    /// measurements and produce inconsistent samples. Treat referenced curves as
    /// immutable for the lifetime of this runtime, or recompile after mutation.
    /// </remarks>
    public sealed class CompiledTrackRuntime
    {
        private readonly CompiledTrackSamplingContext _samplingContext;

        /// <summary>
        /// Compiles reusable sampling state from the document's current segments.
        /// </summary>
        public CompiledTrackRuntime(TrackDocument document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            SegmentCount = document.Segments.Count;
            _samplingContext = CompiledTrackSamplingContext.Compile(document);
        }

        /// <summary>
        /// Measured geometric station length captured during compilation.
        /// </summary>
        public double TotalLength => _samplingContext.TotalLength;

        /// <summary>
        /// Number of ordered centerline segments captured during compilation.
        /// </summary>
        public int SegmentCount { get; }

        internal CompiledTrackSamplingContext SamplingContext => _samplingContext;
    }
}
