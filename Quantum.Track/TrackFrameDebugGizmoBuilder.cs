using System;
using System.Collections.Generic;
using Quantum.Math;

namespace Quantum.Track
{
    public static class TrackFrameDebugGizmoBuilder
    {
        public static double[] BuildUniformFrameDistances(
            double totalLength,
            int sampleCount,
            int subSamplesPerSegment = 1)
        {
            ValidateFiniteNonNegative(totalLength, nameof(totalLength), "Track length must be finite and non-negative.");

            if (sampleCount < 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleCount),
                    sampleCount,
                    "Sample count must be at least two.");
            }

            if (subSamplesPerSegment < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(subSamplesPerSegment),
                    subSamplesPerSegment,
                    "Sub-samples per segment must be at least one.");
            }

            int effectiveSampleCount = checked(((sampleCount - 1) * subSamplesPerSegment) + 1);
            var distances = new double[effectiveSampleCount];

            double interval = totalLength / (effectiveSampleCount - 1);
            for (int i = 0; i < effectiveSampleCount; i++)
            {
                distances[i] = i * interval;
            }

            distances[effectiveSampleCount - 1] = totalLength;
            return distances;
        }

        public static DebugLineSegment[] BuildAxes(TrackFrame frame, double axisLength)
        {
            ValidateAxisLength(axisLength);

            Vector3d origin = frame.Position;

            return new[]
            {
                new DebugLineSegment(origin, origin + (frame.Tangent * axisLength), TrackFrameAxisType.Tangent),
                new DebugLineSegment(origin, origin + (frame.Normal * axisLength), TrackFrameAxisType.Normal),
                new DebugLineSegment(origin, origin + (frame.Binormal * axisLength), TrackFrameAxisType.Binormal)
            };
        }

        public static DebugLineSegment[] BuildAxesAtDistance(TrackEvaluator evaluator, double distance, double axisLength)
        {
            if (evaluator is null)
            {
                throw new ArgumentNullException(nameof(evaluator));
            }

            TrackFrame frame = evaluator.EvaluateFrameAtDistance(distance);
            return BuildAxes(frame, axisLength);
        }

        public static DebugLineSegment[] BuildRailCrossTies(
            IReadOnlyList<TrackFrame> sampledFrames,
            double trackGauge,
            double spacingInterval)
        {
            if (sampledFrames is null)
            {
                throw new ArgumentNullException(nameof(sampledFrames));
            }

            ValidatePositiveFinite(trackGauge, nameof(trackGauge), "Track gauge must be finite and greater than zero.");
            ValidatePositiveFinite(spacingInterval, nameof(spacingInterval), "Spacing interval must be finite and greater than zero.");

            if (sampledFrames.Count == 0)
            {
                return Array.Empty<DebugLineSegment>();
            }

            double halfGauge = trackGauge * 0.5;
            const double DistanceEpsilon = 1e-9;

            var crossTieSegments = new List<DebugLineSegment>();
            ResolveLateralAxis(sampledFrames[0], Vector3d.UnitX, out Vector3d previousLateral);
            GetRailPositions(sampledFrames[0].Position, previousLateral, halfGauge, out Vector3d previousLeftRail, out Vector3d previousRightRail);
            double previousDistance = sampledFrames[0].Distance;

            crossTieSegments.Add(new DebugLineSegment(previousLeftRail, previousRightRail, TrackFrameAxisType.Binormal));
            double nextTieDistance = previousDistance + spacingInterval;

            for (int i = 1; i < sampledFrames.Count; i++)
            {
                TrackFrame currentFrame = sampledFrames[i];
                ResolveLateralAxis(currentFrame, previousLateral, out Vector3d currentLateral);
                if (Vector3d.Dot(currentLateral, previousLateral) < 0.0)
                {
                    currentLateral = currentLateral * -1.0;
                }

                GetRailPositions(currentFrame.Position, currentLateral, halfGauge, out Vector3d currentLeftRail, out Vector3d currentRightRail);
                double currentDistance = currentFrame.Distance;

                if (currentDistance <= previousDistance + DistanceEpsilon)
                {
                    previousDistance = currentDistance;
                    previousLeftRail = currentLeftRail;
                    previousRightRail = currentRightRail;
                    previousLateral = currentLateral;
                    continue;
                }

                double segmentLength = currentDistance - previousDistance;
                while (nextTieDistance <= currentDistance + DistanceEpsilon)
                {
                    double t = Clamp01((nextTieDistance - previousDistance) / segmentLength);
                    Vector3d tieLeft = Lerp(previousLeftRail, currentLeftRail, t);
                    Vector3d tieRight = Lerp(previousRightRail, currentRightRail, t);
                    crossTieSegments.Add(new DebugLineSegment(tieLeft, tieRight, TrackFrameAxisType.Binormal));
                    nextTieDistance += spacingInterval;
                }

                previousDistance = currentDistance;
                previousLeftRail = currentLeftRail;
                previousRightRail = currentRightRail;
                previousLateral = currentLateral;
            }

            return crossTieSegments.ToArray();
        }

        public static DebugLineSegment[] BuildBankingRibbon(
            IReadOnlyList<TrackFrame> sampledFrames,
            double halfWidth,
            double normalOffset)
        {
            if (sampledFrames is null)
            {
                throw new ArgumentNullException(nameof(sampledFrames));
            }

            ValidatePositiveFinite(halfWidth, nameof(halfWidth), "Ribbon half-width must be finite and greater than zero.");
            ValidateFinite(normalOffset, nameof(normalOffset), "Ribbon normal offset must be finite.");

            if (sampledFrames.Count == 0)
            {
                return Array.Empty<DebugLineSegment>();
            }

            var segments = new List<DebugLineSegment>(sampledFrames.Count * 4);

            ResolveRibbonAxes(sampledFrames[0], Vector3d.UnitY, Vector3d.UnitX, out Vector3d previousNormal, out Vector3d previousLateral);
            GetRibbonPositions(
                sampledFrames[0].Position,
                previousNormal,
                previousLateral,
                halfWidth,
                normalOffset,
                out Vector3d firstCenter,
                out Vector3d previousLeft,
                out Vector3d previousRight);
            segments.Add(new DebugLineSegment(sampledFrames[0].Position, firstCenter, TrackFrameAxisType.Normal));
            segments.Add(new DebugLineSegment(previousLeft, previousRight, TrackFrameAxisType.Binormal));

            for (int i = 1; i < sampledFrames.Count; i++)
            {
                TrackFrame frame = sampledFrames[i];
                ResolveRibbonAxes(frame, previousNormal, previousLateral, out Vector3d currentNormal, out Vector3d currentLateral);
                if (Vector3d.Dot(currentLateral, previousLateral) < 0.0)
                {
                    currentLateral = currentLateral * -1.0;
                }

                if (Vector3d.Dot(currentNormal, previousNormal) < 0.0)
                {
                    currentNormal = currentNormal * -1.0;
                }

                GetRibbonPositions(
                    frame.Position,
                    currentNormal,
                    currentLateral,
                    halfWidth,
                    normalOffset,
                    out Vector3d currentCenter,
                    out Vector3d currentLeft,
                    out Vector3d currentRight);

                segments.Add(new DebugLineSegment(frame.Position, currentCenter, TrackFrameAxisType.Normal));
                segments.Add(new DebugLineSegment(previousLeft, currentLeft, TrackFrameAxisType.Binormal));
                segments.Add(new DebugLineSegment(previousRight, currentRight, TrackFrameAxisType.Binormal));
                segments.Add(new DebugLineSegment(currentLeft, currentRight, TrackFrameAxisType.Binormal));

                previousLeft = currentLeft;
                previousRight = currentRight;
                previousNormal = currentNormal;
                previousLateral = currentLateral;
            }

            return segments.ToArray();
        }

        private static void ValidateAxisLength(double axisLength)
        {
            ValidatePositiveFinite(axisLength, nameof(axisLength), "Axis length must be finite and greater than zero.");
        }

        private static void ResolveLateralAxis(TrackFrame frame, Vector3d lateralFallback, out Vector3d lateral)
        {
            lateral = ResolveAxis(frame.Binormal, lateralFallback, Vector3d.UnitX);
        }

        private static void ResolveRibbonAxes(
            TrackFrame frame,
            Vector3d normalFallback,
            Vector3d lateralFallback,
            out Vector3d normal,
            out Vector3d lateral)
        {
            normal = ResolveAxis(frame.Normal, normalFallback, Vector3d.UnitY);
            lateral = ResolveAxis(frame.Binormal, lateralFallback, Vector3d.UnitX);
        }

        private static void GetRailPositions(Vector3d position, Vector3d lateral, double halfGauge, out Vector3d leftRail, out Vector3d rightRail)
        {
            Vector3d railOffset = lateral * halfGauge;
            leftRail = position - railOffset;
            rightRail = position + railOffset;
        }

        private static void GetRibbonPositions(
            Vector3d position,
            Vector3d normal,
            Vector3d lateral,
            double halfWidth,
            double normalOffset,
            out Vector3d center,
            out Vector3d left,
            out Vector3d right)
        {
            center = position + (normal * normalOffset);
            Vector3d halfSpan = lateral * halfWidth;
            left = center - halfSpan;
            right = center + halfSpan;
        }

        private static Vector3d ResolveAxis(Vector3d candidate, Vector3d fallbackAxis, Vector3d defaultAxis)
        {
            Vector3d resolved = candidate.Normalized();
            if (resolved.LengthSquared > 1e-12)
            {
                return resolved;
            }

            resolved = fallbackAxis.Normalized();
            if (resolved.LengthSquared > 1e-12)
            {
                return resolved;
            }

            return defaultAxis;
        }

        private static Vector3d Lerp(Vector3d a, Vector3d b, double t)
        {
            return a + ((b - a) * t);
        }

        private static double Clamp01(double value)
        {
            if (value < 0.0)
            {
                return 0.0;
            }

            if (value > 1.0)
            {
                return 1.0;
            }

            return value;
        }

        private static void ValidatePositiveFinite(double value, string paramName, string message)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(paramName, value, message);
            }
        }

        private static void ValidateFinite(double value, string paramName, string message)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, message);
            }
        }

        private static void ValidateFiniteNonNegative(double value, string paramName, string message)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(paramName, value, message);
            }
        }
    }
}

