using Quantum.Application.Authoring;
using Quantum.Editor.Avalonia.Services;
using Quantum.Editor.Avalonia.Services.Authoring;
using Quantum.Editor.Avalonia.Services.Documents;
using Quantum.IO.TrackLayout.V2;
using Quantum.Track.Authoring;
using Xunit.Abstractions;

namespace Quantum.Tests.Editor;

/// <summary>
/// Repeat with:
/// dotnet test Quantum.Tests/Quantum.Tests.csproj -c Release --filter M167ReleaseLiveEditMeasurementTests
/// Results are observational and intentionally have no latency product gates.
/// </summary>
public sealed class M167ReleaseLiveEditMeasurementTests
{
    private const int Repetitions = 4;
    private const int UpdatesPerInteraction = 24;
    private readonly ITestOutputHelper output;

    public M167ReleaseLiveEditMeasurementTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    public static IEnumerable<object[]> Scenarios()
    {
        yield return new object[] { "small", 1, RouteShape.Straight };
        yield return new object[] { "realistic", 40, RouteShape.Straight };
        yield return new object[] { "long", 160, RouteShape.Straight };
        yield return new object[] { "mixed-geometry", 40, RouteShape.Mixed };
        yield return new object[] { "spatial", 24, RouteShape.Spatial };
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task CollectReleaseInteractionMetrics(
        string routeName,
        int sectionCount,
        RouteShape shape)
    {
        long submitted = 0;
        long coalesced = 0;
        long started = 0;
        long acceptedPreviews = 0;
        long stale = 0;
        int compilerInvocations = 0;
        var submitToPresentSamples = new List<TimeSpan>();
        var finalCommitWaitSamples = new List<TimeSpan>();

        for (int repetition = 0; repetition < Repetitions; repetition++)
        {
            await using var workspace = new EditorWorkspace();
            TrackEditorDocument document = CreateDocument(routeName, sectionCount, shape);
            workspace.Documents.SetActiveDocument(document);
            Assert.True(workspace.BeginStraightLengthEdit("section-000"));

            for (int burst = 0; burst < 2; burst++)
            {
                var completions = new List<Task<AuthoringEvaluationOutcome>>();
                for (int offset = 0; offset < 8; offset++)
                {
                    int update = (burst * 8) + offset + 1;
                    workspace.RecordStraightLengthPointerUpdate();
                    completions.Add(workspace.SubmitStraightLengthEdit(
                        10.0 + (update * 0.05)).Completion);
                }

                foreach (AuthoringEvaluationOutcome outcome in await Task.WhenAll(completions))
                {
                    workspace.PublishStraightLengthOutcome(outcome);
                }
            }

            var finalCompletions = new List<Task<AuthoringEvaluationOutcome>>();
            for (int update = 17; update <= UpdatesPerInteraction; update++)
            {
                workspace.RecordStraightLengthPointerUpdate();
                finalCompletions.Add(workspace.SubmitStraightLengthEdit(
                    10.0 + (update * 0.05)).Completion);
            }

            AuthoringScheduledCommitResult commit =
                (await workspace.CommitStraightLengthEditAsync())!;
            await Task.WhenAll(finalCompletions);
            StraightLengthInteractionMetrics metrics =
                workspace.CaptureStraightLengthMetrics();

            Assert.True(commit.Succeeded);
            Assert.Equal(11.2, StraightLength(document), 9);
            submitted += metrics.SubmittedEvaluations;
            coalesced += metrics.CoalescedUpdates;
            started += metrics.StartedEvaluations;
            acceptedPreviews += metrics.AcceptedPreviews;
            stale += metrics.StaleCompletions;
            compilerInvocations += metrics.CompilerInvocationCount;
            submitToPresentSamples.AddRange(metrics.SubmitToPresentLatencySamples);
            finalCommitWaitSamples.AddRange(metrics.FinalCommitWaitLatencySamples);
        }

        LatencyPercentileSummary present =
            LatencyPercentiles.Calculate(submitToPresentSamples);
        LatencyPercentileSummary finalWait =
            LatencyPercentiles.Calculate(finalCommitWaitSamples);
        string configuration =
#if DEBUG
            "Debug";
#else
            "Release";
#endif
        output.WriteLine(
            $"configuration={configuration}, route={routeName}, shape={shape}, sections={sectionCount}, " +
            $"repetitions={Repetitions}, submitted={submitted}, coalesced={coalesced}, " +
            $"started={started}, compiler-invocations={compilerInvocations}, " +
            $"accepted-previews={acceptedPreviews}, stale={stale}, " +
            $"submit-to-present-samples={present.SampleCount}, " +
            $"submit-to-present-p50-ms={present.P50.TotalMilliseconds:F3}, " +
            $"p95-ms={present.P95.TotalMilliseconds:F3}, p99-ms={present.P99.TotalMilliseconds:F3}, " +
            $"final-wait-samples={finalWait.SampleCount}, " +
            $"final-wait-p50-ms={finalWait.P50.TotalMilliseconds:F3}, " +
            $"p95-ms={finalWait.P95.TotalMilliseconds:F3}, p99-ms={finalWait.P99.TotalMilliseconds:F3}");

        Assert.Equal(Repetitions * UpdatesPerInteraction, submitted);
        Assert.Equal(started, compilerInvocations);
        Assert.True(coalesced > 0);
        Assert.True(acceptedPreviews > 0);
        Assert.Equal(Repetitions, finalWait.SampleCount);
        Assert.True(present.SampleCount > 0);
    }

    private static TrackEditorDocument CreateDocument(
        string routeName,
        int sectionCount,
        RouteShape shape)
    {
        TrackLayoutSectionV2Dto[] sections = Enumerable.Range(0, sectionCount)
            .Select(index => CreateSection(index, shape))
            .ToArray();
        return TrackEditorDocument.Create(
            new TrackLayoutPackageV2Dto
            {
                Metadata = new TrackLayoutMetadataV2Dto
                {
                    Units = "meters",
                    SourceName = $"M167.5 {routeName} Release measurement fixture",
                    LayoutId = $"m167-5-{routeName}-release"
                },
                Sections = sections
            },
            $"M167.5 {routeName} Release measurement fixture");
    }

    private static TrackLayoutSectionV2Dto CreateSection(int index, RouteShape shape)
    {
        string id = $"section-{index:D3}";
        if (index == 0 || shape == RouteShape.Straight)
        {
            return new TrackLayoutSectionV2Dto
            {
                Kind = TrackLayoutPackageV2Vocabulary.StraightSectionKind,
                Id = id,
                Length = 10.0
            };
        }

        if (shape == RouteShape.Spatial)
        {
            return new TrackLayoutSectionV2Dto
            {
                Kind = TrackLayoutPackageV2Vocabulary.SpatialSectionKind,
                Id = id,
                Length = 6.0,
                Degree = 3,
                ControlPoints = new[]
                {
                    Point(0.0, 0.0, 0.0),
                    Point(2.0, 0.0, 0.0),
                    Point(4.0, 0.0, 0.0),
                    Point(6.0, 0.0, 0.0)
                },
                Weights = new[] { 1.0, 0.8, 1.2, 1.0 }
            };
        }

        return (index % 3) switch
        {
            1 => new TrackLayoutSectionV2Dto
            {
                Kind = TrackLayoutPackageV2Vocabulary.ConstantCurvatureSectionKind,
                Id = id,
                Length = 12.0,
                Radius = index % 2 == 0 ? -30.0 : 30.0,
                RollRadians = 0.05
            },
            2 => new TrackLayoutSectionV2Dto
            {
                Kind = TrackLayoutPackageV2Vocabulary.CurvatureTransitionSectionKind,
                Id = id,
                Length = 8.0,
                StartCurvature = 0.02,
                EndCurvature = -0.01,
                InterpolationMode =
                    TrackLayoutPackageV2Vocabulary.CurvatureInterpolationLinear,
                RollRadians = -0.03
            },
            _ => new TrackLayoutSectionV2Dto
            {
                Kind = TrackLayoutPackageV2Vocabulary.StraightSectionKind,
                Id = id,
                Length = 10.0
            }
        };
    }

    private static TrackLayoutVector3dV2Dto Point(double x, double y, double z) =>
        new() { X = x, Y = y, Z = z };

    private static double StraightLength(TrackEditorDocument document)
    {
        return Assert.IsType<StraightSectionDefinition>(
            document.Graph!.Nodes.Single(node => node.Id == "section-000").Section).Length;
    }

    public enum RouteShape
    {
        Straight,
        Mixed,
        Spatial
    }
}
