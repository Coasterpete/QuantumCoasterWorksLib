using System;
using System.Collections.Generic;
using Quantum.Math;
using SplineTrackFrame = Quantum.Splines.TrackFrame;
using SystemMath = System.Math;

namespace Quantum.Track
{
    public readonly struct TransportedFrameComparisonSample
    {
        private const double RadiansToDegrees = 180.0 / SystemMath.PI;

        public TransportedFrameComparisonSample(
            int sampleIndex,
            double distance,
            double tangentAngleDeltaRadians,
            double normalAngleDeltaRadians,
            double binormalAngleDeltaRadians,
            double frameAngleDeltaRadians,
            double rollAngleDeltaRadians,
            double matrixOrientationAngleDeltaRadians)
        {
            if (sampleIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleIndex), "Sample index must be non-negative.");
            }

            ValidateFinite(distance, nameof(distance));
            ValidateFiniteNonNegative(tangentAngleDeltaRadians, nameof(tangentAngleDeltaRadians));
            ValidateFiniteNonNegative(normalAngleDeltaRadians, nameof(normalAngleDeltaRadians));
            ValidateFiniteNonNegative(binormalAngleDeltaRadians, nameof(binormalAngleDeltaRadians));
            ValidateFiniteNonNegative(frameAngleDeltaRadians, nameof(frameAngleDeltaRadians));
            ValidateFinite(rollAngleDeltaRadians, nameof(rollAngleDeltaRadians));
            ValidateFiniteNonNegative(matrixOrientationAngleDeltaRadians, nameof(matrixOrientationAngleDeltaRadians));

