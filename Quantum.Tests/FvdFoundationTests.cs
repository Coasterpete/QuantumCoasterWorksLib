using System;
using System.Collections.Generic;
using System.Reflection;
using Quantum.Math;
using Quantum.Splines;
using Xunit;

namespace Quantum.Tests;

public class FvdFoundationTests
{
    private const double ValueTolerance = 1e-6;
    private const double LengthTolerance = 1e-6;

    [Fact]
    public void FvdGraph_RejectsOutOfRangeUValues()
    {
        Type graphType = RequireFvdGraphType();
        Type nodeType = RequireFvdControlNodeType();

        Assert.ThrowsAny<Exception>(() =>
            CreateGraphOrFail(
                graphType,
                nodeType,
                degree: 3,
                CreateNodeOrFail(nodeType, -0.10, new Vector3d(0, 0, 0), 1.0),
                CreateNodeOrFail(nodeType, 0.40, new Vector3d(4, 1, 0), 1.0),
                CreateNodeOrFail(nodeType, 0.70, new Vector3d(7, 2, 0), 1.0),
                CreateNodeOrFail(nodeType, 1.00, new Vector3d(10, 0, 0), 1.0)));

        Assert.ThrowsAny<Exception>(() =>
            CreateGraphOrFail(
                graphType,
                nodeType,
                degree: 3,
                CreateNodeOrFail(nodeType, 0.00, new Vector3d(0, 0, 0), 1.0),
                CreateNodeOrFail(nodeType, 0.30, new Vector3d(3, 2, 0), 1.0),
                CreateNodeOrFail(nodeType, 0.65, new Vector3d(7, 2, 0), 1.0),
                CreateNodeOrFail(nodeType, 1.10, new Vector3d(10, 0, 0), 1.0)));
    }

    [Fact]
    public void FvdGraph_RejectsUnsortedOrDuplicateUValues()
    {
        Type graphType = RequireFvdGraphType();
        Type nodeType = RequireFvdControlNodeType();

        Assert.ThrowsAny<Exception>(() =>
            CreateGraphOrFail(
                graphType,
                nodeType,
                degree: 3,
                CreateNodeOrFail(nodeType, 0.00, new Vector3d(0, 0, 0), 1.0),
                CreateNodeOrFail(nodeType, 0.80, new Vector3d(8, 2, 0), 1.0),
                CreateNodeOrFail(nodeType, 0.50, new Vector3d(5, 3, 0), 1.0),
                CreateNodeOrFail(nodeType, 1.00, new Vector3d(10, 0, 0), 1.0)));

        Assert.ThrowsAny<Exception>(() =>
            CreateGraphOrFail(
                graphType,
                nodeType,
                degree: 3,
                CreateNodeOrFail(nodeType, 0.00, new Vector3d(0, 0, 0), 1.0),
                CreateNodeOrFail(nodeType, 0.50, new Vector3d(5, 2, 0), 1.0),
                CreateNodeOrFail(nodeType, 0.50, new Vector3d(6, 3, 0), 1.0),
                CreateNodeOrFail(nodeType, 1.00, new Vector3d(10, 0, 0), 1.0)));
    }

    [Fact]
    public void FvdGraph_RejectsNonPositiveOrNonFiniteWeights()
    {
        Type graphType = RequireFvdGraphType();
        Type nodeType = RequireFvdControlNodeType();

        Assert.ThrowsAny<Exception>(() =>
            CreateGraphOrFail(
                graphType,
                nodeType,
                degree: 3,
                CreateNodeOrFail(nodeType, 0.00, new Vector3d(0, 0, 0), 1.0),
                CreateNodeOrFail(nodeType, 0.33, new Vector3d(4, 3, 0), 0.0),
                CreateNodeOrFail(nodeType, 0.66, new Vector3d(7, 3, 0), 1.0),
                CreateNodeOrFail(nodeType, 1.00, new Vector3d(10, 0, 0), 1.0)));

        Assert.ThrowsAny<Exception>(() =>
            CreateGraphOrFail(
                graphType,
                nodeType,
                degree: 3,
                CreateNodeOrFail(nodeType, 0.00, new Vector3d(0, 0, 0), 1.0),
                CreateNodeOrFail(nodeType, 0.33, new Vector3d(4, 3, 0), -0.5),
                CreateNodeOrFail(nodeType, 0.66, new Vector3d(7, 3, 0), 1.0),
                CreateNodeOrFail(nodeType, 1.00, new Vector3d(10, 0, 0), 1.0)));

        Assert.ThrowsAny<Exception>(() =>
            CreateGraphOrFail(
                graphType,
                nodeType,
                degree: 3,
                CreateNodeOrFail(nodeType, 0.00, new Vector3d(0, 0, 0), 1.0),
                CreateNodeOrFail(nodeType, 0.33, new Vector3d(4, 3, 0), double.NaN),
                CreateNodeOrFail(nodeType, 0.66, new Vector3d(7, 3, 0), 1.0),
                CreateNodeOrFail(nodeType, 1.00, new Vector3d(10, 0, 0), 1.0)));

        Assert.ThrowsAny<Exception>(() =>
            CreateGraphOrFail(
                graphType,
                nodeType,
                degree: 3,
                CreateNodeOrFail(nodeType, 0.00, new Vector3d(0, 0, 0), 1.0),
                CreateNodeOrFail(nodeType, 0.33, new Vector3d(4, 3, 0), double.PositiveInfinity),
                CreateNodeOrFail(nodeType, 0.66, new Vector3d(7, 3, 0), 1.0),
                CreateNodeOrFail(nodeType, 1.00, new Vector3d(10, 0, 0), 1.0)));
    }

