using System.IO;
using Quantum.Debug;

namespace Quantum.Tests;

public sealed class ContinuousRollDiagnosticsSampleCommandTests
{
    [Fact]
    public void Run_WithExplicitOutputPath_WritesDeterministicSampleReport()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "continuous-roll-diagnostics.sample.txt");

        try
        {
            int exitCode = ContinuousRollDiagnosticsSampleCommand.Run(outputPath);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));

            string report = File.ReadAllText(outputPath);
            Assert.Contains("Continuous roll diagnostics sample", report);
            Assert.Contains("source=continuous-roll-diagnostics-sample", report);
            Assert.Contains("units=meters,radians", report);
            Assert.Contains("wrapMode=FullTurn", report);
            Assert.Contains("warnings=1", report);
            Assert.Contains("2,3,10.000,-358.000,2.000,0.200,True", report);
            Assert.Contains("5,6,10.000,100.000,100.000,10.000,False", report);
            Assert.Contains("RollDelta,5,6,100.000,45.000", report);
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
        string firstOutputPath = Path.Combine(tempDirectory, "continuous-roll-diagnostics.first.txt");
        string secondOutputPath = Path.Combine(tempDirectory, "continuous-roll-diagnostics.second.txt");

        try
        {
            int firstExitCode = ContinuousRollDiagnosticsSampleCommand.Run(firstOutputPath);
            int secondExitCode = ContinuousRollDiagnosticsSampleCommand.Run(secondOutputPath);

            Assert.Equal(0, firstExitCode);
            Assert.Equal(0, secondExitCode);

            string firstReport = File.ReadAllText(firstOutputPath);
            string secondReport = File.ReadAllText(secondOutputPath);

            Assert.Equal(firstReport, secondReport);
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
            "QuantumCoasterWorks.ContinuousRollDiagnosticsSampleCommandTests",
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
