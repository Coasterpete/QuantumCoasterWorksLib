using System.Globalization;
using System.IO;
using System.Text.Json;
using Quantum.Debug;
using Quantum.IO.TransportedFrameComparison.V1;

namespace Quantum.Tests;

public sealed class TransportedFrameComparisonBrowserCommandTests
{
    [Fact]
    public void Run_ValidComparisonJson_WritesSelfContainedViewerWithExpectedMetricsAndTable()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string artifactDirectory = Path.Combine(tempDirectory, "artifacts", "frame-comparison");
        string comparisonPath = Path.Combine(artifactDirectory, "transported-frame-comparison.sample.json");
        string browserPath = Path.Combine(artifactDirectory, "transported-frame-comparison.browser.html");
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        try
        {
            Assert.Equal(0, TransportedFrameComparisonCommand.Run(comparisonPath));

            int exitCode = TransportedFrameComparisonBrowserCommand.Run(comparisonPath, browserPath, writer);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(browserPath));

            string html = File.ReadAllText(browserPath);
            string lowerHtml = html.ToLowerInvariant();

            Assert.Contains("<title>Quantum Transported Frame Comparison Browser Viewer</title>", html);
            Assert.Contains("Transported Frame Comparison", html);
            Assert.Contains("Summary Metrics", html);
            Assert.Contains("Delta Severity", html);
            Assert.Contains("Per-sample Delta Table", html);
            Assert.Contains("Normal, Binormal, Frame, Matrix Severity", html);
            Assert.Contains("<svg id=\"severityChart\"", html);
            Assert.Contains("<script id=\"comparison-data\" type=\"application/json\">", html);
            Assert.Contains(TransportedFrameComparisonDiagnosticsExportV1Dto.ContractName, html);
            Assert.Contains("\"summaryMetrics\":", html);
            Assert.Contains("\"samples\":", html);
            Assert.Contains("\"normalDegrees\":", html);
            Assert.Contains("\"binormalDegrees\":", html);
            Assert.Contains("\"frameDegrees\":", html);
            Assert.Contains("\"matrixOrientationDegrees\":", html);
            Assert.Contains("normalMax=", html);
            Assert.Contains("matrixMax=", html);
            Assert.Contains("severity-dot severity-zero", html);
            Assert.Contains("severity-low", html);
            Assert.Contains("severity-moderate", html);
            Assert.Contains("severity-high", html);
            Assert.Contains("severityFor(value)", html);
            Assert.Contains("setAttribute('data-metric', metric.key)", html);
            Assert.Contains("{ key: 'normalDegrees', label: 'Normal' }", html);
            Assert.Contains("{ key: 'binormalDegrees', label: 'Binormal' }", html);
            Assert.Contains("{ key: 'frameDegrees', label: 'Frame' }", html);
            Assert.Contains("{ key: 'matrixOrientationDegrees', label: 'Matrix' }", html);
            Assert.Contains("FileReader", html);
            Assert.Contains("Expected ' + CONTRACT + ' v' + VERSION", html);
            Assert.Contains("transported-frame-comparison.sample.json", html);
            Assert.DoesNotContain("DebugViewportSnapshotV1", html);
            Assert.DoesNotContain("TrainPoseExportV1", html);
            Assert.DoesNotContain("<script src=", lowerHtml);
            Assert.DoesNotContain("<link ", lowerHtml);
            Assert.DoesNotContain("type=\"module\"", lowerHtml);
            Assert.DoesNotContain("import ", lowerHtml);
            Assert.DoesNotContain("node_modules", lowerHtml);
            Assert.DoesNotContain("npm", lowerHtml);
            Assert.DoesNotContain("unpkg", lowerHtml);
            Assert.DoesNotContain("cdnjs", lowerHtml);
            Assert.DoesNotContain("Math.random", html);
            Assert.Contains("Wrote transported frame comparison browser viewer", writer.ToString());
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void Run_ValidComparisonJson_GeneratedViewerContentIsDeterministic()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string artifactDirectory = Path.Combine(tempDirectory, "artifacts", "frame-comparison");
        string comparisonPath = Path.Combine(artifactDirectory, "transported-frame-comparison.sample.json");
        string browserPath = Path.Combine(artifactDirectory, "transported-frame-comparison.browser.html");

        try
        {
            Assert.Equal(0, TransportedFrameComparisonCommand.Run(comparisonPath));

            Assert.Equal(0, TransportedFrameComparisonBrowserCommand.Run(comparisonPath, browserPath));
            string firstHtml = File.ReadAllText(browserPath);

            Assert.Equal(0, TransportedFrameComparisonBrowserCommand.Run(comparisonPath, browserPath));
            string secondHtml = File.ReadAllText(browserPath);

            Assert.Equal(firstHtml, secondHtml);
            AssertEmbeddedPayloadPaths(
                secondHtml,
                "transported-frame-comparison.sample.json",
                "transported-frame-comparison.browser.html");
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void Run_MissingComparisonJson_ReturnsErrorWithoutWritingViewer()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string comparisonPath = Path.Combine(tempDirectory, "missing.json");
        string browserPath = Path.Combine(tempDirectory, "viewer.html");
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        try
        {
            int exitCode = TransportedFrameComparisonBrowserCommand.Run(comparisonPath, browserPath, writer);

            Assert.Equal(1, exitCode);
            Assert.False(File.Exists(browserPath));
            Assert.Contains("Transported frame comparison JSON was not found.", writer.ToString());
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    private static void AssertEmbeddedPayloadPaths(
        string html,
        string expectedSourcePath,
        string expectedOutputPath)
    {
        string payloadJson = ExtractEmbeddedJson(html);

        using JsonDocument document = JsonDocument.Parse(payloadJson);
        JsonElement root = document.RootElement;

        Assert.Equal(expectedSourcePath, root.GetProperty("sourcePath").GetString());
        Assert.Equal(expectedOutputPath, root.GetProperty("outputPath").GetString());
    }

    private static string ExtractEmbeddedJson(string html)
    {
        const string Start = "<script id=\"comparison-data\" type=\"application/json\">";
        const string End = "</script>";
        int startIndex = html.IndexOf(Start, StringComparison.Ordinal);
        Assert.True(startIndex >= 0);
        startIndex += Start.Length;
        int endIndex = html.IndexOf(End, startIndex, StringComparison.Ordinal);
        Assert.True(endIndex > startIndex);
        return html.Substring(startIndex, endIndex - startIndex).Trim();
    }

    private static string CreateTempDirectoryPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "QuantumCoasterWorks.TransportedFrameComparisonBrowserCommandTests",
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
