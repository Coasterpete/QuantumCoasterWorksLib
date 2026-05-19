using System;
using System.Collections.Generic;
using Quantum.Math;
using Quantum.Splines;

namespace Quantum.Track
{
    /// <summary>
    /// Explicit adapter for evaluating one geometric section through the existing
    /// TrackDocument/TrackEvaluator segment path.
    /// </summary>
    public static class GeometricSectionTrackDocumentBuilder
    {
        public static TrackDocument BuildDocument(
            GeometricSection section,
            string? segmentId = null,
            string? forceSegmentReference = null)
        {
            return new TrackDocument(new[]
            {
                BuildSegment(section, segmentId, forceSegmentReference)
            });
        }

        /// <summary>
        /// Explicit opt-in adapter for evaluating multiple zero-roll geometric sections
        /// through one existing TrackDocument/TrackEvaluator segment path.
        /// </summary>
        public static TrackDocument BuildZeroRollCompositeDocument(
            IEnumerable<GeometricSection> sections,
            string? segmentId = null,
            string? forceSegmentReference = null)
        {
            if (sections is null)
            {
                throw new ArgumentNullException(nameof(sections));
            }

            var materializedSections = new List<GeometricSection>();

            foreach (GeometricSection section in sections)
            {
                if (section is null)
                {
                    throw new ArgumentException("Section entries cannot be null.", nameof(sections));
                }

                ValidateZeroRoll(section.Roll);
                materializedSections.Add(section);
            }

            if (materializedSections.Count < 2)
            {
                throw new ArgumentException(
                    "At least two geometric sections are required for a composite section document.",
                    nameof(sections));
            }

            CompositeSectionCurve centerline = SectionCurveAssembler.Assemble(materializedSections);
            TrackSegment segment;

            if (ContainsCurvedSection(materializedSections))
            {
                segment = new CurvedSegment(
                    centerline.TotalLength,
                    segmentId,
                    forceSegmentReference,
                    centerline,
                    rollRadians: 0.0);
            }
            else
            {
                segment = new StraightSegment(
                    centerline.TotalLength,
                    segmentId,
                    forceSegmentReference,
                    centerline,
                    rollRadians: 0.0);
            }

            return new TrackDocument(new[] { segment }, materializedSections);
        }

        public static TrackSegment BuildSegment(
            GeometricSection section,
            string? id = null,
            string? forceSegmentReference = null)
        {
            if (section is null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            IParamCurve curve = section.GenerateCurve();
            if (curve is null)
            {
                throw new InvalidOperationException("GeometricSection.GenerateCurve returned null.");
            }

            double rollRadians = section.Roll ?? 0.0;
            if (IsCurved(section.Curvature))
            {
                return new CurvedSegment(
                    section.Length,
                    id,
                    forceSegmentReference,
                    curve,
                    rollRadians);
            }

            return new StraightSegment(
                section.Length,
                id,
                forceSegmentReference,
                curve,
                rollRadians);
        }

        private static void ValidateZeroRoll(double? roll)
        {
            if (!roll.HasValue)
            {
                return;
            }

            if (!IsFinite(roll.Value) || System.Math.Abs(roll.Value) > MathUtil.Epsilon)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(roll),
                    roll.Value,
                    "Composite geometric section documents are limited to zero-roll sections.");
            }
        }

        private static bool ContainsCurvedSection(IReadOnlyList<GeometricSection> sections)
        {
            for (int i = 0; i < sections.Count; i++)
            {
                if (IsCurved(sections[i].Curvature))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsCurved(double? curvature)
        {
            return curvature.HasValue &&
                   IsFinite(curvature.Value) &&
                   System.Math.Abs(curvature.Value) > MathUtil.Epsilon;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
