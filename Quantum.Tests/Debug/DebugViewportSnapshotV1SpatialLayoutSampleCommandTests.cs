using System.Globalization;
using System.IO;
using Quantum.Debug;
using Quantum.IO.DebugViewport.V1;

namespace Quantum.Tests;

public sealed class DebugViewportSnapshotV1SpatialLayoutSampleCommandTests
{
    [Fact]
    public void BuildSample_PreservesSpatialLayoutMetadataAndCounts()
    {
        DebugViewportSnapshotV1Dto sample =
            DebugViewportSnapshotV1SpatialLayoutSampleCommand.BuildSample();

        Assert.Equal(DebugViewportSnapshotV1Dto.ContractName, sample.Contract);
        Assert.Equal(DebugViewportSnapshotV1Dto.ContractVersion, sample.Version);
        Assert.Equal("meters", sample.Metadata.Units);
        Assert.Equal(SpatialLayoutProofScenario.FixtureName, sample.Metadata.SourceFixtureName);
        Assert.Equal(DebugViewportSnapshotV1SpatialLayoutSampleCommand.CenterlineSampleCount,
            sample.Metadata.SampleCount);
        Assert.Equal(25, sample.CenterlinePoints.Length);
        Assert.Equal(25, sample.Frames.Length);
        Assert.Equal(3, sample.Lines.Length);
        Assert.Equal(9, sample.Boxes.Length);
        Assert.NotNull(sample.TrainPose);
        Assert.Equal(9, sample.TrainPose!.Cars.Length);
    }

    [Fact]
    public void Run_OutputIsByteIdenticalValidAndGeneratesSvgInTempDirectory()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string firstSnapshotPath = Path.Combine(tempDirectory, "first.json");
        string secondSnapshotPath = Path.Combine(tempDirectory, "second.json");
        string svgPath = Path.Combine(tempDirectory, "spatial-layout.svg");
        var validationOutput = new StringWriter(CultureInfo.InvariantCulture);

        try
        {
            Assert.Equal(0, DebugViewportSnapshotV1SpatialLayoutSampleCommand.Run(firstSnapshotPath));
            Assert.Equal(0, DebugViewportSnapshotV1SpatialLayoutSampleCommand.Run(secondSnapshotPath));
            Assert.Equal(File.ReadAllBytes(firstSnapshotPath), File.ReadAllBytes(secondSnapshotPath));
            Assert.Equal(0, DebugViewportSnapshotV1ValidateCommand.Run(firstSnapshotPath, validationOutput));
            Assert.Contains("sourceFixtureName: " + SpatialLayoutProofScenario.FixtureName,
                validationOutput.ToString());
            Assert.Contains("validation: PASS", validationOutput.ToString());
            Assert.Equal(0, DebugViewportSnapshotV1SvgCommand.Run(firstSnapshotPath, svgPath));
            Assert.True(File.Exists(svgPath));
            Assert.Contains("<svg", File.ReadAllText(svgPath));
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
            "QuantumCoasterWorks.DebugViewportSnapshotV1SpatialLayoutSampleCommandTests",
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
