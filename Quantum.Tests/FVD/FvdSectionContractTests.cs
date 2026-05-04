using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Quantum.FVD;
using Quantum.Math;
using Xunit;

namespace Quantum.Tests;

public class FvdSectionContractTests
{
    private const double ValueTolerance = 1e-9;

    [Fact]
    public void FvdSectionDefinition_RejectsInvalidRanges()
    {
        Type sectionDefinitionType = RequireSectionDefinitionType();
        Type sectionFunctionType = RequireSectionFunctionType();

        object validForceFunction = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(0.0, 1.0),
            CreateSectionSampleOrFail(1.0, 1.2));

        Assert.ThrowsAny<Exception>(() =>
            CreateSectionDefinitionOrFail(
                sectionDefinitionType,
                kindName: "Force",
                domainName: "Distance",
                startX: 1.0,
                endX: 1.0,
                validForceFunction));

        Assert.ThrowsAny<Exception>(() =>
            CreateSectionDefinitionOrFail(
                sectionDefinitionType,
                kindName: "Force",
                domainName: "Distance",
                startX: 2.0,
                endX: 1.0,
                validForceFunction));

        Assert.ThrowsAny<Exception>(() =>
            CreateSectionDefinitionOrFail(
                sectionDefinitionType,
                kindName: "Force",
                domainName: "Distance",
                startX: double.NaN,
                endX: 1.0,
                validForceFunction));