    [Fact]
    public void FvdGraph_RejectsInsufficientControlNodesForDegree()
    {
        Type graphType = RequireFvdGraphType();
        Type nodeType = RequireFvdControlNodeType();

        Assert.ThrowsAny<Exception>(() =>
            CreateGraphOrFail(
                graphType,
                nodeType,
                degree: 3,
                CreateNodeOrFail(nodeType, 0.00, new Vector3d(0, 0, 0), 1.0),
                CreateNodeOrFail(nodeType, 0.50, new Vector3d(5, 3, 0), 1.0),
                CreateNodeOrFail(nodeType, 1.00, new Vector3d(10, 0, 0), 1.0)));
    }

    [Fact]
    public void FvdGraph_ValidGraph_BuildsNurbsBackedCurve_WithFinitePositionsAndNormalizedTangents()
    {
        Type graphType = RequireFvdGraphType();
        Type nodeType = RequireFvdControlNodeType();

        object graph = CreateGraphOrFail(
            graphType,
            nodeType,
            degree: 3,
            CreateNodeOrFail(nodeType, 0.00, new Vector3d(0, 0, 0), 1.0),
            CreateNodeOrFail(nodeType, 0.33, new Vector3d(4, 5, 0), 0.9),
            CreateNodeOrFail(nodeType, 0.66, new Vector3d(8, -3, 0), 1.2),
            CreateNodeOrFail(nodeType, 1.00, new Vector3d(12, 0, 0), 1.0));

        object buildResult = BuildNurbsCurveOrFail(graph, arcLengthSamples: 200);
        IParamCurve paramCurve = GetParamCurveOrFail(buildResult);

        Assert.Equal("NurbsCurve", paramCurve.GetType().Name);

        foreach (double t in SampleTs())
        {
            Vector3d pos = paramCurve.Evaluate(t);
            Vector3d tan = paramCurve.Tangent(t);

            AssertFinite(pos);
            AssertFinite(tan);
            AssertNormalizedNonZero(tan);
        }
    }

    [Fact]
    public void FvdGraph_BuiltArcLengthCurve_ClampsEndpointsSafely()
    {
        Type graphType = RequireFvdGraphType();
        Type nodeType = RequireFvdControlNodeType();

        object graph = CreateGraphOrFail(
            graphType,
            nodeType,
            degree: 3,
            CreateNodeOrFail(nodeType, 0.00, new Vector3d(0, 0, 0), 1.0),
            CreateNodeOrFail(nodeType, 0.33, new Vector3d(4, 5, 0), 0.9),
            CreateNodeOrFail(nodeType, 0.66, new Vector3d(8, -3, 0), 1.2),
            CreateNodeOrFail(nodeType, 1.00, new Vector3d(12, 0, 0), 1.0));

        object buildResult = BuildNurbsCurveOrFail(graph, arcLengthSamples: 200);
        IArcLengthCurve arcCurve = GetArcCurveOrFail(buildResult);

        Vector3d start = arcCurve.EvaluateByLength(0.0);
        Vector3d end = arcCurve.EvaluateByLength(arcCurve.Length);
        Vector3d beforeStart = arcCurve.EvaluateByLength(-1.0);
        Vector3d afterEnd = arcCurve.EvaluateByLength(arcCurve.Length + 1.0);

        AssertVectorNear(start, beforeStart, ValueTolerance);
        AssertVectorNear(end, afterEnd, ValueTolerance);

        Vector3d tanBeforeStart = arcCurve.TangentByLength(-1.0);
        Vector3d tanAfterEnd = arcCurve.TangentByLength(arcCurve.Length + 1.0);

        AssertFinite(tanBeforeStart);
        AssertFinite(tanAfterEnd);
        AssertNormalizedNonZero(tanBeforeStart);
        AssertNormalizedNonZero(tanAfterEnd);
    }

    private static IEnumerable<double> SampleTs()
    {
        yield return 0.0;
        yield return 0.25;
        yield return 0.5;
        yield return 0.75;
        yield return 1.0;
    }

    private static object BuildNurbsCurveOrFail(object graph, int arcLengthSamples)
    {
        Type graphType = graph.GetType();
        MethodInfo? buildNurbsCurve = graphType.GetMethod(
            "BuildNurbsCurve",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(int) },
            modifiers: null);

