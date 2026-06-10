using System;
using System.IO;
using System.Text;
using Quantum.IO.TrackFrameContinuity.V1;
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

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public static int Run(string? outputPath = null)
        {
            TrackFrameContinuityDiagnosticsExportV1Dto artifact = BuildArtifact();
            string resolvedOutputPath = ResolveOutputPath(outputPath);
            string? parentDirectory = Path.GetDirectoryName(resolvedOutputPath);

            if (!string.IsNullOrEmpty(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            string json = TrackFrameContinuityDiagnosticsExportV1Json.Serialize(artifact, indented: true);
            File.WriteAllText(resolvedOutputPath, json, Utf8NoBom);

            Console.WriteLine($"Generated centerline frame continuity diagnostics for sample '{SampleName}'.");
            Console.WriteLine(artifact.DiagnosticText);
            Console.WriteLine($"Wrote centerline frame continuity artifact to '{resolvedOutputPath}'.");

            return 0;
        }

        private static TrackFrameContinuityDiagnosticsExportV1Dto BuildArtifact()
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

            return TrackFrameContinuityDiagnosticsExportV1Mapper.Export(
                new TrackFrameContinuityDiagnosticsExportV1Source
                {
                    Units = "meters",
                    SourceName = SampleName,
                    TrackLength = document.TotalLength,
                    Frames = frames,
                    SampledDistances = distances,
                    Report = report
                });
        }

        private static TrackDocument BuildSampleCenterline()
        {
            var baseMiddleSpline = new CubicBezierCurve(
                new Vector3d(0.0, 0.0, 0.0),
                new Vector3d(15.0, 0.0, 0.0),
                new Vector3d(25.0, 12.0, 20.0),
                new Vector3d(40.0, 10.0, 30.0));
            double middleScale = 40.0 / new ArcLengthLUT(baseMiddleSpline).TotalLength;
            Vector3d middleStart = new Vector3d(40.0, 0.0, 0.0);
            var middleSpline = new CubicBezierCurve(
                middleStart + (baseMiddleSpline.P0 * middleScale),
                middleStart + (baseMiddleSpline.P1 * middleScale),
                middleStart + (baseMiddleSpline.P2 * middleScale),
                middleStart + (baseMiddleSpline.P3 * middleScale));
            Vector3d finalStart = middleSpline.P3;
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
                    spline: middleSpline,
                    rollRadians: 0.35),
                new StraightSegment(
                    length: 40.0,
                    id: "s2",
                    spline: new LineCurve(
                        finalStart,
                        finalStart + new Vector3d(40.0, 0.0, 0.0)),
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
    }
}
