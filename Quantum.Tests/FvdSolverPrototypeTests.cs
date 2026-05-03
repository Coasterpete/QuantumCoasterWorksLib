using System;
using System.Collections.Generic;
using System.Reflection;
using Quantum.FVD;
using Quantum.Math;
using Xunit;

namespace Quantum.Tests;

public class FvdSolverPrototypeTests
{
    private const double EqualityTolerance = 1e-12;

    [Fact]
    public void Fvd2dNormalGSolver_Step_WithValidNormalTarget_DoesNotIncreaseAbsoluteNormalGError()
    {
        const double midpointX = 50.0;

        FvdGraph graph = BuildSimple2dGraphWithForceDistanceSection(
            includeNormalTarget: true,
            midpointNormalGTarget: 1.60);

        object result = StepGraphOnceOrFail(graph, midpointX);
        double beforeError = ReadDoublePropertyOrFail(result, "BeforeAbsoluteNormalGError");
        double afterError = ReadDoublePropertyOrFail(result, "AfterAbsoluteNormalGError");

        Assert.InRange(afterError, 0.0, beforeError + 1e-9);
    }

    [Fact]
    public void Fvd2dNormalGSolver_Step_NormalGOnlyForceSection_DoesNotRequireOtherForceChannels()
    {
        const double midpointX = 50.0;

        FvdGraph graph = BuildSimple2dGraphWithForceDistanceSection(
            includeNormalTarget: true,
            midpointNormalGTarget: 1.80,
            includeLateralTarget: false,
            includeRollRateTarget: false);

        object result = StepGraphOnceOrFail(graph, midpointX);
        string statusName = ReadEnumNamePropertyOrFail(result, "Status");
        double beforeError = ReadDoublePropertyOrFail(result, "BeforeAbsoluteNormalGError");
        double afterError = ReadDoublePropertyOrFail(result, "AfterAbsoluteNormalGError");

        Assert.Equal("Success", statusName);
        Assert.InRange(afterError, 0.0, beforeError + 1e-9);
    }

    [Fact]
    public void Fvd2dNormalGSolver_Step_MissingNormalGTarget_ReturnsNoOpStatus()
    {
        const double midpointX = 50.0;

        FvdGraph graph = BuildSimple2dGraphWithForceDistanceSection(
            includeNormalTarget: false,
            midpointNormalGTarget: 1.60);

        var beforeNodes = SnapshotNodes(graph.ControlNodes);
        int beforeDegree = graph.Degree;

        object result = StepGraphOnceOrFail(graph, midpointX);
        string statusName = ReadEnumNamePropertyOrFail(result, "Status");
        FvdGraph afterGraph = ReadGraphPropertyOrFail(result, "Graph");

        Assert.Equal("NoNormalTarget", statusName);
        Assert.Equal(beforeDegree, afterGraph.Degree);
        AssertNodesEqual(beforeNodes, afterGraph.ControlNodes);
    }

    [Fact]
    public void Fvd2dNormalGSolver_Step_OnlyOneInteriorNodeYChanges()
    {
        const double midpointX = 50.0;

        FvdGraph graph = BuildSimple2dGraphWithForceDistanceSection(
            includeNormalTarget: true,
            midpointNormalGTarget: 1.80);

        var beforeNodes = SnapshotNodes(graph.ControlNodes);
        int beforeDegree = graph.Degree;

        object result = StepGraphOnceOrFail(graph, midpointX);
        FvdGraph afterGraph = ReadGraphPropertyOrFail(result, "Graph");

        Assert.Equal(beforeDegree, afterGraph.Degree);
        Assert.Equal(beforeNodes.Count, afterGraph.ControlNodes.Count);

        int changedInteriorYCount = 0;

        for (int i = 0; i < beforeNodes.Count; i++)
        {
            NodeSnapshot before = beforeNodes[i];
            FvdControlNode after = afterGraph.ControlNodes[i];

            AssertNear(before.U, after.U);
            AssertNear(before.X, after.Position.X);
            AssertNear(before.Z, after.Position.Z);
            AssertNear(before.Weight, after.Weight);

            bool yChanged = System.Math.Abs(after.Position.Y - before.Y) > EqualityTolerance;
            bool isInterior = i > 0 && i < beforeNodes.Count - 1;

            if (yChanged)
            {
                Assert.True(isInterior, "Only interior node Y values may change.");
                changedInteriorYCount++;
            }
        }

        Assert.Equal(1, changedInteriorYCount);
    }

