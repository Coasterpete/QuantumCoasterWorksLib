using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Debug
{
    public static class CenterlineFrameContinuityCommand
    {
        internal const string DefaultRelativeOutputPath = "artifacts/frame-continuity/centerline-frame-continuity.sample.json";

        private const int SampleCount = 13;
        private const string SampleName = "deterministic-roll-step-centerline";
        private const double RadiansToDegrees = 180.0 / System.Math.PI;

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public static int Run(string? outputPath = null)
        {
            CenterlineFrameContinuityArtifact artifact = BuildArtifact();
            string resolvedOutputPath = ResolveOutputPath(outputPath);
            string? parentDirectory = Path.GetDirectoryName(resolvedOutputPath);

            if (!string.IsNullOrEmpty(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            string json = JsonSerializer.Serialize(artifact, JsonOptions);
            File.WriteAllText(resolvedOutputPath, json, Utf8NoBom);

            Console.WriteLine($"Generated centerline frame continuity diagnostics for sample '{SampleName}'.");
            Console.WriteLine(artifact.DiagnosticText);
            Console.WriteLine($"Wrote centerline frame continuity artifact to '{resolvedOutputPath}'.");

            return 0;
        }

        private static CenterlineFrameContinuityArtifact BuildArtifact()
        {
            TrackDocument document = BuildSampleCenterline();
            var evaluator = new TrackEvaluator(document);
            double[] distances = TrackFrameDebugGizmoBuilder.BuildUniformFrameDistances(
                document.TotalLength,
                SampleCount);
            ExportTrackFrame[] frames = evaluator.EvaluateFramesAtDistances(distances);

            TrackFrameContinuityThresholds thresholds = TrackFrameContinuityThresholds.UniformDegrees(15.0);
            TrackFrameContinuityReport report = TrackFrameContinuityDiagnostics.Analyze(
                frames,
                distances,
                thresholds);

            return new CenterlineFrameContinuityArtifact(
                sampleName: SampleName,
                trackLength: document.TotalLength,
                sampleCount: distances.Length,
                intervalCount: report.IntervalCount,
                hasDiscontinuities: report.HasDiscontinuities,
                discontinuityCount: report.Issues.Count,
                thresholdsDegrees: BuildThresholdsDto(thresholds),
                metricsDegrees: BuildMetricsDto(report),
                samples: BuildSampleDtos(distances, frames),
                intervals: BuildIntervalDtos(report.Intervals),
                issues: BuildIssueDtos(report.Issues),
                diagnosticText: report.ToDiagnosticString());
        }

        private static TrackDocument BuildSampleCenterline()
        {
            TrackSegment[] segments =
            {
                new StraightSegment(
                    length: 40.0,
                    id: "s0",
                    spline: new LineCurve(
                        new Vector3d(0.0, 0.0, 0.0),
                        new Vector3d(40.0, 0.0, 0.0)),
                    rollRadians: 0.0),
                new CurvedSegment(
                    length: 40.0,
                    id: "c1",
                    spline: new CubicBezierCurve(
                        new Vector3d(40.0, 0.0, 0.0),
                        new Vector3d(55.0, 0.0, 0.0),
                        new Vector3d(65.0, 12.0, 20.0),
                        new Vector3d(80.0, 10.0, 30.0)),
                    rollRadians: 0.35),
                new StraightSegment(
                    length: 40.0,
                    id: "s2",
                    spline: new LineCurve(
                        new Vector3d(80.0, 10.0, 30.0),
                        new Vector3d(120.0, 10.0, 30.0)),
                    rollRadians: 0.15)
            };

            return new TrackDocument(segments);
        }

        private static string ResolveOutputPath(string? outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, DefaultRelativeOutputPath));
            }

            return Path.GetFullPath(outputPath);
        }

        private static CenterlineFrameContinuityThresholdsDto BuildThresholdsDto(
            TrackFrameContinuityThresholds thresholds)
        {
            return new CenterlineFrameContinuityThresholdsDto(
                Tangent: ToDegrees(thresholds.TangentAngleRadians),
                Normal: ToDegrees(thresholds.NormalAngleRadians),
                Binormal: ToDegrees(thresholds.BinormalAngleRadians),
                Roll: ToDegrees(thresholds.RollAngleRadians),
                MatrixOrientation: ToDegrees(thresholds.MatrixOrientationAngleRadians));
        }

        private static CenterlineFrameContinuityMetricsDto BuildMetricsDto(
            TrackFrameContinuityReport report)
        {
            return new CenterlineFrameContinuityMetricsDto(
                MaxTangent: report.SmoothnessReport.TangentAngleDelta.MaxAbsoluteDegrees,
                AverageTangent: report.SmoothnessReport.TangentAngleDelta.AverageAbsoluteDegrees,
                MaxNormal: report.SmoothnessReport.NormalAngleDelta.MaxAbsoluteDegrees,
                AverageNormal: report.SmoothnessReport.NormalAngleDelta.AverageAbsoluteDegrees,
                MaxBinormal: report.SmoothnessReport.BinormalAngleDelta.MaxAbsoluteDegrees,
                AverageBinormal: report.SmoothnessReport.BinormalAngleDelta.AverageAbsoluteDegrees,
                MaxRoll: report.SmoothnessReport.FrameTwistDelta.MaxAbsoluteDegrees,
                AverageRoll: report.SmoothnessReport.FrameTwistDelta.AverageAbsoluteDegrees,
                MaxMatrixOrientation: report.MatrixOrientationAngleDelta.MaxAbsoluteDegrees,
                AverageMatrixOrientation: report.MatrixOrientationAngleDelta.AverageAbsoluteDegrees);
        }

        private static CenterlineFrameContinuitySampleDto[] BuildSampleDtos(
            IReadOnlyList<double> distances,
            IReadOnlyList<ExportTrackFrame> frames)
        {
            var samples = new CenterlineFrameContinuitySampleDto[frames.Count];

            for (int i = 0; i < frames.Count; i++)
            {
                ExportTrackFrame frame = frames[i];
                samples[i] = new CenterlineFrameContinuitySampleDto(
                    SampleIndex: i,
                    Distance: distances[i],
                    Position: VectorDto.From(frame.Position),
                    Tangent: VectorDto.From(frame.Tangent),
                    Normal: VectorDto.From(frame.Normal),
                    Binormal: VectorDto.From(frame.Binormal));
            }

            return samples;
        }

        private static CenterlineFrameContinuityIntervalDto[] BuildIntervalDtos(
            IReadOnlyList<TrackFrameContinuityInterval> intervals)
        {
            var result = new CenterlineFrameContinuityIntervalDto[intervals.Count];

            for (int i = 0; i < intervals.Count; i++)
            {
                TrackFrameContinuityInterval interval = intervals[i];
                result[i] = new CenterlineFrameContinuityIntervalDto(
                    StartSampleIndex: interval.StartSampleIndex,
                    EndSampleIndex: interval.EndSampleIndex,
                    StartDistance: interval.StartDistance,
                    EndDistance: interval.EndDistance,
                    DistanceDelta: interval.DistanceDelta,
                    TangentDegrees: ToDegrees(interval.TangentAngleDeltaRadians),
                    NormalDegrees: ToDegrees(interval.NormalAngleDeltaRadians),
                    BinormalDegrees: ToDegrees(interval.BinormalAngleDeltaRadians),
                    RollDegrees: ToDegrees(interval.AbsoluteRollAngleDeltaRadians),
                    MatrixOrientationDegrees: ToDegrees(interval.MatrixOrientationAngleDeltaRadians));
            }

            return result;
        }

        private static CenterlineFrameContinuityIssueDto[] BuildIssueDtos(
            IReadOnlyList<TrackFrameContinuityIssue> issues)
        {
            var result = new CenterlineFrameContinuityIssueDto[issues.Count];

            for (int i = 0; i < issues.Count; i++)
            {
                TrackFrameContinuityIssue issue = issues[i];
                TrackFrameContinuityInterval interval = issue.Interval;
                result[i] = new CenterlineFrameContinuityIssueDto(
                    Kind: issue.Kind.ToString(),
                    StartSampleIndex: interval.StartSampleIndex,
                    EndSampleIndex: interval.EndSampleIndex,
                    StartDistance: interval.StartDistance,
                    EndDistance: interval.EndDistance,
                    ActualDegrees: issue.ActualAngleDegrees,
                    ThresholdDegrees: issue.ThresholdAngleDegrees);
            }

            return result;
        }

        private static double ToDegrees(double radians)
        {
            return radians * RadiansToDegrees;
        }

        private sealed class CenterlineFrameContinuityArtifact
        {
            public CenterlineFrameContinuityArtifact(
                string sampleName,
                double trackLength,
                int sampleCount,
                int intervalCount,
                bool hasDiscontinuities,
                int discontinuityCount,
                CenterlineFrameContinuityThresholdsDto thresholdsDegrees,
                CenterlineFrameContinuityMetricsDto metricsDegrees,
                CenterlineFrameContinuitySampleDto[] samples,
                CenterlineFrameContinuityIntervalDto[] intervals,
                CenterlineFrameContinuityIssueDto[] issues,
                string diagnosticText)
            {
                SampleName = sampleName;
                TrackLength = trackLength;
                SampleCount = sampleCount;
                IntervalCount = intervalCount;
                HasDiscontinuities = hasDiscontinuities;
                DiscontinuityCount = discontinuityCount;
                ThresholdsDegrees = thresholdsDegrees;
                MetricsDegrees = metricsDegrees;
                Samples = samples;
                Intervals = intervals;
                Issues = issues;
                DiagnosticText = diagnosticText;
            }

            public string Kind => "centerline-frame-continuity-diagnostics";

            public int SchemaVersion => 1;

            public bool BackendOnly => true;

            public string SampleName { get; }

            public double TrackLength { get; }

            public int SampleCount { get; }

            public int IntervalCount { get; }

            public bool HasDiscontinuities { get; }

            public int DiscontinuityCount { get; }

            public CenterlineFrameContinuityThresholdsDto ThresholdsDegrees { get; }

            public CenterlineFrameContinuityMetricsDto MetricsDegrees { get; }

            public CenterlineFrameContinuitySampleDto[] Samples { get; }

            public CenterlineFrameContinuityIntervalDto[] Intervals { get; }

            public CenterlineFrameContinuityIssueDto[] Issues { get; }

            public string DiagnosticText { get; }
        }

        private readonly record struct CenterlineFrameContinuityThresholdsDto(
            double Tangent,
            double Normal,
            double Binormal,
            double Roll,
            double MatrixOrientation);

        private readonly record struct CenterlineFrameContinuityMetricsDto(
            double MaxTangent,
            double AverageTangent,
            double MaxNormal,
            double AverageNormal,
            double MaxBinormal,
            double AverageBinormal,
            double MaxRoll,
            double AverageRoll,
            double MaxMatrixOrientation,
            double AverageMatrixOrientation);

        private readonly record struct CenterlineFrameContinuitySampleDto(
            int SampleIndex,
            double Distance,
            VectorDto Position,
            VectorDto Tangent,
            VectorDto Normal,
            VectorDto Binormal);

        private readonly record struct CenterlineFrameContinuityIntervalDto(
            int StartSampleIndex,
            int EndSampleIndex,
            double StartDistance,
            double EndDistance,
            double DistanceDelta,
            double TangentDegrees,
            double NormalDegrees,
            double BinormalDegrees,
            double RollDegrees,
            double MatrixOrientationDegrees);

        private readonly record struct CenterlineFrameContinuityIssueDto(
            string Kind,
            int StartSampleIndex,
            int EndSampleIndex,
            double StartDistance,
            double EndDistance,
            double ActualDegrees,
            double ThresholdDegrees);

        private readonly record struct VectorDto(double X, double Y, double Z)
        {
            public static VectorDto From(Vector3d vector)
            {
                return new VectorDto(vector.X, vector.Y, vector.Z);
            }
        }
    }
}
