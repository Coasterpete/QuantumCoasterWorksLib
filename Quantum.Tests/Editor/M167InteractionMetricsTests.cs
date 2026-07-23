using System.Diagnostics;
using Quantum.Application.Authoring;
using Quantum.Editor.Avalonia.Services;
using Quantum.Editor.Avalonia.Services.Authoring;
using Quantum.Editor.Avalonia.Services.Documents;
using Quantum.IO.TrackLayout.V2;
using Quantum.Track.Authoring;
using Xunit.Abstractions;

namespace Quantum.Tests.Editor;

public sealed class M167InteractionMetricsTests
{
    private readonly ITestOutputHelper output;

    public M167InteractionMetricsTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Theory]
    [InlineData("small", 1)]
    [InlineData("realistic", 40)]
    [InlineData("long", 160)]
    public async Task RepresentativeSerializedBackgroundInteractionMetrics(
        string routeName,
        int sectionCount)
    {
        using var workspace = new EditorWorkspace();
        TrackEditorDocument document = CreateDocument(routeName, sectionCount);
        workspace.Documents.SetActiveDocument(document);
        Assert.True(workspace.BeginStraightLengthEdit("section-000"));
        const int updateCount = 60;
        var interaction = Stopwatch.StartNew();

        for (int burst = 0; burst < 5; burst++)
        {
            var completions = new List<Task<AuthoringEvaluationOutcome>>();
            for (int offset = 0; offset < 10; offset++)
            {
                int update = (burst * 10) + offset + 1;
                workspace.RecordStraightLengthPointerUpdate();
                completions.Add(workspace.SubmitStraightLengthEdit(
                    10.0 + (update * 0.05)).Completion);
            }

            AuthoringEvaluationOutcome[] outcomes = await Task.WhenAll(completions);
            foreach (AuthoringEvaluationOutcome outcome in outcomes)
            {
                workspace.PublishStraightLengthOutcome(outcome);
            }
        }

        var finalCompletions = new List<Task<AuthoringEvaluationOutcome>>();
        for (int update = 51; update <= updateCount; update++)
        {
            workspace.RecordStraightLengthPointerUpdate();
            finalCompletions.Add(workspace.SubmitStraightLengthEdit(
                10.0 + (update * 0.05)).Completion);
        }

        AuthoringScheduledCommitResult commit =
            (await workspace.CommitStraightLengthEditAsync())!;
        await Task.WhenAll(finalCompletions);
        interaction.Stop();
        StraightLengthInteractionMetrics metrics = workspace.CaptureStraightLengthMetrics();
        double meanPresentMilliseconds = metrics.AcceptedPreviews == 0
            ? 0.0
            : metrics.SubmitToPresentLatency.TotalMilliseconds / metrics.AcceptedPreviews;

        output.WriteLine(
            $"{routeName}: sections={sectionCount}, raw={metrics.RawPointerUpdates}, " +
            $"submitted={metrics.SubmittedEvaluations}, coalesced={metrics.CoalescedUpdates}, " +
            $"started={metrics.StartedEvaluations}, accepted-previews={metrics.AcceptedPreviews}, " +
            $"stale={metrics.StaleCompletions}, mean-submit-to-present-ms={meanPresentMilliseconds:F3}, " +
            $"commit-wait-ms={metrics.FinalCommitWaitLatency.TotalMilliseconds:F3}, " +
            $"compiler-invocations={metrics.CompilerInvocationCount}, " +
            $"interaction-ms={interaction.Elapsed.TotalMilliseconds:F3}");

        Assert.True(commit.Succeeded);
        Assert.Equal(updateCount, metrics.RawPointerUpdates);
        Assert.Equal(updateCount, metrics.SubmittedEvaluations);
        Assert.InRange(metrics.AcceptedPreviews, 1, 5);
        Assert.InRange(metrics.StartedEvaluations, 1, updateCount);
        Assert.Equal(metrics.StartedEvaluations, metrics.CompilerInvocationCount);
        Assert.Equal(13.0, StraightLength(document), 9);
        Assert.True(workspace.UndoRedo.CanUndo);
    }

    private static TrackEditorDocument CreateDocument(string routeName, int sectionCount)
    {
        TrackLayoutSectionV2Dto[] sections = Enumerable.Range(0, sectionCount)
            .Select(index => new TrackLayoutSectionV2Dto
            {
                Kind = TrackLayoutPackageV2Vocabulary.StraightSectionKind,
                Id = $"section-{index:D3}",
                Length = 10.0
            })
            .ToArray();
        return TrackEditorDocument.Create(
            new TrackLayoutPackageV2Dto
            {
                Metadata = new TrackLayoutMetadataV2Dto
                {
                    Units = "meters",
                    SourceName = $"M167.4 {routeName} metrics fixture",
                    LayoutId = $"m167-4-{routeName}-metrics"
                },
                Sections = sections
            },
            $"M167.4 {routeName} metrics fixture");
    }

    private static double StraightLength(TrackEditorDocument document)
    {
        return Assert.IsType<StraightSectionDefinition>(
            document.Graph!.Nodes.Single(node => node.Id == "section-000").Section).Length;
    }
}