    [Fact]
    public void Fvd2dNormalGSolver_Step_NonFiniteRealizedNormalG_ReturnsNoOpStatus()
    {
        const double midpointX = 50.0;

        FvdGraph graph = BuildSimple2dGraphWithForceDistanceSection(
            includeNormalTarget: true,
            midpointNormalGTarget: 1.80);

        var beforeNodes = SnapshotNodes(graph.ControlNodes);
        int beforeDegree = graph.Degree;

        object result = StepGraphOnceOrFail(graph, midpointX, speedMps: double.MaxValue);
        string statusName = ReadEnumNamePropertyOrFail(result, "Status");
        double beforeError = ReadDoublePropertyOrFail(result, "BeforeAbsoluteNormalGError");
        double afterError = ReadDoublePropertyOrFail(result, "AfterAbsoluteNormalGError");
        FvdGraph afterGraph = ReadGraphPropertyOrFail(result, "Graph");

        Assert.Equal("NoImprovement", statusName);
        Assert.Equal(0.0, beforeError);
        Assert.Equal(0.0, afterError);
        Assert.Equal(beforeDegree, afterGraph.Degree);
        AssertNodesEqual(beforeNodes, afterGraph.ControlNodes);
    }

    [Fact]
    public void Fvd2dNormalGSolver_Step_InvalidDerivativeEpsilon_ThrowsArgumentOutOfRange()
    {
        const double midpointX = 50.0;

        FvdGraph graph = BuildSimple2dGraphWithForceDistanceSection(
            includeNormalTarget: true,
            midpointNormalGTarget: 1.80);

        TargetInvocationException nonFinite = Assert.Throws<TargetInvocationException>(
            () => StepGraphOnceOrFail(graph, midpointX, derivativeEpsilon: double.NaN));
        TargetInvocationException nonPositive = Assert.Throws<TargetInvocationException>(
            () => StepGraphOnceOrFail(graph, midpointX, derivativeEpsilon: 0.0));

        ArgumentOutOfRangeException nonFiniteInner = Assert.IsType<ArgumentOutOfRangeException>(nonFinite.InnerException);
        ArgumentOutOfRangeException nonPositiveInner = Assert.IsType<ArgumentOutOfRangeException>(nonPositive.InnerException);

        Assert.Equal("DerivativeEpsilon", nonFiniteInner.ParamName);
        Assert.Equal("DerivativeEpsilon", nonPositiveInner.ParamName);
    }

