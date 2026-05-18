using System.IO;
using System.Text.Json;
using Quantum.Debug;

namespace Quantum.Tests;

public sealed class LongitudinalForcePreviewCommandTests
{
    [Fact]
    public void Run_WithExplicitOutputPath_WritesJsonArtifactWithExpectedSchema()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "longitudinal-force-preview.sample.json");

        try
        {
            int exitCode = LongitudinalForcePreviewCommand.Run(outputPath);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(outputPath));
            JsonElement root = document.RootElement;

            Assert.Equal("longitudinal-force-preview", root.GetProperty("kind").GetString());

            JsonElement samples = root.GetProperty("samples");
            int sampleCount = root.GetProperty("sampleCount").GetInt32();
            int rowCount = samples.GetArrayLength();

            Assert.Equal(sampleCount, rowCount);
            Assert.True(rowCount > 0);

            JsonElement firstRow = samples.EnumerateArray().First();
            Assert.True(firstRow.TryGetProperty("distance", out _));
            Assert.True(firstRow.TryGetProperty("normalizedSectionT", out _));
            Assert.True(firstRow.TryGetProperty("targetLongitudinalG", out _));
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
        string firstOutputPath = Path.Combine(tempDirectory, "longitudinal-force-preview.first.json");
        string secondOutputPath = Path.Combine(tempDirectory, "longitudinal-force-preview.second.json");

        try
        {
            int firstExitCode = LongitudinalForcePreviewCommand.Run(firstOutputPath);
            int secondExitCode = LongitudinalForcePreviewCommand.Run(secondOutputPath);

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
    public void Run_DemoPreview_UsesNonLinearLongitudinalInterpolation()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "longitudinal-force-preview.sample.json");

        try
        {
            int exitCode = LongitudinalForcePreviewCommand.Run(outputPath);

            Assert.Equal(0, exitCode);

            PreviewRow[] rows = ParseRows(outputPath);
            PreviewRow smoothStepQuarter = FindRowByDistance(rows, 10.0);
            PreviewRow quinticMidpoint = FindRowByDistance(rows, 55.0);

            Assert.Equal(0.25, smoothStepQuarter.NormalizedSectionT, 10);
            Assert.Equal(-0.1625, smoothStepQuarter.TargetLongitudinalG!.Value, 10);

            Assert.Equal(0.5, quinticMidpoint.NormalizedSectionT, 10);
            Assert.Equal(0.8203125, quinticMidpoint.TargetLongitudinalG!.Value, 10);
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
    public void Run_WithPreset_WritesExpectedLongitudinalProfileSamples(
        LongitudinalForcePreviewPreset preset,
        double expectedAtDistance10,
        double expectedAtDistance55)
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, $"longitudinal-force-preview.{preset}.json");

        try
        {
            int exitCode = LongitudinalForcePreviewCommand.Run(outputPath, preset);

            Assert.Equal(0, exitCode);

            PreviewRow[] rows = ParseRows(outputPath);
            PreviewRow sampleAt10 = FindRowByDistance(rows, 10.0);
            PreviewRow sampleAt55 = FindRowByDistance(rows, 55.0);

            Assert.Equal(expectedAtDistance10, sampleAt10.TargetLongitudinalG!.Value, 10);
            Assert.Equal(expectedAtDistance55, sampleAt55.TargetLongitudinalG!.Value, 10);
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Theory]
    [InlineData("soft", LongitudinalForcePreviewPreset.Soft)]
    [InlineData("SOFT", LongitudinalForcePreviewPreset.Soft)]
    [InlineData("balanced", LongitudinalForcePreviewPreset.Balanced)]
    [InlineData("punchy", LongitudinalForcePreviewPreset.Punchy)]
    public void TryParsePreset_KnownNames_AreAccepted(
        string input,
        LongitudinalForcePreviewPreset expectedPreset)
    {
        bool success = LongitudinalForcePreviewCommand.TryParsePreset(input, out LongitudinalForcePreviewPreset preset);

        Assert.True(success);
        Assert.Equal(expectedPreset, preset);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("unknown")]
    public void TryParsePreset_UnknownNames_AreRejected(string input)
    {
        bool success = LongitudinalForcePreviewCommand.TryParsePreset(input, out _);

        Assert.False(success);
    }

    private static PreviewRow[] ParseRows(string outputPath)
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

                    return new PreviewRow(
                        sample.GetProperty("distance").GetDouble(),
                        sample.GetProperty("normalizedSectionT").GetDouble(),
                        targetLongitudinalG);
                })
            .ToArray();
    }

    private static PreviewRow FindRowByDistance(IReadOnlyList<PreviewRow> rows, double distance)
    {
        const double tolerance = 1e-9;
        return Assert.Single(rows, row => System.Math.Abs(row.Distance - distance) <= tolerance);
    }

    private static string CreateTempDirectoryPath()
    {
        return Path.Combine(Path.GetTempPath(), "QuantumCoasterWorks.LongitudinalForcePreviewCommandTests", Guid.NewGuid().ToString("N"));
    }

    private static void DeleteDirectoryIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private readonly record struct PreviewRow(
        double Distance,
        double NormalizedSectionT,
        double? TargetLongitudinalG);
}
