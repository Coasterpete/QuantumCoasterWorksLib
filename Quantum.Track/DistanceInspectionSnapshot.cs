using System;
using System.Collections.Generic;

namespace Quantum.Track
{
    /// <summary>
    /// Immutable UI-friendly snapshot of distance inspection results.
    /// </summary>
    public sealed class DistanceInspectionSnapshot
    {
        private readonly List<DistanceSectionInspection> _sections;
        private readonly IReadOnlyList<DistanceSectionInspection> _sectionsView;

        public DistanceInspectionSnapshot(
            double distance,
            IReadOnlyList<DistanceSectionInspection> sections)
        {
            if (!IsFinite(distance))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(distance),
                    distance,
                    "Distance must be finite.");
            }

            if (sections is null)
            {
                throw new ArgumentNullException(nameof(sections));
            }

            Distance = distance;

            _sections = new List<DistanceSectionInspection>(sections.Count);
            for (int i = 0; i < sections.Count; i++)
            {
                DistanceSectionInspection section = sections[i] ?? throw new ArgumentException(
                    $"Inspection at index {i} cannot be null.",
                    nameof(sections));

                _sections.Add(section);
            }

            _sectionsView = _sections.AsReadOnly();
        }

        public double Distance { get; }

        public IReadOnlyList<DistanceSectionInspection> Sections => _sectionsView;

        private static bool IsFinite(double value)
        {
            return !(double.IsNaN(value) || double.IsInfinity(value));
        }
    }
}