    [Fact]
    public void Fvd2dNormalGSolver_AcceptanceMatrix_Current2dSingleNodeNormalGScope()
    {
        const double NodeEqualityTolerance = 1e-12;
        const double ErrorEqualityTolerance = 1e-12;
        const double MonotonicTolerance = 1e-9;
        const double midpointX = 50.0;
        const double maxDeltaYStep = 1.0;

        SolverAcceptanceScenario[] scenarios =
        {
            new SolverAcceptanceScenario(
                "S1 Success_ImprovesError",
                () => BuildSimple2dGraphWithForceDistanceSection(includeNormalTarget: true, midpointNormalGTarget: 1.80),
                EvaluationX: midpointX,
                SpeedMps: 20.0,
                MaxDeltaYStep: maxDeltaYStep,
                ExpectedStatus: "Success",
                ExpectGraphUnchanged: false,
                ExpectStrictImprovement: true,
                ExpectAfterEqualsBefore: false,
                ExpectZeroErrors: false,
                ExpectSingleInteriorYChange: true),
            new SolverAcceptanceScenario(
                "S2 NoNormalTarget_NoOp",
                () => BuildSimple2dGraphWithForceDistanceSection(includeNormalTarget: false, midpointNormalGTarget: 1.80),
                EvaluationX: midpointX,
                SpeedMps: 20.0,
                MaxDeltaYStep: maxDeltaYStep,
                ExpectedStatus: "NoNormalTarget",
                ExpectGraphUnchanged: true,
                ExpectStrictImprovement: false,
                ExpectAfterEqualsBefore: false,
                ExpectZeroErrors: true,
                ExpectSingleInteriorYChange: false),
            new SolverAcceptanceScenario(
                "S3 NoInteriorNode_NoOp",
                () => BuildTwoNodeLineGraphWithForceDistanceSection(includeNormalTarget: true, midpointNormalGTarget: 1.80),
                EvaluationX: midpointX,
                SpeedMps: 20.0,
                MaxDeltaYStep: maxDeltaYStep,
                ExpectedStatus: "NoInteriorNode",
                ExpectGraphUnchanged: true,
                ExpectStrictImprovement: false,
                ExpectAfterEqualsBefore: false,
                ExpectZeroErrors: true,
                ExpectSingleInteriorYChange: false),
            new SolverAcceptanceScenario(
                "S4 FlatDerivative_NoOp",
                () => BuildSimple2dGraphWithForceDistanceSection(includeNormalTarget: true, midpointNormalGTarget: 1.80),
                EvaluationX: midpointX,
                SpeedMps: 0.0,
                MaxDeltaYStep: maxDeltaYStep,
                ExpectedStatus: "FlatDerivative",
                ExpectGraphUnchanged: true,
                ExpectStrictImprovement: false,
                ExpectAfterEqualsBefore: true,
                ExpectZeroErrors: false,
                ExpectSingleInteriorYChange: false),
            new SolverAcceptanceScenario(
                "S5 NonFiniteRealized_NoOp",
                () => BuildSimple2dGraphWithForceDistanceSection(includeNormalTarget: true, midpointNormalGTarget: 1.80),
                EvaluationX: midpointX,
                SpeedMps: double.MaxValue,
                MaxDeltaYStep: maxDeltaYStep,
                ExpectedStatus: "NoImprovement",
                ExpectGraphUnchanged: true,
                ExpectStrictImprovement: false,
                ExpectAfterEqualsBefore: false,
                ExpectZeroErrors: true,
                ExpectSingleInteriorYChange: false)
        };

        foreach (SolverAcceptanceScenario scenario in scenarios)
        {
            FvdGraph graph = scenario.BuildGraph();
            var beforeNodes = SnapshotNodes(graph.ControlNodes);
            int beforeDegree = graph.Degree;

            object result = StepGraphOnceOrFail(
                graph,
                evaluationX: scenario.EvaluationX,
                speedMps: scenario.SpeedMps,
                maxDeltaYStep: scenario.MaxDeltaYStep);

            string statusName = ReadEnumNamePropertyOrFail(result, "Status");
            double beforeError = ReadDoublePropertyOrFail(result, "BeforeAbsoluteNormalGError");
            double afterError = ReadDoublePropertyOrFail(result, "AfterAbsoluteNormalGError");
            FvdGraph afterGraph = ReadGraphPropertyOrFail(result, "Graph");

            Assert.Equal(scenario.ExpectedStatus, statusName);

            if (scenario.ExpectGraphUnchanged)
            {
                Assert.Equal(beforeDegree, afterGraph.Degree);
                AssertNodesEqual(beforeNodes, afterGraph.ControlNodes);
            }

            if (scenario.ExpectZeroErrors)
            {
                Assert.InRange(System.Math.Abs(beforeError), 0.0, ErrorEqualityTolerance);
                Assert.InRange(System.Math.Abs(afterError), 0.0, ErrorEqualityTolerance);
            }

            if (scenario.ExpectAfterEqualsBefore)
            {
                Assert.InRange(System.Math.Abs(afterError - beforeError), 0.0, ErrorEqualityTolerance);
            }

            if (scenario.ExpectStrictImprovement)
            {
                Assert.True(
                    afterError < beforeError,
                    $"{scenario.Name}: expected strict improvement but before={beforeError}, after={afterError}.");
                Assert.InRange(afterError, 0.0, beforeError + MonotonicTolerance);
            }

            if (scenario.ExpectSingleInteriorYChange)
            {
                Assert.Equal(beforeDegree, afterGraph.Degree);
                Assert.Equal(beforeNodes.Count, afterGraph.ControlNodes.Count);

                int changedInteriorYCount = 0;
                double changedInteriorYDeltaMagnitude = 0.0;

                for (int i = 0; i < beforeNodes.Count; i++)
                {
                    NodeSnapshot before = beforeNodes[i];
                    FvdControlNode after = afterGraph.ControlNodes[i];

                    Assert.InRange(System.Math.Abs(before.U - after.U), 0.0, NodeEqualityTolerance);
                    Assert.InRange(System.Math.Abs(before.X - after.Position.X), 0.0, NodeEqualityTolerance);
                    Assert.InRange(System.Math.Abs(before.Z - after.Position.Z), 0.0, NodeEqualityTolerance);
                    Assert.InRange(System.Math.Abs(before.Weight - after.Weight), 0.0, NodeEqualityTolerance);

                    double yDelta = after.Position.Y - before.Y;
                    bool yChanged = System.Math.Abs(yDelta) > NodeEqualityTolerance;

                    if (!yChanged)
                    {
                        Assert.InRange(System.Math.Abs(yDelta), 0.0, NodeEqualityTolerance);
                        continue;
                    }

                    bool isInterior = i > 0 && i < beforeNodes.Count - 1;
                    Assert.True(isInterior, $"{scenario.Name}: only interior node Y values may change.");

                    changedInteriorYCount++;
                    changedInteriorYDeltaMagnitude = System.Math.Abs(yDelta);
                }

                Assert.Equal(1, changedInteriorYCount);
                Assert.InRange(
                    changedInteriorYDeltaMagnitude,
                    0.0,
                    scenario.MaxDeltaYStep + NodeEqualityTolerance);

                FvdNurbsBuildResult buildResult = afterGraph.BuildNurbsCurve(64);
                Assert.NotNull(buildResult);
                Assert.False(double.IsNaN(buildResult.ArcCurve.Length));
                Assert.False(double.IsInfinity(buildResult.ArcCurve.Length));
                Assert.True(buildResult.ArcCurve.Length >= 0.0);
            }
        }
    }

