using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Quantum.Track;

namespace Quantum.Debug
{
    public enum LongitudinalForcePreviewPreset
    {
        Soft,
        Balanced,
        Punchy
    }

    internal readonly record struct LongitudinalForcePreviewSamplePoint(
        double Distance,
        double NormalizedSectionT,
        double? TargetLongitudinalG);

    public static class LongitudinalForcePreviewCommand
    {
        internal const string DefaultRelativeOutputPath = "artifacts/force-target/longitudinal-force-preview.sample.json";
        private const int SamplesPerSection = 8;

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public static int Run(
            string? outputPath = null,
            LongitudinalForcePreviewPreset preset = LongitudinalForcePreviewPreset.Balanced)
        {
            LongitudinalForcePreviewSamplePoint[] samplePoints = BuildSamplePoints(preset);
            LongitudinalForcePreviewRow[] rows = BuildRows(samplePoints);

            var artifact = new LongitudinalForcePreviewArtifact(rows);
            string resolvedOutputPath = ResolveOutputPath(outputPath);
            string? parentDirectory = Path.GetDirectoryName(resolvedOutputPath);

            if (!string.IsNullOrEmpty(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            string json = JsonSerializer.Serialize(artifact, JsonOptions);
            File.WriteAllText(resolvedOutputPath, json, Utf8NoBom);

            Console.WriteLine($"Generated longitudinal force preview samples (preset: {FormatPresetName(preset)}).");
            Console.WriteLine($"Wrote longitudinal force preview artifact to '{resolvedOutputPath}'.");

            return 0;
        }

        internal static LongitudinalForcePreviewSamplePoint[] BuildSamplePoints(
            LongitudinalForcePreviewPreset preset)
        {
            IReadOnlyList<ResolvedSectionInterval<ForceSection>> resolvedIntervals =
                ForceTargetResolver.Resolve(BuildDemoSections(preset));
            return BuildSamplePoints(resolvedIntervals);
        }

        public static bool TryParsePreset(
            string value,
            out LongitudinalForcePreviewPreset preset)
        {
            preset = LongitudinalForcePreviewPreset.Balanced;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (string.Equals(value, "soft", StringComparison.OrdinalIgnoreCase))
            {
                preset = LongitudinalForcePreviewPreset.Soft;
                return true;
            }

            if (string.Equals(value, "balanced", StringComparison.OrdinalIgnoreCase))
            {
                preset = LongitudinalForcePreviewPreset.Balanced;
                return true;
            }

            if (string.Equals(value, "punchy", StringComparison.OrdinalIgnoreCase))
            {
                preset = LongitudinalForcePreviewPreset.Punchy;
                return true;
            }

            return false;
        }

        private static string ResolveOutputPath(string? outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, DefaultRelativeOutputPath));
            }

            return Path.GetFullPath(outputPath);
        }

