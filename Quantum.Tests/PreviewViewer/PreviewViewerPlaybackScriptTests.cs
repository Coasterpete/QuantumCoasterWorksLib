using System;
using System.IO;

namespace Quantum.Tests;

public sealed class PreviewViewerPlaybackScriptTests
{
    [Fact]
    public void AppScript_WiresSmoothPlaybackSamplingWithoutRemovingTrainStyleControls()
    {
        string repositoryRoot = FindRepositoryRoot();
        string appScript = File.ReadAllText(
            Path.Combine(repositoryRoot, "Quantum.PreviewViewer", "wwwroot", "app.js"));
        string styleManifest = File.ReadAllText(
            Path.Combine(repositoryRoot, "Quantum.PreviewViewer", "preview-styles.json"));

        Assert.Contains("function interpolateSmoothFrame", appScript);
        Assert.Contains("function interpolateSmoothCenterlineFrame", appScript);
        Assert.Contains("function rebuildEvaluatedCenterline", appScript);
        Assert.Contains("function frameFromEvaluatedCenterline", appScript);
        Assert.Contains("function framePositionDerivative", appScript);
        Assert.Contains("function cubicHermitePosition", appScript);
        Assert.Contains("sampleFrame(state.currentDistance + box.offset)", appScript);
        Assert.Contains("state.evaluatedCenterlineFrames.length > 0", appScript);
        Assert.Contains("[\"Playback sampling\", describePlaybackSampling()]", appScript);
        Assert.Contains("state.selectedTrainStyleId = ui.styleSelect.value", appScript);
        Assert.Contains("\"debug-boxes\"", styleManifest);
    }

    [Fact]
    public void AppScript_AdvancesPlaybackContinuouslyFromAnimationFrameDelta()
    {
        string repositoryRoot = FindRepositoryRoot();
        string appScript = File.ReadAllText(
            Path.Combine(repositoryRoot, "Quantum.PreviewViewer", "wwwroot", "app.js"));
        string indexHtml = File.ReadAllText(
            Path.Combine(repositoryRoot, "Quantum.PreviewViewer", "wwwroot", "index.html"));
        string tickBody = ExtractFunctionBody(appScript, "tick", "resizeRenderer");

        Assert.Contains("requestAnimationFrame(tick)", appScript);
        Assert.Contains("advancePlayback(deltaSeconds)", tickBody);
        Assert.Contains("function advancePlayback(deltaSeconds)", appScript);
        Assert.Contains("state.currentDistance + speed * deltaSeconds", appScript);
        Assert.Contains("wrapDistance(", appScript);
        Assert.Contains("ui.distanceScrubber.step = \"any\"", appScript);
        Assert.Contains("step=\"0.1\"", indexHtml);
        Assert.DoesNotContain("setInterval(", appScript);
        Assert.DoesNotContain("findSegmentIndex", tickBody);
        Assert.DoesNotContain("frameDistances", tickBody);
        Assert.DoesNotContain("pointDistances", tickBody);
    }

    [Fact]
    public void AppScript_BuildsSmoothCenterlineDisplayWhileKeepingRawDebugMode()
    {
        string repositoryRoot = FindRepositoryRoot();
        string appScript = File.ReadAllText(
            Path.Combine(repositoryRoot, "Quantum.PreviewViewer", "wwwroot", "app.js"));
        string indexHtml = File.ReadAllText(
            Path.Combine(repositoryRoot, "Quantum.PreviewViewer", "wwwroot", "index.html"));

        Assert.Contains("centerlineModeInputs", appScript);
        Assert.Contains("state.centerlineMode = input.value === \"raw\" ? \"raw\" : \"smooth\"", appScript);
        Assert.Contains("state.evaluatedCenterlineFrames.map((sample) => sample.position)", appScript);
        Assert.Contains("rawLine.userData.centerlineDisplay = \"raw\"", appScript);
        Assert.Contains("smoothLine.userData.centerlineDisplay = \"smooth\"", appScript);
        Assert.Contains("[\"Raw centerline samples\", formatCount(state.points.length)]", appScript);
        Assert.Contains("[\"Smoothed centerline samples\", formatCount(state.evaluatedCenterlineFrames.length)]", appScript);
        Assert.Contains("name=\"centerlineMode\" type=\"radio\" value=\"smooth\"", indexHtml);
        Assert.Contains("name=\"centerlineMode\" type=\"radio\" value=\"raw\"", indexHtml);
    }

    private static string ExtractFunctionBody(string source, string functionName, string nextFunctionName)
    {
        int start = source.IndexOf(
            "function " + functionName + "(",
            StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find function {functionName}.");

        int end = source.IndexOf(
            "function " + nextFunctionName + "(",
            start,
            StringComparison.Ordinal);
        Assert.True(end > start, $"Could not find function {nextFunctionName} after {functionName}.");

        return source[start..end];
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "QuantumCoasterWorks.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate QuantumCoasterWorks.sln.");
    }
}
