using System;
using System.Collections.Generic;
using Quantum.Math;
using SystemMath = System.Math;

namespace Quantum.Track
{
    public readonly struct TrackFrameSmoothnessMetricSummary
    {
        private const double RadiansToDegrees = 180.0 / SystemMath.PI;

        public TrackFrameSmoothnessMetricSummary(double maxAbsoluteRadians, double averageAbsoluteRadians)
        {
            if (double.IsNaN(maxAbsoluteRadians) || double.IsInfinity(maxAbsoluteRadians) || maxAbsoluteRadians < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAbsoluteRadians), "Max absolute radians must be finite and non-negative.");
            }

            if (double.IsNaN(averageAbsoluteRadians) || double.IsInfinity(averageAbsoluteRadians) || averageAbsoluteRadians < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(averageAbsoluteRadians), "Average absolute radians must be finite and non-negative.");
            }

            MaxAbsoluteRadians = maxAbsoluteRadians;
            AverageAbsoluteRadians = averageAbsoluteRadians;
        }

        public double MaxAbsoluteRadians { get; }

        public double AverageAbsoluteRadians { get; }

        public double MaxAbsoluteDegrees => MaxAbsoluteRadians * RadiansToDegrees;

        public double AverageAbsoluteDegrees => AverageAbsoluteRadians * RadiansToDegrees;
    }

    public readonly struct TrackFrameCurvatureMetricSummary
    {
        public TrackFrameCurvatureMetricSummary(double maxAbsolute, double averageAbsolute)
        {
            if (double.IsNaN(maxAbsolute) || double.IsInfinity(maxAbsolute) || maxAbsolute < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAbsolute), "Max absolute value must be finite and non-negative.");
            }

            if (double.IsNaN(averageAbsolute) || double.IsInfinity(averageAbsolute) || averageAbsolute < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(averageAbsolute), "Average absolute value must be finite and non-negative.");
            }

            MaxAbsolute = maxAbsolute;
            AverageAbsolute = averageAbsolute;
        }

        public double MaxAbsolute { get; }

        public double AverageAbsolute { get; }
    }

    public readonly struct TrackFrameSmoothnessInterval
    {
        public TrackFrameSmoothnessInterval(
            int startSampleIndex,
            int endSampleIndex,
            double startDistance,
            double endDistance,
            double distanceDelta,
            double tangentAngleDeltaRadians,
            double normalAngleDeltaRadians,
            double binormalAngleDeltaRadians,
            double frameAngleDeltaRadians,
            double frameTwistDeltaRadians,
            double curvatureEstimate,
            double curvatureEstimateDelta)
        {
            if (startSampleIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startSampleIndex), "Start sample index must be non-negative.");
            }

            if (endSampleIndex <= startSampleIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(endSampleIndex), "End sample index must be greater than start sample index.");
            }

            if (!IsFinite(startDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(startDistance), "Start distance must be finite.");
            }

            if (!IsFinite(endDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(endDistance), "End distance must be finite.");
            }

            if (!IsFinite(distanceDelta) || distanceDelta < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(distanceDelta), "Distance delta must be finite and non-negative.");
            }

            if (!IsFinite(tangentAngleDeltaRadians) || tangentAngleDeltaRadians < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(tangentAngleDeltaRadians), "Tangent angle delta must be finite and non-negative.");
            }

            if (!IsFinite(normalAngleDeltaRadians) || normalAngleDeltaRadians < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(normalAngleDeltaRadians), "Normal angle delta must be finite and non-negative.");
            }

            if (!IsFinite(binormalAngleDeltaRadians) || binormalAngleDeltaRadians < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(binormalAngleDeltaRadians), "Binormal angle delta must be finite and non-negative.");
            }

            if (!IsFinite(frameAngleDeltaRadians) || frameAngleDeltaRadians < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameAngleDeltaRadians), "Frame angle delta must be finite and non-negative.");
            }

            if (!IsFinite(frameTwistDeltaRadians))
            {
                throw new ArgumentOutOfRangeException(nameof(frameTwistDeltaRadians), "Frame twist delta must be finite.");
            }

            if (!IsFinite(curvatureEstimate))
            {
                throw new ArgumentOutOfRangeException(nameof(curvatureEstimate), "Curvature estimate must be finite.");
            }

            if (!IsFinite(curvatureEstimateDelta))
            {
                throw new ArgumentOutOfRangeException(nameof(curvatureEstimateDelta), "Curvature estimate delta must be finite.");
            }

            StartSampleIndex = startSampleIndex;
            EndSampleIndex = endSampleIndex;
            StartDistance = startDistance;
            EndDistance = endDistance;
            DistanceDelta = distanceDelta;
            TangentAngleDeltaRadians = tangentAngleDeltaRadians;
            NormalAngleDeltaRadians = normalAngleDeltaRadians;
            BinormalAngleDeltaRadians = binormalAngleDeltaRadians;
            FrameAngleDeltaRadians = frameAngleDeltaRadians;
            FrameTwistDeltaRadians = frameTwistDeltaRadians;
            CurvatureEstimate = curvatureEstimate;
            CurvatureEstimateDelta = curvatureEstimateDelta;
        }

        public int StartSampleIndex { get; }

        public int EndSampleIndex { get; }

        public double StartDistance { get; }

        public double EndDistance { get; }

        public double DistanceDelta { get; }

        public double TangentAngleDeltaRadians { get; }

        public double NormalAngleDeltaRadians { get; }

        public double BinormalAngleDeltaRadians { get; }

        public double FrameAngleDeltaRadians { get; }

        public double FrameTwistDeltaRadians { get; }

        public double CurvatureEstimate { get; }

        public double CurvatureEstimateDelta { get; }

        public double FrameTwistDeltaDegrees => FrameTwistDeltaRadians * (180.0 / SystemMath.PI);

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    public sealed class TrackFrameSmoothnessReport
    {
        public TrackFrameSmoothnessReport(
            IReadOnlyList<TrackFrameSmoothnessInterval> intervals,
            TrackFrameSmoothnessMetricSummary tangentAngleDelta,
            TrackFrameSmoothnessMetricSummary normalAngleDelta,
            TrackFrameSmoothnessMetricSummary binormalAngleDelta,
            TrackFrameSmoothnessMetricSummary frameAngleDelta,
            TrackFrameSmoothnessMetricSummary frameTwistDelta,
            TrackFrameCurvatureMetricSummary curvatureEstimate,
            TrackFrameCurvatureMetricSummary curvatureEstimateDelta)
        {
            Intervals = intervals ?? throw new ArgumentNullException(nameof(intervals));
            TangentAngleDelta = tangentAngleDelta;
            NormalAngleDelta = normalAngleDelta;
            BinormalAngleDelta = binormalAngleDelta;
            FrameAngleDelta = frameAngleDelta;
            FrameTwistDelta = frameTwistDelta;
            CurvatureEstimate = curvatureEstimate;
            CurvatureEstimateDelta = curvatureEstimateDelta;
        }

        public IReadOnlyList<TrackFrameSmoothnessInterval> Intervals { get; }

        public int IntervalCount => Intervals.Count;

        public TrackFrameSmoothnessMetricSummary TangentAngleDelta { get; }

        public TrackFrameSmoothnessMetricSummary NormalAngleDelta { get; }

        public TrackFrameSmoothnessMetricSummary BinormalAngleDelta { get; }

        public TrackFrameSmoothnessMetricSummary FrameAngleDelta { get; }

        public TrackFrameSmoothnessMetricSummary FrameTwistDelta { get; }

        public TrackFrameCurvatureMetricSummary CurvatureEstimate { get; }

        public TrackFrameCurvatureMetricSummary CurvatureEstimateDelta { get; }
    }

    public static class TrackFrameSmoothnessDiagnostics
    {
        private const double MinimumVectorMagnitude = 1e-9;
        private const double MinimumDistanceDelta = 1e-9;

        public static TrackFrameSmoothnessReport Analyze(IReadOnlyList<TrackFrame> frames)
        {
            if (frames is null)
            {
                throw new ArgumentNullException(nameof(frames));
            }

            var sampledDistances = new double[frames.Count];
            for (int i = 0; i < frames.Count; i++)
            {
                sampledDistances[i] = frames[i].Distance;
            }

            return Analyze(frames, sampledDistances);
        }

        public static TrackFrameSmoothnessReport Analyze(
            IReadOnlyList<TrackFrame> frames,
            IReadOnlyList<double> sampledDistances)
        {
            if (frames is null)
            {
                throw new ArgumentNullException(nameof(frames));
            }

            if (sampledDistances is null)
            {
                throw new ArgumentNullException(nameof(sampledDistances));
            }

            if (frames.Count != sampledDistances.Count)
            {
                throw new ArgumentException("Frame and distance sample counts must match.", nameof(sampledDistances));
            }

            int frameCount = frames.Count;
            if (frameCount < 2)
            {
                return new TrackFrameSmoothnessReport(
                    Array.Empty<TrackFrameSmoothnessInterval>(),
                    new TrackFrameSmoothnessMetricSummary(0.0, 0.0),
                    new TrackFrameSmoothnessMetricSummary(0.0, 0.0),
                    new TrackFrameSmoothnessMetricSummary(0.0, 0.0),
                    new TrackFrameSmoothnessMetricSummary(0.0, 0.0),
                    new TrackFrameSmoothnessMetricSummary(0.0, 0.0),
                    new TrackFrameCurvatureMetricSummary(0.0, 0.0),
                    new TrackFrameCurvatureMetricSummary(0.0, 0.0));
            }

            var intervals = new TrackFrameSmoothnessInterval[frameCount - 1];
            double previousCurvature = 0.0;
            bool hasPreviousCurvature = false;

            for (int i = 1; i < frameCount; i++)
            {
                TrackFrame previous = frames[i - 1];
                TrackFrame current = frames[i];

                ValidateFiniteFrame(previous, i - 1);
                ValidateFiniteFrame(current, i);

                double startDistance = sampledDistances[i - 1];
                double endDistance = sampledDistances[i];

                if (!IsFinite(startDistance))
                {
                    throw new ArgumentOutOfRangeException(nameof(sampledDistances), "Distance samples must be finite.");
                }

                if (!IsFinite(endDistance))
                {
                    throw new ArgumentOutOfRangeException(nameof(sampledDistances), "Distance samples must be finite.");
                }

                double distanceDelta = SystemMath.Abs(endDistance - startDistance);
                double tangentAngleDelta = ComputeAngleDeltaRadians(previous.Tangent, current.Tangent);
                double normalAngleDelta = ComputeAngleDeltaRadians(previous.Normal, current.Normal);
                double binormalAngleDelta = ComputeAngleDeltaRadians(previous.Binormal, current.Binormal);
                double frameAngleDelta = SystemMath.Max(tangentAngleDelta, SystemMath.Max(normalAngleDelta, binormalAngleDelta));
                double frameTwistDelta = ComputeFrameTwistDeltaRadians(previous, current);

                double curvatureEstimate = distanceDelta > MinimumDistanceDelta
                    ? tangentAngleDelta / distanceDelta
                    : 0.0;

                double curvatureEstimateDelta = hasPreviousCurvature
                    ? curvatureEstimate - previousCurvature
                    : 0.0;

                intervals[i - 1] = new TrackFrameSmoothnessInterval(
                    startSampleIndex: i - 1,
                    endSampleIndex: i,
                    startDistance: startDistance,
                    endDistance: endDistance,
                    distanceDelta: distanceDelta,
                    tangentAngleDeltaRadians: tangentAngleDelta,
                    normalAngleDeltaRadians: normalAngleDelta,
                    binormalAngleDeltaRadians: binormalAngleDelta,
                    frameAngleDeltaRadians: frameAngleDelta,
                    frameTwistDeltaRadians: frameTwistDelta,
                    curvatureEstimate: curvatureEstimate,
                    curvatureEstimateDelta: curvatureEstimateDelta);

                previousCurvature = curvatureEstimate;
                hasPreviousCurvature = true;
            }

            return new TrackFrameSmoothnessReport(
                intervals,
                BuildAngleSummary(intervals, interval => interval.TangentAngleDeltaRadians),
                BuildAngleSummary(intervals, interval => interval.NormalAngleDeltaRadians),
                BuildAngleSummary(intervals, interval => interval.BinormalAngleDeltaRadians),
                BuildAngleSummary(intervals, interval => interval.FrameAngleDeltaRadians),
                BuildAngleSummary(intervals, interval => interval.FrameTwistDeltaRadians),
                BuildCurvatureSummary(intervals, interval => interval.CurvatureEstimate),
                BuildCurvatureSummary(intervals, interval => interval.CurvatureEstimateDelta));
        }

        private static TrackFrameSmoothnessMetricSummary BuildAngleSummary(
            IReadOnlyList<TrackFrameSmoothnessInterval> intervals,
            Func<TrackFrameSmoothnessInterval, double> selector)
        {
            if (intervals.Count == 0)
            {
                return new TrackFrameSmoothnessMetricSummary(0.0, 0.0);
            }

            double maxAbsolute = 0.0;
            double sumAbsolute = 0.0;

            for (int i = 0; i < intervals.Count; i++)
            {
                double absoluteValue = SystemMath.Abs(selector(intervals[i]));
                maxAbsolute = SystemMath.Max(maxAbsolute, absoluteValue);
                sumAbsolute += absoluteValue;
            }

            return new TrackFrameSmoothnessMetricSummary(
                maxAbsolute,
                sumAbsolute / intervals.Count);
        }

        private static TrackFrameCurvatureMetricSummary BuildCurvatureSummary(
            IReadOnlyList<TrackFrameSmoothnessInterval> intervals,
            Func<TrackFrameSmoothnessInterval, double> selector)
        {
            if (intervals.Count == 0)
            {
                return new TrackFrameCurvatureMetricSummary(0.0, 0.0);
            }

            double maxAbsolute = 0.0;
            double sumAbsolute = 0.0;

            for (int i = 0; i < intervals.Count; i++)
            {
                double absoluteValue = SystemMath.Abs(selector(intervals[i]));
                maxAbsolute = SystemMath.Max(maxAbsolute, absoluteValue);
                sumAbsolute += absoluteValue;
            }

            return new TrackFrameCurvatureMetricSummary(
                maxAbsolute,
                sumAbsolute / intervals.Count);
        }

        private static double ComputeAngleDeltaRadians(Vector3d from, Vector3d to)
        {
            Vector3d normalizedFrom = NormalizeOrFallback(from, Vector3d.Zero);
            Vector3d normalizedTo = NormalizeOrFallback(to, Vector3d.Zero);

            if (normalizedFrom.LengthSquared <= MinimumVectorMagnitude ||
                normalizedTo.LengthSquared <= MinimumVectorMagnitude)
            {
                return 0.0;
            }

            double dot = Vector3d.Dot(normalizedFrom, normalizedTo);
            double clampedDot = MathUtil.Clamp(dot, -1.0, 1.0);
            return SystemMath.Acos(clampedDot);
        }

        private static double ComputeFrameTwistDeltaRadians(TrackFrame previous, TrackFrame current)
        {
            Vector3d axis = NormalizeOrFallback(previous.Tangent + current.Tangent, current.Tangent);
            axis = NormalizeOrFallback(axis, previous.Tangent, Vector3d.UnitX);

            Vector3d previousProjected = ProjectOntoPlane(previous.Normal, axis);
            Vector3d currentProjected = ProjectOntoPlane(current.Normal, axis);

            if (previousProjected.LengthSquared <= MinimumVectorMagnitude ||
                currentProjected.LengthSquared <= MinimumVectorMagnitude)
            {
                previousProjected = ProjectOntoPlane(previous.Binormal, axis);
                currentProjected = ProjectOntoPlane(current.Binormal, axis);
            }

            Vector3d from = NormalizeOrFallback(previousProjected, Vector3d.Zero);
            Vector3d to = NormalizeOrFallback(currentProjected, Vector3d.Zero);

            if (from.LengthSquared <= MinimumVectorMagnitude ||
                to.LengthSquared <= MinimumVectorMagnitude)
            {
                return 0.0;
            }

            double dot = MathUtil.Clamp(Vector3d.Dot(from, to), -1.0, 1.0);
            double sine = Vector3d.Dot(Vector3d.Cross(from, to), axis);
            return SystemMath.Atan2(sine, dot);
        }

        private static Vector3d ProjectOntoPlane(Vector3d vector, Vector3d planeNormal)
        {
            return vector - (planeNormal * Vector3d.Dot(vector, planeNormal));
        }

        private static Vector3d NormalizeOrFallback(Vector3d vector, Vector3d fallbackA)
        {
            Vector3d normalized = vector.Normalized();
            if (normalized.LengthSquared > MinimumVectorMagnitude)
            {
                return normalized;
            }

            normalized = fallbackA.Normalized();
            if (normalized.LengthSquared > MinimumVectorMagnitude)
            {
                return normalized;
            }

            return Vector3d.Zero;
        }

        private static Vector3d NormalizeOrFallback(Vector3d vector, Vector3d fallbackA, Vector3d fallbackB)
        {
            Vector3d normalized = NormalizeOrFallback(vector, fallbackA);
            if (normalized.LengthSquared > MinimumVectorMagnitude)
            {
                return normalized;
            }

            return NormalizeOrFallback(fallbackB, Vector3d.Zero);
        }

        private static void ValidateFiniteFrame(TrackFrame frame, int sampleIndex)
        {
            if (!IsFinite(frame.Position) ||
                !IsFinite(frame.Tangent) ||
                !IsFinite(frame.Normal) ||
                !IsFinite(frame.Binormal) ||
                !IsFinite(frame.Distance))
            {
                throw new InvalidOperationException(
                    $"Track frame at sample index {sampleIndex} contains non-finite components.");
            }
        }

        private static bool IsFinite(Vector3d vector)
        {
            return IsFinite(vector.X) && IsFinite(vector.Y) && IsFinite(vector.Z);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
