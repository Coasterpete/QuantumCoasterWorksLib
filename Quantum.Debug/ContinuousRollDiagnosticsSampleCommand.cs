using System;
using System.Globalization;
using System.IO;
using System.Text;
using Quantum.Track;
using SystemMath = System.Math;

namespace Quantum.Debug
{
    public static class ContinuousRollDiagnosticsSampleCommand
    {
        public const string CommandName = "continuous-roll-diagnostics-sample";

        internal const string DefaultRelativeOutputPath =
            "artifacts/banking-profile/continuous-roll-diagnostics.sample.txt";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private const double DegreesToRadians = SystemMath.PI / 180.0;

        public static int Run(string? outputPath = null)
        {
            ContinuousRollDiagnosticsReport report = BuildReport();
            string resolvedOutputPath = ResolveOutputPath(outputPath);
            string? parentDirectory = Path.GetDirectoryName(resolvedOutputPath);

            if (!string.IsNullOrEmpty(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            File.WriteAllText(resolvedOutputPath, BuildReportText(report), Utf8NoBom);

            Console.WriteLine("Generated continuous roll diagnostics sample.");
            Console.WriteLine(report.ToDiagnosticString());
            Console.WriteLine($"Wrote continuous roll diagnostics sample to '{resolvedOutputPath}'.");

            return 0;
        }

        internal static ContinuousRollDiagnosticsReport BuildReport()
        {
            return ContinuousRollDiagnostics.AnalyzeRollRadians(
                new[] { 0.0, 10.0, 20.0, 30.0, 40.0, 50.0, 60.0 },
                new[]
                {
                    ToRadians(340.0),
                    ToRadians(350.0),
                    ToRadians(359.0),
                    ToRadians(1.0),
                    ToRadians(10.0),
                    ToRadians(20.0),
                    ToRadians(120.0)
                });
        }

        internal static string BuildReportText(ContinuousRollDiagnosticsReport report)
        {
            if (report is null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var builder = new StringBuilder();
            builder.AppendLine("Continuous roll diagnostics sample");
            builder.AppendLine("source=continuous-roll-diagnostics-sample");
            builder.AppendLine("units=meters,radians");
            builder.AppendLine(report.ToDiagnosticString());
            builder.AppendLine();
            builder.AppendLine("samples:");
            builder.AppendLine("index,distance,rollDeg,continuousRollDeg");

            for (int i = 0; i < report.Samples.Count; i++)
            {
                ContinuousRollDiagnosticsSample sample = report.Samples[i];
                builder.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0},{1:F3},{2:F3},{3:F3}",
                    sample.SampleIndex,
                    sample.Distance,
                    sample.RollDegrees,
                    sample.ContinuousRollDegrees);
                builder.AppendLine();
            }

            builder.AppendLine();
            builder.AppendLine("intervals:");
            builder.AppendLine("start,end,distanceDelta,rawDeltaDeg,deltaDeg,rateDegPerM,wrapped");

            for (int i = 0; i < report.Intervals.Count; i++)
            {
                ContinuousRollDiagnosticsInterval interval = report.Intervals[i];
                builder.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0},{1},{2:F3},{3:F3},{4:F3},{5:F3},{6}",
                    interval.StartSampleIndex,
                    interval.EndSampleIndex,
                    interval.DistanceDelta,
                    interval.RawRollDeltaDegrees,
                    interval.RollDeltaDegrees,
                    interval.RollRateDegreesPerMeter,
                    interval.UsedWrapAround);
                builder.AppendLine();
            }

            builder.AppendLine();
            builder.AppendLine("warnings:");

            if (report.Warnings.Count == 0)
            {
                builder.AppendLine("none");
            }
            else
            {
                builder.AppendLine("kind,start,end,actualDeg,thresholdDeg");

                for (int i = 0; i < report.Warnings.Count; i++)
                {
                    ContinuousRollWarning warning = report.Warnings[i];
                    builder.AppendFormat(
                        CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3:F3},{4:F3}",
                        warning.Kind,
                        warning.Interval.StartSampleIndex,
                        warning.Interval.EndSampleIndex,
                        warning.ActualValueDegrees,
                        warning.ThresholdValueDegrees);
                    builder.AppendLine();
                }
            }

            return builder.ToString();
        }

        private static string ResolveOutputPath(string? outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, DefaultRelativeOutputPath));
            }

            return Path.GetFullPath(outputPath);
        }

        private static double ToRadians(double degrees)
        {
            return degrees * DegreesToRadians;
        }
    }
}
