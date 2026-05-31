using System.IO;
using System.Text.Json;
using Quantum.Debug;
using Quantum.IO.TransportedFrameComparison.V1;

namespace Quantum.Tests;

public sealed class TransportedFrameComparisonCommandTests
{
    [Fact]
    public void Run_WithExplicitOutputPath_WritesJsonArtifactWithExpectedContent()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "transported-frame-comparison.sample.json");

        try
        {
            int exitCode = TransportedFrameComparisonCommand.Run(outputPath);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(outputPath));
            JsonElement root = document.RootElement;

            Assert.Equal(
                TransportedFrameComparisonDiagnosticsExportV1Dto.ContractName,
                root.GetProperty("contract").GetString());
            Assert.Equal(
                TransportedFrameComparisonDiagnosticsExportV1Dto.ContractVersion,
                root.GetProperty("version").GetInt32());
            Assert.True(root.GetProperty("backendOnly").GetBoolean());

            JsonElement metadata = root.GetProperty("metadata");
            Assert.Equal("meters", metadata.GetProperty("units").GetString());
            Assert.Equal("diagnostic-track-fixtures", metadata.GetProperty("sourceName").GetString());
            Assert.Equal(6, metadata.GetProperty("reportCount").GetInt32());
            Assert.Equal(6, metadata.GetProperty("fixtureNames").GetArrayLength());

            JsonElement reports = root.GetProperty("reports");
            Assert.Equal(6, reports.GetArrayLength());

            JsonElement quarterLoop = Assert.Single(
                reports.EnumerateArray(),
                report => report.GetProperty("sourceName").GetString() == DiagnosticTrackFixtures.QuarterLoopLikeName);

            JsonElement summary = quarterLoop.GetProperty("summaryMetrics");
            Assert.Equal(17, summary.GetProperty("sampleCount").GetInt32());
            Assert.False(summary.GetProperty("statelessHasContinuityIssues").GetBoolean());
            Assert.False(summary.GetProperty("transportedHasContinuityIssues").GetBoolean());
            Assert.True(summary.GetProperty("normalDegrees").GetProperty("maxAbsolute").GetDouble() > 0.0);

            JsonElement samples = quarterLoop.GetProperty("samples");
            Assert.Equal(17, samples.GetArrayLength());
            Assert.Equal(0, samples[0].GetProperty("sampleIndex").GetInt32());
            Assert.Equal(0.0, samples[0].GetProperty("distance").GetDouble(), 10);
            Assert.True(samples[16].GetProperty("distance").GetDouble() > 0.0);
            Assert.True(samples[16].TryGetProperty("matrixOrientationDegrees", out _));

            JsonElement smoothness = quarterLoop.GetProperty("smoothnessMetrics");
            double statelessNormalMax = smoothness
                .GetProperty("stateless")
                .GetProperty("normalDegrees")
                .GetProperty("maxAbsolute")
                .GetDouble();
            double transportedNormalMax = smoothness
                .GetProperty("transported")
                .GetProperty("normalDegrees")
                .GetProperty("maxAbsolute")
                .GetDouble();

            Assert.True(transportedNormalMax < statelessNormalMax);
            Assert.Equal(16, smoothness.GetProperty("stateless").GetProperty("intervals").GetArrayLength());
            Assert.Equal(16, smoothness.GetProperty("transported").GetProperty("intervals").GetArrayLength());

            JsonElement continuity = quarterLoop.GetProperty("continuityMetrics");
            Assert.Equal(181.0, continuity.GetProperty("thresholdsDegrees").GetProperty("tangent").GetDouble(), 10);
            Assert.Equal(16, continuity.GetProperty("stateless").GetProperty("intervals").GetArrayLength());
            Assert.Equal(16, continuity.GetProperty("transported").GetProperty("intervals").GetArrayLength());
            Assert.Equal(0, continuity.GetProperty("stateless").GetProperty("issues").GetArrayLength());
            Assert.Equal(0, continuity.GetProperty("transported").GetProperty("issues").GetArrayLength());
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void Run_WithExplicitOutputPath_IsDeterministicAcrossTwoRuns()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string firstOutputPath = Path.Combine(tempDirectory, "transported-frame-comparison.first.json");
        string secondOutputPath = Path.Combine(tempDirectory, "transported-frame-comparison.second.json");

        try
        {
            int firstExitCode = TransportedFrameComparisonCommand.Run(firstOutputPath);
            int secondExitCode = TransportedFrameComparisonCommand.Run(secondOutputPath);

            Assert.Equal(0, firstExitCode);
            Assert.Equal(0, secondExitCode);

            string firstJson = File.ReadAllText(firstOutputPath);
            string secondJson = File.ReadAllText(secondOutputPath);

            Assert.Equal(firstJson, secondJson);
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
            "QuantumCoasterWorks.TransportedFrameComparisonCommandTests",
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
