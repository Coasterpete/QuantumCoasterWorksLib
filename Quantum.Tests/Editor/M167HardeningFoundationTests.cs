using Quantum.Application.Authoring;
using Quantum.Editor.Avalonia.Services;
using Quantum.Editor.Avalonia.Services.Authoring;
using Quantum.Editor.Avalonia.Services.Documents;
using Quantum.IO.TrackLayout.V2;
using Quantum.Track.Authoring;

namespace Quantum.Tests.Editor;

public sealed class M167HardeningFoundationTests
{
    [Fact]
    public void LatencyPercentilesUseDocumentedNearestRankCalculation()
    {
        TimeSpan[] samples = Enumerable.Range(1, 100)
            .Select(value => TimeSpan.FromMilliseconds(value))
            .Reverse()
            .ToArray();

        LatencyPercentileSummary result = LatencyPercentiles.Calculate(samples);

        Assert.Equal(100, result.SampleCount);
        Assert.Equal(TimeSpan.FromMilliseconds(50), result.P50);
        Assert.Equal(TimeSpan.FromMilliseconds(95), result.P95);
        Assert.Equal(TimeSpan.FromMilliseconds(99), result.P99);
        Assert.Equal(0, LatencyPercentiles.Calculate(Array.Empty<TimeSpan>()).SampleCount);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LatencyPercentiles.Calculate(new[] { TimeSpan.FromTicks(-1) }));
    }

    [Fact]
    public void SensitivityDefaultsAndConfigurationAreValidated()
    {
        StraightLengthScrubSensitivity defaults =
            StraightLengthScrubSensitivity.Default;
        Assert.Equal(0.1, defaults.Resolve(shift: false, control: false), 9);
        Assert.Equal(0.01, defaults.Resolve(shift: true, control: false), 9);
        Assert.Equal(1.0, defaults.Resolve(shift: false, control: true), 9);
        Assert.Equal(0.01, defaults.Resolve(shift: true, control: true), 9);

        var configured = new StraightLengthScrubSensitivity(0.25, 0.025, 2.5);
        Assert.Equal(0.25, configured.NormalMetersPerStep, 9);
        Assert.Equal(0.025, configured.FineMetersPerStep, 9);
        Assert.Equal(2.5, configured.CoarseMetersPerStep, 9);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StraightLengthScrubSensitivity(0.0, 0.01, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StraightLengthScrubSensitivity(0.1, double.NaN, 1.0));
    }

    [Fact]
    public async Task SessionHistoryOrdersOneShotAndLiveEditsInOneStack()
    {
        await using var workspace = new EditorWorkspace();
        TrackEditorDocument document = CreateDocument("history");
        workspace.Documents.SetActiveDocument(document);

        Assert.True(ApplyLength(workspace, 31.0, "One-shot 31"));
        Assert.True(workspace.BeginStraightLengthEdit("launch"));
        AuthoringEvaluationOutcome outcome = await workspace
            .SubmitStraightLengthEdit(33.0)
            .Completion;
        Assert.True(workspace.PublishStraightLengthOutcome(outcome));
        Assert.True((await workspace.CommitStraightLengthEditAsync())!.Succeeded);
        Assert.True(ApplyLength(workspace, 35.0, "One-shot 35"));

        Assert.Equal(3, workspace.AuthoringSession!.History.UndoCount);
        Assert.Equal("One-shot 35", workspace.UndoRedo.UndoDescription);
        Assert.True(workspace.UndoLast());
        Assert.Equal(33.0, StraightLength(document), 9);
        Assert.Equal("Edit straight length launch", workspace.UndoRedo.UndoDescription);
        Assert.True(workspace.UndoLast());
        Assert.Equal(31.0, StraightLength(document), 9);
        Assert.Equal("One-shot 31", workspace.UndoRedo.UndoDescription);
        Assert.True(workspace.UndoLast());
        Assert.Equal(30.0, StraightLength(document), 9);

        Assert.True(workspace.RedoLast());
        Assert.Equal(31.0, StraightLength(document), 9);
        Assert.True(workspace.RedoLast());
        Assert.Equal(33.0, StraightLength(document), 9);
        Assert.True(workspace.RedoLast());
        Assert.Equal(35.0, StraightLength(document), 9);
    }

    private static bool ApplyLength(
        EditorWorkspace workspace,
        double length,
        string description)
    {
        return workspace.ApplyGraphEdit(description, graph =>
        {
            StraightSectionDefinition straight = Assert.IsType<StraightSectionDefinition>(
                graph.Nodes.Single(node => node.Id == "launch").Section);
            return TrackAuthoringGraphOperations.Replace(
                graph,
                "launch",
                new StraightSectionDefinition("launch", length, straight.RollRadians));
        });
    }

    internal static TrackEditorDocument CreateDocument(string suffix)
    {
        return TrackEditorDocument.Create(
            new TrackLayoutPackageV2Dto
            {
                Metadata = new TrackLayoutMetadataV2Dto
                {
                    Units = "meters",
                    SourceName = "M167.5 hardening fixture " + suffix,
                    LayoutId = "m167-5-" + suffix
                },
                Sections = new[]
                {
                    new TrackLayoutSectionV2Dto
                    {
                        Kind = TrackLayoutPackageV2Vocabulary.StraightSectionKind,
                        Id = "launch",
                        Length = 30.0
                    }
                }
            },
            "M167.5 hardening fixture " + suffix);
    }

    internal static double StraightLength(TrackEditorDocument document)
    {
        return Assert.IsType<StraightSectionDefinition>(
            document.Graph!.Nodes.Single(node => node.Id == "launch").Section).Length;
    }
}
