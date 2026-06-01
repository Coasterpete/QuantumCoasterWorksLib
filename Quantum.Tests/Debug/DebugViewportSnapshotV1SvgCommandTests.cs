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
    public void Run_ValidSnapshot_WritesSvgWithRawSamplesAndSmoothPreview()
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
            Assert.Contains("top-down X/Z centerline preview", svg);
            Assert.Contains("elevation/profile preview", svg);
            Assert.Contains("raw samples / exported points", svg);
            Assert.Contains("raw sampled centerline", svg);
            Assert.Contains("smoothed visual preview only", svg);
            Assert.Contains("boxes: 1 | debug lines: 3 | nested TrainPoseExportV1: no | train cars: 0", svg);
            Assert.Contains("frame ticks / debug lines (3)", svg);
            Assert.Contains("train boxes (1), TrainPose: no", svg);
            Assert.Contains("class=\"centerline top-down-centerline raw-centerline\"", svg);
            Assert.Contains("class=\"centerline elevation-centerline raw-centerline\"", svg);
            Assert.Contains("class=\"smooth-preview top-down-smooth-preview\"", svg);
            Assert.Contains("class=\"smooth-preview elevation-smooth-preview\"", svg);
            Assert.Contains("class=\"raw-sample-point sample-point top-down-sample-point\"", svg);
            Assert.Contains("class=\"raw-sample-point sample-point elevation-sample-point\"", svg);
            Assert.Contains("class=\"debug-line debug-line-kind-frame-axis-tangent\"", svg);
            Assert.Contains("class=\"debug-line debug-line-kind-frame-axis-normal\"", svg);
            Assert.Contains("class=\"debug-line debug-line-kind-frame-axis-binormal\"", svg);
            Assert.Contains("class=\"train-box train-box-role-train-body\"", svg);
            Assert.Contains("class=\"train-box-forward\"", svg);
            Assert.Contains("class=\"train-box-label\"", svg);
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
            CreateFrame(distance: 5.0, x: 5.0, y: 1.0, z: 1.5),
            CreateFrame(distance: 10.0, x: 10.0, z: 0.0)
        };
        DebugLineSegment[] lines = TrackFrameDebugGizmoBuilder.BuildAxes(frames[1], axisLength: 2.0);
        var boxes = new[]
        {
            new DebugViewportBoxSource(
                role: DebugViewportSnapshotV1Vocabulary.TrainBodyRole,
                label: "car-0",
                frame: frames[1],
                length: 4.0,
                width: 1.6,
                height: 1.4)
        };

        return DebugViewportSnapshotV1Mapper.Export(new DebugViewportSnapshotV1Source
        {
            Units = "meters",
            SourceFixtureName = "svg-command-output",
            SampledFrames = frames,
            Lines = lines,
            Boxes = boxes
        });
    }

    private static ExportTrackFrame CreateFrame(double distance, double x, double z, double y = 0.0)
    {
        return new ExportTrackFrame(
            distance: distance,
            position: new Vector3d(x, y, z),
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
