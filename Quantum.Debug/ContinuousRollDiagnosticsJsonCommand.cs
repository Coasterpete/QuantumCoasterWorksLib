using System;
using System.IO;
using System.Text;
using Quantum.IO.ContinuousRollDiagnostics.V1;

namespace Quantum.Debug
{
    public static class ContinuousRollDiagnosticsJsonCommand
    {
        public const string CommandName = "continuous-roll-diagnostics-json";

        internal const string DefaultRelativeOutputPath =
            "artifacts/banking-profile/continuous-roll-diagnostics.sample.json";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public static int Run(string? outputPath = null)
        {
            ContinuousRollDiagnosticsExportV1Dto artifact = BuildArtifact();
            string resolvedOutputPath = ResolveOutputPath(outputPath);
            string? parentDirectory = Path.GetDirectoryName(resolvedOutputPath);

            if (!string.IsNullOrEmpty(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            string json = ContinuousRollDiagnosticsExportV1Json.Serialize(artifact, indented: true);
            File.WriteAllText(resolvedOutputPath, json, Utf8NoBom);

            Console.WriteLine("Generated continuous roll diagnostics JSON artifact.");
            Console.WriteLine(
                $"samples={artifact.SampleCount}, " +
                $"warnings={artifact.WarningCount}, " +
                $"maxRollRateRadPerM={artifact.MaxRollRateRadiansPerMeter:0.######}, " +
                $"wrapHandlingEnabled={artifact.WrapHandlingEnabled}");
            Console.WriteLine($"Wrote continuous roll diagnostics JSON artifact to '{resolvedOutputPath}'.");

            return 0;
        }

        internal static ContinuousRollDiagnosticsExportV1Dto BuildArtifact()
        {
            return ContinuousRollDiagnosticsExportV1Mapper.Export(
                ContinuousRollDiagnosticsSampleCommand.BuildReport());
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