        private static IEnumerable<(ForceSection Section, double Length)> BuildDemoSections(
            LongitudinalForcePreviewPreset preset)
        {
            switch (preset)
            {
                case LongitudinalForcePreviewPreset.Soft:
                    return new (ForceSection Section, double Length)[]
                    {
                        (
                            new ForceSection(
                                length: 40.0,
                                interpolationMode: ForceInterpolationMode.SmoothStep,
                                startLongitudinalG: -0.25,
                                endLongitudinalG: 0.70),
                            40.0),
                        (
                            new ForceSection(
                                length: 30.0,
                                interpolationMode: ForceInterpolationMode.SmoothStep,
                                startLongitudinalG: 0.70,
                                endLongitudinalG: -0.05),
                            30.0)
                    };

                case LongitudinalForcePreviewPreset.Balanced:
                    return new (ForceSection Section, double Length)[]
                    {
                        (
                            new ForceSection(
                                length: 40.0,
                                interpolationMode: ForceInterpolationMode.SmoothStep,
                                startLongitudinalG: -0.35,
                                endLongitudinalG: 0.85),
                            40.0),
                        (
                            new ForceSection(
                                length: 30.0,
                                interpolationMode: ForceInterpolationMode.Quintic,
                                startLongitudinalG: 0.85,
                                endLongitudinalG: -0.10),
                            30.0)
                    };

                case LongitudinalForcePreviewPreset.Punchy:
                    return new (ForceSection Section, double Length)[]
                    {
                        (
                            new ForceSection(
                                length: 40.0,
                                interpolationMode: ForceInterpolationMode.Linear,
                                startLongitudinalG: -0.45,
                                endLongitudinalG: 1.25),
                            40.0),
                        (
                            new ForceSection(
                                length: 30.0,
                                interpolationMode: ForceInterpolationMode.Linear,
                                startLongitudinalG: 1.25,
                                endLongitudinalG: -0.25),
                            30.0)
                    };

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(preset),
                        preset,
                        "Unsupported longitudinal force preview preset.");
            }
        }

        internal static string FormatPresetName(LongitudinalForcePreviewPreset preset)
        {
            switch (preset)
            {
                case LongitudinalForcePreviewPreset.Soft:
                    return "soft";
                case LongitudinalForcePreviewPreset.Balanced:
                    return "balanced";
                case LongitudinalForcePreviewPreset.Punchy:
                    return "punchy";
                default:
                    return preset.ToString();
            }
        }

        private static LongitudinalForcePreviewSamplePoint[] BuildSamplePoints(
            IReadOnlyList<ResolvedSectionInterval<ForceSection>> resolvedIntervals)
        {
            var rows = new List<LongitudinalForcePreviewSamplePoint>(resolvedIntervals.Count * SamplesPerSection + 1);

            for (int intervalIndex = 0; intervalIndex < resolvedIntervals.Count; intervalIndex++)
            {
                ResolvedSectionInterval<ForceSection> interval = resolvedIntervals[intervalIndex];

                for (int sampleIndex = 0; sampleIndex <= SamplesPerSection; sampleIndex++)
                {
                    if (intervalIndex > 0 && sampleIndex == 0)
                    {
                        continue;
                    }

                    double normalizedSectionT = sampleIndex / (double)SamplesPerSection;
                    double distance = interval.StartDistance + (interval.Length * normalizedSectionT);
                    SampledForceTarget sample = ForceTargetSampler.Sample(resolvedIntervals, distance);

                    rows.Add(
                        new LongitudinalForcePreviewSamplePoint(
                            distance,
                            sample.NormalizedT,
                            sample.TargetLongitudinalG));
                }
            }

            return rows.ToArray();
        }

        private static LongitudinalForcePreviewRow[] BuildRows(LongitudinalForcePreviewSamplePoint[] samplePoints)
        {
            var rows = new LongitudinalForcePreviewRow[samplePoints.Length];

            for (int i = 0; i < samplePoints.Length; i++)
            {
                LongitudinalForcePreviewSamplePoint samplePoint = samplePoints[i];
                rows[i] = new LongitudinalForcePreviewRow
                {
                    Distance = samplePoint.Distance,
                    NormalizedSectionT = samplePoint.NormalizedSectionT,
                    TargetLongitudinalG = samplePoint.TargetLongitudinalG
                };
            }

            return rows;
        }

        private sealed class LongitudinalForcePreviewArtifact
        {
            public LongitudinalForcePreviewArtifact(LongitudinalForcePreviewRow[] samples)
            {
                Samples = samples;
            }

            public string Kind => "longitudinal-force-preview";

            public int SampleCount => Samples.Length;

            public LongitudinalForcePreviewRow[] Samples { get; }
        }

        private sealed class LongitudinalForcePreviewRow
        {
            public double Distance { get; init; }

            public double NormalizedSectionT { get; init; }

            public double? TargetLongitudinalG { get; init; }
        }
    }
}
