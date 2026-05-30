using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Quantum.Debug;
using Quantum.IO.DebugViewport.V1;

namespace Quantum.Tests;

public sealed class DebugViewportSnapshotBrowserCommandTests
{
    private static readonly Regex GeneratedTimestampRegex = new Regex(
        @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z",
        RegexOptions.Compiled);

    [Fact]
    public void Run_ValidSnapshot_WritesSelfContainedViewerWithExpectedLayersAndMetadata()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string artifactDirectory = Path.Combine(tempDirectory, "artifacts", "debug-viewport");
        string snapshotPath = Path.Combine(artifactDirectory, "DebugViewportSnapshotV1.sample.json");
        string browserPath = Path.Combine(artifactDirectory, "browser.html");
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        try
        {
            Directory.CreateDirectory(artifactDirectory);
            File.WriteAllText(
                snapshotPath,
                DebugViewportSnapshotV1Json.Serialize(DebugViewportSnapshotV1SampleCommand.BuildSample(), indented: true));

            int exitCode = DebugViewportSnapshotBrowserCommand.Run(artifactDirectory, browserPath, writer);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(browserPath));

            string html = File.ReadAllText(browserPath);
            string lowerHtml = html.ToLowerInvariant();

            Assert.Contains("<title>Quantum DebugViewportSnapshotV1 Browser Viewer</title>", html);
            Assert.Contains("Artifact-first browser viewer", html);
            Assert.Contains("DebugViewportSnapshotV1", html);
            Assert.Contains("Centerline samples", html);
            Assert.Contains("Distance labels/ticks", html);
            Assert.Contains("data-layer=\"distances\"", html);
            Assert.Contains("Frame axes", html);
            Assert.Contains("Debug lines", html);
            Assert.Contains("Train boxes", html);
            Assert.Contains("Bogie markers", html);
            Assert.Contains("Wheel markers", html);
            Assert.Contains("Animation", html);
            Assert.Contains("id=\"playPauseButton\"", html);
            Assert.Contains("id=\"timelineSlider\"", html);
            Assert.Contains("id=\"timelineReadout\"", html);
            Assert.Contains("requestAnimationFrame", html);
            Assert.Contains("function animationTrackSamples(snapshot)", html);
            Assert.Contains("function generatedAnimationFrameSamples(snapshot)", html);
            Assert.Contains("function animatedTrain(snapshot, progress)", html);
            Assert.Contains("function setAnimationProgress(value, shouldRender)", html);
            Assert.Contains("metric('Animation'", html);
            Assert.Contains(".distance-tick", html);
            Assert.Contains(".distance-label", html);
            Assert.Contains(".animation-panel", html);
            Assert.Contains("Measurement", html);
            Assert.Contains("id=\"measurementList\"", html);
            Assert.Contains(".measurement-panel", html);
            Assert.Contains("data-sample-index", html);
            Assert.Contains("function distanceSamples(snapshot)", html);
            Assert.Contains("function drawDistanceMarkers(group, snapshot, project)", html);
            Assert.Contains("function centerlineInspectionSamples(snapshot)", html);
            Assert.Contains("function renderMeasurement(sample, mode)", html);
            Assert.Contains("function selectSample(sample, element)", html);
            Assert.Contains("function wireSampleInspection(element, sample, snapshot)", html);
            Assert.Contains("metric('Distance ticks'", html);
            Assert.Contains("measurement('Index'", html);
            Assert.Contains("measurement('Station'", html);
            Assert.Contains("formatNumber(sample.position.x)", html);
            Assert.Contains("Math.hypot(sample.position.x - previous.x", html);
            Assert.Contains("formatDistance(sample.distance)", html);
            Assert.Contains("addEventListener('mouseenter'", html);
            Assert.Contains("addEventListener('click'", html);
            Assert.Contains("Metadata", html);
            Assert.Contains("sampling-perf-smoke", html);
            Assert.Contains("quantum.debug_viewport_snapshot", html);
            Assert.Contains("\"trainPose\":", html);
            Assert.Contains("<style>", html);
            Assert.Contains("<script id=\"snapshot-data\" type=\"application/json\">", html);
            Assert.Contains("<script>", html);
            Assert.Contains("FileReader", html);
            Assert.DoesNotContain("<script src=", lowerHtml);
            Assert.DoesNotContain("<link ", lowerHtml);
            Assert.DoesNotContain("type=\"module\"", lowerHtml);
            Assert.DoesNotContain("import ", lowerHtml);
            Assert.DoesNotContain("node_modules", lowerHtml);
            Assert.DoesNotContain("npm", lowerHtml);
            Assert.DoesNotContain("three.js", lowerHtml);
            Assert.DoesNotContain("gltf", lowerHtml);
            Assert.DoesNotContain("unpkg", lowerHtml);
            Assert.DoesNotContain("cdnjs", lowerHtml);
            Assert.Contains("Wrote DebugViewportSnapshotV1 browser viewer", writer.ToString());
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void Run_ValidSnapshot_GeneratedViewerContentIsDeterministicExceptTimestamp()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string artifactDirectory = Path.Combine(tempDirectory, "artifacts", "debug-viewport");
        string snapshotPath = Path.Combine(artifactDirectory, "DebugViewportSnapshotV1.sample.json");
        string browserPath = Path.Combine(artifactDirectory, "browser.html");

