using System.Globalization;
using System.IO;
using Quantum.Debug;
using Quantum.IO.DistanceInspection.V1;

namespace Quantum.Tests;

public sealed class DistanceInspectionBrowserCommandTests
{
    [Fact]
    public void Run_WithExplicitOutputPath_WritesHtmlPreviewWithExpectedSnapshotContent()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "distance-inspection.browser.html");
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        try
        {
            int exitCode = DistanceInspectionBrowserCommand.Run(outputPath, writer);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));

            string html = File.ReadAllText(outputPath);
            string lowerHtml = html.ToLowerInvariant();

            Assert.Contains("<title>Quantum Distance Inspection Browser Preview</title>", html);
            Assert.Contains("Distance Inspection Browser Preview", html);
            Assert.Contains(DistanceInspectionSnapshotV1Dto.ContractName, html);
            Assert.Contains("<dt>Version</dt><dd>1</dd>", html);
            Assert.Contains("<dt>Inspected distance</dt><dd>12.5 m</dd>", html);
            Assert.Contains("Ordered distance inspection sections", html);
            Assert.Contains("Channels", html);
            Assert.Contains("Channel Values", html);
            Assert.Contains("diagnostic-badge", html);
            Assert.Contains("diagnostic-none", html);
            Assert.Contains("<tr><th>Channel</th><th>Value</th></tr>", html);
            Assert.Contains("<tr><td>NormalG</td><td>1.4</td></tr>", html);
            Assert.Contains("<tr><td>Curvature</td><td>0.015</td></tr>", html);
            Assert.DoesNotContain("<script", lowerHtml);
            Assert.DoesNotContain("<link ", lowerHtml);
            Assert.DoesNotContain("node_modules", lowerHtml);
            Assert.DoesNotContain("npm", lowerHtml);
            Assert.DoesNotContain("unpkg", lowerHtml);
            Assert.DoesNotContain("cdnjs", lowerHtml);
            Assert.Contains("Wrote distance inspection browser preview", writer.ToString());
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void Run_WithDefaultOutputPath_WritesHtmlFileUnderArtifactsTrack()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string originalCurrentDirectory = Environment.CurrentDirectory;
        string outputPath = Path.Combine(
            tempDirectory,
            "artifacts",
            "track",
            "distance-inspection.browser.html");

        try
        {
            Directory.CreateDirectory(tempDirectory);
            Environment.CurrentDirectory = tempDirectory;

            int exitCode = DistanceInspectionBrowserCommand.Run();

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));

            string html = File.ReadAllText(outputPath);
            Assert.Contains(DistanceInspectionSnapshotV1Dto.ContractName, html);
            Assert.Contains("12.5 m", html);
        }
        finally
        {
            Environment.CurrentDirectory = originalCurrentDirectory;
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void Run_OutputHtml_IncludesOrderedForceAndGeometrySectionContent()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "distance-inspection.browser.html");

        try
        {
            int exitCode = DistanceInspectionBrowserCommand.Run(outputPath);

            Assert.Equal(0, exitCode);

            string html = File.ReadAllText(outputPath);
            int forceIndex = html.IndexOf("<h2>Force section</h2>", StringComparison.Ordinal);
            int geometryIndex = html.IndexOf("<h2>Geometry section</h2>", StringComparison.Ordinal);

            Assert.True(forceIndex >= 0);
            Assert.True(geometryIndex > forceIndex);
            Assert.Contains("<dt>Kind</dt><dd>Force</dd>", html);
            Assert.Contains("<dt>Domain</dt><dd>Distance</dd>", html);
            Assert.Contains("<dt>Range</dt><dd>[0, 25]</dd>", html);
            Assert.Contains(
                "<dt>Diagnostic</dt><dd><span class=\"diagnostic-badge diagnostic-none\">None</span></dd>",
                html);
            Assert.Contains("<li>NormalG</li>", html);
            Assert.Contains("<li>LateralG</li>", html);
            Assert.Contains("<li>LongitudinalG</li>", html);
            Assert.Contains("<li>Curvature</li>", html);
            Assert.Contains("<li>Roll</li>", html);
            Assert.Contains("<tr><td>LateralG</td><td>0</td></tr>", html);
            Assert.Contains("<tr><td>LongitudinalG</td><td>0.05</td></tr>", html);
            Assert.Contains("<tr><td>Roll</td><td>0.18</td></tr>", html);
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
            "QuantumCoasterWorks.DistanceInspectionBrowserCommandTests",
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
