using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class TrackAuthoringCandidateEvaluatorTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void AppendCandidate_UsesProductionDefinitionAndRetainsExactResults()
    {
        TrackAuthoringGraph source = Graph();
        var section = new StraightSectionDefinition("entry", 10.0, 0.1);
        TrackAuthoringCandidateOperation operation =
            TrackAuthoringCandidateOperation.Append(section);

        TrackAuthoringCandidateEvaluation result =
            TrackAuthoringCandidateEvaluator.Evaluate(source, operation);

        Assert.True(result.CommitEligible);
        Assert.Same(source, result.SourceGraph);
        Assert.Same(operation, result.Operation);
        Assert.NotNull(result.CandidateGraph);
        Assert.Same(section, Assert.Single(result.CandidateGraph!.Nodes).Section);
        Assert.NotNull(result.RouteResult);
        Assert.NotNull(result.CompileResult);
        Assert.Same(result.CompileResult!.Compilation, result.Compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void InsertBeforeAndAfterCandidates_ApplyExistingProductionOperations()
    {
        TrackAuthoringGraph source = Graph(
            new StraightSectionDefinition("a", 2.0),
            new StraightSectionDefinition("c", 2.0));

        TrackAuthoringCandidateEvaluation before = TrackAuthoringCandidateEvaluator.Evaluate(
            source,
            TrackAuthoringCandidateOperation.InsertBefore(
                "c",
                new ConstantCurvatureSectionDefinition("b", 3.0, 20.0)));
        TrackAuthoringCandidateEvaluation after = TrackAuthoringCandidateEvaluator.Evaluate(
            source,
            TrackAuthoringCandidateOperation.InsertAfter(
                "a",
                new CurvatureTransitionSectionDefinition("b", 3.0, 0.0, 0.05)));

        Assert.True(before.CommitEligible);
        Assert.True(after.CommitEligible);
        Assert.Equal(new[] { "a", "b", "c" }, OrderedIds(before.CandidateGraph!));
        Assert.Equal(new[] { "a", "b", "c" }, OrderedIds(after.CandidateGraph!));
        Assert.Equal(
            TrackAuthoringSectionTypeIds.ConstantCurvature,
            before.CandidateGraph!.Nodes.Single(node => node.Id == "b").TypeId);
        Assert.Equal(
            TrackAuthoringSectionTypeIds.CurvatureTransition,
            after.CandidateGraph!.Nodes.Single(node => node.Id == "b").TypeId);
    }

    [Fact]
    public void ReplaceCandidate_PreservesStableIdAndLeavesSourceUnchanged()
    {
        var original = new StraightSectionDefinition("section", 4.0);
        TrackAuthoringGraph source = Graph(original);
        var replacement = new ConstantCurvatureSectionDefinition("section", 8.0, -30.0);

        TrackAuthoringCandidateEvaluation result = TrackAuthoringCandidateEvaluator.Evaluate(
            source,
            TrackAuthoringCandidateOperation.Replace("section", replacement));

        Assert.True(result.CommitEligible);
        Assert.Same(original, Assert.Single(source.Nodes).Section);
        Assert.Same(replacement, Assert.Single(result.CandidateGraph!.Nodes).Section);
        Assert.NotSame(source, result.CandidateGraph);
    }

    [Fact]
    public void RoutineOperationAndCompilationFailures_AreStructuredAndNotCommitEligible()
    {
        TrackAuthoringGraph source = Graph(new StraightSectionDefinition("existing", 10.0));
        TrackAuthoringCandidateEvaluation duplicate = TrackAuthoringCandidateEvaluator.Evaluate(
            source,
            TrackAuthoringCandidateOperation.Append(
                new StraightSectionDefinition("existing", 2.0)));

        var banking = new TrackBankingDefinition(new[]
        {
            new BankingProfileKey(0.0, 0.0),
            new BankingProfileKey(10.0, 0.0)
        });
        TrackAuthoringGraph bankedSource = new TrackAuthoringGraph(
            source.Nodes,
            source.Edges,
            source.StartPose,
            banking);
        TrackAuthoringCandidateEvaluation compilationFailure =
            TrackAuthoringCandidateEvaluator.Evaluate(
                bankedSource,
                TrackAuthoringCandidateOperation.Append(
                    new StraightSectionDefinition("extra", 5.0)));

        Assert.False(duplicate.CommitEligible);
        Assert.Null(duplicate.CandidateGraph);
        Assert.Null(duplicate.CompileResult);
        Assert.Equal(
            TrackAuthoringGraphDiagnosticCode.CandidateOperationFailed,
            Assert.Single(duplicate.Diagnostics).Code);

        Assert.False(compilationFailure.CommitEligible);
        Assert.NotNull(compilationFailure.CandidateGraph);
        Assert.NotNull(compilationFailure.CompileResult);
        Assert.Null(compilationFailure.Compilation);
        Assert.Contains(
            compilationFailure.Diagnostics,
            diagnostic => diagnostic.Code ==
                TrackAuthoringGraphDiagnosticCode.AuthoringCompilationFailed);
    }

    [Fact]
    public void StaleDetection_UsesConservativeImmutableGraphIdentity()
    {
        TrackAuthoringGraph source = Graph(new StraightSectionDefinition("a", 3.0));
        TrackAuthoringCandidateEvaluation result = TrackAuthoringCandidateEvaluator.Evaluate(
            source,
            TrackAuthoringCandidateOperation.Append(
                new StraightSectionDefinition("b", 3.0)));
        TrackAuthoringGraph equivalentSnapshot = Graph(
            new StraightSectionDefinition("a", 3.0));

        Assert.False(result.IsStaleComparedTo(source));
        Assert.True(result.IsStaleComparedTo(equivalentSnapshot));
        Assert.True(result.IsStaleComparedTo(result.CandidateGraph!));
    }

    [Fact]
    public void SuccessfulEvaluation_InvokesCompilerExactlyOnce()
    {
        TrackAuthoringGraph source = Graph(new StraightSectionDefinition("a", 3.0));
        int compileCount = 0;

        TrackAuthoringCandidateEvaluation result = TrackAuthoringCandidateEvaluator.Evaluate(
            source,
            TrackAuthoringCandidateOperation.Append(
                new StraightSectionDefinition("b", 4.0)),
            graph =>
            {
                compileCount++;
                return TrackAuthoringGraphCompiler.Compile(graph);
            });

        Assert.True(result.CommitEligible);
        Assert.Equal(1, compileCount);
    }

    [Fact]
    public void EmptyCandidate_IsAValidUncompiledEditorState()
    {
        TrackAuthoringGraph source = Graph(new StraightSectionDefinition("a", 3.0));
        var operation = new TestCompoundOperation(_ => Graph());
        int compileCount = 0;

        TrackAuthoringCandidateEvaluation result = TrackAuthoringCandidateEvaluator.Evaluate(
            source,
            operation,
            graph =>
            {
                compileCount++;
                return TrackAuthoringGraphCompiler.Compile(graph);
            });

        Assert.True(result.CommitEligible);
        Assert.NotNull(result.CandidateGraph);
        Assert.Empty(result.CandidateGraph!.Nodes);
        Assert.Null(result.CompileResult);
        Assert.Null(result.Compilation);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(0, compileCount);
    }

    [Fact]
    public void InvalidCandidateRoute_RetainsValidationResultAndDoesNotCompile()
    {
        var source = new TrackAuthoringGraph(
            new[]
            {
                new TrackAuthoringGraphNode(new StraightSectionDefinition("a", 3.0)),
                new TrackAuthoringGraphNode(new StraightSectionDefinition("b", 3.0))
            },
            Array.Empty<TrackAuthoringGraphEdge>());
        int compileCount = 0;

        TrackAuthoringCandidateEvaluation result = TrackAuthoringCandidateEvaluator.Evaluate(
            source,
            TrackAuthoringCandidateOperation.Replace(
                "a",
                new ConstantCurvatureSectionDefinition("a", 3.0, 25.0)),
            graph =>
            {
                compileCount++;
                return TrackAuthoringGraphCompiler.Compile(graph);
            });

        Assert.False(result.CommitEligible);
        Assert.NotNull(result.CandidateGraph);
        Assert.NotNull(result.RouteResult);
        Assert.False(result.RouteResult!.Success);
        Assert.Null(result.CompileResult);
        Assert.Equal(0, compileCount);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == TrackAuthoringGraphDiagnosticCode.DisconnectedNode);
    }

    [Fact]
    public void ExtensibleCompoundOperation_UsesTheSameProductionCandidatePipeline()
    {
        TrackAuthoringGraph source = Graph(
            new StraightSectionDefinition("a", 3.0),
            new StraightSectionDefinition("b", 4.0));
        var operation = new TestCompoundOperation(graph =>
        {
            TrackAuthoringGraph first = TrackAuthoringGraphOperations.Replace(
                graph,
                "a",
                new ConstantCurvatureSectionDefinition("a", 5.0, 25.0));
            return TrackAuthoringGraphOperations.Replace(
                first,
                "b",
                new CurvatureTransitionSectionDefinition("b", 6.0, 0.04, 0.0));
        });
        int compileCount = 0;

        TrackAuthoringCandidateEvaluation result = TrackAuthoringCandidateEvaluator.Evaluate(
            source,
            operation,
            graph =>
            {
                compileCount++;
                return TrackAuthoringGraphCompiler.Compile(graph);
            });

        Assert.True(result.CommitEligible);
        Assert.Same(operation, result.Operation);
        Assert.Equal("test.compound", result.Operation.OperationTypeId);
        Assert.Equal(1, compileCount);
        Assert.IsType<StraightSectionDefinition>(source.Nodes[0].Section);
        Assert.IsType<StraightSectionDefinition>(source.Nodes[1].Section);
        Assert.IsType<ConstantCurvatureSectionDefinition>(result.CandidateGraph!.Nodes[0].Section);
        Assert.IsType<CurvatureTransitionSectionDefinition>(result.CandidateGraph.Nodes[1].Section);
    }

    [Fact]
    public void CandidateEvaluation_MatchesDirectProductionOperationAndCompiler()
    {
        TrackAuthoringGraph source = Graph(
            new StraightSectionDefinition("entry", 5.0),
            new ConstantCurvatureSectionDefinition("turn", 8.0, 20.0));
        var inserted = new CurvatureTransitionSectionDefinition(
            "transition",
            6.0,
            0.0,
            0.05);

        TrackAuthoringCandidateEvaluation evaluated = TrackAuthoringCandidateEvaluator.Evaluate(
            source,
            TrackAuthoringCandidateOperation.InsertBefore("turn", inserted));
        TrackAuthoringGraph directGraph = TrackAuthoringGraphOperations.InsertBefore(
            source,
            "turn",
            inserted);
        TrackAuthoringGraphCompileResult direct = TrackAuthoringGraphCompiler.Compile(directGraph);

        Assert.True(evaluated.CommitEligible);
        Assert.True(direct.Success);
        Assert.Equal(OrderedIds(directGraph), OrderedIds(evaluated.CandidateGraph!));
        Assert.Equal(directGraph.Edges.Count, evaluated.CandidateGraph!.Edges.Count);
        Assert.Equal(direct.Compilation!.TotalLength, evaluated.Compilation!.TotalLength);

        TrackFrame evaluatedEnd = new TrackEvaluator(evaluated.Compilation.Runtime)
            .EvaluateFrameAtDistance(evaluated.Compilation.TotalLength);
        TrackFrame directEnd = new TrackEvaluator(direct.Compilation.Runtime)
            .EvaluateFrameAtDistance(direct.Compilation.TotalLength);
        AssertVectorNear(directEnd.Position, evaluatedEnd.Position);
        AssertVectorNear(directEnd.Tangent, evaluatedEnd.Tangent);
    }

    private static TrackAuthoringGraph Graph(params GeometricSectionDefinition[] sections)
    {
        var nodes = sections.Select(section => new TrackAuthoringGraphNode(section)).ToArray();
        var edges = new TrackAuthoringGraphEdge[System.Math.Max(0, sections.Length - 1)];
        for (int i = 1; i < sections.Length; i++)
        {
            edges[i - 1] = new TrackAuthoringGraphEdge(sections[i - 1].Id, sections[i].Id);
        }

        return new TrackAuthoringGraph(nodes, edges);
    }

    private static string[] OrderedIds(TrackAuthoringGraph graph)
    {
        TrackAuthoringGraphRouteResult route = TrackAuthoringGraphRouteValidator.Validate(graph);
        Assert.True(route.Success);
        return route.OrderedNodes.Select(node => node.Id).ToArray();
    }

    private static void AssertVectorNear(Quantum.Math.Vector3d expected, Quantum.Math.Vector3d actual)
    {
        Assert.InRange(System.Math.Abs(expected.X - actual.X), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(expected.Y - actual.Y), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(expected.Z - actual.Z), 0.0, Tolerance);
    }

    private sealed class TestCompoundOperation : ITrackAuthoringCandidateOperation
    {
        private readonly Func<TrackAuthoringGraph, TrackAuthoringGraph> transform;

        public TestCompoundOperation(Func<TrackAuthoringGraph, TrackAuthoringGraph> transform)
        {
            this.transform = transform;
        }

        public string OperationTypeId => "test.compound";

        public TrackAuthoringGraph Apply(TrackAuthoringGraph sourceGraph)
        {
            return transform(sourceGraph);
        }
    }
}
