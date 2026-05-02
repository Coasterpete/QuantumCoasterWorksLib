using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Xunit;

namespace Quantum.Tests;

public class FvdSectionEvaluationTests
{
    private const double ValueTolerance = 1e-9;

    [Fact]
    public void FvdSectionFunction_EvaluateAt_ExactSampleX_ReturnsExactValue()
    {
        Type functionType = RequireSectionFunctionType();
        object function = CreateSectionFunctionOrFail(
            functionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(0.0, 1.0),
            CreateSectionSampleOrFail(1.0, 3.0));

        double atStart = EvaluateFunctionAtOrFail(function, 0.0);
        double atEnd = EvaluateFunctionAtOrFail(function, 1.0);

        Assert.InRange(System.Math.Abs(atStart - 1.0), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(atEnd - 3.0), 0.0, ValueTolerance);
    }

    [Fact]
    public void FvdSectionFunction_EvaluateAt_InteriorX_InterpolatesLinearly()
    {
        Type functionType = RequireSectionFunctionType();
        object function = CreateSectionFunctionOrFail(
            functionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(0.0, 2.0),
            CreateSectionSampleOrFail(10.0, 6.0));

        double value = EvaluateFunctionAtOrFail(function, 2.5);

        Assert.InRange(System.Math.Abs(value - 3.0), 0.0, ValueTolerance);
    }

    [Fact]
    public void FvdSectionFunction_EvaluateAt_BelowFirstSample_ClampsToFirstValue()
    {
        Type functionType = RequireSectionFunctionType();
        object function = CreateSectionFunctionOrFail(
            functionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(2.0, 4.0),
            CreateSectionSampleOrFail(5.0, 10.0));

        double value = EvaluateFunctionAtOrFail(function, 1.0);

        Assert.InRange(System.Math.Abs(value - 4.0), 0.0, ValueTolerance);
    }

    [Fact]
    public void FvdSectionFunction_EvaluateAt_AboveLastSample_ClampsToLastValue()
    {
        Type functionType = RequireSectionFunctionType();
        object function = CreateSectionFunctionOrFail(
            functionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(2.0, 4.0),
            CreateSectionSampleOrFail(5.0, 10.0));

        double value = EvaluateFunctionAtOrFail(function, 8.0);

        Assert.InRange(System.Math.Abs(value - 10.0), 0.0, ValueTolerance);
    }

    [Fact]
    public void FvdSectionFunction_EvaluateAt_NaNOrInfinity_Throws()
    {
        Type functionType = RequireSectionFunctionType();
        object function = CreateSectionFunctionOrFail(
            functionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(0.0, 1.0),
            CreateSectionSampleOrFail(1.0, 2.0));

        Assert.ThrowsAny<Exception>(() => EvaluateFunctionAtOrFail(function, double.NaN));
        Assert.ThrowsAny<Exception>(() => EvaluateFunctionAtOrFail(function, double.PositiveInfinity));
        Assert.ThrowsAny<Exception>(() => EvaluateFunctionAtOrFail(function, double.NegativeInfinity));
    }

    [Fact]
    public void FvdSectionDefinition_EvaluateAt_ExistingChannel_ReturnsEvaluatedValue()
    {
        Type sectionDefinitionType = RequireSectionDefinitionType();
        Type sectionFunctionType = RequireSectionFunctionType();

        object function = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(0.0, 2.0),
            CreateSectionSampleOrFail(10.0, 6.0));

        object section = CreateSectionDefinitionOrFail(
            sectionDefinitionType,
            kindName: "Force",
            domainName: "Distance",
            startX: 0.0,
            endX: 10.0,
            function);

        double value = EvaluateSectionAtOrFail(section, channelName: "NormalG", x: 2.5);

