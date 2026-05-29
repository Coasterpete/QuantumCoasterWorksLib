using System.Globalization;
using System.IO;
using Quantum.Debug;
using Quantum.IO.DebugViewport.V1;
using Quantum.Math;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class DebugViewportSnapshotPreviewIndexTests
{
    [Fact]
    public void TryWriteForGeneratedOutput_InsideDebugViewportArtifacts_WritesMarkdownIndexWithRelativePaths()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string artifactDirectory = Path.Combine(tempDirectory, "artifacts", "debug-viewport");
        string snapshotPath = Path.Combine(artifactDirectory, "Alpha.snapshot.json");
        string previewPath = Path.Combine(artifactDirectory, "Alpha.snapshot.svg");
        string indexPath = Path.Combine(artifactDirectory, DebugViewportSnapshotPreviewIndex.FileName);
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        try
        {
            Directory.CreateDirectory(artifactDirectory);
            File.WriteAllText(snapshotPath, "{}");
            File.WriteAllText(previewPath, "<svg />");
            File.SetLastWriteTimeUtc(snapshotPath, new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(previewPath, new DateTime(2026, 5, 1, 12, 1, 0, DateTimeKind.Utc));

            bool wrote = DebugViewportSnapshotPreviewIndex.TryWriteForGeneratedOutput(previewPath, writer);

            Assert.True(wrote);
            Assert.True(File.Exists(indexPath));

            string markdown = File.ReadAllText(indexPath);
            Assert.Contains("# Technical Preview 0.1 Debug Artifact Index", markdown);
            Assert.Contains("Directory: `artifacts/debug-viewport`", markdown);
            Assert.Contains("backend/debug-preview artifacts only", markdown);
            Assert.Contains("Static gallery: [`artifacts/debug-viewport/index.html`](index.html)", markdown);
            Assert.Contains("| JSON snapshot | `DebugViewportSnapshotV1` renderer-neutral backend data", markdown);
            Assert.Contains("| SVG preview | Multi-panel technical debug preview", markdown);
            Assert.Contains("| HTML gallery | Static local gallery", markdown);
            Assert.Contains("| # | Last Written (UTC) | Represents | Snapshot JSON | SVG Preview |", markdown);
            Assert.Contains("DebugViewportSnapshotV1 snapshot output and its paired backend-only SVG preview.", markdown);
            Assert.Contains("[`artifacts/debug-viewport/Alpha.snapshot.json`](Alpha.snapshot.json)", markdown);
            Assert.Contains("[`artifacts/debug-viewport/Alpha.snapshot.svg`](Alpha.snapshot.svg)", markdown);
            Assert.Contains("2026-05-01T12:01:00", markdown);
            Assert.Contains("Updated snapshot preview index", writer.ToString());
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void TryWriteForGeneratedOutput_OutsideDebugViewportArtifacts_DoesNotWriteIndex()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputDirectory = Path.Combine(tempDirectory, "artifacts", "other-preview");
        string previewPath = Path.Combine(outputDirectory, "Alpha.snapshot.svg");
        string indexPath = Path.Combine(outputDirectory, DebugViewportSnapshotPreviewIndex.FileName);

        try
        {
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(previewPath, "<svg />");

            bool wrote = DebugViewportSnapshotPreviewIndex.TryWriteForGeneratedOutput(previewPath);

            Assert.False(wrote);
            Assert.False(File.Exists(indexPath));
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void SvgCommand_OutputInsideDebugViewportArtifacts_RefreshesMarkdownIndex()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string artifactDirectory = Path.Combine(tempDirectory, "artifacts", "debug-viewport");
        string snapshotPath = Path.Combine(artifactDirectory, "DebugViewportSnapshotV1.validation.json");
        string svgPath = Path.Combine(artifactDirectory, "DebugViewportSnapshotV1.validation.svg");
        string indexPath = Path.Combine(artifactDirectory, DebugViewportSnapshotPreviewIndex.FileName);

        try
        {
            Directory.CreateDirectory(artifactDirectory);
            File.WriteAllText(
                snapshotPath,
                DebugViewportSnapshotV1Json.Serialize(CreateSnapshot(), indented: true));

            int exitCode = DebugViewportSnapshotV1SvgCommand.Run(snapshotPath, svgPath);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(svgPath));
            Assert.True(File.Exists(indexPath));

            string markdown = File.ReadAllText(indexPath);
            Assert.Contains("artifacts/debug-viewport/DebugViewportSnapshotV1.validation.json", markdown);
            Assert.Contains("artifacts/debug-viewport/DebugViewportSnapshotV1.validation.svg", markdown);
            Assert.Contains("Open First", markdown);
            Assert.Contains("Artifact Types", markdown);
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void WriteIndex_Milestone7FixtureNames_DescribesWhatTheArtifactsRepresent()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string artifactDirectory = Path.Combine(tempDirectory, "artifacts", "debug-viewport");
        string snapshotPath = Path.Combine(artifactDirectory, "Milestone7.synthetic.simple_hill.snapshot.json");
        string previewPath = Path.Combine(artifactDirectory, "Milestone7.synthetic.simple_hill.snapshot.svg");
        string indexPath = Path.Combine(artifactDirectory, DebugViewportSnapshotPreviewIndex.FileName);

        try
        {
            Directory.CreateDirectory(artifactDirectory);
            File.WriteAllText(snapshotPath, "{}");
            File.WriteAllText(previewPath, "<svg />");

            DebugViewportSnapshotPreviewIndex.WriteIndex(artifactDirectory, indexPath, tempDirectory);

            string markdown = File.ReadAllText(indexPath);
            Assert.Contains(
                "Milestone 7 synthetic simple hill CSV fixture converted to DebugViewportSnapshotV1 for centerline/frame preview.",
                markdown);
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
            new ExportTrackFrame(
                distance: 0.0,
                position: new Vector3d(0.0, 0.0, 0.0),
                tangent: Vector3d.UnitX,
                normal: Vector3d.UnitY,
                binormal: Vector3d.UnitZ),
            new ExportTrackFrame(
                distance: 5.0,
                position: new Vector3d(5.0, 1.0, 0.0),
                tangent: Vector3d.UnitX,
                normal: Vector3d.UnitY,
                binormal: Vector3d.UnitZ),
            new ExportTrackFrame(
                distance: 10.0,
                position: new Vector3d(10.0, 0.0, 0.0),
                tangent: Vector3d.UnitX,
                normal: Vector3d.UnitY,
                binormal: Vector3d.UnitZ)
        };

        return DebugViewportSnapshotV1Mapper.Export(new DebugViewportSnapshotV1Source
        {
            Units = "meters",
            SourceFixtureName = "snapshot-preview-index",
            SampledFrames = frames
        });
    }

    private static string CreateTempDirectoryPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "QuantumCoasterWorks.DebugViewportSnapshotPreviewIndexTests",
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
