using System.Reflection;
using Quantum.Math;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class SpatialSectionDefinitionTests
{
    [Fact]
    public void Constructor_CopiesControlPointsAndWeightsIntoReadOnlyCollections()
    {
        var controlPoints = CreateControlPoints();
        var weights = new List<double> { 1.0, 1.5, 0.75, 1.25 };
        var definition = new SpatialSectionDefinition(
            "spatial",
            8.0,
            controlPoints,
            degree: 3,
            weights: weights,
            rollRadians: 0.2);

        controlPoints[1] = new Vector3d(99.0, 99.0, 99.0);
        weights[1] = 99.0;

        Assert.Equal(2.0, definition.ControlPoints[1].X);
        Assert.Equal(0.0, definition.ControlPoints[1].Y);
        Assert.Equal(0.0, definition.ControlPoints[1].Z);
        Assert.Equal(1.5, definition.Weights[1]);
        Assert.Equal(3, definition.Degree);
        Assert.Equal(0.2, definition.RollRadians);

        IList<Vector3d> exposedControlPoints =
            Assert.IsAssignableFrom<IList<Vector3d>>(definition.ControlPoints);
        IList<double> exposedWeights = Assert.IsAssignableFrom<IList<double>>(definition.Weights);
        Assert.True(exposedControlPoints.IsReadOnly);
        Assert.True(exposedWeights.IsReadOnly);
        Assert.Throws<NotSupportedException>(
            () => exposedControlPoints[0] = Vector3d.UnitX);
        Assert.Throws<NotSupportedException>(() => exposedWeights[0] = 2.0);
    }

    [Fact]
    public void Constructor_UsesUnitWeightsWhenWeightsAreOmitted()
    {
        var definition = new SpatialSectionDefinition(
            "spatial",
            8.0,
            CreateControlPoints());

        Assert.Equal(4, definition.Weights.Count);
        Assert.All(definition.Weights, weight => Assert.Equal(1.0, weight));
    }

    [Fact]
    public void Constructor_RejectsNullOrEmptyControlPoints()
    {
        Assert.Throws<ArgumentNullException>(
            () => new SpatialSectionDefinition("spatial", 1.0, null!));
        Assert.Throws<ArgumentException>(
            () => new SpatialSectionDefinition("spatial", 1.0, Array.Empty<Vector3d>()));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_RejectsNonFiniteControlPoints(double invalid)
    {
        List<Vector3d> controlPoints = CreateControlPoints();
        controlPoints[2] = new Vector3d(4.0, invalid, 1.0);

        Assert.Throws<ArgumentException>(
            () => new SpatialSectionDefinition("spatial", 8.0, controlPoints));
    }

    [Fact]
    public void Constructor_RejectsInvalidDegreeAndControlPointCountCombinations()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SpatialSectionDefinition(
                "spatial",
                8.0,
                CreateControlPoints(),
                degree: 0));
        Assert.Throws<ArgumentException>(
            () => new SpatialSectionDefinition(
                "spatial",
                8.0,
                CreateControlPoints().Take(3),
                degree: 3));
        Assert.Throws<ArgumentException>(
            () => new SpatialSectionDefinition(
                "spatial",
                8.0,
                CreateControlPoints(),
                degree: int.MaxValue));
    }

    [Fact]
    public void Constructor_RejectsInvalidWeights()
    {
        List<Vector3d> controlPoints = CreateControlPoints();

        Assert.Throws<ArgumentException>(
            () => new SpatialSectionDefinition(
                "spatial",
                8.0,
                controlPoints,
                weights: new[] { 1.0, 1.0, 1.0 }));

        foreach (double invalid in new[]
                 {
                     0.0,
                     -1.0,
                     double.NaN,
                     double.PositiveInfinity,
                     double.NegativeInfinity
                 })
        {
            Assert.Throws<ArgumentException>(
                () => new SpatialSectionDefinition(
                    "spatial",
                    8.0,
                    controlPoints,
                    weights: new[] { 1.0, invalid, 1.0, 1.0 }));
        }
    }

    [Fact]
    public void Constructor_RejectsInvalidLocalStartContract()
    {
        Assert.Throws<ArgumentException>(
            () => new SpatialSectionDefinition(
                "spatial",
                8.0,
                new[]
                {
                    new Vector3d(1.0, 0.0, 0.0),
                    new Vector3d(2.0, 0.0, 0.0),
                    new Vector3d(4.0, 1.0, 1.0),
                    new Vector3d(6.0, 2.0, 2.0)
                }));
        Assert.Throws<ArgumentException>(
            () => new SpatialSectionDefinition(
                "spatial",
                8.0,
                new[]
                {
                    Vector3d.Zero,
                    Vector3d.Zero,
                    new Vector3d(4.0, 1.0, 1.0),
                    new Vector3d(6.0, 2.0, 2.0)
                }));
        Assert.Throws<ArgumentException>(
            () => new SpatialSectionDefinition(
                "spatial",
                8.0,
                new[]
                {
                    Vector3d.Zero,
                    new Vector3d(2.0, 0.1, 0.0),
                    new Vector3d(4.0, 1.0, 1.0),
                    new Vector3d(6.0, 2.0, 2.0)
                }));
        Assert.Throws<ArgumentException>(
            () => new SpatialSectionDefinition(
                "spatial",
                8.0,
                new[]
                {
                    Vector3d.Zero,
                    new Vector3d(-2.0, 0.0, 0.0),
                    new Vector3d(4.0, 1.0, 1.0),
                    new Vector3d(6.0, 2.0, 2.0)
                }));
    }

    [Fact]
    public void PublicApi_UsesOnlyBackendAndStandardCollectionTypes()
    {
        Type type = typeof(SpatialSectionDefinition);
        ConstructorInfo constructor = Assert.Single(type.GetConstructors());
        ParameterInfo[] parameters = constructor.GetParameters();

        Assert.Equal(typeof(GeometricSectionDefinition), type.BaseType);
        Assert.Equal(
            new[]
            {
                typeof(string),
                typeof(double),
                typeof(IEnumerable<Vector3d>),
                typeof(int),
                typeof(IEnumerable<double>),
                typeof(double)
            },
            parameters.Select(parameter => parameter.ParameterType));
        Assert.Equal(3, parameters[3].DefaultValue);
        Assert.Null(parameters[4].DefaultValue);
        Assert.Equal(0.0, parameters[5].DefaultValue);
        Assert.Equal(typeof(IReadOnlyList<Vector3d>), type.GetProperty(nameof(SpatialSectionDefinition.ControlPoints))!.PropertyType);
        Assert.Equal(typeof(IReadOnlyList<double>), type.GetProperty(nameof(SpatialSectionDefinition.Weights))!.PropertyType);
        Assert.Equal(typeof(int), type.GetProperty(nameof(SpatialSectionDefinition.Degree))!.PropertyType);

        foreach (Type exposedType in parameters.Select(parameter => parameter.ParameterType)
                     .Concat(type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .Select(property => property.PropertyType)))
        {
            AssertDoesNotContainFrameworkType(exposedType);
        }
    }

    private static List<Vector3d> CreateControlPoints()
    {
        return new List<Vector3d>
        {
            Vector3d.Zero,
            new Vector3d(2.0, 0.0, 0.0),
            new Vector3d(4.0, 1.0, 1.0),
            new Vector3d(6.0, 2.0, 2.0)
        };
    }

    private static void AssertDoesNotContainFrameworkType(Type type)
    {
        string? namespaceName = type.Namespace;
        Assert.False(namespaceName?.StartsWith("GShark", StringComparison.Ordinal) == true);
        Assert.False(namespaceName?.StartsWith("Quantum.Splines", StringComparison.Ordinal) == true);
        Assert.False(namespaceName?.StartsWith("UnityEngine", StringComparison.Ordinal) == true);

        if (type.IsGenericType)
        {
            foreach (Type argument in type.GetGenericArguments())
            {
                AssertDoesNotContainFrameworkType(argument);
            }
        }
    }
}
