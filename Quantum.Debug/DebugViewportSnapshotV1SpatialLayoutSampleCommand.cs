using System;
using System.IO;
using System.Text;
using Quantum.IO.DebugViewport.V1;

namespace Quantum.Debug
{
    public static class DebugViewportSnapshotV1SpatialLayoutSampleCommand
    {
        public const string CommandName = "debug-viewport-snapshot-v1-spatial-layout";
        public const int CenterlineSampleCount = SpatialLayoutProofScenario.FrameCount;
        public const int TrainCarCount = SpatialLayoutProofScenario.TrainCarCount;

        internal const string DefaultRelativeOutputPath =
            "artifacts/debug-viewport/DebugViewportSnapshotV1.spatial-layout.sample.json";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public static int Run(string? outputPath = null)
        {
            DebugViewportSnapshotV1Dto dto = BuildSample();
            string json = DebugViewportSnapshotV1Json.Serialize(dto, indented: true);

            string resolvedOutputPath = ResolveOutputPath(outputPath);
            string? parentDirectory = Path.GetDirectoryName(resolvedOutputPath);

            if (!string.IsNullOrEmpty(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            File.WriteAllText(resolvedOutputPath, json, Utf8NoBom);
            Console.WriteLine($"Wrote spatial-layout DebugViewportSnapshotV1 sample to '{resolvedOutputPath}'.");
            DebugViewportSnapshotPreviewIndex.TryWriteForGeneratedOutput(resolvedOutputPath, Console.Out);
            return 0;
        }

        public static DebugViewportSnapshotV1Dto BuildSample()
        {
            return SpatialLayoutProofScenario.CreateDeterministic().Snapshot;
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
