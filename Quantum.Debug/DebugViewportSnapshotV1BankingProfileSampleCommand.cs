using System;
using System.IO;
using System.Text;
using Quantum.IO.DebugViewport.V1;

namespace Quantum.Debug
{
    public static class DebugViewportSnapshotV1BankingProfileSampleCommand
    {
        public const int CenterlineSampleCount = 10;
        public const int TrainCarCount = 3;

        public const string CommandName = "debug-viewport-snapshot-v1-banking-profile";

        internal const string DefaultRelativeOutputPath =
            "artifacts/debug-viewport/DebugViewportSnapshotV1.banking-profile.sample.json";

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
            Console.WriteLine($"Wrote BankingProfile DebugViewportSnapshotV1 sample to '{resolvedOutputPath}'.");
            DebugViewportSnapshotPreviewIndex.TryWriteForGeneratedOutput(resolvedOutputPath, Console.Out);
            return 0;
        }

        public static DebugViewportSnapshotV1Dto BuildSample()
        {
            BankingProfileTrainPoseFixture fixture =
                BankingProfileTrainPoseFixtures.ProfileBackedTrainPose();
            return fixture.BuildDebugViewportSnapshot();
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
