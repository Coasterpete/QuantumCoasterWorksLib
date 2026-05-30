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
        Assert.Contains("debug-viewport-snapshot-v1-gallery [artifactDirectory] [outputHtmlPath]", output);
        Assert.Contains("debug-viewport-snapshot-v1-browser [artifactDirectory] [outputHtmlPath]", output);
        Assert.Contains("centerline-frame-continuity [outputPath]", output);
        Assert.Contains("Examples:", output);
        Assert.Contains(DebugCommandHelp.GeneratedArtifactsNote, output);
        Assert.Contains("Geometry Interchange Roadmap:", output);
        Assert.Contains(DebugCommandHelp.GeometryInterchangeRoadmapNote, output);
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
    public void TryWriteRequestedHelp_DebugViewportGallery_PrintsStaticGalleryDetails()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        bool handled = DebugCommandHelp.TryWriteRequestedHelp(
            new[] { "help", "debug-viewport-snapshot-v1-gallery" },
            writer,
            out int exitCode);

        Assert.True(handled);
        Assert.Equal(0, exitCode);

        string output = writer.ToString();
        Assert.Contains("debug-viewport-snapshot-v1-gallery [artifactDirectory] [outputHtmlPath]", output);
        Assert.Contains("Defaults to artifacts/debug-viewport", output);
        Assert.Contains("links source JSON/SVG files", output);
        Assert.Contains("static local debug artifact", output);
    }

    [Fact]
    public void TryWriteRequestedHelp_DebugViewportBrowser_PrintsBrowserViewerDetails()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        bool handled = DebugCommandHelp.TryWriteRequestedHelp(
            new[] { "help", "debug-viewport-snapshot-v1-browser" },
            writer,
            out int exitCode);

        Assert.True(handled);
        Assert.Equal(0, exitCode);

        string output = writer.ToString();
        Assert.Contains("debug-viewport-snapshot-v1-browser [artifactDirectory] [outputHtmlPath]", output);
        Assert.Contains("Defaults to artifacts/debug-viewport", output);
        Assert.Contains("centerline samples, frame axes, debug lines, train boxes, bogie markers, wheel markers", output);
        Assert.Contains("backend-only debug aid", output);
        Assert.Contains("not a final editor, frontend, renderer, or JSON contract change", output);
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
    public void TryWriteRequestedHelp_CenterlineFrameContinuity_PrintsDiagnosticDetails()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        bool handled = DebugCommandHelp.TryWriteRequestedHelp(
            new[] { "help", "centerline-frame-continuity" },
            writer,
            out int exitCode);

        Assert.True(handled);
        Assert.Equal(0, exitCode);

        string output = writer.ToString();
        Assert.Contains("centerline-frame-continuity [outputPath]", output);
        Assert.Contains("deterministic centerline", output);
        Assert.Contains("tangent, normal, binormal, roll, and matrix orientation continuity", output);
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
        Assert.Contains("debug-viewport-snapshot-v1-gallery [artifactDirectory] [outputHtmlPath]", output);
        Assert.Contains("debug-viewport-snapshot-v1-browser [artifactDirectory] [outputHtmlPath]", output);
        Assert.Contains("longitudinal-speed-preview [preset] [outputPath] [initialSpeedMps]", output);
        Assert.Contains("centerline-frame-continuity [outputPath]", output);
    }
}
