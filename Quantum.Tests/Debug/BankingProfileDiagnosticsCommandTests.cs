using System.IO;
using System.Text.Json;
using Quantum.Debug;
using Quantum.IO.BankingProfile.V1;

namespace Quantum.Tests;

public sealed class BankingProfileDiagnosticsCommandTests
{
    [Fact]
    public void Run_WithExplicitOutputPath_WritesJsonArtifactWithExpectedContent()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "banking-profile-diagnostics.sample.json");

        try
        {
            int exitCode = BankingProfileDiagnosticsCommand.Run(outputPath);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(outputPath));
            JsonElement root = document.RootElement;

            Assert.Equal(
                BankingProfileDiagnosticsExportV1Dto.ContractName,
                root.GetProperty("contract").GetString());
            Assert.Equal(
                BankingProfileDiagnosticsExportV1Dto.ContractVersion,
                root.GetProperty("version").GetInt32());
            Assert.True(root.GetProperty("backendOnly").GetBoolean());

            JsonElement metadata = root.GetProperty("metadata");
            Assert.Equal("meters,radians", metadata.GetProperty("units").GetString());
            Assert.Equal(
                BankingProfileFixtures.RollHoldWithMultipleKeysName,
                metadata.GetProperty("sourceName").GetString());
            Assert.Equal(5, metadata.GetProperty("profileKeyCount").GetInt32());
            Assert.Equal("radians_per_meter", metadata.GetProperty("rollSlopeUnit").GetString());

            JsonElement summary = root.GetProperty("summaryMetrics");
            Assert.Equal(11, summary.GetProperty("sampleCount").GetInt32());
            Assert.True(summary.GetProperty("minRollRadians").GetDouble() < 0.0);
            Assert.True(summary.GetProperty("maxRollRadians").GetDouble() > 0.0);
            Assert.True(summary.GetProperty("maxAbsoluteRollSlopeRadPerMeter").GetDouble() > 0.0);

            JsonElement samples = root.GetProperty("samples");
            Assert.Equal(11, samples.GetArrayLength());
            Assert.Equal(0, samples[0].GetProperty("sampleIndex").GetInt32());
            Assert.Equal(0.0, samples[0].GetProperty("distance").GetDouble(), 10);
            Assert.Equal("ClampBeforeFirstKey", samples[0].GetProperty("sourceKind").GetString());

            JsonElement linearSample = Assert.Single(
                samples.EnumerateArray(),
                sample => sample.GetProperty("distance").GetDouble() == 10.0);

            Assert.Equal("Linear", linearSample.GetProperty("interpolationMode").GetString());
            Assert.Equal("KeyInterval", linearSample.GetProperty("sourceKind").GetString());
            Assert.Equal(0, linearSample.GetProperty("sourceInterval").GetProperty("startKeyIndex").GetInt32());
            Assert.Equal(1, linearSample.GetProperty("sourceInterval").GetProperty("endKeyIndex").GetInt32());
            Assert.True(linearSample.TryGetProperty("approximateRollSlopeRadPerMeter", out _));

            JsonElement holdSample = Assert.Single(
                samples.EnumerateArray(),
                sample => sample.GetProperty("distance").GetDouble() == 30.0);

            Assert.Equal("Constant", holdSample.GetProperty("interpolationMode").GetString());
            Assert.Equal("KeyInterval", holdSample.GetProperty("sourceKind").GetString());

            JsonElement smoothStepSample = Assert.Single(
                samples.EnumerateArray(),
                sample => sample.GetProperty("distance").GetDouble() == 70.0);

            Assert.Equal("SmoothStep", smoothStepSample.GetProperty("interpolationMode").GetString());
            Assert.Equal("KeyInterval", smoothStepSample.GetProperty("sourceKind").GetString());
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
        string firstOutputPath = Path.Combine(tempDirectory, "banking-profile-diagnostics.first.json");
        string secondOutputPath = Path.Combine(tempDirectory, "banking-profile-diagnostics.second.json");

        try
        {
            int firstExitCode = BankingProfileDiagnosticsCommand.Run(firstOutputPath);
            int secondExitCode = BankingProfileDiagnosticsCommand.Run(secondOutputPath);

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
            "QuantumCoasterWorks.BankingProfileDiagnosticsCommandTests",
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
