using System.Globalization;
using System.IO;
using Quantum.Debug;
using Quantum.IO.DebugViewport.V1;
using Quantum.Math;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class DebugViewportSnapshotGalleryCommandTests
{
    [Fact]
    public void Run_SnapshotAndSvg_WritesStaticGalleryWithMetadataAndArtifactLinks()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string artifactDirectory = Path.Combine(tempDirectory, "artifacts", "debug-viewport");
        string snapshotPath = Path.Combine(artifactDirectory, "Alpha.snapshot.json");
        string svgPath = Path.Combine(artifactDirectory, "Alpha.snapshot.svg");
        string galleryPath = Path.Combine(artifactDirectory, DebugViewportSnapshotPreviewIndex.GalleryFileName);
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        try
        {
            Directory.CreateDirectory(artifactDirectory);
            File.WriteAllText(
                snapshotPath,
                DebugViewportSnapshotV1Json.Serialize(CreateSnapshot(), indented: true));
            File.WriteAllText(svgPath, "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>");

            int exitCode = DebugViewportSnapshotGalleryCommand.Run(artifactDirectory, galleryPath, writer);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(galleryPath));

            string html = File.ReadAllText(galleryPath);
            Assert.Contains("<title>Quantum DebugViewportSnapshotV1 Gallery</title>", html);
            Assert.Contains("JSON snapshot", html);
            Assert.Contains("SVG preview", html);
            Assert.Contains("HTML gallery", html);
            Assert.Contains("Generated <code>artifacts/debug-viewport</code> output is ignored by default", html);
            Assert.Contains("Source: gallery-command-test", html);
            Assert.Contains("<dt>Contract</dt><dd><code>quantum.debug_viewport_snapshot</code></dd>", html);
            Assert.Contains("<dt>Version</dt><dd>1</dd>", html);
            Assert.Contains("<dt>Sample count</dt><dd>2</dd>", html);
            Assert.Contains("<dt>Box count</dt><dd>1</dd>", html);
            Assert.Contains("<dt>Line count</dt><dd>3</dd>", html);
            Assert.Contains("href=\"Alpha.snapshot.json\">Source JSON: Alpha.snapshot.json</a>", html);
            Assert.Contains("href=\"Alpha.snapshot.svg\">SVG preview: Alpha.snapshot.svg</a>", html);
            Assert.Contains("<img src=\"Alpha.snapshot.svg\" alt=\"Alpha SVG preview\">", html);
            Assert.Contains("Wrote DebugViewportSnapshotV1 static gallery", writer.ToString());
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void Run_EmptyArtifactDirectory_WritesArtifactGuideAndEmptyState()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string artifactDirectory = Path.Combine(tempDirectory, "artifacts", "debug-viewport");
        string galleryPath = Path.Combine(artifactDirectory, DebugViewportSnapshotPreviewIndex.GalleryFileName);

        try
        {
            int exitCode = DebugViewportSnapshotGalleryCommand.Run(artifactDirectory, galleryPath);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(galleryPath));

            string html = File.ReadAllText(galleryPath);
            Assert.Contains("Artifact Guide", html);
            Assert.Contains("No DebugViewportSnapshotV1 JSON snapshots or SVG previews were found.", html);
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    private static DebugViewportSnapshotV1Dto CreateSnapshot()
    {
        ExportTrackFrame[] frames =
        {
            CreateFrame(distance: 0.0, x: 0.0, z: 0.0),
            CreateFrame(distance: 6.0, x: 6.0, y: 1.5, z: 2.0)
        };

        DebugLineSegment[] lines = TrackFrameDebugGizmoBuilder.BuildAxes(frames[0], axisLength: 2.0);
        var boxes = new[]
        {
            new DebugViewportBoxSource(
                role: "train.body",
                label: "car-0",
                frame: frames[1],
                length: 4.0,
                width: 1.8,
                height: 1.4)
        };

        return DebugViewportSnapshotV1Mapper.Export(new DebugViewportSnapshotV1Source
        {
            Units = "meters",
            SourceFixtureName = "gallery-command-test",
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
            "QuantumCoasterWorks.DebugViewportSnapshotGalleryCommandTests",
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
