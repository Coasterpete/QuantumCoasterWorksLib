using System;
using System.IO;
using System.Text;
using Quantum.IO.BankingProfile.V1;
using Quantum.Track;

namespace Quantum.Debug
{
    public static class BankingProfileDiagnosticsCommand
    {
        public const string CommandName = "banking-profile-diagnostics";

        internal const string DefaultRelativeOutputPath =
            "artifacts/banking-profile/banking-profile-diagnostics.sample.json";

        private const int SampleCount = 11;
        private const double ProfileLength = 100.0;
        private const string SourceName = "deterministic-banking-profile-roll-ramp";
        private const double DegreesToRadians = System.Math.PI / 180.0;

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public static int Run(string? outputPath = null)
        {
            BankingProfileDiagnosticsExportV1Dto artifact = BuildArtifact();
            string resolvedOutputPath = ResolveOutputPath(outputPath);
            string? parentDirectory = Path.GetDirectoryName(resolvedOutputPath);

            if (!string.IsNullOrEmpty(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            string json = BankingProfileDiagnosticsExportV1Json.Serialize(artifact, indented: true);
            File.WriteAllText(resolvedOutputPath, json, Utf8NoBom);

            Console.WriteLine($"Generated BankingProfile diagnostics for sample '{SourceName}'.");
            Console.WriteLine(
                $"samples={artifact.SummaryMetrics.SampleCount}, " +
                $"rollDeg=[{artifact.SummaryMetrics.MinRollDegrees:0.###}, {artifact.SummaryMetrics.MaxRollDegrees:0.###}], " +
                $"maxAbsSlopeRadPerM={artifact.SummaryMetrics.MaxAbsoluteRollSlopeRadPerMeter:0.######}");
            Console.WriteLine($"Wrote BankingProfile diagnostics artifact to '{resolvedOutputPath}'.");

            return 0;
        }

        internal static BankingProfileDiagnosticsExportV1Dto BuildArtifact()
        {
            BankingProfile profile = BuildSampleProfile();
            double[] distances = BuildUniformDistances(ProfileLength, SampleCount);
            BankingProfileDiagnosticsReport report = BankingProfileDiagnostics.Sample(profile, distances);

            return BankingProfileDiagnosticsExportV1Mapper.Export(
                new BankingProfileDiagnosticsExportV1Source
                {
                    Units = "meters,radians",
                    SourceName = SourceName,
                    ProfileKeyCount = profile.Keys.Count,
                    Report = report
                });
        }

        private static BankingProfile BuildSampleProfile()
        {
            return new BankingProfile(new[]
            {
                new BankingProfileKey(
                    0.0,
                    0.0,
                    BankingProfileInterpolationMode.Constant),
                new BankingProfileKey(
                    20.0,
                    0.0,
                    BankingProfileInterpolationMode.Linear),
                new BankingProfileKey(
                    50.0,
                    30.0 * DegreesToRadians,
                    BankingProfileInterpolationMode.SmoothStep),
                new BankingProfileKey(
                    80.0,
                    -15.0 * DegreesToRadians,
                    BankingProfileInterpolationMode.Linear),
                new BankingProfileKey(
                    100.0,
                    20.0 * DegreesToRadians,
                    BankingProfileInterpolationMode.Constant)
            });
        }

        private static double[] BuildUniformDistances(double totalLength, int sampleCount)
        {
            if (sampleCount < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount), sampleCount, "Sample count must be at least two.");
            }

            var distances = new double[sampleCount];
            double interval = totalLength / (sampleCount - 1);

            for (int i = 0; i < sampleCount; i++)
            {
                distances[i] = i * interval;
            }

            distances[sampleCount - 1] = totalLength;
            return distances;
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
