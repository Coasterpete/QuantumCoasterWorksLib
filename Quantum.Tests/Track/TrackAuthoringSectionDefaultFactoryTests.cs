using Quantum.Math;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class TrackAuthoringSectionDefaultFactoryTests
{
    [Fact]
    public void EmptyAppend_UsesDeterministicCatalogDefaults()
    {
        TrackAuthoringGraph empty = Graph();

        TrackAuthoringSectionDefaults straight = TrackAuthoringSectionDefaultFactory.ForAppend(
            empty,
            TrackAuthoringSectionTypeIds.Straight,
            "straight");
        TrackAuthoringSectionDefaults arc = TrackAuthoringSectionDefaultFactory.ForAppend(
            empty,
            TrackAuthoringSectionTypeIds.ConstantCurvature,
            "arc");
        TrackAuthoringSectionDefaults transition = TrackAuthoringSectionDefaultFactory.ForAppend(
            empty,
            TrackAuthoringSectionTypeIds.CurvatureTransition,
            "transition");

        Assert.Equal(0.0, straight.InsertionStation);
        Assert.Null(straight.UpstreamNodeId);
        Assert.Null(straight.DownstreamNodeId);
        Assert.Equal(10.0, Assert.IsType<StraightSectionDefinition>(straight.Definition).Length);
        Assert.Equal(0.0, straight.InheritedRollRadians);
        Assert.True(straight.Flags.HasFlag(TrackAuthoringSectionDefaultFlags.ZeroRollFallback));

        ConstantCurvatureSectionDefinition constant =
            Assert.IsType<ConstantCurvatureSectionDefinition>(arc.Definition);
        Assert.Equal(10.0, constant.Length);
        Assert.Equal(25.0, constant.Radius);
        Assert.True(arc.Flags.HasFlag(TrackAuthoringSectionDefaultFlags.PositiveRadiusFallback));

        CurvatureTransitionSectionDefinition curve =
            Assert.IsType<CurvatureTransitionSectionDefinition>(transition.Definition);
        Assert.Equal(0.0, curve.StartCurvature);
        Assert.Equal(0.0, curve.EndCurvature);
        Assert.True(transition.Flags.HasFlag(
            TrackAuthoringSectionDefaultFlags.ZeroCurvatureFallback));
    }

    [Fact]
    public void ConstantCurvature_UsesNearestSupportedNonzeroNeighbor()
    {
        TrackAuthoringGraph graph = Graph(
            new StraightSectionDefinition("entry", 4.0, 0.1),
            new ConstantCurvatureSectionDefinition("turn", 6.0, -20.0, 0.2));

        TrackAuthoringSectionDefaults beforeTurn =
            TrackAuthoringSectionDefaultFactory.ForInsertBefore(
                graph,
                "turn",
                TrackAuthoringSectionTypeIds.ConstantCurvature,
                "inserted");

        ConstantCurvatureSectionDefinition definition =
            Assert.IsType<ConstantCurvatureSectionDefinition>(beforeTurn.Definition);
        Assert.Equal(-20.0, definition.Radius, 12);
        Assert.Equal(4.0, beforeTurn.InsertionStation);
        Assert.Equal("entry", beforeTurn.UpstreamNodeId);
        Assert.Equal("turn", beforeTurn.DownstreamNodeId);
        Assert.Equal(0.0, beforeTurn.UpstreamEndCurvature);
        Assert.Equal(-0.05, beforeTurn.DownstreamStartCurvature!.Value, 12);
        Assert.True(beforeTurn.Flags.HasFlag(
            TrackAuthoringSectionDefaultFlags.CurvatureInheritedFromDownstream));
    }

    [Fact]
    public void AppendAndReplacement_UseCorrectRoutePositionContext()
    {
        TrackAuthoringGraph graph = Graph(
            new ConstantCurvatureSectionDefinition("a", 3.0, 50.0, 0.1),
            new StraightSectionDefinition("b", 7.0, 0.2),
            new ConstantCurvatureSectionDefinition("c", 5.0, -25.0, 0.3));

        TrackAuthoringSectionDefaults append = TrackAuthoringSectionDefaultFactory.ForAppend(
            graph,
            TrackAuthoringSectionTypeIds.CurvatureTransition,
            "tail");
        TrackAuthoringSectionDefaults replacement =
            TrackAuthoringSectionDefaultFactory.ForReplacement(
                graph,
                "b",
                TrackAuthoringSectionTypeIds.CurvatureTransition);

        CurvatureTransitionSectionDefinition appended =
            Assert.IsType<CurvatureTransitionSectionDefinition>(append.Definition);
        Assert.Equal(15.0, append.InsertionStation);
        Assert.Equal("c", append.UpstreamNodeId);
        Assert.Null(append.DownstreamNodeId);
        Assert.Equal(-0.04, appended.StartCurvature, 12);
        Assert.Equal(-0.04, appended.EndCurvature, 12);

        CurvatureTransitionSectionDefinition replaced =
            Assert.IsType<CurvatureTransitionSectionDefinition>(replacement.Definition);
        Assert.Equal("b", replaced.Id);
        Assert.Equal(3.0, replacement.InsertionStation);
        Assert.Equal("a", replacement.UpstreamNodeId);
        Assert.Equal("c", replacement.DownstreamNodeId);
        Assert.Equal(0.02, replaced.StartCurvature, 12);
        Assert.Equal(-0.04, replaced.EndCurvature, 12);
        Assert.True(replacement.Flags.HasFlag(
            TrackAuthoringSectionDefaultFlags.TransitionBridgesNeighbors));
    }

    [Fact]
    public void Transition_BridgesBothSupportedNeighborEndpoints()
    {
        TrackAuthoringGraph graph = Graph(
            new CurvatureTransitionSectionDefinition("in", 6.0, 0.0, 0.05),
            new ConstantCurvatureSectionDefinition("out", 8.0, -10.0));

        TrackAuthoringSectionDefaults defaults =
            TrackAuthoringSectionDefaultFactory.ForInsertAfter(
                graph,
                "in",
                TrackAuthoringSectionTypeIds.CurvatureTransition,
                "bridge");

        CurvatureTransitionSectionDefinition bridge =
            Assert.IsType<CurvatureTransitionSectionDefinition>(defaults.Definition);
        Assert.Equal(0.05, bridge.StartCurvature, 12);
        Assert.Equal(-0.1, bridge.EndCurvature, 12);
        Assert.True(defaults.Flags.HasFlag(
            TrackAuthoringSectionDefaultFlags.TransitionBridgesNeighbors));
    }

    [Fact]
    public void Roll_InheritsUpstreamThenDownstreamThenZero()
    {
        TrackAuthoringGraph graph = Graph(
            new StraightSectionDefinition("up", 4.0, 0.25),
            new StraightSectionDefinition("down", 5.0, -0.5));

        TrackAuthoringSectionDefaults between =
            TrackAuthoringSectionDefaultFactory.ForInsertBefore(
                graph,
                "down",
                TrackAuthoringSectionTypeIds.Straight,
                "between");
        TrackAuthoringSectionDefaults atStart =
            TrackAuthoringSectionDefaultFactory.ForInsertBefore(
                graph,
                "up",
                TrackAuthoringSectionTypeIds.Straight,
                "start");

        Assert.Equal(0.25, between.InheritedRollRadians);
        Assert.Equal(0.25, Assert.IsType<StraightSectionDefinition>(between.Definition).RollRadians);
        Assert.True(between.Flags.HasFlag(
            TrackAuthoringSectionDefaultFlags.RollInheritedFromUpstream));
        Assert.Equal(0.25, atStart.InheritedRollRadians);
        Assert.True(atStart.Flags.HasFlag(
            TrackAuthoringSectionDefaultFlags.RollInheritedFromDownstream));
    }

    [Fact]
    public void SpatialNeighbor_ReportsScalarUnavailableAndUsesDocumentedFallback()
    {
        var spatial = new SpatialSectionDefinition(
            "spatial",
            3.0,
            new[]
            {
                Vector3d.Zero,
                new Vector3d(1.0, 0.0, 0.0),
                new Vector3d(2.0, 1.0, 0.0),
                new Vector3d(3.0, 1.0, 1.0)
            },
            rollRadians: 0.4);
        TrackAuthoringGraph graph = Graph(spatial);

        TrackAuthoringSectionDefaults arc = TrackAuthoringSectionDefaultFactory.ForAppend(
            graph,
            TrackAuthoringSectionTypeIds.ConstantCurvature,
            "arc");
        TrackAuthoringSectionDefaults transition = TrackAuthoringSectionDefaultFactory.ForAppend(
            graph,
            TrackAuthoringSectionTypeIds.CurvatureTransition,
            "transition");

        Assert.Null(arc.UpstreamEndCurvature);
        Assert.Equal(0.4, arc.InheritedRollRadians);
        Assert.Equal(25.0, Assert.IsType<ConstantCurvatureSectionDefinition>(arc.Definition).Radius);
        Assert.True(arc.Flags.HasFlag(
            TrackAuthoringSectionDefaultFlags.UpstreamScalarCurvatureUnavailable));
        Assert.True(arc.Flags.HasFlag(
            TrackAuthoringSectionDefaultFlags.PositiveRadiusFallback));

        CurvatureTransitionSectionDefinition transitionDefinition =
            Assert.IsType<CurvatureTransitionSectionDefinition>(transition.Definition);
        Assert.Equal(0.0, transitionDefinition.StartCurvature);
        Assert.Equal(0.0, transitionDefinition.EndCurvature);
        Assert.True(transition.Flags.HasFlag(
            TrackAuthoringSectionDefaultFlags.ZeroCurvatureFallback));
    }

    [Fact]
    public void MixedFamilyRoute_ReturnsAvailableContextWithoutAssumingGeometryOnly()
    {
        TrackAuthoringGraph graph = Graph(
            new StraightSectionDefinition("entry", 4.0, 0.1),
            new TestForceSectionDefinition("force"),
            new ConstantCurvatureSectionDefinition("exit", 5.0, -20.0, 0.3));

        TrackAuthoringSectionDefaults defaults =
            TrackAuthoringSectionDefaultFactory.ForInsertAfter(
                graph,
                "force",
                TrackAuthoringSectionTypeIds.CurvatureTransition,
                "transition");

        CurvatureTransitionSectionDefinition transition =
            Assert.IsType<CurvatureTransitionSectionDefinition>(defaults.Definition);
        Assert.False(defaults.HasInsertionStation);
        Assert.Null(defaults.InsertionStation);
        Assert.Equal("force", defaults.UpstreamNodeId);
        Assert.Equal("exit", defaults.DownstreamNodeId);
        Assert.Null(defaults.UpstreamEndCurvature);
        Assert.Equal(-0.05, defaults.DownstreamStartCurvature!.Value, 12);
        Assert.Equal(-0.05, transition.StartCurvature, 12);
        Assert.Equal(-0.05, transition.EndCurvature, 12);
        Assert.Equal(0.3, defaults.InheritedRollRadians);
        Assert.True(defaults.Flags.HasFlag(
            TrackAuthoringSectionDefaultFlags.InsertionStationUnavailable));
        Assert.True(defaults.Flags.HasFlag(
            TrackAuthoringSectionDefaultFlags.UpstreamScalarCurvatureUnavailable));
        Assert.True(defaults.Flags.HasFlag(
            TrackAuthoringSectionDefaultFlags.UpstreamRollUnavailable));
        Assert.True(defaults.Flags.HasFlag(
            TrackAuthoringSectionDefaultFlags.RollInheritedFromDownstream));
    }

    private static TrackAuthoringGraph Graph(params TrackAuthoringSectionDefinition[] sections)
    {
        var nodes = sections.Select(section => new TrackAuthoringGraphNode(section)).ToArray();
        var edges = new TrackAuthoringGraphEdge[System.Math.Max(0, sections.Length - 1)];
        for (int i = 1; i < sections.Length; i++)
        {
            edges[i - 1] = new TrackAuthoringGraphEdge(sections[i - 1].Id, sections[i].Id);
        }

        return new TrackAuthoringGraph(nodes, edges);
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