            SampleIndex = sampleIndex;
            Distance = distance;
            TangentAngleDeltaRadians = tangentAngleDeltaRadians;
            NormalAngleDeltaRadians = normalAngleDeltaRadians;
            BinormalAngleDeltaRadians = binormalAngleDeltaRadians;
            FrameAngleDeltaRadians = frameAngleDeltaRadians;
            RollAngleDeltaRadians = rollAngleDeltaRadians;
            MatrixOrientationAngleDeltaRadians = matrixOrientationAngleDeltaRadians;
        }

        public int SampleIndex { get; }

        public double Distance { get; }

        public double TangentAngleDeltaRadians { get; }

        public double NormalAngleDeltaRadians { get; }

        public double BinormalAngleDeltaRadians { get; }

        public double FrameAngleDeltaRadians { get; }

        public double RollAngleDeltaRadians { get; }

        public double MatrixOrientationAngleDeltaRadians { get; }

        public double TangentAngleDeltaDegrees => TangentAngleDeltaRadians * RadiansToDegrees;

        public double NormalAngleDeltaDegrees => NormalAngleDeltaRadians * RadiansToDegrees;

        public double BinormalAngleDeltaDegrees => BinormalAngleDeltaRadians * RadiansToDegrees;

        public double FrameAngleDeltaDegrees => FrameAngleDeltaRadians * RadiansToDegrees;

        public double RollAngleDeltaDegrees => RollAngleDeltaRadians * RadiansToDegrees;

        public double MatrixOrientationAngleDeltaDegrees => MatrixOrientationAngleDeltaRadians * RadiansToDegrees;

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

    public sealed class TransportedFrameComparisonReport
    {
        public TransportedFrameComparisonReport(
            IReadOnlyList<TransportedFrameComparisonSample> samples,
            TrackFrameSmoothnessReport statelessSmoothnessReport,
            TrackFrameSmoothnessReport transportedSmoothnessReport,
            TrackFrameContinuityReport statelessContinuityReport,
            TrackFrameContinuityReport transportedContinuityReport,
            TrackFrameSmoothnessMetricSummary tangentAngleDelta,
            TrackFrameSmoothnessMetricSummary normalAngleDelta,
            TrackFrameSmoothnessMetricSummary binormalAngleDelta,
            TrackFrameSmoothnessMetricSummary frameAngleDelta,
            TrackFrameSmoothnessMetricSummary rollAngleDelta,
            TrackFrameSmoothnessMetricSummary matrixOrientationAngleDelta)
        {
            Samples = samples ?? throw new ArgumentNullException(nameof(samples));
            StatelessSmoothnessReport = statelessSmoothnessReport ?? throw new ArgumentNullException(nameof(statelessSmoothnessReport));
            TransportedSmoothnessReport = transportedSmoothnessReport ?? throw new ArgumentNullException(nameof(transportedSmoothnessReport));
            StatelessContinuityReport = statelessContinuityReport ?? throw new ArgumentNullException(nameof(statelessContinuityReport));
            TransportedContinuityReport = transportedContinuityReport ?? throw new ArgumentNullException(nameof(transportedContinuityReport));
            TangentAngleDelta = tangentAngleDelta;
            NormalAngleDelta = normalAngleDelta;
            BinormalAngleDelta = binormalAngleDelta;
            FrameAngleDelta = frameAngleDelta;
            RollAngleDelta = rollAngleDelta;
            MatrixOrientationAngleDelta = matrixOrientationAngleDelta;
        }

        public IReadOnlyList<TransportedFrameComparisonSample> Samples { get; }

        public int SampleCount => Samples.Count;

        public TrackFrameSmoothnessReport StatelessSmoothnessReport { get; }

        public TrackFrameSmoothnessReport TransportedSmoothnessReport { get; }

        public TrackFrameContinuityReport StatelessContinuityReport { get; }

        public TrackFrameContinuityReport TransportedContinuityReport { get; }

        public TrackFrameSmoothnessMetricSummary TangentAngleDelta { get; }

        public TrackFrameSmoothnessMetricSummary NormalAngleDelta { get; }

        public TrackFrameSmoothnessMetricSummary BinormalAngleDelta { get; }

        public TrackFrameSmoothnessMetricSummary FrameAngleDelta { get; }

        public TrackFrameSmoothnessMetricSummary RollAngleDelta { get; }

        public TrackFrameSmoothnessMetricSummary MatrixOrientationAngleDelta { get; }
    }

    public static class TransportedFrameComparisonDiagnostics
    {
        private const double MinimumVectorMagnitude = 1e-9;

        public static TransportedFrameComparisonReport Compare(
            TrackDocument document,
            IReadOnlyList<double> distances)
        {
            return Compare(document, distances, TrackFrameContinuityThresholds.Default);
        }

        public static TransportedFrameComparisonReport Compare(
            TrackDocument document,
            IReadOnlyList<double> distances,
            TrackFrameContinuityThresholds thresholds)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            return Compare(document, new TrackEvaluator(document), distances, thresholds);
        }

        public static TransportedFrameComparisonReport Compare(
            TrackDocument document,
            TrackEvaluator evaluator,
            IReadOnlyList<double> distances,
            TrackFrameContinuityThresholds thresholds)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (evaluator is null)
            {
                throw new ArgumentNullException(nameof(evaluator));
            }

            if (distances is null)
            {
                throw new ArgumentNullException(nameof(distances));
            }

            TrackFrame[] statelessFrames = EvaluateStatelessFrames(document, evaluator, distances);
            TrackFrame[] transportedFrames = TransportedTrackFrameSampler.SampleFramesAtDistances(
                document,
                evaluator,
                distances);

            if (statelessFrames.Length != transportedFrames.Length ||
                statelessFrames.Length != distances.Count)
            {
                throw new InvalidOperationException("Stateless, transported, and distance sample counts must match.");
            }

            TransportedFrameComparisonSample[] samples = BuildComparisonSamples(
                statelessFrames,
                transportedFrames,
                distances);

            TrackFrameSmoothnessReport statelessSmoothnessReport = TrackFrameSmoothnessDiagnostics.Analyze(
                statelessFrames,
                distances);
            TrackFrameSmoothnessReport transportedSmoothnessReport = TrackFrameSmoothnessDiagnostics.Analyze(
                transportedFrames,
                distances);
            TrackFrameContinuityReport statelessContinuityReport = TrackFrameContinuityDiagnostics.Analyze(
                statelessFrames,
                distances,
                thresholds);
            TrackFrameContinuityReport transportedContinuityReport = TrackFrameContinuityDiagnostics.Analyze(
                transportedFrames,
                distances,
                thresholds);

            return new TransportedFrameComparisonReport(
                samples,
                statelessSmoothnessReport,
                transportedSmoothnessReport,
                statelessContinuityReport,
                transportedContinuityReport,
                BuildAngleSummary(samples, sample => sample.TangentAngleDeltaRadians),
                BuildAngleSummary(samples, sample => sample.NormalAngleDeltaRadians),
                BuildAngleSummary(samples, sample => sample.BinormalAngleDeltaRadians),
                BuildAngleSummary(samples, sample => sample.FrameAngleDeltaRadians),
                BuildAngleSummary(samples, sample => sample.RollAngleDeltaRadians),
                BuildAngleSummary(samples, sample => sample.MatrixOrientationAngleDeltaRadians));
        }

        private static TrackFrame[] EvaluateStatelessFrames(
            TrackDocument document,
            TrackEvaluator evaluator,
            IReadOnlyList<double> distances)
        {
            SplineTrackFrame[] splineFrames = evaluator.EvaluateSplineFramesAtDistances(document, distances);
            var frames = new TrackFrame[splineFrames.Length];

            for (int i = 0; i < splineFrames.Length; i++)
            {
                frames[i] = BuildExportFrame(splineFrames[i]);
            }

            return frames;
        }

        private static TrackFrame BuildExportFrame(SplineTrackFrame sourceFrame)
        {
            Vector3d tangent = NormalizeOrThrow(sourceFrame.Tangent, "tangent");
            Vector3d projectedNormal = sourceFrame.Normal - (tangent * Vector3d.Dot(sourceFrame.Normal, tangent));
            Vector3d normal = NormalizeOrThrow(projectedNormal, "normal");
            Vector3d binormal = NormalizeOrThrow(Vector3d.Cross(tangent, normal), "binormal");
            normal = NormalizeOrThrow(Vector3d.Cross(binormal, tangent), "normal");

            return new TrackFrame(sourceFrame.S, sourceFrame.Position, tangent, normal, binormal);
        }

        private static TransportedFrameComparisonSample[] BuildComparisonSamples(
            IReadOnlyList<TrackFrame> statelessFrames,
            IReadOnlyList<TrackFrame> transportedFrames,
            IReadOnlyList<double> distances)
        {
            var samples = new TransportedFrameComparisonSample[distances.Count];

            for (int i = 0; i < samples.Length; i++)
            {
                TrackFrame stateless = statelessFrames[i];
                TrackFrame transported = transportedFrames[i];
                double tangentAngleDelta = ComputeAngleDeltaRadians(stateless.Tangent, transported.Tangent);
                double normalAngleDelta = ComputeAngleDeltaRadians(stateless.Normal, transported.Normal);
                double binormalAngleDelta = ComputeAngleDeltaRadians(stateless.Binormal, transported.Binormal);
                double frameAngleDelta = SystemMath.Max(
                    tangentAngleDelta,
                    SystemMath.Max(normalAngleDelta, binormalAngleDelta));

                samples[i] = new TransportedFrameComparisonSample(
                    i,
                    distances[i],
                    tangentAngleDelta,
                    normalAngleDelta,
                    binormalAngleDelta,
                    frameAngleDelta,
                    ComputeRollAngleDeltaRadians(stateless, transported),
                    ComputeMatrixOrientationAngleDeltaRadians(stateless, transported));
            }

            return samples;
        }

        private static TrackFrameSmoothnessMetricSummary BuildAngleSummary(
            IReadOnlyList<TransportedFrameComparisonSample> samples,
            Func<TransportedFrameComparisonSample, double> selector)
        {
            if (samples.Count == 0)
            {
                return new TrackFrameSmoothnessMetricSummary(0.0, 0.0);
            }

            double maxAbsolute = 0.0;
            double sumAbsolute = 0.0;

            for (int i = 0; i < samples.Count; i++)
            {
                double absoluteValue = SystemMath.Abs(selector(samples[i]));
                maxAbsolute = SystemMath.Max(maxAbsolute, absoluteValue);
                sumAbsolute += absoluteValue;
            }

            return new TrackFrameSmoothnessMetricSummary(
                maxAbsolute,
                sumAbsolute / samples.Count);
        }

        private static double ComputeAngleDeltaRadians(Vector3d from, Vector3d to)
        {
            Vector3d normalizedFrom = NormalizeOrThrow(from, "from");
            Vector3d normalizedTo = NormalizeOrThrow(to, "to");
            double dot = MathUtil.Clamp(Vector3d.Dot(normalizedFrom, normalizedTo), -1.0, 1.0);
            return SystemMath.Acos(dot);
        }

        private static double ComputeRollAngleDeltaRadians(TrackFrame stateless, TrackFrame transported)
        {
            Vector3d axis = NormalizeOrFallback(
                stateless.Tangent + transported.Tangent,
                transported.Tangent,
                stateless.Tangent,
                Vector3d.UnitX);

            Vector3d from = ProjectOntoPlane(stateless.Normal, axis);
            Vector3d to = ProjectOntoPlane(transported.Normal, axis);

            if (from.LengthSquared <= MinimumVectorMagnitude ||
                to.LengthSquared <= MinimumVectorMagnitude)
            {
                from = ProjectOntoPlane(stateless.Binormal, axis);
                to = ProjectOntoPlane(transported.Binormal, axis);
            }

            from = NormalizeOrFallback(from, Vector3d.Zero);
            to = NormalizeOrFallback(to, Vector3d.Zero);

            if (from.LengthSquared <= MinimumVectorMagnitude ||
                to.LengthSquared <= MinimumVectorMagnitude)
            {
                return 0.0;
            }

            double dot = MathUtil.Clamp(Vector3d.Dot(from, to), -1.0, 1.0);
            double sine = Vector3d.Dot(Vector3d.Cross(from, to), axis);
            return SystemMath.Atan2(sine, dot);
        }

        private static double ComputeMatrixOrientationAngleDeltaRadians(TrackFrame stateless, TrackFrame transported)
        {
            Vector3d statelessTangent = NormalizeOrThrow(stateless.Tangent, "stateless tangent");
            Vector3d statelessNormal = NormalizeOrThrow(stateless.Normal, "stateless normal");
            Vector3d statelessBinormal = NormalizeOrThrow(stateless.Binormal, "stateless binormal");
            Vector3d transportedTangent = NormalizeOrThrow(transported.Tangent, "transported tangent");
            Vector3d transportedNormal = NormalizeOrThrow(transported.Normal, "transported normal");
            Vector3d transportedBinormal = NormalizeOrThrow(transported.Binormal, "transported binormal");

            double trace =
                Vector3d.Dot(statelessTangent, transportedTangent) +
                Vector3d.Dot(statelessNormal, transportedNormal) +
                Vector3d.Dot(statelessBinormal, transportedBinormal);
            double cosine = MathUtil.Clamp((trace - 1.0) * 0.5, -1.0, 1.0);
            return SystemMath.Acos(cosine);
        }

        private static Vector3d ProjectOntoPlane(Vector3d vector, Vector3d planeNormal)
        {
            return vector - (planeNormal * Vector3d.Dot(vector, planeNormal));
        }

        private static Vector3d NormalizeOrFallback(
            Vector3d vector,
            Vector3d fallbackA,
            Vector3d fallbackB,
            Vector3d fallbackC)
        {
            Vector3d normalized = NormalizeOrFallback(vector, fallbackA);
            if (normalized.LengthSquared > MinimumVectorMagnitude)
            {
                return normalized;
            }

            return NormalizeOrFallback(fallbackB, fallbackC);
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

        private static Vector3d NormalizeOrThrow(Vector3d vector, string label)
        {
            if (!IsFinite(vector))
            {
                throw new InvalidOperationException(
                    $"Unable to compute transported frame comparison: {label} contains non-finite components.");
            }

            double length = vector.Length;
            if (length <= MinimumVectorMagnitude)
            {
                throw new InvalidOperationException(
                    $"Unable to compute transported frame comparison: {label} magnitude is near zero.");
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
