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

            Console.WriteLine($"Generated BankingProfile diagnostics for sample '{artifact.Metadata.SourceName}'.");
            Console.WriteLine(
                $"samples={artifact.SummaryMetrics.SampleCount}, " +
                $"rollDeg=[{artifact.SummaryMetrics.MinRollDegrees:0.###}, {artifact.SummaryMetrics.MaxRollDegrees:0.###}], " +
                $"maxAbsSlopeRadPerM={artifact.SummaryMetrics.MaxAbsoluteRollSlopeRadPerMeter:0.######}");
            Console.WriteLine($"Wrote BankingProfile diagnostics artifact to '{resolvedOutputPath}'.");

            return 0;
        }

        internal static BankingProfileDiagnosticsExportV1Dto BuildArtifact()
        {
            BankingProfileFixture fixture = BankingProfileFixtures.DefaultDiagnostics();
            BankingProfileDiagnosticsReport report = BankingProfileDiagnostics.Sample(
                fixture.Profile,
                fixture.SampleDistances);

            return BankingProfileDiagnosticsExportV1Mapper.Export(
                new BankingProfileDiagnosticsExportV1Source
                {
                    Units = "meters,radians",
                    SourceName = fixture.Name,
                    ProfileKeyCount = fixture.Profile.Keys.Count,
                    Report = report
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
