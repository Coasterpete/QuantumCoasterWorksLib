using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Quantum.IO.TransportedFrameComparison.V1;
using Quantum.Track;

namespace Quantum.Debug
{
    public static class TransportedFrameComparisonCommand
    {
        internal const string DefaultRelativeOutputPath =
            "artifacts/frame-comparison/transported-frame-comparison.sample.json";

        private const string SourceName = "diagnostic-track-fixtures";
        private const double ContinuityThresholdDegrees = 181.0;

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public static int Run(string? outputPath = null)
        {
            TransportedFrameComparisonDiagnosticsExportV1Dto artifact = BuildArtifact();
            string resolvedOutputPath = ResolveOutputPath(outputPath);
            string? parentDirectory = Path.GetDirectoryName(resolvedOutputPath);

            if (!string.IsNullOrEmpty(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            string json = TransportedFrameComparisonDiagnosticsExportV1Json.Serialize(artifact, indented: true);
            File.WriteAllText(resolvedOutputPath, json, Utf8NoBom);

            Console.WriteLine(
                $"Generated transported frame comparison diagnostics for {artifact.Metadata.ReportCount} fixture(s).");

            for (int i = 0; i < artifact.Reports.Length; i++)
            {
                TransportedFrameComparisonReportV1Dto report = artifact.Reports[i];
                Console.WriteLine(
                    $"{report.SourceName}: samples={report.SummaryMetrics.SampleCount}, " +
                    $"maxNormalDeltaDeg={report.SummaryMetrics.NormalDegrees.MaxAbsolute:0.###}, " +
                    $"statelessIssues={report.SummaryMetrics.StatelessContinuityIssueCount}, " +
                    $"transportedIssues={report.SummaryMetrics.TransportedContinuityIssueCount}");
            }

            Console.WriteLine($"Wrote transported frame comparison artifact to '{resolvedOutputPath}'.");

            return 0;
        }

        internal static TransportedFrameComparisonDiagnosticsExportV1Dto BuildArtifact()
        {
            IReadOnlyList<DiagnosticTrackFixture> fixtures = DiagnosticTrackFixtures.All();
            var reports = new List<TransportedFrameComparisonDiagnosticsExportV1ReportSource>(fixtures.Count);
            TrackFrameContinuityThresholds thresholds =
                TrackFrameContinuityThresholds.UniformDegrees(ContinuityThresholdDegrees);

            for (int i = 0; i < fixtures.Count; i++)
            {
                DiagnosticTrackFixture fixture = fixtures[i];
                TransportedFrameComparisonReport report = TransportedFrameComparisonDiagnostics.Compare(
                    fixture.Document,
                    fixture.SampleDistances,
                    thresholds);

                reports.Add(new TransportedFrameComparisonDiagnosticsExportV1ReportSource
                {
                    SourceName = fixture.Name,
                    TrackLength = fixture.Document.TotalLength,
                    Report = report
                });
            }

            return TransportedFrameComparisonDiagnosticsExportV1Mapper.Export(
                new TransportedFrameComparisonDiagnosticsExportV1Source
                {
                    Units = "meters",
                    SourceName = SourceName,
                    Reports = reports
                });
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