    [Fact]
    public void Fvd2dNormalGSolver_MultiInteriorNodeOrchestration_CanonicalFiveNode_RedContract()
    {
        const double NodeEqualityTolerance = 1e-12;
        const double ErrorEqualityTolerance = 1e-12;
        const double MonotonicTolerance = 1e-9;
        const double maxDeltaYStep = 0.5;
        const double evaluationX = 50.0;

        MultiNodeOrchestrationRun firstRun = RunMultiInteriorOrchestrationContractOnce(
            BuildCanonicalFiveNodeGraphWithForceDistanceSection(midpointNormalGTarget: 2.2),
            evaluationX,
            maxDeltaYStep,
            NodeEqualityTolerance,
            ErrorEqualityTolerance,
            MonotonicTolerance);

        MultiNodeOrchestrationRun secondRun = RunMultiInteriorOrchestrationContractOnce(
            BuildCanonicalFiveNodeGraphWithForceDistanceSection(midpointNormalGTarget: 2.2),
            evaluationX,
            maxDeltaYStep,
            NodeEqualityTolerance,
            ErrorEqualityTolerance,
            MonotonicTolerance);

        Assert.Equal(firstRun.ChangedInteriorNodeOrder.Count, secondRun.ChangedInteriorNodeOrder.Count);
        for (int i = 0; i < firstRun.ChangedInteriorNodeOrder.Count; i++)
            Assert.Equal(firstRun.ChangedInteriorNodeOrder[i], secondRun.ChangedInteriorNodeOrder[i]);

        var uniqueChangedInteriorNodes = new HashSet<int>(firstRun.ChangedInteriorNodeOrder);
        Assert.Equal(3, uniqueChangedInteriorNodes.Count);
        Assert.Contains(1, uniqueChangedInteriorNodes);
        Assert.Contains(2, uniqueChangedInteriorNodes);
        Assert.Contains(3, uniqueChangedInteriorNodes);
    }

    private static FvdGraph BuildSimple2dGraphWithForceDistanceSection(
        bool includeNormalTarget,
        double midpointNormalGTarget,
        bool includeLateralTarget = true,
        bool includeRollRateTarget = true)
    {
        var controlNodes = new List<FvdControlNode>
        {
            new FvdControlNode(0.00, new Vector3d(0.0, 0.0, 0.0), 1.0),
            new FvdControlNode(0.33, new Vector3d(10.0, 2.0, 0.0), 1.0),
            new FvdControlNode(0.66, new Vector3d(20.0, -2.0, 0.0), 1.0),
            new FvdControlNode(1.00, new Vector3d(30.0, 0.0, 0.0), 1.0)
        };

        var functions = new List<FvdSectionFunction>();

        if (includeNormalTarget)
        {
            functions.Add(new FvdSectionFunction(
                FvdSectionChannel.NormalG,
                new List<FvdSectionSample>
                {
                    new FvdSectionSample(0.0, midpointNormalGTarget),
                    new FvdSectionSample(50.0, midpointNormalGTarget),
                    new FvdSectionSample(100.0, midpointNormalGTarget)
                }));
        }

        if (includeLateralTarget)
        {
            functions.Add(new FvdSectionFunction(
                FvdSectionChannel.LateralG,
                new List<FvdSectionSample>
                {
                    new FvdSectionSample(0.0, 0.0),
                    new FvdSectionSample(50.0, 0.0),
                    new FvdSectionSample(100.0, 0.0)
                }));
        }

        if (includeRollRateTarget)
        {
            functions.Add(new FvdSectionFunction(
                FvdSectionChannel.RollRateDegPerSec,
                new List<FvdSectionSample>
                {
                    new FvdSectionSample(0.0, 0.0),
                    new FvdSectionSample(50.0, 0.0),
                    new FvdSectionSample(100.0, 0.0)
                }));
        }

        var sections = new List<FvdSectionDefinition>
        {
            new FvdSectionDefinition(
                FvdSectionKind.Force,
                FvdFunctionDomain.Distance,
                startX: 0.0,
                endX: 100.0,
                functions)
        };

        return new FvdGraph(
            controlNodes,
            degree: 3,
            forceSamples: new List<FvdForceSample>(),
            sections: sections);
    }

