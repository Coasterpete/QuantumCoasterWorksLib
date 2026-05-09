using System.IO;
using Quantum.Debug;
using Quantum.IO.TrainPose.V1;

namespace Quantum.Tests;

public sealed class TrainPoseExportV1CommandTests
{
    [Fact]
    public void Run_WithExplicitOutputPath_WritesJsonFile()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "sample.json");

        try
        {
            int exitCode = TrainPoseExportV1Command.Run(outputPath);

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
        string firstOutputPath = Path.Combine(tempDirectory, "sample.first.json");
        string secondOutputPath = Path.Combine(tempDirectory, "sample.second.json");

        try
        {
            int firstExitCode = TrainPoseExportV1Command.Run(firstOutputPath);
            int secondExitCode = TrainPoseExportV1Command.Run(secondOutputPath);

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
    public void Run_OutputJson_DeserializeAndValidateCleanly()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "sample.json");

        try
        {
            int exitCode = TrainPoseExportV1Command.Run(outputPath);

            Assert.Equal(0, exitCode);

            string json = File.ReadAllText(outputPath);
            TrainPoseExportV1Dto dto = TrainPoseExportV1Json.Deserialize(json);
            bool isValid = TrainPoseExportV1Validator.TryValidate(
                dto,
                out IReadOnlyList<TrainPoseExportV1ValidationDiagnostic> diagnostics);

            Assert.True(isValid);
            Assert.Empty(diagnostics);
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    private static string CreateTempDirectoryPath()
    {
        return Path.Combine(Path.GetTempPath(), "QuantumCoasterWorks.TrainPoseExportV1CommandTests", Guid.NewGuid().ToString("N"));
    }

    private static void DeleteDirectoryIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
