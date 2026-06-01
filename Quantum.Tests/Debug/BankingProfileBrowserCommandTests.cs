using System.Globalization;
using System.IO;
using System.Text.Json;
using Quantum.Debug;
using Quantum.IO.BankingProfile.V1;

namespace Quantum.Tests;

public sealed class BankingProfileBrowserCommandTests
{
    [Fact]
    public void Run_ValidDiagnosticsJson_WritesSelfContainedViewerWithExpectedSummaryGraphsAndTable()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string artifactDirectory = Path.Combine(tempDirectory, "artifacts", "banking-profile");
        string diagnosticsPath = Path.Combine(artifactDirectory, "banking-profile-diagnostics.sample.json");
        string browserPath = Path.Combine(artifactDirectory, "banking-profile.browser.html");
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        try
        {
            Assert.Equal(0, BankingProfileDiagnosticsCommand.Run(diagnosticsPath));

            int exitCode = BankingProfileBrowserCommand.Run(diagnosticsPath, browserPath, writer);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(browserPath));

            string html = File.ReadAllText(browserPath);
            string lowerHtml = html.ToLowerInvariant();

            Assert.Contains("<title>Quantum BankingProfile Browser Viewer</title>", html);
            Assert.Contains("BankingProfile Browser Viewer", html);
            Assert.Contains("Profile Metadata", html);
            Assert.Contains("Summary Metrics", html);
            Assert.Contains("Roll Slope Severity", html);
            Assert.Contains("Roll Angle vs Station Distance", html);
            Assert.Contains("Roll Slope vs Station Distance", html);
            Assert.Contains("Roll Samples", html);
            Assert.Contains("<svg id=\"rollChart\"", html);
            Assert.Contains("<svg id=\"slopeChart\"", html);
            Assert.Contains("<script id=\"banking-profile-data\" type=\"application/json\">", html);
            Assert.Contains(BankingProfileDiagnosticsExportV1Dto.ContractName, html);
            Assert.Contains("\"summaryMetrics\":", html);
            Assert.Contains("\"samples\":", html);
            Assert.Contains("\"rollRadians\":", html);
            Assert.Contains("\"rollDegrees\":", html);
            Assert.Contains("\"interpolationMode\":", html);
            Assert.Contains("\"approximateRollSlopeRadPerMeter\":", html);
            Assert.Contains("severityForSlope(value)", html);
            Assert.Contains("collectMarkers(points)", html);
            Assert.Contains("key-marker", html);
            Assert.Contains("transition-marker", html);
            Assert.Contains("data-interpolation-mode", html);
            Assert.Contains("rollRad=[", html);
            Assert.Contains("rollDeg=[", html);
            Assert.Contains("maxSlope=", html);
            Assert.Contains("FileReader", html);
            Assert.Contains("Expected ' + CONTRACT + ' v' + VERSION", html);
            Assert.Contains("banking-profile-diagnostics.sample.json", html);
            Assert.Contains("does not change TrackFrame", html);
            Assert.DoesNotContain("DebugViewportSnapshotV1 JSON", html);
            Assert.DoesNotContain("TrainPoseExportV1 JSON", html);
            Assert.DoesNotContain("<script src=", lowerHtml);
            Assert.DoesNotContain("<link ", lowerHtml);
            Assert.DoesNotContain("type=\"module\"", lowerHtml);
            Assert.DoesNotContain("import ", lowerHtml);
            Assert.DoesNotContain("node_modules", lowerHtml);
            Assert.DoesNotContain("npm", lowerHtml);
            Assert.DoesNotContain("unpkg", lowerHtml);
            Assert.DoesNotContain("cdnjs", lowerHtml);
            Assert.DoesNotContain("Math.random", html);
            Assert.Contains("Wrote BankingProfile browser viewer", writer.ToString());
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void Run_ValidDiagnosticsJson_EmbedsParseableDiagnosticsJsonPayload()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string artifactDirectory = Path.Combine(tempDirectory, "artifacts", "banking-profile");
        string diagnosticsPath = Path.Combine(artifactDirectory, "banking-profile-diagnostics.sample.json");
        string browserPath = Path.Combine(artifactDirectory, "banking-profile.browser.html");

        try
        {
            Assert.Equal(0, BankingProfileDiagnosticsCommand.Run(diagnosticsPath));
            Assert.Equal(0, BankingProfileBrowserCommand.Run(diagnosticsPath, browserPath));

            string html = File.ReadAllText(browserPath);
            string payloadJson = ExtractEmbeddedJson(html);

            using JsonDocument document = JsonDocument.Parse(payloadJson);
            JsonElement root = document.RootElement;
            JsonElement artifact = root.GetProperty("artifact");
            JsonElement metadata = artifact.GetProperty("metadata");
            JsonElement summary = artifact.GetProperty("summaryMetrics");
            JsonElement samples = artifact.GetProperty("samples");

            Assert.Equal(
                BankingProfileDiagnosticsExportV1Dto.ContractName,
                artifact.GetProperty("contract").GetString());
            Assert.Equal(
                BankingProfileDiagnosticsExportV1Dto.ContractVersion,
                artifact.GetProperty("version").GetInt32());
            Assert.Equal(
                BankingProfileFixtures.RollHoldWithMultipleKeysName,
                metadata.GetProperty("sourceName").GetString());
            Assert.Equal(11, summary.GetProperty("sampleCount").GetInt32());
            Assert.Equal(11, samples.GetArrayLength());
            Assert.Equal("Linear", samples[1].GetProperty("interpolationMode").GetString());
            Assert.Equal("Constant", samples[3].GetProperty("interpolationMode").GetString());
            Assert.True(samples[1].TryGetProperty("approximateRollSlopeRadPerMeter", out _));
            Assert.Contains("banking-profile-diagnostics.sample.json", root.GetProperty("sourcePath").GetString());
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void Run_ValidDiagnosticsJson_GeneratedViewerContentIsDeterministic()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string artifactDirectory = Path.Combine(tempDirectory, "artifacts", "banking-profile");
        string diagnosticsPath = Path.Combine(artifactDirectory, "banking-profile-diagnostics.sample.json");
        string browserPath = Path.Combine(artifactDirectory, "banking-profile.browser.html");

        try
        {
            Assert.Equal(0, BankingProfileDiagnosticsCommand.Run(diagnosticsPath));

            Assert.Equal(0, BankingProfileBrowserCommand.Run(diagnosticsPath, browserPath));
            string firstHtml = File.ReadAllText(browserPath);

            Assert.Equal(0, BankingProfileBrowserCommand.Run(diagnosticsPath, browserPath));
            string secondHtml = File.ReadAllText(browserPath);

            Assert.Equal(firstHtml, secondHtml);
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void Run_MissingDiagnosticsJson_ReturnsErrorWithoutWritingViewer()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string diagnosticsPath = Path.Combine(tempDirectory, "missing.json");
        string browserPath = Path.Combine(tempDirectory, "viewer.html");
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        try
        {
            int exitCode = BankingProfileBrowserCommand.Run(diagnosticsPath, browserPath, writer);

            Assert.Equal(1, exitCode);
            Assert.False(File.Exists(browserPath));
            Assert.Contains("BankingProfile diagnostics JSON was not found.", writer.ToString());
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    private static string ExtractEmbeddedJson(string html)
    {
        const string Start = "<script id=\"banking-profile-data\" type=\"application/json\">";
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
            "QuantumCoasterWorks.BankingProfileBrowserCommandTests",
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
