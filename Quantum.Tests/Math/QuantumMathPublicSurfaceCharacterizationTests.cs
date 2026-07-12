using System;
using System.Linq;
using Quantum.Math;

namespace Quantum.Tests;

public sealed class QuantumMathPublicSurfaceCharacterizationTests
{
    private static readonly Type[] StablePublicTypes =
    {
        typeof(Vector3d),
        typeof(MathUtil)
    };

    private static readonly Type[] TransitionalPublicTypes =
    {
        typeof(Matrix4x4d),
        typeof(Matrix3x3),
        typeof(Transform3d),
        typeof(ITrackFrameBasis)
    };

    [Fact]
    public void QuantumMath_PublicTypesAreExplicitlyClassified()
    {
        Type[] exportedMathTypes = typeof(Vector3d).Assembly
            .GetExportedTypes()
            .Where(type => type.Namespace == "Quantum.Math")
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Type[] classifiedTypes = StablePublicTypes
            .Concat(TransitionalPublicTypes)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            classifiedTypes.Select(type => type.FullName),
            exportedMathTypes.Select(type => type.FullName));
    }

    [Fact]
    public void Vector3d_And_MathUtil_RemainPublicStableSurface()
    {
        AssertPublicType(typeof(Vector3d));
        Assert.True(typeof(Vector3d).IsValueType);
        Assert.Equal("Quantum.Math", typeof(Vector3d).Namespace);

        AssertPublicType(typeof(MathUtil));
        Assert.True(typeof(MathUtil).IsAbstract);
        Assert.True(typeof(MathUtil).IsSealed);
        Assert.Equal("Quantum.Math", typeof(MathUtil).Namespace);
    }

    [Fact]
    public void MatrixTransformAndFrameBasisTypes_AreTransitionalPublicSurface()
    {
        foreach (Type type in TransitionalPublicTypes)
        {
            AssertPublicType(type);
            Assert.Equal("Quantum.Math", type.Namespace);
        }

        Assert.True(typeof(Matrix4x4d).IsValueType);
        Assert.True(typeof(Matrix3x3).IsValueType);
        Assert.True(typeof(Transform3d).IsValueType);
        Assert.True(typeof(ITrackFrameBasis).IsInterface);
    }

    private static void AssertPublicType(Type type)
    {
        Assert.True(type.IsPublic, $"{type.FullName} should remain public.");
    }
}
