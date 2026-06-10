using System;
using System.Collections.Generic;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Validated ordered geometric input for building a <see cref="TrackDocument"/>.
    /// </summary>
    public sealed class TrackAuthoringDefinition
    {
        private readonly IReadOnlyList<GeometricSectionDefinition> _sections;

        public TrackAuthoringDefinition(IEnumerable<GeometricSectionDefinition> sections)
        {
            if (sections is null)
            {
                throw new ArgumentNullException(nameof(sections));
            }

            var copiedSections = new List<GeometricSectionDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            double totalLength = 0.0;

            foreach (GeometricSectionDefinition section in sections)
            {
                if (section is null)
                {
                    throw new ArgumentException("Section entries cannot be null.", nameof(sections));
                }

                if (!ids.Add(section.Id))
                {
                    throw new ArgumentException(
                        $"Duplicate section ID '{section.Id}' is not allowed.",
                        nameof(sections));
                }

                totalLength += section.Length;
                if (!AuthoringValidation.IsFinite(totalLength))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(sections),
                        "Combined section length must be finite.");
                }

                copiedSections.Add(section);
            }

            if (copiedSections.Count == 0)
            {
                throw new ArgumentException(
                    "At least one geometric section definition is required.",
                    nameof(sections));
            }

            _sections = copiedSections.AsReadOnly();
        }

        /// <summary>
        /// Ordered validated sections. The input sequence is copied at construction.
        /// </summary>
        public IReadOnlyList<GeometricSectionDefinition> Sections => _sections;
    }
}
