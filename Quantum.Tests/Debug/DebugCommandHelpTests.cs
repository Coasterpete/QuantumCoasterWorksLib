using System.Globalization;
using System.IO;
using Quantum.Debug;

namespace Quantum.Tests;

public sealed class DebugCommandHelpTests
{
    [Theory]
    [InlineData("help")]
    [InlineData("--help")]
    [InlineData("-h")]
    public void TryWriteRequestedHelp_TopLevelHelpTokens_PrintGeneralHelp(string token)
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        bool handled = DebugCommandHelp.TryWriteRequestedHelp(new[] { token }, writer, out int exitCode);

        Assert.True(handled);
        Assert.Equal(0, exitCode);

        string output = writer.ToString();
        Assert.Contains(DebugCommandHelp.ProjectPurpose, output);
        Assert.Contains("Commands:", output);
        Assert.Contains("debug-viewport-snapshot-v1-from-csv <inputCsvPath> [outputJsonPath]", output);
        Assert.Contains("Examples:", output);
        Assert.Contains(DebugCommandHelp.GeneratedArtifactsNote, output);
    }

    [Fact]
    public void TryWriteRequestedHelp_CommandSpecificOption_PrintsCommandHelp()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        bool handled = DebugCommandHelp.TryWriteRequestedHelp(
            new[] { "debug-viewport-snapshot-v1", "--help" },
            writer,
            out int exitCode);

        Assert.True(handled);
        Assert.Equal(0, exitCode);

        string output = writer.ToString();
        Assert.Contains("Command:", output);
        Assert.Contains("debug-viewport-snapshot-v1", output);
        Assert.Contains("Arguments:", output);
        Assert.Contains("outputPath: Optional JSON output path.", output);
        Assert.Contains("dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1", output);
        Assert.Contains(DebugCommandHelp.GeneratedArtifactsNote, output);
    }

    [Fact]
    public void TryWriteRequestedHelp_HelpCommandWithCommandName_PrintsCommandHelp()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        bool handled = DebugCommandHelp.TryWriteRequestedHelp(
            new[] { "help", "debug-viewport-snapshot-v1-validate" },
            writer,
            out int exitCode);

        Assert.True(handled);
        Assert.Equal(0, exitCode);

        string output = writer.ToString();
        Assert.Contains("debug-viewport-snapshot-v1-validate", output);
        Assert.Contains("snapshotJsonPath: Required DebugViewportSnapshotV1 JSON path.", output);
    }

    [Fact]
    public void TryWriteRequestedHelp_TrainPoseExportV1_PrintsRegressionSampleDetails()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        bool handled = DebugCommandHelp.TryWriteRequestedHelp(
            new[] { "help", "train-pose-export-v1" },
            writer,
            out int exitCode);

        Assert.True(handled);
        Assert.Equal(0, exitCode);

        string output = writer.ToString();
        Assert.Contains("train-pose-export-v1 [outputPath]", output);
        Assert.Contains("deterministic TrainPoseExportV1 regression sample", output);
        Assert.Contains("Quantum.Tests/IO/Fixtures/TrainPoseExportV1.golden.json", output);
        Assert.Contains("backend-only JSON", output);
    }

    [Fact]
    public void WriteUnknownCommand_PrintsUnknownCommandAndSupportedCommands()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        DebugCommandHelp.WriteUnknownCommand(writer);

        string output = writer.ToString();
        Assert.Contains("Unknown command.", output);
        Assert.Contains("Supported commands:", output);
        Assert.Contains("debug-viewport-snapshot-v1 [outputPath]", output);
        Assert.Contains("longitudinal-speed-preview [preset] [outputPath] [initialSpeedMps]", output);
    }
}
