using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class TrackAuthoringGraphCompilerTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void Compile_UsesEdgesRatherThanNodeCollectionOrderWithoutMutatingGraph()
    {
        var entry = new StraightSectionDefinition("entry", 10.0);
        var turn = new ConstantCurvatureSectionDefinition("turn", 8.0, 20.0);
        var exit = new StraightSectionDefinition("exit", 4.0);
        var graph = new TrackAuthoringGraph(
            new[]
            {
                new TrackAuthoringGraphNode(exit),
                new TrackAuthoringGraphNode(entry),
                new TrackAuthoringGraphNode(turn)
            },
            new[]
            {
                new TrackAuthoringGraphEdge("entry", "turn"),
                new TrackAuthoringGraphEdge("turn", "exit")
            });

        TrackAuthoringGraphCompileResult result = TrackAuthoringGraphCompiler.Compile(graph);

        Assert.True(result.Success);
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Definition);
        Assert.NotNull(result.Compilation);
        Assert.Equal(new[] { "entry", "turn", "exit" }, result.OrderedNodes.Select(node => node.Id));
        Assert.Equal(new[] { "entry", "turn", "exit" }, result.Definition!.Sections.Select(section => section.Id));
        Assert.Same(entry, result.Definition.Sections[0]);
        Assert.Same(turn, result.Definition.Sections[1]);
        Assert.Same(exit, result.Definition.Sections[2]);
        Assert.Equal(22.0, result.Compilation!.TotalLength, Tolerance);

        TrackAuthoringCompilation direct = TrackAuthoringDocumentBuilder.Compile(
            new TrackAuthoringDefinition(new GeometricSectionDefinition[] { entry, turn, exit }));
        TrackFrame graphEnd = new TrackEvaluator(result.Compilation.Runtime)
            .EvaluateFrameAtDistance(result.Compilation.TotalLength);
        TrackFrame directEnd = new TrackEvaluator(direct.Runtime)
            .EvaluateFrameAtDistance(direct.TotalLength);
        AssertVectorNear(directEnd.Position, graphEnd.Position);
        AssertVectorNear(directEnd.Tangent, graphEnd.Tangent);

        Assert.Equal(new[] { "exit", "entry", "turn" }, graph.Nodes.Select(node => node.Id));
        Assert.Equal("entry", graph.Edges[0].SourceNodeId);
        Assert.Equal("turn", graph.Edges[0].TargetNodeId);
    }

    [Fact]
    public void WithSection_ReturnsNewSnapshotAndPreservesOriginalGraph()
    {
        var originalSection = new ConstantCurvatureSectionDefinition("sweeper", 35.0, 50.0);
        var sourceNodes = new List<TrackAuthoringGraphNode>
        {
            new TrackAuthoringGraphNode(originalSection)
        };
        var sourceEdges = new List<TrackAuthoringGraphEdge>();
        var graph = new TrackAuthoringGraph(sourceNodes, sourceEdges);
        var replacement = new ConstantCurvatureSectionDefinition("sweeper", 35.0, 30.0);

        TrackAuthoringGraph changed = graph.WithSection("sweeper", replacement);
        sourceNodes.Clear();
        sourceEdges.Add(new TrackAuthoringGraphEdge("sweeper", "sweeper"));

        Assert.NotSame(graph, changed);
        Assert.Single(graph.Nodes);
        Assert.Same(originalSection, graph.Nodes[0].Section);
        Assert.Same(replacement, changed.Nodes[0].Section);
        Assert.Equal(50.0, Assert.IsType<ConstantCurvatureSectionDefinition>(graph.Nodes[0].Section).Radius);
        Assert.Equal(30.0, Assert.IsType<ConstantCurvatureSectionDefinition>(changed.Nodes[0].Section).Radius);
        Assert.Empty(graph.Edges);
        Assert.Empty(changed.Edges);
    }

    [Fact]
    public void Compile_EmptyDuplicateAndUnknownEndpointGraphsReturnSpecificDiagnostics()
    {
        TrackAuthoringGraphCompileResult empty = TrackAuthoringGraphCompiler.Compile(
            new TrackAuthoringGraph(
                Array.Empty<TrackAuthoringGraphNode>(),
                Array.Empty<TrackAuthoringGraphEdge>()));
        TrackAuthoringGraphCompileResult duplicate = TrackAuthoringGraphCompiler.Compile(
            new TrackAuthoringGraph(
                new[]
                {
                    Node("same"),
                    Node("same")
                },
                Array.Empty<TrackAuthoringGraphEdge>()));
        TrackAuthoringGraphCompileResult unknownEndpoint = TrackAuthoringGraphCompiler.Compile(
            new TrackAuthoringGraph(
                new[] { Node("known") },
                new[] { new TrackAuthoringGraphEdge("known", "missing") }));

        AssertRejected(empty, TrackAuthoringGraphDiagnosticCode.EmptyGraph);
        TrackAuthoringGraphDiagnostic duplicateDiagnostic = AssertRejected(
            duplicate,
            TrackAuthoringGraphDiagnosticCode.DuplicateNodeId);
        Assert.Equal("same", duplicateDiagnostic.NodeId);
        TrackAuthoringGraphDiagnostic endpointDiagnostic = AssertRejected(
            unknownEndpoint,
            TrackAuthoringGraphDiagnosticCode.UnknownEdgeEndpoint);
        Assert.Equal("known", endpointDiagnostic.SourceNodeId);
        Assert.Equal("missing", endpointDiagnostic.TargetNodeId);
    }

    [Fact]
    public void Compile_CycleDisconnectedBranchingAndMergingGraphsAreRejected()
    {
        TrackAuthoringGraphCompileResult cycle = TrackAuthoringGraphCompiler.Compile(
            new TrackAuthoringGraph(
                new[] { Node("a"), Node("b") },
                new[]
                {
                    new TrackAuthoringGraphEdge("a", "b"),
                    new TrackAuthoringGraphEdge("b", "a")
                }));
        TrackAuthoringGraphCompileResult disconnected = TrackAuthoringGraphCompiler.Compile(
            new TrackAuthoringGraph(
                new[] { Node("a"), Node("b"), Node("orphan") },
                new[] { new TrackAuthoringGraphEdge("a", "b") }));
        TrackAuthoringGraphCompileResult branching = TrackAuthoringGraphCompiler.Compile(
            new TrackAuthoringGraph(
                new[] { Node("a"), Node("b"), Node("c") },
                new[]
                {
                    new TrackAuthoringGraphEdge("a", "b"),
                    new TrackAuthoringGraphEdge("a", "c")
                }));
        TrackAuthoringGraphCompileResult merging = TrackAuthoringGraphCompiler.Compile(
            new TrackAuthoringGraph(
                new[] { Node("a"), Node("b"), Node("c") },
                new[]
                {
                    new TrackAuthoringGraphEdge("a", "c"),
                    new TrackAuthoringGraphEdge("b", "c")
                }));

        AssertRejected(cycle, TrackAuthoringGraphDiagnosticCode.CycleDetected);
        AssertRejected(disconnected, TrackAuthoringGraphDiagnosticCode.DisconnectedNode);
        TrackAuthoringGraphDiagnostic branchDiagnostic = AssertRejected(
            branching,
            TrackAuthoringGraphDiagnosticCode.MultipleOutgoingEdges);
        Assert.Equal("a", branchDiagnostic.NodeId);
        TrackAuthoringGraphDiagnostic mergeDiagnostic = AssertRejected(
            merging,
            TrackAuthoringGraphDiagnosticCode.MultipleIncomingEdges);
        Assert.Equal("c", mergeDiagnostic.NodeId);
    }

    [Fact]
    public void Compile_DuplicateEdgeIsRejectedClearly()
    {
        var edge = new TrackAuthoringGraphEdge("a", "b");
        TrackAuthoringGraphCompileResult result = TrackAuthoringGraphCompiler.Compile(
            new TrackAuthoringGraph(
                new[] { Node("a"), Node("b") },
                new[] { edge, edge }));

        TrackAuthoringGraphDiagnostic diagnostic = AssertRejected(
            result,
            TrackAuthoringGraphDiagnosticCode.DuplicateEdge);
        Assert.Equal("a", diagnostic.SourceNodeId);
        Assert.Equal("b", diagnostic.TargetNodeId);
    }

    [Fact]
    public void Compile_BackendAuthoringFailureReturnsDiagnosticWithoutCompilation()
    {
        var banking = new TrackBankingDefinition(new[]
        {
            new BankingProfileKey(0.0, 0.0),
            new BankingProfileKey(5.0, 0.0)
        });
        var graph = new TrackAuthoringGraph(
            new[] { Node("track", 10.0) },
            Array.Empty<TrackAuthoringGraphEdge>(),
            TrackStartPose.Identity,
            banking);

        TrackAuthoringGraphCompileResult result = TrackAuthoringGraphCompiler.Compile(graph);

        AssertRejected(result, TrackAuthoringGraphDiagnosticCode.AuthoringCompilationFailed);
        Assert.NotNull(result.Definition);
        Assert.Null(result.Compilation);
        Assert.Equal("track", Assert.Single(result.OrderedNodes).Id);
    }

    private static TrackAuthoringGraphNode Node(string id, double length = 1.0)
    {
        return new TrackAuthoringGraphNode(new StraightSectionDefinition(id, length));
    }

    private static TrackAuthoringGraphDiagnostic AssertRejected(
        TrackAuthoringGraphCompileResult result,
        TrackAuthoringGraphDiagnosticCode expectedCode)
    {
        Assert.False(result.Success);
        Assert.Null(result.Compilation);
        return Assert.Single(result.Diagnostics, diagnostic => diagnostic.Code == expectedCode);
    }

    private static void AssertVectorNear(Quantum.Math.Vector3d expected, Quantum.Math.Vector3d actual)
    {
        Assert.InRange(System.Math.Abs(expected.X - actual.X), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(expected.Y - actual.Y), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(expected.Z - actual.Z), 0.0, Tolerance);
    }
}
