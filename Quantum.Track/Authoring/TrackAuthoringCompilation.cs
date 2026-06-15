using System;
using System.Collections.Generic;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Deterministic compiled snapshot of a validated track authoring definition.
    /// </summary>
    /// <remarks>
    /// Resolved section intervals retain the exact source definition instances and
    /// align by index with both <see cref="TrackDocument.Segments"/> and
    /// <see cref="TrackDocument.Sections"/>. <see cref="Runtime"/> captures the
    /// document's sampling state at compilation time. The document remains mutable
    /// by its existing contract; compile the definition again after mutation to
    /// restore alignment with the runtime and resolved intervals.
    /// </remarks>
    public sealed class TrackAuthoringCompilation
    {
        private readonly IReadOnlyList<ResolvedSectionInterval<GeometricSectionDefinition>>
            _resolvedSections;

        internal TrackAuthoringCompilation(
            TrackAuthoringDefinition definition,
            TrackDocument document,
            CompiledTrackRuntime runtime,
            BankingProfile bankingProfile,
            IEnumerable<ResolvedSectionInterval<GeometricSectionDefinition>> resolvedSections,
            double totalLength)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            BankingProfile = bankingProfile ?? throw new ArgumentNullException(nameof(bankingProfile));

            if (resolvedSections is null)
            {
                throw new ArgumentNullException(nameof(resolvedSections));
            }

            _resolvedSections = new List<ResolvedSectionInterval<GeometricSectionDefinition>>(
                resolvedSections).AsReadOnly();
            TotalLength = totalLength;
        }

        /// <summary>
        /// Validated source definition used for this compilation.
        /// </summary>
        public TrackAuthoringDefinition Definition { get; }

        /// <summary>
        /// Mutable evaluator-ready document snapshot produced by this compilation.
        /// </summary>
        public TrackDocument Document { get; }

        /// <summary>
        /// Compile-once sampling snapshot produced from <see cref="Document"/>.
        /// </summary>
        public CompiledTrackRuntime Runtime { get; }

        /// <summary>
        /// Compiled opt-in banking snapshot for this authored track.
        /// </summary>
        public BankingProfile BankingProfile { get; }

        /// <summary>
        /// Ordered source-definition ranges in station-distance units.
        /// </summary>
        public IReadOnlyList<ResolvedSectionInterval<GeometricSectionDefinition>> ResolvedSections =>
            _resolvedSections;

        /// <summary>
        /// Compiled track length in station-distance units.
        /// </summary>
        public double TotalLength { get; }
    }
}
