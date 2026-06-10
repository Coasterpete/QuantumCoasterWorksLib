using System;
using System.IO;
using System.Text;
using Quantum.IO.DebugViewport.V1;

namespace Quantum.Debug
{
    public static class DebugViewportSnapshotV1SampleCommand
    {
        public const int CenterlineSampleCount = AuthoringPipelineProofScenario.FrameCount;
        public const int TrainCarCount = AuthoringPipelineProofScenario.TrainCarCount;

        internal const string DefaultRelativeOutputPath = "artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json";

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
            Console.WriteLine($"Wrote DebugViewportSnapshotV1 sample to '{resolvedOutputPath}'.");
            DebugViewportSnapshotPreviewIndex.TryWriteForGeneratedOutput(resolvedOutputPath, Console.Out);
            return 0;
        }

        public static DebugViewportSnapshotV1Dto BuildSample()
        {
            return AuthoringPipelineProofScenario.CreateDeterministic().Snapshot;
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
