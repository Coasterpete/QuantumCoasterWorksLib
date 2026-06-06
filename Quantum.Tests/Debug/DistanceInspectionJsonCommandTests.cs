using System.IO;
using Quantum.Debug;
using Quantum.IO.DistanceInspection.V1;

namespace Quantum.Tests;

public sealed class DistanceInspectionJsonCommandTests
{
    [Fact]
    public void Run_WithExplicitOutputPath_WritesDeserializableJsonWithExpectedContract()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "distance-inspection.sample.json");

        try
        {
            int exitCode = DistanceInspectionJsonCommand.Run(outputPath);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));

            DistanceInspectionSnapshotV1Dto dto =
                DistanceInspectionSnapshotV1Json.Deserialize(File.ReadAllText(outputPath));

            Assert.Equal(DistanceInspectionSnapshotV1Dto.ContractName, dto.Contract);
            Assert.Equal(DistanceInspectionSnapshotV1Dto.ContractVersion, dto.Version);
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void Run_WithDefaultOutputPath_WritesJsonFileUnderArtifactsTrack()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string originalCurrentDirectory = Environment.CurrentDirectory;
        string outputPath = Path.Combine(
            tempDirectory,
            "artifacts",
            "track",
            "distance-inspection.sample.json");

        try
        {
            Directory.CreateDirectory(tempDirectory);
            Environment.CurrentDirectory = tempDirectory;

            int exitCode = DistanceInspectionJsonCommand.Run();

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));
            _ = DistanceInspectionSnapshotV1Json.Deserialize(File.ReadAllText(outputPath));
        }
        finally
        {
            Environment.CurrentDirectory = originalCurrentDirectory;
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void Run_WithExplicitOutputPath_IsDeterministicAcrossTwoRuns()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string firstOutputPath = Path.Combine(tempDirectory, "distance-inspection.first.json");
        string secondOutputPath = Path.Combine(tempDirectory, "distance-inspection.second.json");

        try
        {
            int firstExitCode = DistanceInspectionJsonCommand.Run(firstOutputPath);
            int secondExitCode = DistanceInspectionJsonCommand.Run(secondOutputPath);

            Assert.Equal(0, firstExitCode);
            Assert.Equal(0, secondExitCode);

            Assert.Equal(File.ReadAllText(firstOutputPath), File.ReadAllText(secondOutputPath));
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void Run_OutputJson_IncludesOrderedSectionsAndChannelValues()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "distance-inspection.sample.json");

        try
        {
            int exitCode = DistanceInspectionJsonCommand.Run(outputPath);

            Assert.Equal(0, exitCode);

            DistanceInspectionSnapshotV1Dto dto =
                DistanceInspectionSnapshotV1Json.Deserialize(File.ReadAllText(outputPath));

            Assert.Equal(12.5, dto.Distance);
            Assert.Equal(2, dto.Sections.Length);

            Assert.Equal("Force", dto.Sections[0].Kind);
            Assert.Equal("Distance", dto.Sections[0].Domain);
            Assert.Equal(new[] { "NormalG", "LateralG", "LongitudinalG" }, dto.Sections[0].Channels);
            Assert.Equal(
                new[] { "NormalG", "LateralG", "LongitudinalG" },
                dto.Sections[0].ChannelValues.Select(value => value.Channel).ToArray());
            Assert.Equal(1.4, dto.Sections[0].ChannelValues[0].Value, 10);
            Assert.Equal(0.0, dto.Sections[0].ChannelValues[1].Value, 10);
            Assert.Equal(0.05, dto.Sections[0].ChannelValues[2].Value, 10);

            Assert.Equal("Geometry", dto.Sections[1].Kind);
            Assert.Equal("Distance", dto.Sections[1].Domain);
            Assert.Equal(new[] { "Curvature", "Roll" }, dto.Sections[1].Channels);
            Assert.Equal(
                new[] { "Curvature", "Roll" },
                dto.Sections[1].ChannelValues.Select(value => value.Channel).ToArray());
            Assert.Equal(0.015, dto.Sections[1].ChannelValues[0].Value, 10);
            Assert.Equal(0.18, dto.Sections[1].ChannelValues[1].Value, 10);
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
            "QuantumCoasterWorks.DistanceInspectionJsonCommandTests",
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
