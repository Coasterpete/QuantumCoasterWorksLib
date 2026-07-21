using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class TrackAuthoringGraphOperationsTests
{
    [Fact]
    public void BuiltInDefinitions_ExposeStableBackendFamilyAndTypeDiscriminators()
    {
        TrackAuthoringSectionDefinition[] sections =
        {
            new StraightSectionDefinition("straight", 1.0),
            new ConstantCurvatureSectionDefinition("arc", 1.0, 20.0),
            new CurvatureTransitionSectionDefinition("transition", 1.0, 0.0, 0.05),
            new SpatialSectionDefinition(
                "spatial",
                3.0,
                new[]
                {
                    new Quantum.Math.Vector3d(0.0, 0.0, 0.0),
                    new Quantum.Math.Vector3d(1.0, 0.0, 0.0),
                    new Quantum.Math.Vector3d(2.0, 0.0, 0.0),
                    new Quantum.Math.Vector3d(3.0, 0.0, 0.0)
                })
        };

        Assert.All(
            sections,
            section => Assert.Equal(TrackAuthoringSectionFamily.Geometry, section.Family));
        Assert.Equal(
            new[]
            {
                TrackAuthoringSectionTypeIds.Straight,
                TrackAuthoringSectionTypeIds.ConstantCurvature,
                TrackAuthoringSectionTypeIds.CurvatureTransition,
                TrackAuthoringSectionTypeIds.Spatial
            },
            sections.Select(section => section.TypeId));
        Assert.Equal(
            sections.Select(section => section.TypeId),
            TrackAuthoringSectionCatalog.Types.Select(type => type.TypeId));
    }

    [Fact]
    public void RouteValidator_AcceptsEmptyAuthoringRouteWhileCompilerRequiresGeometry()
    {
        var graph = new TrackAuthoringGraph(
            Array.Empty<TrackAuthoringGraphNode>(),
            Array.Empty<TrackAuthoringGraphEdge>());

        TrackAuthoringGraphRouteResult route =
            TrackAuthoringGraphRouteValidator.Validate(graph);
        TrackAuthoringGraphCompileResult compilation =
            TrackAuthoringGraphCompiler.Compile(graph);

        Assert.True(route.Success);
        Assert.Empty(route.OrderedNodes);
        Assert.False(compilation.Success);
        Assert.Contains(
            compilation.Diagnostics,
            diagnostic => diagnostic.Code == TrackAuthoringGraphDiagnosticCode.EmptyGraph);
    }

    [Fact]
    public void AppendAndInsert_ReturnNewCanonicalRoutesWithoutMutatingSource()
    {
        TrackAuthoringGraph empty = EmptyGraph();

        TrackAuthoringGraph first = TrackAuthoringGraphOperations.Append(
            empty,
            new StraightSectionDefinition("a", 2.0));
        TrackAuthoringGraph second = TrackAuthoringGraphOperations.Append(
            first,
            new StraightSectionDefinition("c", 2.0));
        TrackAuthoringGraph before = TrackAuthoringGraphOperations.InsertBefore(
            second,
            "c",
            new ConstantCurvatureSectionDefinition("b", 2.0, 20.0));
        TrackAuthoringGraph after = TrackAuthoringGraphOperations.InsertAfter(
            before,
            "c",
            new CurvatureTransitionSectionDefinition("d", 2.0, 0.05, 0.0));

        Assert.Empty(empty.Nodes);
        AssertRoute(first, "a");
        AssertRoute(second, "a", "c");
        AssertRoute(before, "a", "b", "c");
        AssertRoute(after, "a", "b", "c", "d");
        AssertEdges(after, ("a", "b"), ("b", "c"), ("c", "d"));
        Assert.True(TrackAuthoringGraphCompiler.Compile(after).Success);
    }

    [Fact]
    public void Delete_RewiresNeighborsAndCanReturnToEmptyAuthoringRoute()
    {
        TrackAuthoringGraph source = Graph("a", "b", "c");

        TrackAuthoringGraph middleDeleted =
            TrackAuthoringGraphOperations.Delete(source, "b");
        TrackAuthoringGraph firstDeleted =
            TrackAuthoringGraphOperations.Delete(middleDeleted, "a");
        TrackAuthoringGraph empty =
            TrackAuthoringGraphOperations.Delete(firstDeleted, "c");

        AssertRoute(source, "a", "b", "c");
        AssertRoute(middleDeleted, "a", "c");
        AssertEdges(middleDeleted, ("a", "c"));
        AssertRoute(firstDeleted, "c");
        Assert.Empty(empty.Nodes);
        Assert.Empty(empty.Edges);
        Assert.True(TrackAuthoringGraphRouteValidator.Validate(empty).Success);
    }

    [Fact]
    public void MoveBeforeAndAfter_PreserveNodeIdentityAndRebuildRouteOrder()
    {
        TrackAuthoringGraph source = Graph("a", "b", "c", "d");
        TrackAuthoringGraphNode originalC = source.Nodes.Single(node => node.Id == "c");

        TrackAuthoringGraph before =
            TrackAuthoringGraphOperations.MoveBefore(source, "d", "b");
        TrackAuthoringGraph after =
            TrackAuthoringGraphOperations.MoveAfter(before, "a", "c");

        AssertRoute(before, "a", "d", "b", "c");
        AssertRoute(after, "d", "b", "c", "a");
        Assert.Same(originalC, after.Nodes.Single(node => node.Id == "c"));
        Assert.Same(
            after,
            TrackAuthoringGraphOperations.MoveAfter(after, "a", "a"));
    }

    [Fact]
    public void Replace_PreservesStableIdentityAndCanChangeTypedPayload()
    {
        TrackAuthoringGraph source = Graph("section");
        var replacement = new ConstantCurvatureSectionDefinition(
            "section",
            8.0,
            -30.0,
            0.2);

        TrackAuthoringGraph changed = TrackAuthoringGraphOperations.Replace(
            source,
            "section",
            replacement);

        Assert.Equal("section", Assert.Single(changed.Nodes).Id);
        Assert.Equal(
            TrackAuthoringSectionTypeIds.ConstantCurvature,
            Assert.Single(changed.Nodes).TypeId);
        Assert.Same(replacement, Assert.Single(changed.Nodes).Section);
        Assert.IsType<StraightSectionDefinition>(Assert.Single(source.Nodes).Section);
    }

    [Fact]
    public void Operations_RejectDuplicateMissingAndInvalidSourceRoutes()
    {
        TrackAuthoringGraph source = Graph("a", "b");
        var invalidSource = new TrackAuthoringGraph(
            source.Nodes,
            source.Edges.Concat(new[] { new TrackAuthoringGraphEdge("a", "missing") }));

        Assert.Throws<ArgumentException>(() =>
            TrackAuthoringGraphOperations.Append(
                source,
                new StraightSectionDefinition("a", 1.0)));
        Assert.Throws<ArgumentException>(() =>
            TrackAuthoringGraphOperations.InsertBefore(
                source,
                "missing",
                new StraightSectionDefinition("c", 1.0)));
        Assert.Throws<ArgumentException>(() =>
            TrackAuthoringGraphOperations.Delete(source, "missing"));
        Assert.Throws<InvalidOperationException>(() =>
            TrackAuthoringGraphOperations.Append(
                invalidSource,
                new StraightSectionDefinition("c", 1.0)));
    }

    [Fact]
    public void FutureFamilyPayload_CanParticipateInRouteOperationsButRequiresItsOwnCompiler()
    {
        TrackAuthoringGraph graph = TrackAuthoringGraphOperations.Append(
            EmptyGraph(),
            new TestForceSectionDefinition("force"));

        TrackAuthoringGraphRouteResult route =
            TrackAuthoringGraphRouteValidator.Validate(graph);
        TrackAuthoringGraphCompileResult compilation =
            TrackAuthoringGraphCompiler.Compile(graph);

        Assert.True(route.Success);
        TrackAuthoringGraphNode node = Assert.Single(route.OrderedNodes);
        Assert.Equal(TrackAuthoringSectionFamily.Force, node.Family);
        Assert.Equal("force.test", node.TypeId);
        Assert.False(compilation.Success);
        TrackAuthoringGraphDiagnostic diagnostic = Assert.Single(compilation.Diagnostics);
        Assert.Equal(TrackAuthoringGraphDiagnosticCode.UnsupportedSectionFamily, diagnostic.Code);
        Assert.Equal("force", diagnostic.NodeId);
    }

    private static TrackAuthoringGraph EmptyGraph()
    {
        return new TrackAuthoringGraph(
            Array.Empty<TrackAuthoringGraphNode>(),
            Array.Empty<TrackAuthoringGraphEdge>());
    }

    private static TrackAuthoringGraph Graph(params string[] ids)
    {
        var nodes = ids
            .Select(id => new TrackAuthoringGraphNode(
                new StraightSectionDefinition(id, 1.0)))
            .ToArray();
        var edges = new TrackAuthoringGraphEdge[System.Math.Max(0, ids.Length - 1)];
        for (int i = 1; i < ids.Length; i++)
        {
            edges[i - 1] = new TrackAuthoringGraphEdge(ids[i - 1], ids[i]);
        }

        return new TrackAuthoringGraph(nodes, edges);
    }

    private static void AssertRoute(TrackAuthoringGraph graph, params string[] expectedIds)
    {
        TrackAuthoringGraphRouteResult result =
            TrackAuthoringGraphRouteValidator.Validate(graph);
        Assert.True(
            result.Success,
            string.Join(" ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(expectedIds, result.OrderedNodes.Select(node => node.Id));
    }

    private static void AssertEdges(
        TrackAuthoringGraph graph,
        params (string Source, string Target)[] expected)
    {
        Assert.Equal(expected.Length, graph.Edges.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Source, graph.Edges[i].SourceNodeId);
            Assert.Equal(expected[i].Target, graph.Edges[i].TargetNodeId);
        }
    }

    private sealed class TestForceSectionDefinition : TrackAuthoringSectionDefinition
    {
        public TestForceSectionDefinition(string id)
            : base(id, TrackAuthoringSectionFamily.Force)
        {
        }

        public override string TypeId => "force.test";
    }
}
