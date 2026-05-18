using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Quantum.Debug
{
    public static class LongitudinalSpeedPreviewCommand
    {
        internal const string DefaultRelativeOutputPath = "artifacts/speed-preview/longitudinal-speed-preview.sample.json";

        private const double GravityMps2 = 9.80665;
        private const double MpsToMph = 2.2369362920544;

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public static int Run(
            string? outputPath = null,
            LongitudinalForcePreviewPreset preset = LongitudinalForcePreviewPreset.Balanced,
            double initialSpeedMps = 0.0)
        {
            if (initialSpeedMps < 0.0 || double.IsNaN(initialSpeedMps) || double.IsInfinity(initialSpeedMps))
            {
                Console.WriteLine("initialSpeedMps must be a finite value greater than or equal to 0.");
                return 1;
            }

            LongitudinalForcePreviewSamplePoint[] profileSamples = LongitudinalForcePreviewCommand.BuildSamplePoints(preset);
            LongitudinalSpeedPreviewRow[] rows = BuildRows(profileSamples, initialSpeedMps);

            var artifact = new LongitudinalSpeedPreviewArtifact(rows);
            string resolvedOutputPath = ResolveOutputPath(outputPath);
            string? parentDirectory = Path.GetDirectoryName(resolvedOutputPath);

            if (!string.IsNullOrEmpty(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            string json = JsonSerializer.Serialize(artifact, JsonOptions);
            File.WriteAllText(resolvedOutputPath, json, Utf8NoBom);

            Console.WriteLine(
                $"Generated longitudinal speed preview samples (preset: {LongitudinalForcePreviewCommand.FormatPresetName(preset)}, initialSpeedMps: {initialSpeedMps:0.###}).");
            Console.WriteLine($"Wrote longitudinal speed preview artifact to '{resolvedOutputPath}'.");

            return 0;
        }

        private static string ResolveOutputPath(string? outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, DefaultRelativeOutputPath));
            }

            return Path.GetFullPath(outputPath);
        }

        private static LongitudinalSpeedPreviewRow[] BuildRows(
            LongitudinalForcePreviewSamplePoint[] profileSamples,
            double initialSpeedMps)
        {
            if (profileSamples.Length == 0)
            {
                return Array.Empty<LongitudinalSpeedPreviewRow>();
            }

            var rows = new LongitudinalSpeedPreviewRow[profileSamples.Length];
            double speedMps = initialSpeedMps;

            for (int sampleIndex = 0; sampleIndex < profileSamples.Length; sampleIndex++)
            {
                LongitudinalForcePreviewSamplePoint sample = profileSamples[sampleIndex];

                if (sampleIndex > 0)
                {
                    LongitudinalForcePreviewSamplePoint previous = profileSamples[sampleIndex - 1];
                    double deltaDistance = sample.Distance - previous.Distance;

                    if (deltaDistance > 0.0)
                    {
                        double previousG = previous.TargetLongitudinalG ?? 0.0;
                        double currentG = sample.TargetLongitudinalG ?? 0.0;
                        double averageLongitudinalG = (previousG + currentG) * 0.5;
                        double accelerationMps2 = averageLongitudinalG * GravityMps2;
                        double nextSpeedSquared = (speedMps * speedMps) + (2.0 * accelerationMps2 * deltaDistance);

                        speedMps = nextSpeedSquared <= 0.0 ? 0.0 : System.Math.Sqrt(nextSpeedSquared);
                    }
                }

                rows[sampleIndex] = new LongitudinalSpeedPreviewRow
                {
                    Distance = sample.Distance,
                    TargetLongitudinalG = sample.TargetLongitudinalG,
                    EstimatedSpeedMps = speedMps,
                    EstimatedSpeedMph = speedMps * MpsToMph
                };
            }

            return rows;
        }

        private sealed class LongitudinalSpeedPreviewArtifact
        {
            public LongitudinalSpeedPreviewArtifact(LongitudinalSpeedPreviewRow[] samples)
            {
                Samples = samples;
            }

            public string Kind => "longitudinal-speed-preview";

            public int SampleCount => Samples.Length;

            public LongitudinalSpeedPreviewRow[] Samples { get; }
        }

        private sealed class LongitudinalSpeedPreviewRow
        {
            public double Distance { get; init; }

            public double? TargetLongitudinalG { get; init; }

            public double EstimatedSpeedMps { get; init; }

            public double EstimatedSpeedMph { get; init; }
        }
    }
}
