using System.IO;
using System.Text.Json;
using Quantum.Debug;

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

            Assert.Equal("centerline-frame-continuity-diagnostics", root.GetProperty("kind").GetString());
            Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
            Assert.True(root.GetProperty("backendOnly").GetBoolean());
            Assert.Equal("deterministic-roll-step-centerline", root.GetProperty("sampleName").GetString());
            Assert.Equal(120.0, root.GetProperty("trackLength").GetDouble(), 10);
            Assert.Equal(13, root.GetProperty("sampleCount").GetInt32());
            Assert.Equal(12, root.GetProperty("intervalCount").GetInt32());
            Assert.True(root.GetProperty("hasDiscontinuities").GetBoolean());
            Assert.True(root.GetProperty("discontinuityCount").GetInt32() > 0);

            Assert.Equal(
                root.GetProperty("sampleCount").GetInt32(),
                root.GetProperty("samples").GetArrayLength());
            Assert.Equal(
                root.GetProperty("intervalCount").GetInt32(),
                root.GetProperty("intervals").GetArrayLength());
            Assert.Equal(
                root.GetProperty("discontinuityCount").GetInt32(),
                root.GetProperty("issues").GetArrayLength());

            JsonElement thresholds = root.GetProperty("thresholdsDegrees");
            Assert.Equal(15.0, thresholds.GetProperty("tangent").GetDouble(), 10);
            Assert.Equal(15.0, thresholds.GetProperty("roll").GetDouble(), 10);

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
                    issue.GetProperty("kind").GetString() == "Roll" &&
                    issue.GetProperty("startSampleIndex").GetInt32() == 3 &&
                    issue.GetProperty("endSampleIndex").GetInt32() == 4);

            Assert.Equal(30.0, rollIssue.GetProperty("startDistance").GetDouble(), 10);
            Assert.Equal(40.0, rollIssue.GetProperty("endDistance").GetDouble(), 10);
            Assert.True(rollIssue.GetProperty("actualDegrees").GetDouble() > 15.0);
            Assert.Equal(15.0, rollIssue.GetProperty("thresholdDegrees").GetDouble(), 10);
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
