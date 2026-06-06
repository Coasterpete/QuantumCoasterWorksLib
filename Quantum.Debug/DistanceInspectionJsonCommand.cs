using System;
using System.IO;
using System.Text;
using Quantum.IO.DistanceInspection.V1;
using Quantum.Track;

namespace Quantum.Debug
{
    public static class DistanceInspectionJsonCommand
    {
        public const string CommandName = "distance-inspection-json";

        internal const string DefaultRelativeOutputPath =
            "artifacts/track/distance-inspection.sample.json";

        private const double SampleDistance = 12.5;

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public static int Run(string? outputPath = null)
        {
            DistanceInspectionSnapshotV1Dto dto = BuildSample();
            string json = DistanceInspectionSnapshotV1Json.Serialize(dto, indented: true);

            string resolvedOutputPath = ResolveOutputPath(outputPath);
            string? parentDirectory = Path.GetDirectoryName(resolvedOutputPath);

            if (!string.IsNullOrEmpty(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            File.WriteAllText(resolvedOutputPath, json, Utf8NoBom);
            Console.WriteLine($"Wrote distance inspection sample to '{resolvedOutputPath}'.");
            return 0;
        }

        public static DistanceInspectionSnapshotV1Dto BuildSample()
        {
            NormalizedSectionEvaluator evaluator = BuildSampleEvaluator();
            DistanceInspectionSnapshot snapshot = evaluator.InspectDistance(SampleDistance);
            return DistanceInspectionSnapshotV1Mapper.Export(snapshot);
        }

        private static NormalizedSectionEvaluator BuildSampleEvaluator()
        {
            SectionDefinition force = SectionNormalizer.Normalize(
                new ResolvedSectionInterval<ForceSection>(
                    new ForceSection(
                        length: 25.0,
                        interpolationMode: ForceInterpolationMode.Linear,
                        startNormalG: 1.0,
                        endNormalG: 1.8,
                        startLateralG: -0.2,
                        endLateralG: 0.2,
                        targetLongitudinalG: 0.05),
                    startDistance: 0.0,
                    endDistance: 25.0));

            SectionDefinition geometry = SectionNormalizer.Normalize(
                new ResolvedSectionInterval<GeometricSection>(
                    new GeometricSection(
                        length: 25.0,
                        curvature: 0.015,
                        roll: 0.18),
                    startDistance: 0.0,
                    endDistance: 25.0));

            return new NormalizedSectionEvaluator(new[] { force, geometry });
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