        Assert.InRange(System.Math.Abs(value - 3.0), 0.0, ValueTolerance);
    }

    [Fact]
    public void FvdSectionDefinition_EvaluateAt_MissingChannel_ThrowsClearly()
    {
        Type sectionDefinitionType = RequireSectionDefinitionType();
        Type sectionFunctionType = RequireSectionFunctionType();

        object function = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(0.0, 2.0),
            CreateSectionSampleOrFail(10.0, 6.0));

        object section = CreateSectionDefinitionOrFail(
            sectionDefinitionType,
            kindName: "Force",
            domainName: "Distance",
            startX: 0.0,
            endX: 10.0,
            function);

        Exception ex = Assert.ThrowsAny<Exception>(() =>
            EvaluateSectionAtOrFail(section, channelName: "LateralG", x: 2.5));

        Assert.Contains("channel", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FvdSectionDefinition_EvaluateAllAt_ReturnsAllDefinedChannels_WithValuesMatchingEvaluateAt()
    {
        Type sectionDefinitionType = RequireSectionDefinitionType();
        Type sectionFunctionType = RequireSectionFunctionType();

        object rollRate = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "RollRateDegPerSec",
            CreateSectionSampleOrFail(0.0, 1.0),
            CreateSectionSampleOrFail(10.0, 3.0));
        object normal = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(0.0, 2.0),
            CreateSectionSampleOrFail(10.0, 6.0));
        object lateral = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "LateralG",
            CreateSectionSampleOrFail(0.0, -1.0),
            CreateSectionSampleOrFail(10.0, 1.0));

        object section = CreateSectionDefinitionOrFail(
            sectionDefinitionType,
            kindName: "Force",
            domainName: "Distance",
            startX: 0.0,
            endX: 10.0,
            rollRate,
            normal,
            lateral);

        const double x = 2.5;
        IReadOnlyList<(string ChannelName, double Value)> evaluations = EvaluateAllSectionChannelsAtOrFail(section, x);

        Assert.Equal(3, evaluations.Count);
        Assert.Equal(
            new[] { "NormalG", "LateralG", "RollRateDegPerSec" },
            evaluations.Select(e => e.ChannelName).ToArray());

        foreach ((string channelName, double value) in evaluations)
        {
            double expected = EvaluateSectionAtOrFail(section, channelName, x);
            Assert.InRange(System.Math.Abs(value - expected), 0.0, ValueTolerance);
        }
    }

    [Fact]
    public void FvdSectionDefinition_EvaluateAllAt_OrdersOutputByCanonicalSectionChannelEnumOrder()
    {
        Type sectionDefinitionType = RequireSectionDefinitionType();
        Type sectionFunctionType = RequireSectionFunctionType();

        object rollRate = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "RollRateDegPerSec",
            CreateSectionSampleOrFail(0.0, 0.0),
            CreateSectionSampleOrFail(10.0, 1.0));
        object normal = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(0.0, 1.0),
            CreateSectionSampleOrFail(10.0, 2.0));
        object lateral = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "LateralG",
            CreateSectionSampleOrFail(0.0, 2.0),
            CreateSectionSampleOrFail(10.0, 3.0));

        object section = CreateSectionDefinitionOrFail(
            sectionDefinitionType,
            kindName: "Force",
            domainName: "Distance",
            startX: 0.0,
            endX: 10.0,
            rollRate,
            normal,
            lateral);

        IReadOnlyList<(string ChannelName, double Value)> evaluations = EvaluateAllSectionChannelsAtOrFail(section, 4.0);
        string[] actualOrder = evaluations.Select(e => e.ChannelName).ToArray();

        string[] expectedOrder = Enum
            .GetNames(RequireSectionChannelType())
            .Where(name => name is "NormalG" or "LateralG" or "RollRateDegPerSec")
            .ToArray();

        Assert.Equal(expectedOrder, actualOrder);
    }

    [Fact]
    public void FvdSectionDefinition_EvaluateAllAt_RepeatedCallAtSameX_IsIdenticalAndOrdered()
    {
        Type sectionDefinitionType = RequireSectionDefinitionType();
        Type sectionFunctionType = RequireSectionFunctionType();

        object rollRate = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "RollRateDegPerSec",
            CreateSectionSampleOrFail(0.0, 4.0),
            CreateSectionSampleOrFail(10.0, 8.0));
        object normal = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(0.0, -2.0),
            CreateSectionSampleOrFail(10.0, 2.0));
        object lateral = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "LateralG",
            CreateSectionSampleOrFail(0.0, 3.0),
            CreateSectionSampleOrFail(10.0, 5.0));

        object section = CreateSectionDefinitionOrFail(
            sectionDefinitionType,
            kindName: "Force",
            domainName: "Distance",
            startX: 0.0,
            endX: 10.0,
            rollRate,
            normal,
            lateral);

        const double x = 1.25;
        IReadOnlyList<(string ChannelName, double Value)> first = EvaluateAllSectionChannelsAtOrFail(section, x);
        IReadOnlyList<(string ChannelName, double Value)> second = EvaluateAllSectionChannelsAtOrFail(section, x);

        Assert.Equal(first.Select(e => e.ChannelName).ToArray(), second.Select(e => e.ChannelName).ToArray());
        Assert.Equal(
            new[] { "NormalG", "LateralG", "RollRateDegPerSec" },
            first.Select(e => e.ChannelName).ToArray());

        for (int i = 0; i < first.Count; i++)
        {
            Assert.InRange(System.Math.Abs(first[i].Value - second[i].Value), 0.0, ValueTolerance);
        }
    }

    [Fact]
    public void FvdGraph_EvaluateSectionChannelAt_TouchingSections_UsesRightSectionAtBoundary()
    {
        Type sectionDefinitionType = RequireSectionDefinitionType();
        Type sectionFunctionType = RequireSectionFunctionType();

        object leftNormal = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(0.0, 1.0),
            CreateSectionSampleOrFail(10.0, 2.0));

        object rightNormal = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(10.0, 7.0),
            CreateSectionSampleOrFail(20.0, 8.0));

        object leftSection = CreateSectionDefinitionOrFail(
            sectionDefinitionType,
            kindName: "Force",
            domainName: "Distance",
            startX: 0.0,
            endX: 10.0,
            leftNormal);

        object rightSection = CreateSectionDefinitionOrFail(
            sectionDefinitionType,
            kindName: "Force",
            domainName: "Distance",
            startX: 10.0,
            endX: 20.0,
            rightNormal);

        Type sectionListType = typeof(List<>).MakeGenericType(sectionDefinitionType);
        object sectionList = CreateTypedList(sectionListType, leftSection, rightSection);

        Type nodeType = RequireFvdControlNodeType();
        Type nodeListType = typeof(List<>).MakeGenericType(nodeType);
        ConstructorInfo? nodeCtor = nodeType.GetConstructor(new[] { typeof(double), typeof(Quantum.Math.Vector3d), typeof(double) });

        Assert.True(
            nodeCtor is not null,
            "Expected constructor: FvdControlNode(double u, Vector3d position, double weight).");

        object nodeList = CreateTypedList(
            nodeListType,
            nodeCtor!.Invoke(new object[] { 0.00, new Quantum.Math.Vector3d(0, 0, 0), 1.0 })!,
            nodeCtor!.Invoke(new object[] { 0.33, new Quantum.Math.Vector3d(4, 5, 0), 0.9 })!,
            nodeCtor!.Invoke(new object[] { 0.66, new Quantum.Math.Vector3d(8, -3, 0), 1.2 })!,
            nodeCtor!.Invoke(new object[] { 1.00, new Quantum.Math.Vector3d(12, 0, 0), 1.0 })!);

        Type forceSampleType = RequireFvdForceSampleType();
        Type forceSampleListType = typeof(List<>).MakeGenericType(forceSampleType);
        object forceSampleList = CreateTypedList(forceSampleListType);

        Type graphType = RequireFvdGraphType();
        ConstructorInfo? graphCtor = graphType.GetConstructor(
            new[] { nodeListType, typeof(int), forceSampleListType, sectionListType });

        Assert.True(
            graphCtor is not null,
            "Expected constructor: FvdGraph(List<FvdControlNode>, int, List<FvdForceSample>, List<FvdSectionDefinition>).");

        object graph = graphCtor!.Invoke(new[] { nodeList, (object)3, forceSampleList, sectionList })!;

        Type sectionKindType = RequireSectionKindType();
        Type functionDomainType = RequireFunctionDomainType();
        Type sectionChannelType = RequireSectionChannelType();

        MethodInfo? dispatchMethod = graphType.GetMethod(
            "EvaluateSectionChannelAt",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[] { sectionKindType, functionDomainType, sectionChannelType, typeof(double) },
            modifiers: null);

        Assert.True(
            dispatchMethod is not null,
            "Expected method: FvdGraph.EvaluateSectionChannelAt(FvdSectionKind kind, FvdFunctionDomain domain, FvdSectionChannel channel, double x).");

        object kind = ParseEnumOrFail(sectionKindType, "Force");
        object domain = ParseEnumOrFail(functionDomainType, "Distance");
        object channel = ParseEnumOrFail(sectionChannelType, "NormalG");

        object? value;
        try
        {
            value = dispatchMethod!.Invoke(graph, new[] { kind, domain, channel, (object)10.0 });
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }

        Assert.True(value is double, "Expected EvaluateSectionChannelAt to return a double.");
        Assert.InRange(System.Math.Abs((double)value! - 7.0), 0.0, ValueTolerance);
    }

    [Fact]
    public void FvdGraph_EvaluateSectionAllAt_TouchingSections_UsesRightSectionAndReturnsCanonicalOrder()
    {
        Type sectionDefinitionType = RequireSectionDefinitionType();
        Type sectionFunctionType = RequireSectionFunctionType();

        object leftNormal = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(0.0, 1.0),
            CreateSectionSampleOrFail(10.0, 2.0));
        object leftLateral = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "LateralG",
            CreateSectionSampleOrFail(0.0, -3.0),
            CreateSectionSampleOrFail(10.0, -2.0));

        object rightNormal = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(10.0, 7.0),
            CreateSectionSampleOrFail(20.0, 8.0));
        object rightLateral = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "LateralG",
            CreateSectionSampleOrFail(10.0, 11.0),
            CreateSectionSampleOrFail(20.0, 12.0));

        object leftSection = CreateSectionDefinitionOrFail(
            sectionDefinitionType,
            kindName: "Force",
            domainName: "Distance",
            startX: 0.0,
            endX: 10.0,
            leftNormal,
            leftLateral);

        object rightSection = CreateSectionDefinitionOrFail(
            sectionDefinitionType,
            kindName: "Force",
            domainName: "Distance",
            startX: 10.0,
            endX: 20.0,
            rightNormal,
            rightLateral);

        Type sectionListType = typeof(List<>).MakeGenericType(sectionDefinitionType);
        object sectionList = CreateTypedList(sectionListType, leftSection, rightSection);

        Type nodeType = RequireFvdControlNodeType();
        Type nodeListType = typeof(List<>).MakeGenericType(nodeType);
        ConstructorInfo? nodeCtor = nodeType.GetConstructor(new[] { typeof(double), typeof(Quantum.Math.Vector3d), typeof(double) });

        Assert.True(
            nodeCtor is not null,
            "Expected constructor: FvdControlNode(double u, Vector3d position, double weight).");

        object nodeList = CreateTypedList(
            nodeListType,
            nodeCtor!.Invoke(new object[] { 0.00, new Quantum.Math.Vector3d(0, 0, 0), 1.0 })!,
            nodeCtor!.Invoke(new object[] { 0.33, new Quantum.Math.Vector3d(4, 5, 0), 0.9 })!,
            nodeCtor!.Invoke(new object[] { 0.66, new Quantum.Math.Vector3d(8, -3, 0), 1.2 })!,
            nodeCtor!.Invoke(new object[] { 1.00, new Quantum.Math.Vector3d(12, 0, 0), 1.0 })!);

        Type forceSampleType = RequireFvdForceSampleType();
        Type forceSampleListType = typeof(List<>).MakeGenericType(forceSampleType);
        object forceSampleList = CreateTypedList(forceSampleListType);

        Type graphType = RequireFvdGraphType();
        ConstructorInfo? graphCtor = graphType.GetConstructor(
            new[] { nodeListType, typeof(int), forceSampleListType, sectionListType });

        Assert.True(
            graphCtor is not null,
            "Expected constructor: FvdGraph(List<FvdControlNode>, int, List<FvdForceSample>, List<FvdSectionDefinition>).");

        object graph = graphCtor!.Invoke(new[] { nodeList, (object)3, forceSampleList, sectionList })!;

        Type sectionKindType = RequireSectionKindType();
        Type functionDomainType = RequireFunctionDomainType();

        MethodInfo? dispatchAllMethod = graphType.GetMethod(
            "EvaluateSectionAllAt",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[] { sectionKindType, functionDomainType, typeof(double) },
            modifiers: null);

        Assert.True(
            dispatchAllMethod is not null,
            "Expected method: FvdGraph.EvaluateSectionAllAt(FvdSectionKind kind, FvdFunctionDomain domain, double x).");

        object kind = ParseEnumOrFail(sectionKindType, "Force");
        object domain = ParseEnumOrFail(functionDomainType, "Distance");

        object? evaluationsObject;
        try
        {
            evaluationsObject = dispatchAllMethod!.Invoke(graph, new[] { kind, domain, (object)10.0 });
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }

        Assert.True(evaluationsObject is not null, "Expected EvaluateSectionAllAt to return a non-null value.");
        Assert.True(evaluationsObject is System.Collections.IEnumerable, "Expected EvaluateSectionAllAt to return an enumerable.");

        var evaluations = new List<(string ChannelName, double Value)>();
        foreach (object? item in (System.Collections.IEnumerable)evaluationsObject!)
        {
            Assert.True(item is not null, "Expected each EvaluateSectionAllAt item to be non-null.");

            Type itemType = item!.GetType();
            PropertyInfo? channelProperty = itemType.GetProperty("Channel", BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo? valueProperty = itemType.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);

            Assert.True(channelProperty is not null, "Expected EvaluateSectionAllAt item type to expose a Channel property.");
            Assert.True(valueProperty is not null, "Expected EvaluateSectionAllAt item type to expose a Value property.");

            object? channelValue = channelProperty!.GetValue(item);
            object? valueValue = valueProperty!.GetValue(item);

            Assert.True(channelValue is not null, "Expected evaluation Channel to be non-null.");
            Assert.True(valueValue is double, "Expected evaluation Value to be a double.");

            evaluations.Add((Enum.GetName(channelValue!.GetType(), channelValue)!, (double)valueValue!));
        }

        string[] actualOrder = evaluations.Select(e => e.ChannelName).ToArray();
        string[] expectedOrder = Enum
            .GetNames(RequireSectionChannelType())
            .Where(name => name is "NormalG" or "LateralG")
            .ToArray();

        Assert.Equal(expectedOrder, actualOrder);

        foreach ((string channelName, double value) in evaluations)
        {
            double expected = EvaluateSectionAtOrFail(rightSection, channelName, x: 10.0);
            Assert.InRange(System.Math.Abs(value - expected), 0.0, ValueTolerance);
        }
    }

    private static double EvaluateFunctionAtOrFail(object function, double x)
    {
        Type functionType = function.GetType();
        MethodInfo? method = functionType.GetMethod(
            "EvaluateAt",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(double) },
            modifiers: null);

        Assert.True(
            method is not null,
            "Expected method: FvdSectionFunction.EvaluateAt(double x).");

        object? value = method!.Invoke(function, new object[] { x });
        Assert.True(value is double, "Expected EvaluateAt to return a double.");
        return (double)value!;
    }

    private static double EvaluateSectionAtOrFail(object section, string channelName, double x)
    {
        Type sectionType = section.GetType();
        Type channelType = RequireSectionChannelType();
        object channel = ParseEnumOrFail(channelType, channelName);

        MethodInfo? method = sectionType.GetMethod(
            "EvaluateAt",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[] { channelType, typeof(double) },
            modifiers: null);

        Assert.True(
            method is not null,
            "Expected method: FvdSectionDefinition.EvaluateAt(FvdSectionChannel channel, double x).");

        object? value;
        try
        {
            value = method!.Invoke(section, new[] { channel, (object)x });
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }

        Assert.True(value is double, "Expected section EvaluateAt to return a double.");
        return (double)value!;
    }

    private static IReadOnlyList<(string ChannelName, double Value)> EvaluateAllSectionChannelsAtOrFail(object section, double x)
    {
        Type sectionType = section.GetType();

        MethodInfo? method = sectionType.GetMethod(
            "EvaluateAllAt",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(double) },
            modifiers: null);

        Assert.True(
            method is not null,
            "Expected method: FvdSectionDefinition.EvaluateAllAt(double x).");

        object? result;
        try
        {
            result = method!.Invoke(section, new object[] { x });
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }

        Assert.True(result is not null, "Expected EvaluateAllAt to return a non-null value.");
        Assert.True(result is System.Collections.IEnumerable, "Expected EvaluateAllAt to return an enumerable.");

        var evaluations = new List<(string ChannelName, double Value)>();
        foreach (object? item in (System.Collections.IEnumerable)result!)
        {
            Assert.True(item is not null, "Expected each EvaluateAllAt item to be non-null.");

            Type itemType = item!.GetType();
            PropertyInfo? channelProperty = itemType.GetProperty("Channel", BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo? valueProperty = itemType.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);

            Assert.True(
                channelProperty is not null,
                "Expected EvaluateAllAt item type to expose a Channel property.");
            Assert.True(
                valueProperty is not null,
                "Expected EvaluateAllAt item type to expose a Value property.");

            object? channelValue = channelProperty!.GetValue(item);
            object? valueValue = valueProperty!.GetValue(item);

            Assert.True(channelValue is not null, "Expected evaluation Channel to be non-null.");
            Assert.True(valueValue is double, "Expected evaluation Value to be a double.");

            evaluations.Add((Enum.GetName(channelValue!.GetType(), channelValue)!, (double)valueValue!));
        }

        return evaluations;
    }

    private static object CreateSectionDefinitionOrFail(
        Type sectionDefinitionType,
        string kindName,
        string domainName,
        double startX,
        double endX,
        params object[] functions)
    {
        Type sectionKindType = RequireSectionKindType();
        Type functionDomainType = RequireFunctionDomainType();
        Type sectionFunctionType = RequireSectionFunctionType();

        object kind = ParseEnumOrFail(sectionKindType, kindName);
        object domain = ParseEnumOrFail(functionDomainType, domainName);

        Type functionListType = typeof(List<>).MakeGenericType(sectionFunctionType);
        object functionList = CreateTypedList(functionListType, functions);

        ConstructorInfo? ctor = sectionDefinitionType.GetConstructor(
            new[] { sectionKindType, functionDomainType, typeof(double), typeof(double), functionListType });

        Assert.True(
            ctor is not null,
            "Expected constructor: FvdSectionDefinition(FvdSectionKind, FvdFunctionDomain, double, double, List<FvdSectionFunction>).");

        object? instance = ctor!.Invoke(new[] { kind, domain, (object)startX, (object)endX, functionList });
        Assert.True(instance is not null, "Expected FvdSectionDefinition constructor to return a non-null instance.");
        return instance!;
    }

    private static object CreateSectionFunctionOrFail(Type sectionFunctionType, string channelName, params object[] samples)
    {
        Type sectionChannelType = RequireSectionChannelType();
        Type sectionSampleType = RequireSectionSampleType();

        object channel = ParseEnumOrFail(sectionChannelType, channelName);

        Type sampleListType = typeof(List<>).MakeGenericType(sectionSampleType);
        object sampleList = CreateTypedList(sampleListType, samples);

        ConstructorInfo? ctor = sectionFunctionType.GetConstructor(new[] { sectionChannelType, sampleListType });

        Assert.True(
            ctor is not null,
            "Expected constructor: FvdSectionFunction(FvdSectionChannel, List<FvdSectionSample>).");

        object? instance = ctor!.Invoke(new[] { channel, sampleList });
        Assert.True(instance is not null, "Expected FvdSectionFunction constructor to return a non-null instance.");
        return instance!;
    }

    private static object CreateSectionSampleOrFail(double x, double value)
    {
        Type sectionSampleType = RequireSectionSampleType();
        ConstructorInfo? ctor = sectionSampleType.GetConstructor(new[] { typeof(double), typeof(double) });

        Assert.True(
            ctor is not null,
            "Expected constructor: FvdSectionSample(double x, double value).");

        object? instance = ctor!.Invoke(new object[] { x, value });
        Assert.True(instance is not null, "Expected FvdSectionSample constructor to return a non-null instance.");
        return instance!;
    }

    private static object ParseEnumOrFail(Type enumType, string memberName)
    {
        if (!enumType.IsEnum)
            throw new Xunit.Sdk.XunitException($"Expected {enumType.FullName} to be an enum.");

        object? parsed = Enum.Parse(enumType, memberName, ignoreCase: false);
        Assert.True(parsed is not null, $"Expected enum member '{memberName}' in {enumType.FullName}.");
        return parsed!;
    }

    private static object CreateTypedList(Type listType, params object[] items)
    {
        object list = Activator.CreateInstance(listType)
            ?? throw new Xunit.Sdk.XunitException($"Expected to create list instance of {listType.FullName}.");

        Type itemType = listType.GenericTypeArguments[0];
        MethodInfo? addMethod = listType.GetMethod("Add", new[] { itemType });
        Assert.True(addMethod is not null, $"Expected {listType.FullName}.Add({itemType.Name}) to exist.");

        foreach (object item in items)
        {
            addMethod!.Invoke(list, new[] { item });
        }

        return list;
    }

    private static Type RequireSectionDefinitionType()
    {
        Type? type = RequireFvdAssembly().GetType("Quantum.FVD.FvdSectionDefinition");
        Assert.True(type is not null, "Expected Quantum.FVD.FvdSectionDefinition to exist.");
        return type!;
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

    private static Type RequireFvdForceSampleType()
    {
        Type? type = RequireFvdAssembly().GetType("Quantum.FVD.FvdForceSample");
        Assert.True(type is not null, "Expected Quantum.FVD.FvdForceSample to exist.");
        return type!;
    }

    private static Type RequireSectionFunctionType()
    {
        Type? type = RequireFvdAssembly().GetType("Quantum.FVD.FvdSectionFunction");
        Assert.True(type is not null, "Expected Quantum.FVD.FvdSectionFunction to exist.");
        return type!;
    }

    private static Type RequireSectionSampleType()
    {
        Type? type = RequireFvdAssembly().GetType("Quantum.FVD.FvdSectionSample");
        Assert.True(type is not null, "Expected Quantum.FVD.FvdSectionSample to exist.");
        return type!;
    }

    private static Type RequireSectionKindType()
    {
        Type? type = RequireFvdAssembly().GetType("Quantum.FVD.FvdSectionKind");
        Assert.True(type is not null, "Expected Quantum.FVD.FvdSectionKind to exist.");
        return type!;
    }

    private static Type RequireFunctionDomainType()
    {
        Type? type = RequireFvdAssembly().GetType("Quantum.FVD.FvdFunctionDomain");
        Assert.True(type is not null, "Expected Quantum.FVD.FvdFunctionDomain to exist.");
        return type!;
    }

    private static Type RequireSectionChannelType()
    {
        Type? type = RequireFvdAssembly().GetType("Quantum.FVD.FvdSectionChannel");
        Assert.True(type is not null, "Expected Quantum.FVD.FvdSectionChannel to exist.");
        return type!;
    }

    private static Assembly RequireFvdAssembly()
    {
        return Assembly.Load("Quantum.FVD");
    }
}
