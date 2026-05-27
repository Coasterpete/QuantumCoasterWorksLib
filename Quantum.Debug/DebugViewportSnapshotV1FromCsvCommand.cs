using System;
using System.IO;
using System.Text;
using Quantum.IO.DebugViewport.V1;
using Quantum.IO.Fixtures.Csv;

namespace Quantum.Debug
{
    public static class DebugViewportSnapshotV1FromCsvCommand
    {
        private const string DefaultOutputExtension = ".debug-viewport-snapshot-v1.json";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public static int Run(string inputCsvPath, string? outputJsonPath = null)
        {
            if (string.IsNullOrWhiteSpace(inputCsvPath))
            {
                Console.WriteLine("inputCsvPath is required.");
                return 1;
            }

            string resolvedInputPath = Path.GetFullPath(inputCsvPath);
            string sourceFixtureName = ResolveSourceFixtureName(inputCsvPath);
            CenterlineFrameCsvFixture fixture = CenterlineFrameCsvFixtureParser.ParseFile(
                resolvedInputPath,
                sourceFixtureName);

            DebugViewportSnapshotV1Dto dto = BuildSnapshot(fixture);
            string json = DebugViewportSnapshotV1Json.Serialize(dto, indented: true);

            string resolvedOutputPath = ResolveOutputPath(resolvedInputPath, outputJsonPath);
            string? parentDirectory = Path.GetDirectoryName(resolvedOutputPath);

            if (!string.IsNullOrEmpty(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            File.WriteAllText(resolvedOutputPath, json, Utf8NoBom);
            Console.WriteLine($"Wrote DebugViewportSnapshotV1 CSV snapshot to '{resolvedOutputPath}'.");
            DebugViewportSnapshotPreviewIndex.TryWriteForGeneratedOutput(resolvedOutputPath, Console.Out);
            return 0;
        }

        public static DebugViewportSnapshotV1Dto BuildSnapshot(CenterlineFrameCsvFixture fixture)
        {
            if (fixture == null)
            {
                throw new ArgumentNullException(nameof(fixture));
            }

            var source = new DebugViewportSnapshotV1Source
            {
                Units = "meters",
                SourceFixtureName = fixture.SourceFixtureName,
                SampledFrames = fixture.Frames
            };

            return DebugViewportSnapshotV1Mapper.Export(source);
        }

        private static string ResolveOutputPath(string resolvedInputPath, string? outputJsonPath)
        {
            if (!string.IsNullOrWhiteSpace(outputJsonPath))
            {
                return Path.GetFullPath(outputJsonPath);
            }

            string inputDirectory = Path.GetDirectoryName(resolvedInputPath) ?? Environment.CurrentDirectory;
            string inputFileName = Path.GetFileNameWithoutExtension(resolvedInputPath);

            if (string.IsNullOrWhiteSpace(inputFileName))
            {
                inputFileName = "DebugViewportSnapshotV1";
            }

            return Path.GetFullPath(Path.Combine(inputDirectory, inputFileName + DefaultOutputExtension));
        }

        private static string ResolveSourceFixtureName(string inputCsvPath)
        {
            string fileName = Path.GetFileName(inputCsvPath);
            return string.IsNullOrWhiteSpace(fileName) ? inputCsvPath : fileName;
        }
    }
}
