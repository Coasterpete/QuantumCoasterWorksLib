using System.Globalization;
using System.IO;
using Quantum.Debug;
using Quantum.IO.DebugViewport.V1;
using Quantum.Math;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class DebugViewportSnapshotV1ValidateCommandTests
{
    [Fact]
    public void Run_ValidSnapshot_PrintsConciseSummary()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string snapshotPath = Path.Combine(tempDirectory, "DebugViewportSnapshotV1.validation.json");
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        try
        {
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllText(
                snapshotPath,
                DebugViewportSnapshotV1Json.Serialize(CreateSnapshot(), indented: true));

            int exitCode = DebugViewportSnapshotV1ValidateCommand.Run(snapshotPath, writer);

            Assert.Equal(0, exitCode);
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }

        string output = writer.ToString();

        Assert.Contains("DebugViewportSnapshotV1 validation summary", output);
        Assert.Contains("contract: quantum.debug_viewport_snapshot", output);
        Assert.Contains("version: 1", output);
        Assert.Contains("units: meters", output);
        Assert.Contains("sourceFixtureName: command-output", output);
        Assert.Contains("centerlineCount: 2", output);
        Assert.Contains("frameCount: 2", output);
        Assert.Contains("lineCount: 3", output);
        Assert.Contains("boxCount: 1", output);
        Assert.Contains("trainPose: absent", output);
        Assert.Contains("validation: PASS", output);
    }

    private static DebugViewportSnapshotV1Dto CreateSnapshot()
    {
        ExportTrackFrame[] frames =
        {
            CreateFrame(distance: 0.0, x: 0.0),
            CreateFrame(distance: 5.0, x: 5.0)
        };
        DebugLineSegment[] lines = TrackFrameDebugGizmoBuilder.BuildAxes(frames[0], axisLength: 2.0);
        var boxes = new[]
        {
            new DebugViewportBoxSource(
                role: "train.body",
                label: "car-0",
                frame: frames[1],
                length: 4.5,
                width: 1.8,
                height: 2.1)
        };

        return DebugViewportSnapshotV1Mapper.Export(new DebugViewportSnapshotV1Source
        {
            Units = "meters",
            SourceFixtureName = "command-output",
            SampledFrames = frames,
            Lines = lines,
            Boxes = boxes
        });
    }

    private static ExportTrackFrame CreateFrame(double distance, double x)
    {
        return new ExportTrackFrame(
            distance: distance,
            position: new Vector3d(x, 0.0, 0.0),
            tangent: Vector3d.UnitX,
            normal: Vector3d.UnitY,
            binormal: Vector3d.UnitZ);
    }

    private static string CreateTempDirectoryPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "QuantumCoasterWorks.DebugViewportSnapshotV1ValidateCommandTests",
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
