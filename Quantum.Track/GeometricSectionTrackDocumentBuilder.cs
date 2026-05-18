using System;
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
