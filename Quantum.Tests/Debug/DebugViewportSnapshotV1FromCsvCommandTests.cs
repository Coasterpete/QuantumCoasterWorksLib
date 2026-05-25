using System.IO;
using Quantum.Debug;
using Quantum.IO.DebugViewport.V1;

namespace Quantum.Tests;

public sealed class DebugViewportSnapshotV1FromCsvCommandTests
{
    private const string FixtureFileName = "Milestone5.synthetic.centerline_frames.csv";
    private const int FixtureSampleCount = 3;

    [Fact]
    public void Run_WithExplicitOutputPath_WritesJsonSnapshotFromCsvFixture()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "Milestone5.synthetic.snapshot.json");

        try
        {
            int exitCode = DebugViewportSnapshotV1FromCsvCommand.Run(GetFixturePath(), outputPath);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void Run_OutputJson_DeserializesAndPreservesContractMetadataAndCounts()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "Milestone5.synthetic.snapshot.json");

        try
        {
            int exitCode = DebugViewportSnapshotV1FromCsvCommand.Run(GetFixturePath(), outputPath);

            Assert.Equal(0, exitCode);

            string json = File.ReadAllText(outputPath);
            DebugViewportSnapshotV1Dto dto = DebugViewportSnapshotV1Json.Deserialize(json);

            Assert.Equal(DebugViewportSnapshotV1Dto.ContractName, dto.Contract);
            Assert.Equal(DebugViewportSnapshotV1Dto.ContractVersion, dto.Version);
            Assert.Equal("meters", dto.Metadata.Units);
            Assert.Equal(FixtureFileName, dto.Metadata.SourceFixtureName);
            Assert.Equal(FixtureSampleCount, dto.Metadata.SampleCount);

            Assert.Equal(FixtureSampleCount, dto.CenterlinePoints.Length);
            Assert.Equal(FixtureSampleCount, dto.Frames.Length);
            Assert.Empty(dto.Lines);
            Assert.Empty(dto.Boxes);
            Assert.Null(dto.TrainPose);

            Assert.Equal(0.0, dto.Frames[0].Distance);
            Assert.Equal(3.25, dto.Frames[1].Distance);
            Assert.Equal(8.75, dto.Frames[2].Distance);
            Assert.Equal(dto.Frames[2].Position.X, dto.CenterlinePoints[2].Position.X);
            Assert.Equal(dto.Frames[2].Position.Y, dto.CenterlinePoints[2].Position.Y);
            Assert.Equal(dto.Frames[2].Position.Z, dto.CenterlinePoints[2].Position.Z);
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void Run_WithExplicitOutputPath_IsDeterministicAcrossTwoRuns()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string firstOutputPath = Path.Combine(tempDirectory, "Milestone5.synthetic.first.json");
        string secondOutputPath = Path.Combine(tempDirectory, "Milestone5.synthetic.second.json");

        try
        {
            int firstExitCode = DebugViewportSnapshotV1FromCsvCommand.Run(GetFixturePath(), firstOutputPath);
            int secondExitCode = DebugViewportSnapshotV1FromCsvCommand.Run(GetFixturePath(), secondOutputPath);

            Assert.Equal(0, firstExitCode);
            Assert.Equal(0, secondExitCode);

            string firstJson = File.ReadAllText(firstOutputPath);
            string secondJson = File.ReadAllText(secondOutputPath);

            Assert.Equal(firstJson, secondJson);
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    private static string GetFixturePath()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "IO", "Fixtures", FixtureFileName);
        Assert.True(File.Exists(path), $"Synthetic CSV fixture file was not found at '{path}'.");
        return path;
    }

    private static string CreateTempDirectoryPath()
    {
        return Path.Combine(Path.GetTempPath(), "QuantumCoasterWorks.DebugViewportSnapshotV1FromCsvCommandTests", Guid.NewGuid().ToString("N"));
    }

    private static void DeleteDirectoryIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
