using System;
using Quantum.Math;
using Quantum.Splines;

namespace Quantum.Track.Internal
{
    internal sealed class CompiledTrackSamplingContext
    {
        private const int ArcLengthSamples = 100;
        private const double DeclaredLengthAbsoluteTolerance = 1e-3;
        private const double DeclaredLengthRelativeTolerance = 1e-6;

        private readonly CompiledTrackSegment[] _segments;

        private CompiledTrackSamplingContext(
            CompiledTrackSegment[] segments,
            double totalLength)
        {
            _segments = segments;
            TotalLength = totalLength;
        }

        public double TotalLength { get; }

        public static CompiledTrackSamplingContext Compile(TrackDocument document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            int segmentCount = document.Segments.Count;
            var segments = new CompiledTrackSegment[segmentCount];
            double totalLength = 0.0;

            for (int i = 0; i < segmentCount; i++)
            {
                TrackSegment segment = document.Segments[i];

                if (segment is null)
                {
                    throw new InvalidOperationException("TrackDocument contains a null segment entry.");
                }

                ValidateFinitePositiveLength(segment, i);
                ValidateFiniteRoll(segment, i);

                ArcLengthLUT? arcLengthLookup = null;
                double geometricLength = segment.Length;

                if (segment.Spline is IParamCurve spline)
                {
                    arcLengthLookup = BuildArcLengthLookup(spline, i);
                    ValidateMeasuredLength(arcLengthLookup.TotalLength, i);
                    if (segment.Spline is IArcLengthCurve arcLengthCurve)
                    {
                        geometricLength = arcLengthCurve.Length;
                        ValidateMeasuredLength(geometricLength, i);
                        ValidateLengthsMatch(
                            geometricLength,
                            arcLengthLookup.TotalLength,
                            i,
                            "reported arc length");
                    }
                    else
                    {
                        geometricLength = arcLengthLookup.TotalLength;
                    }

                    ValidateMeasuredLength(geometricLength, i);
                    ValidateDeclaredLengthMatchesMeasured(segment.Length, geometricLength, i);
                    ValidateSplineTangents(spline, i);
                }

                segments[i] = new CompiledTrackSegment(
                    segment,
                    totalLength,
                    geometricLength,
                    arcLengthLookup,
                    arcLengthLookup?.TotalLength ?? geometricLength);
                totalLength += geometricLength;
            }

            return new CompiledTrackSamplingContext(segments, totalLength);
        }

        public ResolvedTrackDistance Resolve(double distance)
        {
            double clampedDistance = System.Math.Max(0.0, System.Math.Min(distance, TotalLength));

            for (int i = 0; i < _segments.Length; i++)
            {
                CompiledTrackSegment segment = _segments[i];
                double segmentEndDistance = segment.StationStartDistance + segment.GeometricLength;
                bool isLastSegment = i == _segments.Length - 1;

                if (clampedDistance < segmentEndDistance || isLastSegment)
                {
                    double localDistance = System.Math.Max(
                        0.0,
                        System.Math.Min(
                            clampedDistance - segment.StationStartDistance,
                            segment.GeometricLength));
                    double localT = segment.MapLocalDistanceToParameter(localDistance);

                    return new ResolvedTrackDistance(
                        segment.Segment,
                        localT,
                        localDistance,
                        clampedDistance);
                }
            }

            throw new InvalidOperationException("TrackDocument could not be evaluated at the specified distance.");
        }

        private static void ValidateFinitePositiveLength(TrackSegment segment, int segmentIndex)
        {
            if (!IsFinite(segment.Length) || segment.Length <= 0.0)
            {
                throw new InvalidOperationException(
                    $"Track segment at index {segmentIndex} must have a finite declared length greater than zero.");
            }
        }

        private static void ValidateFiniteRoll(TrackSegment segment, int segmentIndex)
        {
            if (!IsFinite(segment.RollRadians))
            {
                throw new InvalidOperationException(
                    $"Track segment at index {segmentIndex} must have a finite roll angle.");
            }
        }

        private static ArcLengthLUT BuildArcLengthLookup(IParamCurve spline, int segmentIndex)
        {
            try
            {
                return new ArcLengthLUT(
                    spline,
                    samples: ArcLengthSamples,
                    tolerance: ArcLengthLUT.DefaultTolerance);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                throw new InvalidOperationException(
                    $"Track segment at index {segmentIndex} spline could not be measured.",
                    ex);
            }
        }

        private static void ValidateMeasuredLength(double measuredLength, int segmentIndex)
        {
            if (!IsFinite(measuredLength) || measuredLength <= MathUtil.Epsilon)
            {
                throw new InvalidOperationException(
                    $"Track segment at index {segmentIndex} spline must have a finite geometric length greater than zero.");
            }
        }

        private static void ValidateDeclaredLengthMatchesMeasured(
            double declaredLength,
            double measuredLength,
            int segmentIndex)
        {
            ValidateLengthsMatch(declaredLength, measuredLength, segmentIndex, "declared length");
        }

        private static void ValidateLengthsMatch(
            double statedLength,
            double measuredLength,
            int segmentIndex,
            string statedLengthLabel)
        {
            double tolerance = System.Math.Max(
                DeclaredLengthAbsoluteTolerance,
                measuredLength * DeclaredLengthRelativeTolerance);

            if (System.Math.Abs(statedLength - measuredLength) > tolerance)
            {
                throw new InvalidOperationException(
                    $"Track segment at index {segmentIndex} {statedLengthLabel} {statedLength:R} does not match measured geometric length {measuredLength:R} within tolerance {tolerance:R}.");
            }
        }

        private static void ValidateSplineTangents(IParamCurve spline, int segmentIndex)
        {
            for (int sampleIndex = 0; sampleIndex <= ArcLengthSamples; sampleIndex++)
            {
                double t = (double)sampleIndex / ArcLengthSamples;
                Vector3d tangent;

                try
                {
                    tangent = spline.Tangent(t);
                }
                catch (Exception ex) when (!(ex is OutOfMemoryException))
                {
                    throw new InvalidOperationException(
                        $"Track segment at index {segmentIndex} has an invalid tangent at t={t:R}.",
                        ex);
                }

                if (!IsFinite(tangent) || tangent.Length <= MathUtil.Epsilon)
                {
                    throw new InvalidOperationException(
                        $"Track segment at index {segmentIndex} has an invalid tangent at t={t:R}.");
                }
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFinite(Vector3d value)
        {
            return IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);
        }
    }
}
