using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Quantum.Math;
using SystemMath = System.Math;

namespace Quantum.Track
{
    public enum TrackFrameContinuityIssueKind
    {
        Tangent,
        Normal,
        Binormal,
        Roll,
        MatrixOrientation
    }

    public readonly struct TrackFrameContinuityThresholds
    {
        private const double DegreesToRadians = SystemMath.PI / 180.0;

        public TrackFrameContinuityThresholds(
            double tangentAngleRadians,
            double normalAngleRadians,
            double binormalAngleRadians,
            double rollAngleRadians,
            double matrixOrientationAngleRadians)
        {
            ValidateAngle(tangentAngleRadians, nameof(tangentAngleRadians));
            ValidateAngle(normalAngleRadians, nameof(normalAngleRadians));
            ValidateAngle(binormalAngleRadians, nameof(binormalAngleRadians));
            ValidateAngle(rollAngleRadians, nameof(rollAngleRadians));
            ValidateAngle(matrixOrientationAngleRadians, nameof(matrixOrientationAngleRadians));

            TangentAngleRadians = tangentAngleRadians;
            NormalAngleRadians = normalAngleRadians;
            BinormalAngleRadians = binormalAngleRadians;
            RollAngleRadians = rollAngleRadians;
            MatrixOrientationAngleRadians = matrixOrientationAngleRadians;
        }

        public double TangentAngleRadians { get; }

        public double NormalAngleRadians { get; }

        public double BinormalAngleRadians { get; }

        public double RollAngleRadians { get; }

        public double MatrixOrientationAngleRadians { get; }

        public static TrackFrameContinuityThresholds Default => FromDegrees(
            tangentAngleDegrees: 30.0,
            normalAngleDegrees: 30.0,
            binormalAngleDegrees: 30.0,
            rollAngleDegrees: 30.0,
            matrixOrientationAngleDegrees: 30.0);

        public static TrackFrameContinuityThresholds UniformDegrees(double angleDegrees)
        {
            ValidateAngle(angleDegrees, nameof(angleDegrees));

            return FromDegrees(
                tangentAngleDegrees: angleDegrees,
                normalAngleDegrees: angleDegrees,
                binormalAngleDegrees: angleDegrees,
                rollAngleDegrees: angleDegrees,
                matrixOrientationAngleDegrees: angleDegrees);
        }

        public static TrackFrameContinuityThresholds FromDegrees(
            double tangentAngleDegrees,
            double normalAngleDegrees,
            double binormalAngleDegrees,
            double rollAngleDegrees,
            double matrixOrientationAngleDegrees)
        {
            ValidateAngle(tangentAngleDegrees, nameof(tangentAngleDegrees));
            ValidateAngle(normalAngleDegrees, nameof(normalAngleDegrees));
            ValidateAngle(binormalAngleDegrees, nameof(binormalAngleDegrees));
            ValidateAngle(rollAngleDegrees, nameof(rollAngleDegrees));
            ValidateAngle(matrixOrientationAngleDegrees, nameof(matrixOrientationAngleDegrees));

            return new TrackFrameContinuityThresholds(
                tangentAngleDegrees * DegreesToRadians,
                normalAngleDegrees * DegreesToRadians,
                binormalAngleDegrees * DegreesToRadians,
                rollAngleDegrees * DegreesToRadians,
                matrixOrientationAngleDegrees * DegreesToRadians);
        }

        private static void ValidateAngle(double value, string paramName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "Angle threshold must be finite and non-negative.");
            }
        }
    }

    public readonly struct TrackFrameContinuityInterval
    {
        public TrackFrameContinuityInterval(
            int startSampleIndex,
            int endSampleIndex,
            double startDistance,
            double endDistance,
            double distanceDelta,
            double tangentAngleDeltaRadians,
            double normalAngleDeltaRadians,
            double binormalAngleDeltaRadians,
            double rollAngleDeltaRadians,
            double matrixOrientationAngleDeltaRadians)
        {
            if (startSampleIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startSampleIndex), "Start sample index must be non-negative.");
            }

            if (endSampleIndex <= startSampleIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(endSampleIndex), "End sample index must be greater than start sample index.");
            }

            ValidateFiniteNonNegative(distanceDelta, nameof(distanceDelta));
            ValidateFiniteNonNegative(tangentAngleDeltaRadians, nameof(tangentAngleDeltaRadians));
            ValidateFiniteNonNegative(normalAngleDeltaRadians, nameof(normalAngleDeltaRadians));
            ValidateFiniteNonNegative(binormalAngleDeltaRadians, nameof(binormalAngleDeltaRadians));
            ValidateFinite(rollAngleDeltaRadians, nameof(rollAngleDeltaRadians));
            ValidateFiniteNonNegative(matrixOrientationAngleDeltaRadians, nameof(matrixOrientationAngleDeltaRadians));

            if (!IsFinite(startDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(startDistance), "Start distance must be finite.");
            }

            if (!IsFinite(endDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(endDistance), "End distance must be finite.");
            }

            StartSampleIndex = startSampleIndex;
            EndSampleIndex = endSampleIndex;
            StartDistance = startDistance;
            EndDistance = endDistance;
            DistanceDelta = distanceDelta;
            TangentAngleDeltaRadians = tangentAngleDeltaRadians;
            NormalAngleDeltaRadians = normalAngleDeltaRadians;
            BinormalAngleDeltaRadians = binormalAngleDeltaRadians;
            RollAngleDeltaRadians = rollAngleDeltaRadians;
            MatrixOrientationAngleDeltaRadians = matrixOrientationAngleDeltaRadians;
        }

        public int StartSampleIndex { get; }

        public int EndSampleIndex { get; }

        public double StartDistance { get; }

        public double EndDistance { get; }

        public double DistanceDelta { get; }

        public double TangentAngleDeltaRadians { get; }

        public double NormalAngleDeltaRadians { get; }

        public double BinormalAngleDeltaRadians { get; }

        public double RollAngleDeltaRadians { get; }

        public double MatrixOrientationAngleDeltaRadians { get; }

        public double AbsoluteRollAngleDeltaRadians => SystemMath.Abs(RollAngleDeltaRadians);

        private static void ValidateFiniteNonNegative(double value, string paramName)
        {
            if (!IsFinite(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "Value must be finite and non-negative.");
            }
        }

        private static void ValidateFinite(double value, string paramName)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, "Value must be finite.");
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    public readonly struct TrackFrameContinuityIssue
    {
        private const double RadiansToDegrees = 180.0 / SystemMath.PI;

        public TrackFrameContinuityIssue(
            TrackFrameContinuityIssueKind kind,
            TrackFrameContinuityInterval interval,
            double actualAngleRadians,
            double thresholdAngleRadians)
        {
            if (double.IsNaN(actualAngleRadians) || double.IsInfinity(actualAngleRadians) || actualAngleRadians < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(actualAngleRadians), "Actual angle must be finite and non-negative.");
            }

            if (double.IsNaN(thresholdAngleRadians) || double.IsInfinity(thresholdAngleRadians) || thresholdAngleRadians < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(thresholdAngleRadians), "Threshold angle must be finite and non-negative.");
            }

            Kind = kind;
            Interval = interval;
            ActualAngleRadians = actualAngleRadians;
            ThresholdAngleRadians = thresholdAngleRadians;
        }

        public TrackFrameContinuityIssueKind Kind { get; }

        public TrackFrameContinuityInterval Interval { get; }

        public double ActualAngleRadians { get; }

        public double ThresholdAngleRadians { get; }

        public double ActualAngleDegrees => ActualAngleRadians * RadiansToDegrees;

        public double ThresholdAngleDegrees => ThresholdAngleRadians * RadiansToDegrees;
    }

    public sealed class TrackFrameContinuityReport
    {
        public TrackFrameContinuityReport(
            IReadOnlyList<TrackFrameContinuityInterval> intervals,
            IReadOnlyList<TrackFrameContinuityIssue> issues,
            TrackFrameContinuityThresholds thresholds,
            TrackFrameSmoothnessReport smoothnessReport,
            TrackFrameSmoothnessMetricSummary matrixOrientationAngleDelta)
        {
            Intervals = intervals ?? throw new ArgumentNullException(nameof(intervals));
            Issues = issues ?? throw new ArgumentNullException(nameof(issues));
            Thresholds = thresholds;
            SmoothnessReport = smoothnessReport ?? throw new ArgumentNullException(nameof(smoothnessReport));
            MatrixOrientationAngleDelta = matrixOrientationAngleDelta;
        }

        public IReadOnlyList<TrackFrameContinuityInterval> Intervals { get; }

        public IReadOnlyList<TrackFrameContinuityIssue> Issues { get; }

        public TrackFrameContinuityThresholds Thresholds { get; }

        public TrackFrameSmoothnessReport SmoothnessReport { get; }

        public TrackFrameSmoothnessMetricSummary MatrixOrientationAngleDelta { get; }

        public int IntervalCount => Intervals.Count;

        public bool HasDiscontinuities => Issues.Count > 0;

        public string ToDiagnosticString()
        {
            var builder = new StringBuilder();
            builder.AppendFormat(
                CultureInfo.InvariantCulture,
                "Frame continuity: intervals={0}, discontinuities={1}, maxTangentDeg={2:F3}, maxNormalDeg={3:F3}, maxBinormalDeg={4:F3}, maxRollDeg={5:F3}, maxMatrixDeg={6:F3}",
                IntervalCount,
                Issues.Count,
                SmoothnessReport.TangentAngleDelta.MaxAbsoluteDegrees,
                SmoothnessReport.NormalAngleDelta.MaxAbsoluteDegrees,
                SmoothnessReport.BinormalAngleDelta.MaxAbsoluteDegrees,
                SmoothnessReport.FrameTwistDelta.MaxAbsoluteDegrees,
                MatrixOrientationAngleDelta.MaxAbsoluteDegrees);

            for (int i = 0; i < Issues.Count; i++)
            {
                TrackFrameContinuityIssue issue = Issues[i];
                TrackFrameContinuityInterval interval = issue.Interval;
                builder.Append('\n');
                builder.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0}: samples={1}->{2}, distance={3:F6}->{4:F6}, actualDeg={5:F3}, thresholdDeg={6:F3}",
                    issue.Kind,
                    interval.StartSampleIndex,
                    interval.EndSampleIndex,
                    interval.StartDistance,
                    interval.EndDistance,
                    issue.ActualAngleDegrees,
                    issue.ThresholdAngleDegrees);
            }

            return builder.ToString();
        }
    }

    public static class TrackFrameContinuityDiagnostics
    {
        private const double MinimumVectorMagnitude = 1e-9;

        public static TrackFrameContinuityReport Analyze(IReadOnlyList<TrackFrame> frames)
        {
            return Analyze(frames, TrackFrameContinuityThresholds.Default);
        }

        public static TrackFrameContinuityReport Analyze(
            IReadOnlyList<TrackFrame> frames,
            TrackFrameContinuityThresholds thresholds)
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

            return Analyze(frames, sampledDistances, thresholds);
        }

        public static TrackFrameContinuityReport Analyze(
            IReadOnlyList<TrackFrame> frames,
            IReadOnlyList<double> sampledDistances,
            TrackFrameContinuityThresholds thresholds)
        {
            if (frames is null)
            {
                throw new ArgumentNullException(nameof(frames));
            }

            if (sampledDistances is null)
            {
                throw new ArgumentNullException(nameof(sampledDistances));
            }

            TrackFrameSmoothnessReport smoothnessReport = TrackFrameSmoothnessDiagnostics.Analyze(
                frames,
                sampledDistances);

            if (smoothnessReport.IntervalCount == 0)
            {
                return new TrackFrameContinuityReport(
                    Array.Empty<TrackFrameContinuityInterval>(),
                    Array.Empty<TrackFrameContinuityIssue>(),
                    thresholds,
                    smoothnessReport,
                    new TrackFrameSmoothnessMetricSummary(0.0, 0.0));
            }

            var intervals = new TrackFrameContinuityInterval[smoothnessReport.IntervalCount];
            var issues = new List<TrackFrameContinuityIssue>();
            double maxMatrixOrientationDelta = 0.0;
            double sumMatrixOrientationDelta = 0.0;

            for (int i = 0; i < smoothnessReport.IntervalCount; i++)
            {
                TrackFrameSmoothnessInterval smoothnessInterval = smoothnessReport.Intervals[i];
                double matrixOrientationAngleDelta = ComputeMatrixOrientationAngleDeltaRadians(
                    frames[smoothnessInterval.StartSampleIndex],
                    frames[smoothnessInterval.EndSampleIndex]);

                TrackFrameContinuityInterval interval = new TrackFrameContinuityInterval(
                    smoothnessInterval.StartSampleIndex,
                    smoothnessInterval.EndSampleIndex,
                    smoothnessInterval.StartDistance,
                    smoothnessInterval.EndDistance,
                    smoothnessInterval.DistanceDelta,
                    smoothnessInterval.TangentAngleDeltaRadians,
                    smoothnessInterval.NormalAngleDeltaRadians,
                    smoothnessInterval.BinormalAngleDeltaRadians,
                    smoothnessInterval.FrameTwistDeltaRadians,
                    matrixOrientationAngleDelta);

                intervals[i] = interval;
                maxMatrixOrientationDelta = SystemMath.Max(maxMatrixOrientationDelta, matrixOrientationAngleDelta);
                sumMatrixOrientationDelta += matrixOrientationAngleDelta;

                AddIssueIfExceeded(
                    issues,
                    TrackFrameContinuityIssueKind.Tangent,
                    interval,
                    interval.TangentAngleDeltaRadians,
                    thresholds.TangentAngleRadians);
                AddIssueIfExceeded(
                    issues,
                    TrackFrameContinuityIssueKind.Normal,
                    interval,
                    interval.NormalAngleDeltaRadians,
                    thresholds.NormalAngleRadians);
                AddIssueIfExceeded(
                    issues,
                    TrackFrameContinuityIssueKind.Binormal,
                    interval,
                    interval.BinormalAngleDeltaRadians,
                    thresholds.BinormalAngleRadians);
                AddIssueIfExceeded(
                    issues,
                    TrackFrameContinuityIssueKind.Roll,
                    interval,
                    interval.AbsoluteRollAngleDeltaRadians,
                    thresholds.RollAngleRadians);
                AddIssueIfExceeded(
                    issues,
                    TrackFrameContinuityIssueKind.MatrixOrientation,
                    interval,
                    interval.MatrixOrientationAngleDeltaRadians,
                    thresholds.MatrixOrientationAngleRadians);
            }

            return new TrackFrameContinuityReport(
                intervals,
                issues,
                thresholds,
                smoothnessReport,
                new TrackFrameSmoothnessMetricSummary(
                    maxMatrixOrientationDelta,
                    sumMatrixOrientationDelta / intervals.Length));
        }

        public static TrackFrameContinuityReport AnalyzeSampledCenterline(
            TrackEvaluator evaluator,
            IReadOnlyList<double> sampledDistances,
            TrackFrameContinuityThresholds thresholds)
        {
            if (evaluator is null)
            {
                throw new ArgumentNullException(nameof(evaluator));
            }

            if (sampledDistances is null)
            {
                throw new ArgumentNullException(nameof(sampledDistances));
            }

            TrackFrame[] frames = evaluator.EvaluateFramesAtDistances(sampledDistances);
            return Analyze(frames, sampledDistances, thresholds);
        }

        private static void AddIssueIfExceeded(
            List<TrackFrameContinuityIssue> issues,
            TrackFrameContinuityIssueKind kind,
            TrackFrameContinuityInterval interval,
            double actualAngleRadians,
            double thresholdAngleRadians)
        {
            if (actualAngleRadians > thresholdAngleRadians)
            {
                issues.Add(new TrackFrameContinuityIssue(
                    kind,
                    interval,
                    actualAngleRadians,
                    thresholdAngleRadians));
            }
        }

        private static double ComputeMatrixOrientationAngleDeltaRadians(TrackFrame previous, TrackFrame current)
        {
            Vector3d previousTangent = NormalizeOrThrow(previous.Tangent, "previous tangent");
            Vector3d previousNormal = NormalizeOrThrow(previous.Normal, "previous normal");
            Vector3d previousBinormal = NormalizeOrThrow(previous.Binormal, "previous binormal");
            Vector3d currentTangent = NormalizeOrThrow(current.Tangent, "current tangent");
            Vector3d currentNormal = NormalizeOrThrow(current.Normal, "current normal");
            Vector3d currentBinormal = NormalizeOrThrow(current.Binormal, "current binormal");

            double trace =
                Vector3d.Dot(previousTangent, currentTangent) +
                Vector3d.Dot(previousNormal, currentNormal) +
                Vector3d.Dot(previousBinormal, currentBinormal);
            double cosine = MathUtil.Clamp((trace - 1.0) * 0.5, -1.0, 1.0);
            return SystemMath.Acos(cosine);
        }

        private static Vector3d NormalizeOrThrow(Vector3d vector, string label)
        {
            if (!IsFinite(vector))
            {
                throw new InvalidOperationException(
                    $"Unable to compute matrix orientation continuity: {label} contains non-finite components.");
            }

            double length = vector.Length;
            if (length <= MinimumVectorMagnitude)
            {
                throw new InvalidOperationException(
                    $"Unable to compute matrix orientation continuity: {label} magnitude is near zero.");
            }

            return vector / length;
        }

        private static bool IsFinite(Vector3d vector)
        {
            return !(double.IsNaN(vector.X) ||
                     double.IsNaN(vector.Y) ||
                     double.IsNaN(vector.Z) ||
                     double.IsInfinity(vector.X) ||
                     double.IsInfinity(vector.Y) ||
                     double.IsInfinity(vector.Z));
        }
    }
}
