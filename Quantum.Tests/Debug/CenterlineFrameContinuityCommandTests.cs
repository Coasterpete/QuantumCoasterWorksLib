using System.IO;
using System.Text.Json;
using Quantum.Debug;
using Quantum.IO.TrackFrameContinuity.V1;

namespace Quantum.Tests;

public sealed class CenterlineFrameContinuityCommandTests
{
    [Fact]
    public void Run_WithExplicitOutputPath_WritesJsonArtifactWithExpectedSchema()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "centerline-frame-continuity.sample.json");

        try
        {
            int exitCode = CenterlineFrameContinuityCommand.Run(outputPath);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(outputPath));
            JsonElement root = document.RootElement;

            Assert.Equal(
                TrackFrameContinuityDiagnosticsExportV1Dto.ContractName,
                root.GetProperty("contract").GetString());
            Assert.Equal(
                TrackFrameContinuityDiagnosticsExportV1Dto.ContractVersion,
                root.GetProperty("version").GetInt32());
            Assert.True(root.GetProperty("backendOnly").GetBoolean());

            JsonElement metadata = root.GetProperty("metadata");
            Assert.Equal("meters", metadata.GetProperty("units").GetString());
            Assert.Equal("deterministic-roll-step-centerline", metadata.GetProperty("sourceName").GetString());
            Assert.Equal(120.0, metadata.GetProperty("trackLength").GetDouble(), 10);

            JsonElement summary = root.GetProperty("summaryStatistics");
            Assert.Equal(13, summary.GetProperty("sampleCount").GetInt32());
            Assert.Equal(12, summary.GetProperty("intervalCount").GetInt32());
            Assert.True(summary.GetProperty("hasIssues").GetBoolean());
            Assert.True(summary.GetProperty("issueCount").GetInt32() > 0);

            Assert.Equal(
                summary.GetProperty("sampleCount").GetInt32(),
                root.GetProperty("samples").GetArrayLength());
            Assert.Equal(
                summary.GetProperty("intervalCount").GetInt32(),
                root.GetProperty("intervals").GetArrayLength());
            Assert.Equal(
                summary.GetProperty("issueCount").GetInt32(),
                root.GetProperty("issues").GetArrayLength());

            JsonElement thresholds = root.GetProperty("thresholdsDegrees");
            Assert.Equal(15.0, thresholds.GetProperty("tangent").GetDouble(), 10);
            Assert.Equal(15.0, thresholds.GetProperty("roll").GetDouble(), 10);

            JsonElement rollStatistics = summary.GetProperty("rollDegrees");
            Assert.True(rollStatistics.GetProperty("maxAbsolute").GetDouble() > 15.0);
            Assert.True(rollStatistics.GetProperty("averageAbsolute").GetDouble() > 0.0);

            string diagnosticText = root.GetProperty("diagnosticText").GetString()!;
            Assert.Contains("Frame continuity:", diagnosticText);
            Assert.Contains("discontinuities=", diagnosticText);
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
        string firstOutputPath = Path.Combine(tempDirectory, "centerline-frame-continuity.first.json");
        string secondOutputPath = Path.Combine(tempDirectory, "centerline-frame-continuity.second.json");

        try
        {
            int firstExitCode = CenterlineFrameContinuityCommand.Run(firstOutputPath);
            int secondExitCode = CenterlineFrameContinuityCommand.Run(secondOutputPath);

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

    [Fact]
    public void Run_DeterministicSample_ReportsRollStepNearFirstSegmentBoundary()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "centerline-frame-continuity.sample.json");

        try
        {
            int exitCode = CenterlineFrameContinuityCommand.Run(outputPath);

            Assert.Equal(0, exitCode);

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(outputPath));
            JsonElement issues = document.RootElement.GetProperty("issues");

            JsonElement rollIssue = Assert.Single(
                issues.EnumerateArray(),
                issue =>
                    issue.GetProperty("issueType").GetString() == "Roll" &&
                    issue.GetProperty("startSampleIndex").GetInt32() == 3 &&
                    issue.GetProperty("endSampleIndex").GetInt32() == 4);

            Assert.Equal(4, rollIssue.GetProperty("sampleIndex").GetInt32());
            Assert.Equal(40.0, rollIssue.GetProperty("distance").GetDouble(), 10);
            Assert.Equal(30.0, rollIssue.GetProperty("startDistance").GetDouble(), 10);
            Assert.Equal(40.0, rollIssue.GetProperty("endDistance").GetDouble(), 10);
            Assert.True(rollIssue.GetProperty("actualDegrees").GetDouble() > 15.0);
            Assert.Equal(15.0, rollIssue.GetProperty("thresholdDegrees").GetDouble(), 10);
            Assert.True(rollIssue.GetProperty("exceededByDegrees").GetDouble() > 0.0);
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    private static string CreateTempDirectoryPath()
    {
        return Path.Combine(Path.GetTempPath(), "QuantumCoasterWorks.CenterlineFrameContinuityCommandTests", Guid.NewGuid().ToString("N"));
    }

    private static void DeleteDirectoryIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
