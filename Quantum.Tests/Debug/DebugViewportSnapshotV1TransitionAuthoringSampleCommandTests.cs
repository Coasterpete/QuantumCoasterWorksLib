using System.Globalization;
using System.IO;
using Quantum.Debug;
using Quantum.IO.DebugViewport.V1;

namespace Quantum.Tests;

public sealed class DebugViewportSnapshotV1TransitionAuthoringSampleCommandTests
{
    [Fact]
    public void BuildSample_PreservesTransitionAuthoringMetadataAndCounts()
    {
        DebugViewportSnapshotV1Dto sample =
            DebugViewportSnapshotV1TransitionAuthoringSampleCommand.BuildSample();

        Assert.Equal(DebugViewportSnapshotV1Dto.ContractName, sample.Contract);
        Assert.Equal(DebugViewportSnapshotV1Dto.ContractVersion, sample.Version);
        Assert.Equal("meters", sample.Metadata.Units);
        Assert.Equal(TransitionAuthoringProofScenario.FixtureName, sample.Metadata.SourceFixtureName);
        Assert.Equal(DebugViewportSnapshotV1TransitionAuthoringSampleCommand.CenterlineSampleCount,
            sample.Metadata.SampleCount);
        Assert.Equal(DebugViewportSnapshotV1TransitionAuthoringSampleCommand.CenterlineSampleCount,
            sample.CenterlinePoints.Length);
        Assert.Equal(DebugViewportSnapshotV1TransitionAuthoringSampleCommand.CenterlineSampleCount,
            sample.Frames.Length);
        Assert.Equal(3, sample.Lines.Length);
        Assert.Equal(DebugViewportSnapshotV1TransitionAuthoringSampleCommand.TrainCarCount,
            sample.Boxes.Length);
        Assert.NotNull(sample.TrainPose);
        Assert.Equal(DebugViewportSnapshotV1TransitionAuthoringSampleCommand.TrainCarCount,
            sample.TrainPose!.Cars.Length);
    }

    [Fact]
    public void Run_OutputValidatesAndGeneratesSvg()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string snapshotPath = Path.Combine(
            tempDirectory,
            "DebugViewportSnapshotV1.transition-authoring.sample.json");
        string svgPath = Path.Combine(
            tempDirectory,
            "DebugViewportSnapshotV1.transition-authoring.sample.svg");
        var validationOutput = new StringWriter(CultureInfo.InvariantCulture);

        try
        {
            Assert.Equal(0, DebugViewportSnapshotV1TransitionAuthoringSampleCommand.Run(snapshotPath));
            Assert.True(File.Exists(snapshotPath));
            Assert.Equal(0, DebugViewportSnapshotV1ValidateCommand.Run(snapshotPath, validationOutput));
            Assert.Contains("sourceFixtureName: " + TransitionAuthoringProofScenario.FixtureName,
                validationOutput.ToString());
            Assert.Contains("validation: PASS", validationOutput.ToString());
            Assert.Equal(0, DebugViewportSnapshotV1SvgCommand.Run(snapshotPath, svgPath));
            Assert.True(File.Exists(svgPath));
            Assert.Contains("<svg", File.ReadAllText(svgPath));
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
        string firstOutputPath = Path.Combine(tempDirectory, "first.json");
        string secondOutputPath = Path.Combine(tempDirectory, "second.json");

        try
        {
            Assert.Equal(0, DebugViewportSnapshotV1TransitionAuthoringSampleCommand.Run(firstOutputPath));
            Assert.Equal(0, DebugViewportSnapshotV1TransitionAuthoringSampleCommand.Run(secondOutputPath));
            Assert.Equal(File.ReadAllText(firstOutputPath), File.ReadAllText(secondOutputPath));
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
            "QuantumCoasterWorks.DebugViewportSnapshotV1TransitionAuthoringSampleCommandTests",
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