    private static FvdGraph BuildTwoNodeLineGraphWithForceDistanceSection(
        bool includeNormalTarget,
        double midpointNormalGTarget)
    {
        var controlNodes = new List<FvdControlNode>
        {
            new FvdControlNode(0.0, new Vector3d(0.0, 0.0, 0.0), 1.0),
            new FvdControlNode(1.0, new Vector3d(30.0, 0.0, 0.0), 1.0)
        };

        var functions = new List<FvdSectionFunction>();
        if (includeNormalTarget)
        {
            functions.Add(new FvdSectionFunction(
                FvdSectionChannel.NormalG,
                new List<FvdSectionSample>
                {
                    new FvdSectionSample(0.0, midpointNormalGTarget),
                    new FvdSectionSample(50.0, midpointNormalGTarget),
                    new FvdSectionSample(100.0, midpointNormalGTarget)
                }));
        }

        var sections = new List<FvdSectionDefinition>
        {
            new FvdSectionDefinition(
                FvdSectionKind.Force,
                FvdFunctionDomain.Distance,
                startX: 0.0,
                endX: 100.0,
                functions)
        };

        return new FvdGraph(
            controlNodes,
            degree: 1,
            forceSamples: new List<FvdForceSample>(),
            sections: sections);
    }

    private static FvdGraph BuildCanonicalFiveNodeGraphWithForceDistanceSection(double midpointNormalGTarget)
    {
        var controlNodes = new List<FvdControlNode>
        {
            new FvdControlNode(0.00, new Vector3d(0.0, 0.0, 0.0), 1.0),
            new FvdControlNode(0.25, new Vector3d(8.0, 3.0, 0.0), 1.0),
            new FvdControlNode(0.50, new Vector3d(16.0, -3.0, 0.0), 1.0),
            new FvdControlNode(0.75, new Vector3d(24.0, 3.0, 0.0), 1.0),
            new FvdControlNode(1.00, new Vector3d(32.0, 0.0, 0.0), 1.0)
        };

        var sections = new List<FvdSectionDefinition>
        {
            new FvdSectionDefinition(
                FvdSectionKind.Force,
                FvdFunctionDomain.Distance,
                startX: 0.0,
                endX: 100.0,
                new List<FvdSectionFunction>
                {
                    new FvdSectionFunction(
                        FvdSectionChannel.NormalG,
                        new List<FvdSectionSample>
                        {
                            new FvdSectionSample(0.0, midpointNormalGTarget),
                            new FvdSectionSample(50.0, midpointNormalGTarget),
                            new FvdSectionSample(100.0, midpointNormalGTarget)
                        })
                })
        };

        return new FvdGraph(
            controlNodes,
            degree: 3,
            forceSamples: new List<FvdForceSample>(),
            sections: sections);
    }

