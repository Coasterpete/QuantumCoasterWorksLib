using Quantum.IO.TrackLayout.V2;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class TrackLayoutPackageV2GraphAdapterTests
{
    [Fact]
    public void Import_RepresentativePackageCreatesConnectedGraphAndAncillaryState()
    {
        TrackLayoutPackageV2Dto dto = CreateRepresentativeDto();

        TrackLayoutPackageV2GraphImportResult result = TrackLayoutPackageV2GraphAdapter.Import(dto);

        Assert.True(result.Success);
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Graph);
        Assert.NotNull(result.AncillaryState);
        Assert.Equal(
            new[] { "entry", "turn", "transition", "spatial" },
            result.Graph!.Nodes.Select(node => node.Id));
        Assert.Equal(3, result.Graph.Edges.Count);
        Assert.Collection(
            result.Graph.Edges,
            edge => AssertEdge(edge, "entry", "turn"),
            edge => AssertEdge(edge, "turn", "transition"),
            edge => AssertEdge(edge, "transition", "spatial"));
        Assert.Equal(3, result.Graph.StartPose.Position.X);
        Assert.NotNull(result.Graph.Banking);
        Assert.Equal(4, result.Graph.Banking!.Keys.Count);
        Assert.Equal("meters", result.AncillaryState!.Units);
        Assert.Equal("representative graph adapter", result.AncillaryState.SourceName);
        Assert.Equal("layout.m157.graph-adapter", result.AncillaryState.LayoutId);
        Assert.True(result.AncillaryState.HeartlineOffset.HasValue);
        Assert.Equal(1.1, result.AncillaryState.HeartlineOffset!.Value.NormalOffsetMeters);
        Assert.Equal(-0.2, result.AncillaryState.HeartlineOffset.Value.LateralOffsetMeters);

        TrackAuthoringGraphCompileResult compilation =
            TrackAuthoringGraphCompiler.Compile(result.Graph);
        Assert.True(compilation.Success);
        Assert.Equal(36.0, compilation.Compilation!.TotalLength, 9);
    }

    [Fact]
    public void ImportThenExport_RepresentativePackagePreservesCanonicalV2Json()
    {
        TrackLayoutPackageV2Dto dto = CreateRepresentativeDto();
        string before = TrackLayoutPackageV2Json.Serialize(dto, indented: true);
        TrackLayoutPackageV2GraphImportResult import = TrackLayoutPackageV2GraphAdapter.Import(dto);

        TrackLayoutPackageV2GraphExportResult export = TrackLayoutPackageV2GraphAdapter.Export(
            import.Graph!,
            import.AncillaryState!);

        Assert.True(export.Success);
        Assert.Empty(export.GraphDiagnostics);
        Assert.Empty(export.PackageDiagnostics);
        Assert.NotNull(export.Package);
        string after = TrackLayoutPackageV2Json.Serialize(export.Package!, indented: true);
        Assert.Equal(before, after);
    }

    [Fact]
    public void Export_UsesGraphRouteAndReplacementSectionAsOnlySectionSource()
    {
        TrackLayoutPackageV2GraphImportResult import = TrackLayoutPackageV2GraphAdapter.Import(
            CreateRepresentativeDto());
        var replacement = new ConstantCurvatureSectionDefinition(
            "turn",
            length: 12.0,
            radius: -45.0,
            rollRadians: -0.2);
        TrackAuthoringGraph changed = import.Graph!.WithSection("turn", replacement);
        var shuffled = new TrackAuthoringGraph(
            changed.Nodes.Reverse(),
            changed.Edges,
            changed.StartPose,
            changed.Banking);

        TrackLayoutPackageV2GraphExportResult export = TrackLayoutPackageV2GraphAdapter.Export(
            shuffled,
            import.AncillaryState!);

        Assert.True(export.Success);
        Assert.Equal(
            new[] { "entry", "turn", "transition", "spatial" },
            export.Package!.Sections.Select(section => section.Id));
        Assert.Equal(-45.0, export.Package.Sections[1].Radius);
        Assert.Equal(-30.0, Assert.IsType<ConstantCurvatureSectionDefinition>(
            import.Graph.Nodes[1].Section).Radius);
    }

    [Fact]
    public void Import_InvalidPackageReturnsExistingV2DiagnosticsWithoutGraph()
    {
        TrackLayoutPackageV2Dto dto = CreateRepresentativeDto();
        dto.Sections = Array.Empty<TrackLayoutSectionV2Dto>();

        TrackLayoutPackageV2GraphImportResult result = TrackLayoutPackageV2GraphAdapter.Import(dto);

        Assert.False(result.Success);
        Assert.Null(result.Graph);
        Assert.Null(result.AncillaryState);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == TrackLayoutPackageV2ValidationCode.EmptySections);
    }

    [Fact]
    public void Export_InvalidGraphReturnsGraphDiagnosticsWithoutPackage()
    {
        var graph = new TrackAuthoringGraph(
            new[] { Node("a"), Node("b"), Node("c") },
            new[]
            {
                new TrackAuthoringGraphEdge("a", "b"),
                new TrackAuthoringGraphEdge("a", "c")
            });
        var ancillary = new TrackLayoutPackageV2GraphAncillaryState(
            TrackLayoutPackageV2Dto.ContractName,
            TrackLayoutPackageV2Dto.ContractVersion,
            "meters",
            "invalid branching graph",
            "invalid.branch",
            null);

        TrackLayoutPackageV2GraphExportResult result =
            TrackLayoutPackageV2GraphAdapter.Export(graph, ancillary);

        Assert.False(result.Success);
        Assert.Null(result.Package);
        Assert.Empty(result.PackageDiagnostics);
        Assert.Contains(
            result.GraphDiagnostics,
            diagnostic => diagnostic.Code == TrackAuthoringGraphDiagnosticCode.MultipleOutgoingEdges);
    }

    private static TrackLayoutPackageV2Dto CreateRepresentativeDto()
    {
        return new TrackLayoutPackageV2Dto
        {
            Metadata = new TrackLayoutMetadataV2Dto
            {
                Units = "meters",
                SourceName = "representative graph adapter",
                LayoutId = "layout.m157.graph-adapter"
            },
            StartPose = new TrackStartPoseV2Dto
            {
                Position = new TrackLayoutVector3dV2Dto { X = 3.0, Y = 2.0, Z = 1.0 },
                Tangent = new TrackLayoutVector3dV2Dto { X = 0.0, Y = 1.0, Z = 0.0 },
                Normal = new TrackLayoutVector3dV2Dto { X = 0.0, Y = 0.0, Z = 1.0 },
                Binormal = new TrackLayoutVector3dV2Dto { X = 1.0, Y = 0.0, Z = 0.0 }
            },
            Sections = new[]
            {
                new TrackLayoutSectionV2Dto
                {
                    Kind = TrackLayoutPackageV2Vocabulary.StraightSectionKind,
                    Id = "entry",
                    Length = 10.0,
                    RollRadians = 0.1
                },
                new TrackLayoutSectionV2Dto
                {
                    Kind = TrackLayoutPackageV2Vocabulary.ConstantCurvatureSectionKind,
                    Id = "turn",
                    Length = 12.0,
                    RollRadians = -0.2,
                    Radius = -30.0
                },
                new TrackLayoutSectionV2Dto
                {
                    Kind = TrackLayoutPackageV2Vocabulary.CurvatureTransitionSectionKind,
                    Id = "transition",
                    Length = 8.0,
                    RollRadians = 0.05,
                    StartCurvature = 0.02,
                    EndCurvature = -0.01,
                    InterpolationMode = TrackLayoutPackageV2Vocabulary.CurvatureInterpolationLinear
                },
                new TrackLayoutSectionV2Dto
                {
                    Kind = TrackLayoutPackageV2Vocabulary.SpatialSectionKind,
                    Id = "spatial",
                    Length = 6.0,
                    RollRadians = 0.25,
                    Degree = 3,
                    ControlPoints = new[]
                    {
                        Point(0.0, 0.0, 0.0),
                        Point(2.0, 0.0, 0.0),
                        Point(4.0, 0.0, 0.0),
                        Point(6.0, 0.0, 0.0)
                    },
                    Weights = new[] { 1.0, 1.0, 1.0, 1.0 }
                }
            },
            Banking = new TrackBankingV2Dto
            {
                Keys = new[]
                {
                    BankingKey(0.0, 0.0, TrackLayoutPackageV2Vocabulary.BankingInterpolationLinear),
                    BankingKey(10.0, 0.2, TrackLayoutPackageV2Vocabulary.BankingInterpolationSmoothStep),
                    BankingKey(22.0, -0.35, TrackLayoutPackageV2Vocabulary.BankingInterpolationSinusoidal),
                    BankingKey(36.0, 0.1, TrackLayoutPackageV2Vocabulary.BankingInterpolationConstant)
                }
            },
            Heartline = new TrackHeartlineV2Dto
            {
                NormalOffset = 1.1,
                LateralOffset = -0.2
            }
        };
    }

    private static TrackAuthoringGraphNode Node(string id)
    {
        return new TrackAuthoringGraphNode(new StraightSectionDefinition(id, 1.0));
    }

    private static TrackLayoutVector3dV2Dto Point(double x, double y, double z)
    {
        return new TrackLayoutVector3dV2Dto { X = x, Y = y, Z = z };
    }

    private static TrackBankingKeyV2Dto BankingKey(
        double distance,
        double rollRadians,
        string interpolation)
    {
        return new TrackBankingKeyV2Dto
        {
            Distance = distance,
            RollRadians = rollRadians,
            InterpolationToNext = interpolation
        };
    }

    private static void AssertEdge(
        TrackAuthoringGraphEdge edge,
        string expectedSource,
        string expectedTarget)
    {
        Assert.Equal(expectedSource, edge.SourceNodeId);
        Assert.Equal(expectedTarget, edge.TargetNodeId);
    }
}
