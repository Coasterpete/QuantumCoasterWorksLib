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
        Assert.Contains("mesh-export-v1-sample [outputPath]", output);
        Assert.Contains("debug-viewport-snapshot-v1-from-csv <inputCsvPath> [outputJsonPath]", output);
        Assert.Contains("debug-viewport-snapshot-v1-gallery [artifactDirectory] [outputHtmlPath]", output);
        Assert.Contains("debug-viewport-snapshot-v1-browser [artifactDirectory] [outputHtmlPath]", output);
        Assert.Contains("debug-viewport-snapshot-v1-transition-authoring [outputPath]", output);
        Assert.Contains("debug-viewport-snapshot-v1-spatial-layout [outputPath]", output);
        Assert.Contains("debug-viewport-snapshot-v1-banking-profile [outputPath]", output);
        Assert.Contains("centerline-frame-continuity [outputPath]", output);
        Assert.Contains("transported-frame-comparison [outputPath]", output);
        Assert.Contains("transported-frame-comparison-browser [comparisonJsonPath] [outputHtmlPath]", output);
        Assert.Contains("banking-profile-diagnostics [outputPath]", output);
        Assert.Contains("continuous-roll-diagnostics-sample [outputPath]", output);
        Assert.Contains("continuous-roll-diagnostics-json [outputPath]", output);
        Assert.Contains("distance-inspection-json [outputPath]", output);
        Assert.Contains("distance-inspection-browser [outputHtmlPath]", output);
        Assert.Contains("distance-inspection-browser <inputJsonPath> <outputHtmlPath>", output);
        Assert.Contains("banking-profile-browser [diagnosticsJsonPath] [outputHtmlPath]", output);
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
        Assert.Contains("centerline samples, distance labels/ticks, curvature/radius diagnostics, frame axes, debug lines, train boxes, bogie markers, wheel markers", output);
        Assert.Contains("derive deterministic approximate curvature from neighboring centerline samples", output);
        Assert.Contains("sample measurement readout", output);
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
    public void TryWriteRequestedHelp_MeshExportV1Sample_PrintsSampleArtifactDetails()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        bool handled = DebugCommandHelp.TryWriteRequestedHelp(
            new[] { "help", "mesh-export-v1-sample" },
            writer,
            out int exitCode);

        Assert.True(handled);
        Assert.Equal(0, exitCode);

        string output = writer.ToString();
        Assert.Contains("mesh-export-v1-sample [outputPath]", output);
        Assert.Contains("deterministic MeshExportV1 sample", output);
        Assert.Contains("tiny self-authored quad mesh", output);
        Assert.Contains("not a real mesh exporter", output);
        Assert.Contains("Blender importer", output);
    }

    [Fact]
    public void TryWriteRequestedHelp_DebugViewportTransitionAuthoring_PrintsScenarioDetails()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        bool handled = DebugCommandHelp.TryWriteRequestedHelp(
            new[] { "help", "debug-viewport-snapshot-v1-transition-authoring" },
            writer,
            out int exitCode);

        Assert.True(handled);
        Assert.Equal(0, exitCode);

        string output = writer.ToString();
        Assert.Contains("debug-viewport-snapshot-v1-transition-authoring [outputPath]", output);
        Assert.Contains("straight, transition-in, constant arc, transition-out, and straight", output);
        Assert.Contains("17 frames at 3 m intervals", output);
        Assert.Contains("36, 30, 24, 18, and 12 m", output);
        Assert.Contains("reuses DebugViewportSnapshotV1 and TrainPoseExportV1 unchanged", output);
    }

    [Fact]
    public void TryWriteRequestedHelp_DebugViewportSpatialLayout_PrintsScenarioDetails()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        bool handled = DebugCommandHelp.TryWriteRequestedHelp(
            new[] { "help", "debug-viewport-snapshot-v1-spatial-layout" },
            writer,
            out int exitCode);

        Assert.True(handled);
        Assert.Equal(0, exitCode);

        string output = writer.ToString();
        Assert.Contains("debug-viewport-snapshot-v1-spatial-layout [outputPath]", output);
        Assert.Contains("translated 45-degree yawed start pose", output);
        Assert.Contains("25 frames at 3 m intervals", output);
        Assert.Contains("60, 54, 48, 42, 36, 30, 24, 18, and 12 m", output);
        Assert.Contains("compiled-geometry continuity diagnostics", output);
        Assert.Contains("reuses DebugViewportSnapshotV1 and TrainPoseExportV1 unchanged", output);
    }

    [Fact]
    public void TryWriteRequestedHelp_DebugViewportBankingProfile_PrintsFixtureDetails()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        bool handled = DebugCommandHelp.TryWriteRequestedHelp(
            new[] { "help", "debug-viewport-snapshot-v1-banking-profile" },
            writer,
            out int exitCode);

        Assert.True(handled);
        Assert.Equal(0, exitCode);

        string output = writer.ToString();
        Assert.Contains("debug-viewport-snapshot-v1-banking-profile [outputPath]", output);
        Assert.Contains("opt-in BankingProfile train-pose path", output);
        Assert.Contains("EvaluateTrainPose(..., BankingProfile)", output);
        Assert.Contains("TrainPoseExportV1Mapper.Export", output);
        Assert.Contains("default TrackEvaluator", output);
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
    public void TryWriteRequestedHelp_TransportedFrameComparison_PrintsDiagnosticDetails()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        bool handled = DebugCommandHelp.TryWriteRequestedHelp(
            new[] { "help", "transported-frame-comparison" },
            writer,
            out int exitCode);

        Assert.True(handled);
        Assert.Equal(0, exitCode);

        string output = writer.ToString();
        Assert.Contains("transported-frame-comparison [outputPath]", output);
        Assert.Contains("stateless TrackEvaluator frames", output);
        Assert.Contains("canonical transported TrackEvaluator frames", output);
        Assert.Contains("per-sample deltas, summary metrics, smoothness metrics, and continuity metrics", output);
        Assert.Contains("backend-only JSON", output);
    }

    [Fact]
    public void TryWriteRequestedHelp_TransportedFrameComparisonBrowser_PrintsViewerDetails()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        bool handled = DebugCommandHelp.TryWriteRequestedHelp(
            new[] { "help", "transported-frame-comparison-browser" },
            writer,
            out int exitCode);

        Assert.True(handled);
        Assert.Equal(0, exitCode);

        string output = writer.ToString();
        Assert.Contains("transported-frame-comparison-browser [comparisonJsonPath] [outputHtmlPath]", output);
        Assert.Contains("TransportedFrameComparisonDiagnosticsExportV1 JSON", output);
        Assert.Contains("summary metrics", output);
        Assert.Contains("per-sample delta table", output);
        Assert.Contains("normal/binormal/frame/matrix delta severity", output);
        Assert.Contains("local-file-friendly HTML/SVG/vanilla JavaScript", output);
        Assert.Contains("does not change DebugViewportSnapshotV1", output);
    }

    [Fact]
    public void TryWriteRequestedHelp_BankingProfileDiagnostics_PrintsDiagnosticDetails()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        bool handled = DebugCommandHelp.TryWriteRequestedHelp(
            new[] { "help", "banking-profile-diagnostics" },
            writer,
            out int exitCode);

        Assert.True(handled);
        Assert.Equal(0, exitCode);

        string output = writer.ToString();
        Assert.Contains("banking-profile-diagnostics [outputPath]", output);
        Assert.Contains("BankingProfile roll sampling diagnostics", output);
        Assert.Contains("roll radians, roll degrees", output);
        Assert.Contains("approximate roll slope", output);
        Assert.Contains("does not change TrackEvaluator", output);
    }

    [Fact]
    public void TryWriteRequestedHelp_ContinuousRollDiagnosticsSample_PrintsDiagnosticDetails()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        bool handled = DebugCommandHelp.TryWriteRequestedHelp(
            new[] { "help", "continuous-roll-diagnostics-sample" },
            writer,
            out int exitCode);

        Assert.True(handled);
        Assert.Equal(0, exitCode);

        string output = writer.ToString();
        Assert.Contains("continuous-roll-diagnostics-sample [outputPath]", output);
        Assert.Contains("ContinuousRollDiagnostics", output);
        Assert.Contains("adjacent roll deltas", output);
        Assert.Contains("359 degrees to 1 degree", output);
        Assert.Contains("does not change DebugViewportSnapshotV1", output);
    }

    [Fact]
    public void TryWriteRequestedHelp_ContinuousRollDiagnosticsJson_PrintsArtifactDetails()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        bool handled = DebugCommandHelp.TryWriteRequestedHelp(
            new[] { "help", "continuous-roll-diagnostics-json" },
            writer,
            out int exitCode);

        Assert.True(handled);
        Assert.Equal(0, exitCode);

        string output = writer.ToString();
        Assert.Contains("continuous-roll-diagnostics-json [outputPath]", output);
        Assert.Contains("quantum.continuous_roll_diagnostics version 1", output);
        Assert.Contains("System.Text.Json camelCase", output);

        Assert.Contains("Quantum.Tests/IO/Fixtures/ContinuousRollDiagnosticsExportV1.golden.json", output);

        Assert.Contains("mapped from ContinuousRollDiagnostics", output);
        Assert.Contains("backend inspection artifact only", output);
        Assert.Contains("default TrackEvaluator behavior", output);
    }

    [Fact]
    public void TryWriteRequestedHelp_BankingProfileBrowser_PrintsViewerDetails()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        bool handled = DebugCommandHelp.TryWriteRequestedHelp(
            new[] { "help", "banking-profile-browser" },
            writer,
            out int exitCode);

        Assert.True(handled);
        Assert.Equal(0, exitCode);

        string output = writer.ToString();
        Assert.Contains("banking-profile-browser [diagnosticsJsonPath] [outputHtmlPath]", output);
        Assert.Contains("BankingProfileDiagnosticsExportV1 JSON", output);
        Assert.Contains("profile metadata", output);
        Assert.Contains("sample count, min/max roll, maximum roll slope", output);
        Assert.Contains("SVG graphs for roll angle and roll slope", output);
        Assert.Contains("source key markers", output);
        Assert.Contains("roll slope severity indicators", output);
        Assert.Contains("local-file-friendly HTML/SVG/vanilla JavaScript", output);
        Assert.Contains("does not change TrackFrame", output);
    }

    [Fact]
    public void TryWriteRequestedHelp_DistanceInspectionJson_PrintsSampleDetails()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        bool handled = DebugCommandHelp.TryWriteRequestedHelp(
            new[] { "help", "distance-inspection-json" },
            writer,
            out int exitCode);

        Assert.True(handled);
        Assert.Equal(0, exitCode);

        string output = writer.ToString();
        Assert.Contains("distance-inspection-json [outputPath]", output);
        Assert.Contains("DistanceInspectionSnapshotV1 sample", output);
        Assert.Contains("distance-domain Force and Geometry sections", output);
        Assert.Contains("DistanceInspectionSnapshotV1Mapper", output);
        Assert.Contains("DistanceInspectionSnapshotV1Json", output);
        Assert.Contains("does not change evaluator behavior", output);
    }

    [Fact]
    public void TryWriteRequestedHelp_DistanceInspectionBrowser_PrintsPreviewDetails()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        bool handled = DebugCommandHelp.TryWriteRequestedHelp(
            new[] { "help", "distance-inspection-browser" },
            writer,
            out int exitCode);

        Assert.True(handled);
        Assert.Equal(0, exitCode);

        string output = writer.ToString();
        Assert.Contains("distance-inspection-browser [outputHtmlPath]", output);
        Assert.Contains("distance-inspection-browser <inputJsonPath> <outputHtmlPath>", output);
        Assert.Contains("DistanceInspectionSnapshotV1 sample", output);
        Assert.Contains("DistanceInspectionJsonCommand.BuildSample()", output);
        Assert.Contains("existing DistanceInspectionSnapshotV1 JSON artifact", output);
        Assert.Contains("two-argument form", output);
        Assert.Contains("ordered section cards", output);
        Assert.Contains("channelValues tables", output);
        Assert.Contains("self-contained, static, escaped", output);
        Assert.Contains("does not change evaluator behavior", output);
    }

    [Fact]
    public void WriteUnknownCommand_PrintsUnknownCommandAndSupportedCommands()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        DebugCommandHelp.WriteUnknownCommand(writer);

        string output = writer.ToString();
        Assert.Contains("Unknown command.", output);
        Assert.Contains("Supported commands:", output);
        Assert.Contains("mesh-export-v1-sample [outputPath]", output);
        Assert.Contains("debug-viewport-snapshot-v1 [outputPath]", output);
        Assert.Contains("debug-viewport-snapshot-v1-gallery [artifactDirectory] [outputHtmlPath]", output);
        Assert.Contains("debug-viewport-snapshot-v1-browser [artifactDirectory] [outputHtmlPath]", output);
        Assert.Contains("debug-viewport-snapshot-v1-spatial-layout [outputPath]", output);
        Assert.Contains("debug-viewport-snapshot-v1-banking-profile [outputPath]", output);
        Assert.Contains("longitudinal-speed-preview [preset] [outputPath] [initialSpeedMps]", output);
        Assert.Contains("centerline-frame-continuity [outputPath]", output);
        Assert.Contains("transported-frame-comparison [outputPath]", output);
        Assert.Contains("transported-frame-comparison-browser [comparisonJsonPath] [outputHtmlPath]", output);
        Assert.Contains("banking-profile-diagnostics [outputPath]", output);
        Assert.Contains("continuous-roll-diagnostics-sample [outputPath]", output);
        Assert.Contains("continuous-roll-diagnostics-json [outputPath]", output);
        Assert.Contains("distance-inspection-json [outputPath]", output);
        Assert.Contains("distance-inspection-browser [outputHtmlPath]", output);
        Assert.Contains("distance-inspection-browser <inputJsonPath> <outputHtmlPath>", output);
        Assert.Contains("banking-profile-browser [diagnosticsJsonPath] [outputHtmlPath]", output);
    }
}
