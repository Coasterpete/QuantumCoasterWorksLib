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
    /// <see cref="TrackDocument.Sections"/>. The document remains mutable by its
    /// existing contract; compile the definition again after mutation to restore
    /// that alignment.
    /// </remarks>
    public sealed class TrackAuthoringCompilation
    {
        private readonly IReadOnlyList<ResolvedSectionInterval<GeometricSectionDefinition>>
            _resolvedSections;

        internal TrackAuthoringCompilation(
            TrackAuthoringDefinition definition,
            TrackDocument document,
            IEnumerable<ResolvedSectionInterval<GeometricSectionDefinition>> resolvedSections,
            double totalLength)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Document = document ?? throw new ArgumentNullException(nameof(document));

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
