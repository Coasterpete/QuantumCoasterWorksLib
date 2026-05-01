using System;
using System.Collections.Generic;
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