    private static MultiNodeOrchestrationRun RunMultiInteriorOrchestrationContractOnce(
        FvdGraph initialGraph,
        double evaluationX,
        double maxDeltaYStep,
        double nodeEqualityTolerance,
        double errorEqualityTolerance,
        double monotonicTolerance)
    {
        var initialNodes = SnapshotNodes(initialGraph.ControlNodes);
        int initialDegree = initialGraph.Degree;

        FvdGraph currentGraph = initialGraph;
        var changedInteriorNodeOrder = new List<int>(capacity: 3);
        double initialAbsoluteError = double.NaN;
        double finalAbsoluteError = double.NaN;

        for (int step = 0; step < 3; step++)
        {
            object result = StepGraphOnceOrFail(
                currentGraph,
                evaluationX,
                speedMps: 20.0,
                maxDeltaYStep: maxDeltaYStep,
                enableDeterministicInteriorNodeSweep: true,
                interiorNodeSweepStartIndex: step);

            string statusName = ReadEnumNamePropertyOrFail(result, "Status");
            double beforeError = ReadDoublePropertyOrFail(result, "BeforeAbsoluteNormalGError");
            double afterError = ReadDoublePropertyOrFail(result, "AfterAbsoluteNormalGError");
            FvdGraph nextGraph = ReadGraphPropertyOrFail(result, "Graph");

            AssertCompatibleWithCurrentStatusErrorModel(statusName, beforeError, afterError, errorEqualityTolerance, monotonicTolerance);

            if (step == 0)
                initialAbsoluteError = beforeError;

            finalAbsoluteError = afterError;

            Assert.Equal(initialDegree, nextGraph.Degree);
            Assert.Equal(initialNodes.Count, nextGraph.ControlNodes.Count);

            int changedInteriorIndex = AssertSingleInteriorYChangeAndReturnIndex(
                currentGraph.ControlNodes,
                nextGraph.ControlNodes,
                nodeEqualityTolerance);

            changedInteriorNodeOrder.Add(changedInteriorIndex);
            currentGraph = nextGraph;
        }

        Assert.InRange(finalAbsoluteError, 0.0, initialAbsoluteError + monotonicTolerance);

        AssertEndpointUnchanged(initialNodes, currentGraph.ControlNodes, nodeEqualityTolerance);
        AssertPerNodeInvariantFieldsUnchanged(initialNodes, currentGraph.ControlNodes, nodeEqualityTolerance);
        AssertFinalInteriorDeltasWithinStepBound(initialNodes, currentGraph.ControlNodes, maxDeltaYStep, nodeEqualityTolerance);

        return new MultiNodeOrchestrationRun(changedInteriorNodeOrder, initialAbsoluteError, finalAbsoluteError);
    }

    private static void AssertCompatibleWithCurrentStatusErrorModel(
        string statusName,
        double beforeError,
        double afterError,
        double errorEqualityTolerance,
        double monotonicTolerance)
    {
        Assert.Contains(
            statusName,
            new[]
            {
                "Success",
                "NoNormalTarget",
                "NoInteriorNode",
                "FlatDerivative",
                "NoImprovement"
            });

        Assert.False(double.IsNaN(beforeError));
        Assert.False(double.IsInfinity(beforeError));
        Assert.False(double.IsNaN(afterError));
        Assert.False(double.IsInfinity(afterError));
        Assert.True(beforeError >= 0.0);
        Assert.True(afterError >= 0.0);

        if (statusName == "Success")
        {
            Assert.InRange(afterError, 0.0, beforeError + monotonicTolerance);
            return;
        }

        if (statusName == "NoNormalTarget" || statusName == "NoInteriorNode")
        {
            Assert.InRange(System.Math.Abs(beforeError), 0.0, errorEqualityTolerance);
            Assert.InRange(System.Math.Abs(afterError), 0.0, errorEqualityTolerance);
            return;
        }

        Assert.InRange(System.Math.Abs(afterError - beforeError), 0.0, errorEqualityTolerance);
    }

    private static int AssertSingleInteriorYChangeAndReturnIndex(
        IReadOnlyList<FvdControlNode> beforeNodes,
        IReadOnlyList<FvdControlNode> afterNodes,
        double nodeEqualityTolerance)
    {
        Assert.Equal(beforeNodes.Count, afterNodes.Count);

        int changedInteriorIndex = -1;

        for (int i = 0; i < beforeNodes.Count; i++)
        {
            FvdControlNode before = beforeNodes[i];
            FvdControlNode after = afterNodes[i];

            Assert.InRange(System.Math.Abs(before.U - after.U), 0.0, nodeEqualityTolerance);
            Assert.InRange(System.Math.Abs(before.Position.X - after.Position.X), 0.0, nodeEqualityTolerance);
            Assert.InRange(System.Math.Abs(before.Position.Z - after.Position.Z), 0.0, nodeEqualityTolerance);
            Assert.InRange(System.Math.Abs(before.Weight - after.Weight), 0.0, nodeEqualityTolerance);

            double deltaY = after.Position.Y - before.Position.Y;
            if (System.Math.Abs(deltaY) <= nodeEqualityTolerance)
                continue;

            bool isInterior = i > 0 && i < beforeNodes.Count - 1;
            Assert.True(isInterior, "Only interior node Y values may change.");

            if (changedInteriorIndex >= 0)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Expected exactly one changed interior node per step, but found indices {changedInteriorIndex} and {i}.");
            }

            changedInteriorIndex = i;
        }

