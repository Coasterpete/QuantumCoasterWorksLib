using System.IO;
using System.Text.Json;
using Quantum.Debug;

namespace Quantum.Tests;

public sealed class LongitudinalSpeedPreviewCommandTests
{
    [Fact]
    public void Run_WithExplicitOutputPath_WritesJsonArtifactWithExpectedSchema()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "longitudinal-speed-preview.sample.json");

        try
        {
            int exitCode = LongitudinalSpeedPreviewCommand.Run(outputPath);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(outputPath));
            JsonElement root = document.RootElement;

            Assert.Equal("longitudinal-speed-preview", root.GetProperty("kind").GetString());

            JsonElement samples = root.GetProperty("samples");
            int sampleCount = root.GetProperty("sampleCount").GetInt32();
            int rowCount = samples.GetArrayLength();

            Assert.Equal(sampleCount, rowCount);
            Assert.True(rowCount > 0);

            JsonElement firstRow = samples.EnumerateArray().First();
            Assert.True(firstRow.TryGetProperty("distance", out _));
            Assert.True(firstRow.TryGetProperty("targetLongitudinalG", out _));
            Assert.True(firstRow.TryGetProperty("estimatedSpeedMps", out _));
            Assert.True(firstRow.TryGetProperty("estimatedSpeedMph", out _));
            Assert.False(firstRow.TryGetProperty("normalizedSectionT", out _));
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
        string firstOutputPath = Path.Combine(tempDirectory, "longitudinal-speed-preview.first.json");
        string secondOutputPath = Path.Combine(tempDirectory, "longitudinal-speed-preview.second.json");

        try
        {
            int firstExitCode = LongitudinalSpeedPreviewCommand.Run(firstOutputPath);
            int secondExitCode = LongitudinalSpeedPreviewCommand.Run(secondOutputPath);

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

    [Theory]
    [InlineData(LongitudinalForcePreviewPreset.Soft, -0.1015625, 0.325)]
    [InlineData(LongitudinalForcePreviewPreset.Balanced, -0.1625, 0.8203125)]
    [InlineData(LongitudinalForcePreviewPreset.Punchy, -0.025, 0.5)]
    public void Run_WithPreset_ReusesLongitudinalForcePreviewProfile(
        LongitudinalForcePreviewPreset preset,
        double expectedAtDistance10,
        double expectedAtDistance55)
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, $"longitudinal-speed-preview.{preset}.json");

        try
        {
            int exitCode = LongitudinalSpeedPreviewCommand.Run(outputPath, preset);

            Assert.Equal(0, exitCode);

            SpeedPreviewRow[] rows = ParseRows(outputPath);
            SpeedPreviewRow sampleAt10 = FindRowByDistance(rows, 10.0);
            SpeedPreviewRow sampleAt55 = FindRowByDistance(rows, 55.0);

            Assert.Equal(expectedAtDistance10, sampleAt10.TargetLongitudinalG!.Value, 10);
            Assert.Equal(expectedAtDistance55, sampleAt55.TargetLongitudinalG!.Value, 10);
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void Run_WithInitialSpeed_AppliesInitialRowAndMphConversion()
    {
        const double initialSpeedMps = 12.5;
        const double expectedMph = initialSpeedMps * 2.2369362920544;

        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "longitudinal-speed-preview.initial.json");

        try
        {
            int exitCode = LongitudinalSpeedPreviewCommand.Run(outputPath, initialSpeedMps: initialSpeedMps);

            Assert.Equal(0, exitCode);

            SpeedPreviewRow[] rows = ParseRows(outputPath);
            SpeedPreviewRow firstRow = FindRowByDistance(rows, 0.0);

            Assert.Equal(initialSpeedMps, firstRow.EstimatedSpeedMps, 10);
            Assert.Equal(expectedMph, firstRow.EstimatedSpeedMph, 10);
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void Run_WithZeroInitialSpeed_ClampsEstimatedSpeedToNonNegativeValues()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "longitudinal-speed-preview.zero-initial.json");

        try
        {
            int exitCode = LongitudinalSpeedPreviewCommand.Run(outputPath, LongitudinalForcePreviewPreset.Balanced, initialSpeedMps: 0.0);

            Assert.Equal(0, exitCode);

            SpeedPreviewRow[] rows = ParseRows(outputPath);

            Assert.All(rows, static row => Assert.True(row.EstimatedSpeedMps >= 0.0));
            Assert.Contains(rows, static row => row.EstimatedSpeedMps > 0.0);
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Run_WithInvalidInitialSpeed_ReturnsFailure(double invalidInitialSpeed)
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "longitudinal-speed-preview.invalid.json");

        try
        {
            int exitCode = LongitudinalSpeedPreviewCommand.Run(outputPath, initialSpeedMps: invalidInitialSpeed);

            Assert.Equal(1, exitCode);
            Assert.False(File.Exists(outputPath));
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    private static SpeedPreviewRow[] ParseRows(string outputPath)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(outputPath));
        JsonElement samples = document.RootElement.GetProperty("samples");

        return samples
            .EnumerateArray()
            .Select(
                static sample =>
                {
                    JsonElement targetElement = sample.GetProperty("targetLongitudinalG");
                    double? targetLongitudinalG = targetElement.ValueKind == JsonValueKind.Null
                        ? null
                        : targetElement.GetDouble();

                    return new SpeedPreviewRow(
                        sample.GetProperty("distance").GetDouble(),
                        targetLongitudinalG,
                        sample.GetProperty("estimatedSpeedMps").GetDouble(),
                        sample.GetProperty("estimatedSpeedMph").GetDouble());
                })
            .ToArray();
    }

    private static SpeedPreviewRow FindRowByDistance(IReadOnlyList<SpeedPreviewRow> rows, double distance)
    {
        const double tolerance = 1e-9;
        return Assert.Single(rows, row => System.Math.Abs(row.Distance - distance) <= tolerance);
    }

    private static string CreateTempDirectoryPath()
    {
        return Path.Combine(Path.GetTempPath(), "QuantumCoasterWorks.LongitudinalSpeedPreviewCommandTests", Guid.NewGuid().ToString("N"));
    }

    private static void DeleteDirectoryIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private readonly record struct SpeedPreviewRow(
        double Distance,
        double? TargetLongitudinalG,
        double EstimatedSpeedMps,
        double EstimatedSpeedMph);
}
