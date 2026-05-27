using System.Globalization;
using System.IO;
using Quantum.Debug;
using Quantum.IO.DebugViewport.V1;
using Quantum.Math;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class DebugViewportSnapshotV1SvgCommandTests
{
    [Fact]
    public void Run_ValidSnapshot_WritesSvgWithCenterlinePolyline()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string snapshotPath = Path.Combine(tempDirectory, "DebugViewportSnapshotV1.validation.json");
        string svgPath = Path.Combine(tempDirectory, "DebugViewportSnapshotV1.validation.svg");

        try
        {
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllText(
                snapshotPath,
                DebugViewportSnapshotV1Json.Serialize(CreateSnapshot(), indented: true));

            int exitCode = DebugViewportSnapshotV1SvgCommand.Run(snapshotPath, svgPath);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(svgPath));

            string svg = File.ReadAllText(svgPath);
            Assert.Contains("<svg", svg);
            Assert.Contains("<polyline", svg);
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void Run_MissingSnapshotPath_FailsCleanly()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string snapshotPath = Path.Combine(tempDirectory, "missing.json");
        string svgPath = Path.Combine(tempDirectory, "missing.svg");
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        try
        {
            int exitCode = DebugViewportSnapshotV1SvgCommand.Run(snapshotPath, svgPath, writer);

            Assert.Equal(1, exitCode);
            Assert.False(File.Exists(svgPath));
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }

        Assert.Contains("Failed to read DebugViewportSnapshotV1 JSON.", writer.ToString());
    }

    [Fact]
    public void Run_InvalidJson_FailsCleanly()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string snapshotPath = Path.Combine(tempDirectory, "invalid.json");
        string svgPath = Path.Combine(tempDirectory, "invalid.svg");
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        try
        {
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllText(snapshotPath, "{ invalid json");

            int exitCode = DebugViewportSnapshotV1SvgCommand.Run(snapshotPath, svgPath, writer);

            Assert.Equal(1, exitCode);
            Assert.False(File.Exists(svgPath));
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }

        Assert.Contains("Failed to read DebugViewportSnapshotV1 JSON.", writer.ToString());
    }

    private static DebugViewportSnapshotV1Dto CreateSnapshot()
    {
        ExportTrackFrame[] frames =
        {
            CreateFrame(distance: 0.0, x: 0.0, z: 0.0),
            CreateFrame(distance: 5.0, x: 5.0, z: 1.5),
            CreateFrame(distance: 10.0, x: 10.0, z: 0.0)
        };
        DebugLineSegment[] lines = TrackFrameDebugGizmoBuilder.BuildAxes(frames[1], axisLength: 2.0);

        return DebugViewportSnapshotV1Mapper.Export(new DebugViewportSnapshotV1Source
        {
            Units = "meters",
            SourceFixtureName = "svg-command-output",
            SampledFrames = frames,
            Lines = lines
        });
    }

    private static ExportTrackFrame CreateFrame(double distance, double x, double z)
    {
        return new ExportTrackFrame(
            distance: distance,
            position: new Vector3d(x, 0.0, z),
            tangent: Vector3d.UnitX,
            normal: Vector3d.UnitY,
            binormal: Vector3d.UnitZ);
    }

    private static string CreateTempDirectoryPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "QuantumCoasterWorks.DebugViewportSnapshotV1SvgCommandTests",
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