        Assert.True(changedInteriorIndex >= 0, "Expected one interior node Y change per orchestration step.");
        return changedInteriorIndex;
    }

    private static void AssertEndpointUnchanged(
        IReadOnlyList<NodeSnapshot> before,
        IReadOnlyList<FvdControlNode> after,
        double nodeEqualityTolerance)
    {
        Assert.Equal(before.Count, after.Count);

        int first = 0;
        int last = before.Count - 1;

        Assert.InRange(System.Math.Abs(before[first].Y - after[first].Position.Y), 0.0, nodeEqualityTolerance);
        Assert.InRange(System.Math.Abs(before[last].Y - after[last].Position.Y), 0.0, nodeEqualityTolerance);
    }

    private static void AssertPerNodeInvariantFieldsUnchanged(
        IReadOnlyList<NodeSnapshot> before,
        IReadOnlyList<FvdControlNode> after,
        double nodeEqualityTolerance)
    {
        Assert.Equal(before.Count, after.Count);

        for (int i = 0; i < before.Count; i++)
        {
            NodeSnapshot left = before[i];
            FvdControlNode right = after[i];

            Assert.InRange(System.Math.Abs(left.X - right.Position.X), 0.0, nodeEqualityTolerance);
            Assert.InRange(System.Math.Abs(left.Z - right.Position.Z), 0.0, nodeEqualityTolerance);
            Assert.InRange(System.Math.Abs(left.Weight - right.Weight), 0.0, nodeEqualityTolerance);
        }
    }

    private static void AssertFinalInteriorDeltasWithinStepBound(
        IReadOnlyList<NodeSnapshot> before,
        IReadOnlyList<FvdControlNode> after,
        double maxDeltaYStep,
        double nodeEqualityTolerance)
    {
        Assert.Equal(before.Count, after.Count);

        for (int i = 1; i < before.Count - 1; i++)
        {
            double deltaY = after[i].Position.Y - before[i].Y;
            if (System.Math.Abs(deltaY) <= nodeEqualityTolerance)
                continue;

            Assert.InRange(
                System.Math.Abs(deltaY),
                0.0,
                maxDeltaYStep + nodeEqualityTolerance);
        }
    }

    private static object StepGraphOnceOrFail(
        FvdGraph graph,
        double evaluationX,
        double speedMps = 20.0,
        double finiteDifferenceDeltaY = 0.50,
        double maxDeltaYStep = 1.00,
        double? derivativeEpsilon = null,
        bool? enableDeterministicInteriorNodeSweep = null,
        int? interiorNodeSweepStartIndex = null)
    {
        Type solverType = RequireFvdType("Quantum.FVD.Fvd2dNormalGSolver");
        Type optionsType = RequireFvdType("Quantum.FVD.Fvd2dNormalGSolverOptions");

        object solver = Activator.CreateInstance(solverType)
            ?? throw new Xunit.Sdk.XunitException("Expected to create Fvd2dNormalGSolver.");
        object options = Activator.CreateInstance(optionsType)
            ?? throw new Xunit.Sdk.XunitException("Expected to create Fvd2dNormalGSolverOptions.");

        SetPropertyIfPresent(options, "Domain", FvdFunctionDomain.Distance);
        SetPropertyIfPresent(options, "EvaluationX", evaluationX);
        SetPropertyIfPresent(options, "SpeedMps", speedMps);
        SetPropertyIfPresent(options, "FiniteDifferenceDeltaY", finiteDifferenceDeltaY);
        SetPropertyIfPresent(options, "MaxDeltaYStep", maxDeltaYStep);
        if (derivativeEpsilon.HasValue)
            SetPropertyIfPresent(options, "DerivativeEpsilon", derivativeEpsilon.Value);
        if (enableDeterministicInteriorNodeSweep.HasValue)
            SetPropertyIfPresent(options, "EnableDeterministicInteriorNodeSweep", enableDeterministicInteriorNodeSweep.Value);
        if (interiorNodeSweepStartIndex.HasValue)
            SetPropertyIfPresent(options, "InteriorNodeSweepStartIndex", interiorNodeSweepStartIndex.Value);

        MethodInfo? stepMethod = solverType.GetMethod(
            "Step",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(FvdGraph), optionsType },
            modifiers: null);

        Assert.True(
            stepMethod is not null,
            "Expected method: Fvd2dNormalGSolver.Step(FvdGraph graph, Fvd2dNormalGSolverOptions options).");

        object? result = stepMethod!.Invoke(solver, new[] { (object)graph, options });
        Assert.True(result is not null, "Expected solver Step(...) to return a non-null result.");

        return result!;
    }

    private static FvdGraph ReadGraphPropertyOrFail(object instance, string propertyName)
    {
        PropertyInfo? property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.True(
            property is not null,
            $"Expected solver step result to expose property '{propertyName}'.");

        object? value = property!.GetValue(instance);
        return Assert.IsType<FvdGraph>(value);
    }

    private static string ReadEnumNamePropertyOrFail(object instance, string propertyName)
    {
        PropertyInfo? property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.True(
            property is not null,
            $"Expected solver step result to expose property '{propertyName}'.");

        object? value = property!.GetValue(instance);
        Assert.True(value is Enum, $"Expected property '{propertyName}' to be enum-valued.");

        string? name = Enum.GetName(value!.GetType(), value);
        Assert.True(!string.IsNullOrWhiteSpace(name), $"Expected enum name for property '{propertyName}'.");

        return name!;
    }

    private static double ReadDoublePropertyOrFail(object instance, string propertyName)
    {
        PropertyInfo? property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.True(
            property is not null,
            $"Expected solver step result to expose property '{propertyName}'.");

        object? value = property!.GetValue(instance);
        Assert.True(value is double, $"Expected property '{propertyName}' to be a double.");
        return (double)value!;
    }

    private static void SetPropertyIfPresent(object instance, string propertyName, object value)
    {
        PropertyInfo? property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property is null || !property.CanWrite)
            return;

        if (value is null)
        {
            property.SetValue(instance, null);
            return;
        }

        if (property.PropertyType.IsAssignableFrom(value.GetType()))
        {
            property.SetValue(instance, value);
            return;
        }

        if (property.PropertyType.IsEnum && value is string enumName)
        {
            object parsed = Enum.Parse(property.PropertyType, enumName, ignoreCase: false);
            property.SetValue(instance, parsed);
            return;
        }

        object converted = Convert.ChangeType(value, property.PropertyType);
        property.SetValue(instance, converted);
    }

    private static Type RequireFvdType(string fullName)
    {
        Type? type = Assembly.Load("Quantum.FVD").GetType(fullName);
        Assert.True(type is not null, $"Expected {fullName} to exist.");
        return type!;
    }

    private static List<NodeSnapshot> SnapshotNodes(IReadOnlyList<FvdControlNode> nodes)
    {
        var snapshot = new List<NodeSnapshot>(nodes.Count);
        for (int i = 0; i < nodes.Count; i++)
        {
            FvdControlNode node = nodes[i];
            snapshot.Add(new NodeSnapshot(
                node.U,
                node.Position.X,
                node.Position.Y,
                node.Position.Z,
                node.Weight));
        }

        return snapshot;
    }

    private static void AssertNodesEqual(IReadOnlyList<NodeSnapshot> expected, IReadOnlyList<FvdControlNode> actual)
    {
        Assert.Equal(expected.Count, actual.Count);

        for (int i = 0; i < expected.Count; i++)
        {
            NodeSnapshot left = expected[i];
            FvdControlNode right = actual[i];

            AssertNear(left.U, right.U);
            AssertNear(left.X, right.Position.X);
            AssertNear(left.Y, right.Position.Y);
            AssertNear(left.Z, right.Position.Z);
            AssertNear(left.Weight, right.Weight);
        }
    }

    private static void AssertNear(double expected, double actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, EqualityTolerance);
    }

    private readonly record struct SolverAcceptanceScenario(
        string Name,
        Func<FvdGraph> BuildGraph,
        double EvaluationX,
        double SpeedMps,
        double MaxDeltaYStep,
        string ExpectedStatus,
        bool ExpectGraphUnchanged,
        bool ExpectStrictImprovement,
        bool ExpectAfterEqualsBefore,
        bool ExpectZeroErrors,
        bool ExpectSingleInteriorYChange);

    private readonly record struct MultiNodeOrchestrationRun(
        IReadOnlyList<int> ChangedInteriorNodeOrder,
        double InitialAbsoluteError,
        double FinalAbsoluteError);

    private readonly record struct NodeSnapshot(double U, double X, double Y, double Z, double Weight);
}