        Assert.ThrowsAny<Exception>(() =>
            CreateSectionDefinitionOrFail(
                sectionDefinitionType,
                kindName: "Force",
                domainName: "Distance",
                startX: 0.0,
                endX: double.PositiveInfinity,
                validForceFunction));
    }

    [Fact]
    public void FvdSectionDefinition_RejectsUnsortedOrDuplicateSampleXValues()
    {
        Type sectionDefinitionType = RequireSectionDefinitionType();
        Type sectionFunctionType = RequireSectionFunctionType();

        object unsortedForceFunction = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(0.0, 1.0),
            CreateSectionSampleOrFail(0.8, 1.3),
            CreateSectionSampleOrFail(0.5, 1.1));

        object duplicateForceFunction = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(0.0, 1.0),
            CreateSectionSampleOrFail(0.5, 1.2),
            CreateSectionSampleOrFail(0.5, 1.1));

        Assert.ThrowsAny<Exception>(() =>
            CreateSectionDefinitionOrFail(
                sectionDefinitionType,
                kindName: "Force",
                domainName: "Distance",
                startX: 0.0,
                endX: 1.0,
                unsortedForceFunction));

        Assert.ThrowsAny<Exception>(() =>
            CreateSectionDefinitionOrFail(
                sectionDefinitionType,
                kindName: "Force",
                domainName: "Distance",
                startX: 0.0,
                endX: 1.0,
                duplicateForceFunction));
    }

    [Fact]
    public void FvdSectionDefinition_RejectsSampleXOutsideRange()
    {
        Type sectionDefinitionType = RequireSectionDefinitionType();
        Type sectionFunctionType = RequireSectionFunctionType();

        object belowRangeFunction = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(-0.1, 1.0),
            CreateSectionSampleOrFail(0.5, 1.2));

        object aboveRangeFunction = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(0.5, 1.2),
            CreateSectionSampleOrFail(1.1, 1.3));

        Assert.ThrowsAny<Exception>(() =>
            CreateSectionDefinitionOrFail(
                sectionDefinitionType,
                kindName: "Force",
                domainName: "Distance",
                startX: 0.0,
                endX: 1.0,
                belowRangeFunction));

        Assert.ThrowsAny<Exception>(() =>
            CreateSectionDefinitionOrFail(
                sectionDefinitionType,
                kindName: "Force",
                domainName: "Distance",
                startX: 0.0,
                endX: 1.0,
                aboveRangeFunction));
    }

    [Fact]
    public void FvdSectionDefinition_RejectsNonFiniteSampleValues()
    {
        Type sectionDefinitionType = RequireSectionDefinitionType();
        Type sectionFunctionType = RequireSectionFunctionType();

        object nanValueFunction = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(0.0, 1.0),
            CreateSectionSampleOrFail(1.0, double.NaN));

        object infinityValueFunction = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(0.0, 1.0),
            CreateSectionSampleOrFail(1.0, double.PositiveInfinity));

        Assert.ThrowsAny<Exception>(() =>
            CreateSectionDefinitionOrFail(
                sectionDefinitionType,
                kindName: "Force",
                domainName: "Distance",
                startX: 0.0,
                endX: 1.0,
                nanValueFunction));

        Assert.ThrowsAny<Exception>(() =>
            CreateSectionDefinitionOrFail(
                sectionDefinitionType,
                kindName: "Force",
                domainName: "Distance",
                startX: 0.0,
                endX: 1.0,
                infinityValueFunction));
    }

    [Fact]
    public void FvdSectionDefinition_RejectsInvalidChannelForSectionKind()
    {
        Type sectionDefinitionType = RequireSectionDefinitionType();
        Type sectionFunctionType = RequireSectionFunctionType();

        object geometryChannelInForceSection = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "Curvature",
            CreateSectionSampleOrFail(0.0, 0.02),
            CreateSectionSampleOrFail(1.0, 0.03));

        object forceChannelInGeometrySection = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(0.0, 1.0),
            CreateSectionSampleOrFail(1.0, 1.2));

        Assert.ThrowsAny<Exception>(() =>
            CreateSectionDefinitionOrFail(
                sectionDefinitionType,
                kindName: "Force",
                domainName: "Distance",
                startX: 0.0,
                endX: 1.0,
                geometryChannelInForceSection));

        Assert.ThrowsAny<Exception>(() =>
            CreateSectionDefinitionOrFail(
                sectionDefinitionType,
                kindName: "Geometry",
                domainName: "Distance",
                startX: 0.0,
                endX: 1.0,
                forceChannelInGeometrySection));
    }

    [Fact]
    public void FvdSectionDefinition_RejectsDuplicateChannelFunctions_InForceSections()
    {
        Type sectionDefinitionType = RequireSectionDefinitionType();
        Type sectionFunctionType = RequireSectionFunctionType();

        object normalA = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(0.0, 1.0),
            CreateSectionSampleOrFail(1.0, 1.2));

        object normalB = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(0.0, 1.1),
            CreateSectionSampleOrFail(1.0, 1.3));

        Assert.ThrowsAny<Exception>(() =>
            CreateSectionDefinitionOrFail(
                sectionDefinitionType,
                kindName: "Force",
                domainName: "Distance",
                startX: 0.0,
                endX: 1.0,
                normalA,
                normalB));
    }

    [Fact]
    public void FvdSectionDefinition_RejectsDuplicateChannelFunctions_InGeometrySections()
    {
        Type sectionDefinitionType = RequireSectionDefinitionType();
        Type sectionFunctionType = RequireSectionFunctionType();

        object curvatureA = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "Curvature",
            CreateSectionSampleOrFail(0.0, 0.01),
            CreateSectionSampleOrFail(1.0, 0.03));

        object curvatureB = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "Curvature",
            CreateSectionSampleOrFail(0.0, 0.02),
            CreateSectionSampleOrFail(1.0, 0.04));

        Assert.ThrowsAny<Exception>(() =>
            CreateSectionDefinitionOrFail(
                sectionDefinitionType,
                kindName: "Geometry",
                domainName: "Distance",
                startX: 0.0,
                endX: 1.0,
                curvatureA,
                curvatureB));
    }

    [Fact]
    public void FvdSectionDefinition_AcceptsDistinctChannelFunctions()
    {
        Type sectionDefinitionType = RequireSectionDefinitionType();
        Type sectionFunctionType = RequireSectionFunctionType();

        object normal = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(0.0, 1.0),
            CreateSectionSampleOrFail(1.0, 1.2));

        object lateral = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "LateralG",
            CreateSectionSampleOrFail(0.0, 0.1),
            CreateSectionSampleOrFail(1.0, 0.2));

        object section = CreateSectionDefinitionOrFail(
            sectionDefinitionType,
            kindName: "Force",
            domainName: "Distance",
            startX: 0.0,
            endX: 1.0,
            normal,
            lateral);

        object functionsObject = GetRequiredMember(section, "Functions");
        var functions = AsObjectList(functionsObject);

        Assert.Equal(2, functions.Count);
        Assert.Equal("NormalG", GetEnumMemberName(functions[0], "Channel"));
        Assert.Equal("LateralG", GetEnumMemberName(functions[1], "Channel"));
    }

    [Fact]
    public void FvdSectionDefinition_AcceptsValidSection_AndPreservesShapeAndValues()
    {
        Type sectionDefinitionType = RequireSectionDefinitionType();
        Type sectionFunctionType = RequireSectionFunctionType();
        Type sectionSampleType = RequireSectionSampleType();

        object functionA = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(10.0, 1.0),
            CreateSectionSampleOrFail(20.0, 1.2));

        object functionB = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "RollRateDegPerSec",
            CreateSectionSampleOrFail(10.0, 0.5),
            CreateSectionSampleOrFail(20.0, 1.5));

        object section = CreateSectionDefinitionOrFail(
            sectionDefinitionType,
            kindName: "Force",
            domainName: "Distance",
            startX: 10.0,
            endX: 20.0,
            functionA,
            functionB);

        Assert.Equal("Force", GetEnumMemberName(section, "Kind"));
        Assert.Equal("Distance", GetEnumMemberName(section, "Domain"));
        Assert.InRange(System.Math.Abs(GetDoubleMember(section, "StartX") - 10.0), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(GetDoubleMember(section, "EndX") - 20.0), 0.0, ValueTolerance);

        object functionsObject = GetRequiredMember(section, "Functions");
        var functions = AsObjectList(functionsObject);
        Assert.Equal(2, functions.Count);
        Assert.Equal("NormalG", GetEnumMemberName(functions[0], "Channel"));
        Assert.Equal("RollRateDegPerSec", GetEnumMemberName(functions[1], "Channel"));

        object firstFunctionSamplesObject = GetRequiredMember(functions[0], "Samples");
        var firstFunctionSamples = AsObjectList(firstFunctionSamplesObject);
        Assert.Equal(2, firstFunctionSamples.Count);
        Assert.Equal(sectionSampleType, firstFunctionSamples[0].GetType());
        Assert.Equal(sectionSampleType, firstFunctionSamples[1].GetType());
        Assert.InRange(System.Math.Abs(GetDoubleMember(firstFunctionSamples[0], "X") - 10.0), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(GetDoubleMember(firstFunctionSamples[0], "Value") - 1.0), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(GetDoubleMember(firstFunctionSamples[1], "X") - 20.0), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(GetDoubleMember(firstFunctionSamples[1], "Value") - 1.2), 0.0, ValueTolerance);
    }

    [Fact]
    public void FvdGraph_AcceptsValidSections_AndPreservesSectionCountAndOrder()
    {
        Type sectionDefinitionType = RequireSectionDefinitionType();
        Type sectionFunctionType = RequireSectionFunctionType();

        object forceFunction = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(0.0, 1.0),
            CreateSectionSampleOrFail(1.0, 1.2));

        object geometryFunction = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "Curvature",
            CreateSectionSampleOrFail(2.0, 0.02),
            CreateSectionSampleOrFail(4.0, 0.03));

        object forceSection = CreateSectionDefinitionOrFail(
            sectionDefinitionType,
            kindName: "Force",
            domainName: "Distance",
            startX: 0.0,
            endX: 1.0,
            forceFunction);

        object geometrySection = CreateSectionDefinitionOrFail(
            sectionDefinitionType,
            kindName: "Geometry",
            domainName: "Time",
            startX: 2.0,
            endX: 4.0,
            geometryFunction);

        Type sectionListType = typeof(List<>).MakeGenericType(sectionDefinitionType);
        object sectionList = CreateTypedList(sectionListType, forceSection, geometrySection);

        List<FvdControlNode> controlNodes = BuildValidControlNodes();
        List<FvdForceSample> forceSamples = BuildValidForceSamples();

        Type graphType = typeof(FvdGraph);
        ConstructorInfo? ctor = graphType.GetConstructor(
            new[] { typeof(List<FvdControlNode>), typeof(int), typeof(List<FvdForceSample>), sectionListType });

        Assert.True(
            ctor is not null,
            "Expected constructor: FvdGraph(List<FvdControlNode>, int, List<FvdForceSample>, List<FvdSectionDefinition>).");

        object? graph = ctor!.Invoke(new object[] { controlNodes, 3, forceSamples, sectionList });
        Assert.True(graph is not null, "Expected FvdGraph constructor to return a non-null instance.");

        object sectionsObject = GetRequiredMember(graph!, "Sections");
        var sections = AsObjectList(sectionsObject);

        Assert.Equal(2, sections.Count);
        Assert.Equal("Force", GetEnumMemberName(sections[0], "Kind"));
        Assert.Equal("Geometry", GetEnumMemberName(sections[1], "Kind"));
    }

    [Fact]
    public void FvdGraph_RejectsOverlappingSections_InSameKindAndDomain()
    {
        Type sectionDefinitionType = RequireSectionDefinitionType();
        Type sectionFunctionType = RequireSectionFunctionType();

        object firstFunction = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "NormalG",
            CreateSectionSampleOrFail(0.0, 1.0),
            CreateSectionSampleOrFail(10.0, 1.2));

        object secondFunction = CreateSectionFunctionOrFail(
            sectionFunctionType,
            channelName: "LateralG",
            CreateSectionSampleOrFail(9.0, 0.1),
            CreateSectionSampleOrFail(15.0, 0.2));

        object firstSection = CreateSectionDefinitionOrFail(
            sectionDefinitionType,
            kindName: "Force",
            domainName: "Distance",
            startX: 0.0,
            endX: 10.0,
            firstFunction);

        object secondSection = CreateSectionDefinitionOrFail(
            sectionDefinitionType,
            kindName: "Force",
            domainName: "Distance",
            startX: 9.0,
            endX: 15.0,
            secondFunction);

        Type sectionListType = typeof(List<>).MakeGenericType(sectionDefinitionType);
        object sectionList = CreateTypedList(sectionListType, firstSection, secondSection);

        List<FvdControlNode> controlNodes = BuildValidControlNodes();
        List<FvdForceSample> forceSamples = BuildValidForceSamples();

        Type graphType = typeof(FvdGraph);
        ConstructorInfo? ctor = graphType.GetConstructor(
            new[] { typeof(List<FvdControlNode>), typeof(int), typeof(List<FvdForceSample>), sectionListType });

        Assert.True(
            ctor is not null,
            "Expected constructor: FvdGraph(List<FvdControlNode>, int, List<FvdForceSample>, List<FvdSectionDefinition>).");

        Exception ex = Assert.ThrowsAny<Exception>(() =>
        {
            try
            {
                ctor!.Invoke(new object[] { controlNodes, 3, forceSamples, sectionList });
            }
            catch (TargetInvocationException invocationEx) when (invocationEx.InnerException is not null)
            {
                throw invocationEx.InnerException;
            }
        });

        Assert.Contains("overlap", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("section", ex.Message, StringComparison.OrdinalIgnoreCase);
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

    private static object GetRequiredMember(object instance, string memberName)
    {
        Type type = instance.GetType();

        PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
        if (property is not null)
        {
            object? value = property.GetValue(instance);
            Assert.True(value is not null, $"Expected {type.FullName}.{memberName} property to be non-null.");
            return value!;
        }

        FieldInfo? field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
        if (field is not null)
        {
            object? value = field.GetValue(instance);
            Assert.True(value is not null, $"Expected {type.FullName}.{memberName} field to be non-null.");
            return value!;
        }

        throw new Xunit.Sdk.XunitException($"Expected {type.FullName} to contain public member '{memberName}'.");
    }

    private static string GetEnumMemberName(object instance, string memberName)
    {
        object value = GetRequiredMember(instance, memberName);
        Type type = value.GetType();

        Assert.True(type.IsEnum, $"Expected {type.FullName} to be enum-valued.");
        return Enum.GetName(type, value) ?? string.Empty;
    }

    private static double GetDoubleMember(object instance, string memberName)
    {
        object value = GetRequiredMember(instance, memberName);
        Assert.True(value is double, $"Expected {instance.GetType().FullName}.{memberName} to be a double.");
        return (double)value;
    }

    private static List<object> AsObjectList(object enumerable)
    {
        Assert.True(enumerable is IEnumerable, "Expected value to implement IEnumerable.");

        var list = new List<object>();
        foreach (object? item in (IEnumerable)enumerable)
        {
            Assert.True(item is not null, "Expected enumerable items to be non-null.");
            list.Add(item!);
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

    private static List<FvdControlNode> BuildValidControlNodes()
    {
        return new List<FvdControlNode>
        {
            new FvdControlNode(0.00, new Vector3d(0, 0, 0), 1.0),
            new FvdControlNode(0.33, new Vector3d(4, 5, 0), 0.9),
            new FvdControlNode(0.66, new Vector3d(8, -3, 0), 1.2),
            new FvdControlNode(1.00, new Vector3d(12, 0, 0), 1.0)
        };
    }

    private static List<FvdForceSample> BuildValidForceSamples()
    {
        return new List<FvdForceSample>
        {
            new FvdForceSample(0.00, normalG: 1.00, lateralG: 0.00, rollRateDegPerSec: 0.00),
            new FvdForceSample(0.30, normalG: 1.15, lateralG: 0.05, rollRateDegPerSec: 1.50),
            new FvdForceSample(0.70, normalG: 1.25, lateralG: -0.10, rollRateDegPerSec: 3.25),
            new FvdForceSample(1.00, normalG: 1.05, lateralG: 0.00, rollRateDegPerSec: 0.50)
        };
    }
}
