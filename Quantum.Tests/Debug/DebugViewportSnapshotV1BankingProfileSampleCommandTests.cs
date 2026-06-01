using System.IO;
using Quantum.Debug;
using Quantum.IO.DebugViewport.V1;
using Quantum.IO.TrainPose.V1;

namespace Quantum.Tests;

public sealed class DebugViewportSnapshotV1BankingProfileSampleCommandTests
{
    [Fact]
    public void BuildSample_SerializeDeserialize_PreservesProfileBackedTrainPose()
    {
        DebugViewportSnapshotV1Dto expected = DebugViewportSnapshotV1BankingProfileSampleCommand.BuildSample();
        string json = DebugViewportSnapshotV1Json.Serialize(expected, indented: true);

        DebugViewportSnapshotV1Dto actual = DebugViewportSnapshotV1Json.Deserialize(json);

        Assert.Equal(DebugViewportSnapshotV1Dto.ContractName, actual.Contract);
        Assert.Equal(DebugViewportSnapshotV1Dto.ContractVersion, actual.Version);
        Assert.Equal("meters", actual.Metadata.Units);
        Assert.Equal(BankingProfileTrainPoseFixtures.ProfileBackedTrainPoseName, actual.Metadata.SourceFixtureName);
        Assert.Equal(DebugViewportSnapshotV1BankingProfileSampleCommand.CenterlineSampleCount, actual.Metadata.SampleCount);
        Assert.Equal(DebugViewportSnapshotV1BankingProfileSampleCommand.CenterlineSampleCount, actual.CenterlinePoints.Length);
        Assert.Equal(DebugViewportSnapshotV1BankingProfileSampleCommand.CenterlineSampleCount, actual.Frames.Length);
        Assert.Equal(DebugViewportSnapshotV1BankingProfileSampleCommand.TrainCarCount, actual.Boxes.Length);

        Assert.NotNull(actual.TrainPose);
        Assert.Equal(TrainPoseExportV1Dto.ContractName, actual.TrainPose!.Contract);
        Assert.Equal(TrainPoseExportV1Dto.ContractVersion, actual.TrainPose.Version);
        Assert.Equal(DebugViewportSnapshotV1BankingProfileSampleCommand.TrainCarCount, actual.TrainPose.Cars.Length);
    }

    [Fact]
    public void Run_WithExplicitOutputPath_WritesValidJsonFile()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "DebugViewportSnapshotV1.banking-profile.sample.json");

        try
        {
            int exitCode = DebugViewportSnapshotV1BankingProfileSampleCommand.Run(outputPath);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));

            DebugViewportSnapshotV1Dto dto = DebugViewportSnapshotV1Json.Deserialize(File.ReadAllText(outputPath));
            bool isValid = DebugViewportSnapshotV1Validator.TryValidate(
                dto,
                out IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics);

            Assert.True(isValid, string.Join(Environment.NewLine, diagnostics.Select(d => $"{d.Code} {d.Path}")));
            Assert.Empty(diagnostics);
            Assert.NotNull(dto.TrainPose);
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
            int firstExitCode = DebugViewportSnapshotV1BankingProfileSampleCommand.Run(firstOutputPath);
            int secondExitCode = DebugViewportSnapshotV1BankingProfileSampleCommand.Run(secondOutputPath);

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

    private static string CreateTempDirectoryPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "QuantumCoasterWorks.DebugViewportSnapshotV1BankingProfileSampleCommandTests",
            Guid.NewGuid().ToString("N"));
    }

    private static void DeleteDirectoryIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