        try
        {
            Directory.CreateDirectory(artifactDirectory);
            File.WriteAllText(
                snapshotPath,
                DebugViewportSnapshotV1Json.Serialize(DebugViewportSnapshotV1SampleCommand.BuildSample(), indented: true));

            Assert.Equal(0, DebugViewportSnapshotBrowserCommand.Run(artifactDirectory, browserPath));
            string firstHtml = File.ReadAllText(browserPath);

            Assert.Equal(0, DebugViewportSnapshotBrowserCommand.Run(artifactDirectory, browserPath));
            string secondHtml = File.ReadAllText(browserPath);

            Assert.Equal(
                NormalizeGeneratedTimestamp(firstHtml),
                NormalizeGeneratedTimestamp(secondHtml));
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void Run_EmptyArtifactDirectory_WritesEmptyStateViewer()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string artifactDirectory = Path.Combine(tempDirectory, "artifacts", "debug-viewport");
        string browserPath = Path.Combine(artifactDirectory, "browser.html");

        try
        {
            int exitCode = DebugViewportSnapshotBrowserCommand.Run(artifactDirectory, browserPath);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(browserPath));

            string html = File.ReadAllText(browserPath);
            Assert.Contains("No valid DebugViewportSnapshotV1 JSON snapshots are embedded.", html);
            Assert.Contains("\"entries\":[]", html);
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void Run_InvalidJson_DoesNotBlockViewerGeneration()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string artifactDirectory = Path.Combine(tempDirectory, "artifacts", "debug-viewport");
        string snapshotPath = Path.Combine(artifactDirectory, "invalid.json");
        string browserPath = Path.Combine(artifactDirectory, "browser.html");

        try
        {
            Directory.CreateDirectory(artifactDirectory);
            File.WriteAllText(snapshotPath, "{ invalid json");

            int exitCode = DebugViewportSnapshotBrowserCommand.Run(artifactDirectory, browserPath);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(browserPath));

            string html = File.ReadAllText(browserPath);
            Assert.Contains("Skipped invalid.json", html);
            Assert.Contains("\"snapshot\":null", html);
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
            "QuantumCoasterWorks.DebugViewportSnapshotBrowserCommandTests",
            Guid.NewGuid().ToString("N"));
    }

    private static string NormalizeGeneratedTimestamp(string html)
    {
        return GeneratedTimestampRegex.Replace(html, "<generated-at>");
    }

    private static void DeleteDirectoryIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
