using System.IO;
using Quantum.Debug;
using Quantum.IO.ContinuousRollDiagnostics.V1;
using SystemMath = System.Math;

namespace Quantum.Tests;

public sealed class ContinuousRollDiagnosticsJsonCommandTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void Run_WithExplicitOutputPath_WritesValidDeterministicJsonArtifact()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "continuous-roll-diagnostics.sample.json");

        try
        {
            int exitCode = ContinuousRollDiagnosticsJsonCommand.Run(outputPath);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));

            string json = File.ReadAllText(outputPath);
            ContinuousRollDiagnosticsExportV1Dto artifact =
                ContinuousRollDiagnosticsExportV1Json.Deserialize(json);

            Assert.Equal(ContinuousRollDiagnosticsExportV1Dto.ContractName, artifact.Contract);
            Assert.Equal(ContinuousRollDiagnosticsExportV1Dto.ContractVersion, artifact.Version);
            Assert.Equal(7, artifact.SampleCount);
            Assert.Equal(7, artifact.Samples.Length);
            Assert.True(artifact.WrapHandlingEnabled);
            Assert.Equal(1, artifact.WarningCount);
            AssertNear(2.0, artifact.Samples[3].DeltaDegrees);
            AssertNear(361.0, artifact.Samples[3].RollDegrees);
            Assert.NotNull(artifact.Samples[6].Warning);
            Assert.Contains("RollDelta exceeded", artifact.Samples[6].Warning, StringComparison.Ordinal);
            Assert.Contains("\"contract\": \"quantum.continuous_roll_diagnostics\"", json);
            Assert.Contains("\"warning\": \"RollDelta exceeded", json);
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
        string firstOutputPath = Path.Combine(tempDirectory, "continuous-roll-diagnostics.first.json");
        string secondOutputPath = Path.Combine(tempDirectory, "continuous-roll-diagnostics.second.json");

        try
        {
            int firstExitCode = ContinuousRollDiagnosticsJsonCommand.Run(firstOutputPath);
            int secondExitCode = ContinuousRollDiagnosticsJsonCommand.Run(secondOutputPath);

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
    public void Run_WithExplicitOutputPath_MatchesGoldenRegressionFixture()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "continuous-roll-diagnostics.sample.json");

        try
        {
            int exitCode = ContinuousRollDiagnosticsJsonCommand.Run(outputPath);

            Assert.Equal(0, exitCode);

            string actual = NormalizeLineEndings(File.ReadAllText(outputPath)).TrimEnd();
            string expected = NormalizeLineEndings(LoadGoldenFixtureJson()).TrimEnd();

            Assert.Equal(expected, actual);
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
            "QuantumCoasterWorks.ContinuousRollDiagnosticsJsonCommandTests",
            Guid.NewGuid().ToString("N"));
    }


    private static string LoadGoldenFixtureJson()
    {
        string fixturePath = Path.Combine(AppContext.BaseDirectory, "IO", "Fixtures", "ContinuousRollDiagnosticsExportV1.golden.json");
        Assert.True(File.Exists(fixturePath), $"Golden fixture file was not found at '{fixturePath}'.");
        return File.ReadAllText(fixturePath);
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.ReplaceLineEndings("\n");
    }


    private static void DeleteDirectoryIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void AssertNear(double expected, double actual)
    {
        Assert.InRange(SystemMath.Abs(expected - actual), 0.0, Tolerance);
    }
}