        Assert.True(
            buildNurbsCurve is not null,
            "Expected method: FvdGraph.BuildNurbsCurve(int arcLengthSamples).");

        object? result = buildNurbsCurve!.Invoke(graph, new object[] { arcLengthSamples });
        Assert.True(result is not null, "Expected BuildNurbsCurve to return a non-null build result.");

        Type buildResultType = RequireFvdNurbsBuildResultType();
        Assert.Equal(buildResultType, result!.GetType());
        return result!;
    }

    private static IParamCurve GetParamCurveOrFail(object buildResult)
    {
        Type resultType = buildResult.GetType();
        PropertyInfo? prop = resultType.GetProperty("ParamCurve", BindingFlags.Public | BindingFlags.Instance);

        Assert.True(
            prop is not null,
            "Expected FvdNurbsBuildResult.ParamCurve property.");

        object? value = prop!.GetValue(buildResult);
        return Assert.IsAssignableFrom<IParamCurve>(value);
    }

    private static IArcLengthCurve GetArcCurveOrFail(object buildResult)
    {
        Type resultType = buildResult.GetType();
        PropertyInfo? prop = resultType.GetProperty("ArcCurve", BindingFlags.Public | BindingFlags.Instance);

        Assert.True(
            prop is not null,
            "Expected FvdNurbsBuildResult.ArcCurve property.");

        object? value = prop!.GetValue(buildResult);
        return Assert.IsAssignableFrom<IArcLengthCurve>(value);
    }

    private static object CreateGraphOrFail(Type graphType, Type nodeType, int degree, params object[] nodes)
    {
        Type nodeListType = typeof(List<>).MakeGenericType(nodeType);
        object nodeList = Activator.CreateInstance(nodeListType)
            ?? throw new Xunit.Sdk.XunitException("Expected to create List<FvdControlNode>.");

        MethodInfo? addMethod = nodeListType.GetMethod("Add", new[] { nodeType });
        Assert.True(addMethod is not null, "Expected List<FvdControlNode>.Add(FvdControlNode) to exist.");

        foreach (object node in nodes)
        {
            addMethod!.Invoke(nodeList, new[] { node });
        }

        ConstructorInfo? ctor = graphType.GetConstructor(new[] { nodeListType, typeof(int) });
        Assert.True(
            ctor is not null,
            "Expected constructor: FvdGraph(List<FvdControlNode> controlNodes, int degree).");

        object? graph = ctor!.Invoke(new[] { nodeList, (object)degree });
        Assert.True(graph is not null, "Expected FvdGraph constructor to return a non-null instance.");
        return graph!;
    }

    private static object CreateNodeOrFail(Type nodeType, double u, Vector3d position, double weight)
    {
        ConstructorInfo? ctor = nodeType.GetConstructor(new[] { typeof(double), typeof(Vector3d), typeof(double) });
        Assert.True(
            ctor is not null,
            "Expected constructor: FvdControlNode(double u, Vector3d position, double weight).");

        object? node = ctor!.Invoke(new object[] { u, position, weight });
        Assert.True(node is not null, "Expected FvdControlNode constructor to return a non-null instance.");
        return node!;
    }

    private static Type RequireFvdGraphType()
    {
        Type? type = RequireFvdAssembly().GetType("Quantum.FVD.FvdGraph");
        Assert.True(type is not null, "Expected Quantum.FVD.FvdGraph to exist.");
        return type!;
    }

    private static Type RequireFvdControlNodeType()
    {
        Type? type = RequireFvdAssembly().GetType("Quantum.FVD.FvdControlNode");
        Assert.True(type is not null, "Expected Quantum.FVD.FvdControlNode to exist.");
        return type!;
    }

    private static Type RequireFvdNurbsBuildResultType()
    {
        Type? type = RequireFvdAssembly().GetType("Quantum.FVD.FvdNurbsBuildResult");
        Assert.True(type is not null, "Expected Quantum.FVD.FvdNurbsBuildResult to exist.");
        return type!;
    }

    private static Assembly RequireFvdAssembly()
    {
        return Assembly.Load("Quantum.FVD");
    }

    private static void AssertFinite(Vector3d value)
    {
        Assert.False(double.IsNaN(value.X) || double.IsNaN(value.Y) || double.IsNaN(value.Z));
        Assert.False(double.IsInfinity(value.X) || double.IsInfinity(value.Y) || double.IsInfinity(value.Z));
    }

    private static void AssertNormalizedNonZero(Vector3d tangent)
    {
        double len = tangent.Length;
        Assert.False(double.IsNaN(len) || double.IsInfinity(len));
        Assert.True(len > MathUtil.Epsilon, $"Expected non-zero tangent length, got {len}.");
        Assert.InRange(System.Math.Abs(len - 1.0), 0.0, LengthTolerance);
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual, double tolerance)
    {
        Assert.InRange(System.Math.Abs(expected.X - actual.X), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(expected.Y - actual.Y), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(expected.Z - actual.Z), 0.0, tolerance);
    }
}
