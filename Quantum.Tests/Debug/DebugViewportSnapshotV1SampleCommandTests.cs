using System.IO;
using Quantum.Debug;
using Quantum.IO.DebugViewport.V1;
using Quantum.IO.TrainPose.V1;

namespace Quantum.Tests;

public sealed class DebugViewportSnapshotV1SampleCommandTests
{
    [Fact]
    public void BuildSample_SerializeDeserialize_PreservesContractMetadataAndCounts()
    {
        DebugViewportSnapshotV1Dto expected = DebugViewportSnapshotV1SampleCommand.BuildSample();
        string json = DebugViewportSnapshotV1Json.Serialize(expected, indented: true);

        DebugViewportSnapshotV1Dto actual = DebugViewportSnapshotV1Json.Deserialize(json);

        Assert.Equal(DebugViewportSnapshotV1Dto.ContractName, actual.Contract);
        Assert.Equal(DebugViewportSnapshotV1Dto.ContractVersion, actual.Version);
        Assert.Equal("meters", actual.Metadata.Units);
        Assert.Equal("sampling-perf-smoke", actual.Metadata.SourceFixtureName);
        Assert.Equal(DebugViewportSnapshotV1SampleCommand.CenterlineSampleCount, actual.Metadata.SampleCount);

        Assert.Equal(DebugViewportSnapshotV1SampleCommand.CenterlineSampleCount, actual.CenterlinePoints.Length);
        Assert.Equal(DebugViewportSnapshotV1SampleCommand.CenterlineSampleCount, actual.Frames.Length);
        Assert.Equal(3, actual.Lines.Length);
        Assert.Equal(DebugViewportSnapshotV1SampleCommand.TrainCarCount, actual.Boxes.Length);

        Assert.NotNull(actual.TrainPose);
        Assert.Equal(TrainPoseExportV1Dto.ContractName, actual.TrainPose!.Contract);
        Assert.Equal(TrainPoseExportV1Dto.ContractVersion, actual.TrainPose.Version);
        Assert.Equal(DebugViewportSnapshotV1SampleCommand.TrainCarCount, actual.TrainPose.Cars.Length);
    }

    [Fact]
    public void Run_WithExplicitOutputPath_WritesJsonFile()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "DebugViewportSnapshotV1.sample.json");

        try
        {
            int exitCode = DebugViewportSnapshotV1SampleCommand.Run(outputPath);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));
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
        string firstOutputPath = Path.Combine(tempDirectory, "DebugViewportSnapshotV1.first.json");
        string secondOutputPath = Path.Combine(tempDirectory, "DebugViewportSnapshotV1.second.json");

        try
        {
            int firstExitCode = DebugViewportSnapshotV1SampleCommand.Run(firstOutputPath);
            int secondExitCode = DebugViewportSnapshotV1SampleCommand.Run(secondOutputPath);

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

    [Fact]
    public void Run_OutputJson_DeserializesAndPreservesSnapshotIdentity()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "DebugViewportSnapshotV1.sample.json");

        try
        {
            int exitCode = DebugViewportSnapshotV1SampleCommand.Run(outputPath);

            Assert.Equal(0, exitCode);

            string json = File.ReadAllText(outputPath);
            DebugViewportSnapshotV1Dto dto = DebugViewportSnapshotV1Json.Deserialize(json);

            Assert.Equal(DebugViewportSnapshotV1Dto.ContractName, dto.Contract);
            Assert.Equal(DebugViewportSnapshotV1Dto.ContractVersion, dto.Version);
            Assert.Equal(dto.Metadata.SampleCount, dto.CenterlinePoints.Length);
            Assert.Equal(dto.CenterlinePoints.Length, dto.Frames.Length);
            Assert.Equal(DebugViewportSnapshotV1SampleCommand.TrainCarCount, dto.Boxes.Length);
            Assert.NotNull(dto.TrainPose);
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    private static string CreateTempDirectoryPath()
    {
        return Path.Combine(Path.GetTempPath(), "QuantumCoasterWorks.DebugViewportSnapshotV1SampleCommandTests", Guid.NewGuid().ToString("N"));
    }

    private static void DeleteDirectoryIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
