using System;
using System.Reflection;
using Xunit;

namespace Quantum.Tests;

public class CoreGuardTests
{
    [Fact]
    public void Numeric_IsFinite_AcceptsNormalFiniteValues()
    {
        MethodInfo isFinite = RequireNumericIsFiniteMethod();

        Assert.True(InvokeIsFinite(isFinite, 0.0));
        Assert.True(InvokeIsFinite(isFinite, -123.456));
        Assert.True(InvokeIsFinite(isFinite, double.Epsilon));
        Assert.True(InvokeIsFinite(isFinite, 1_000_000.0));
    }

    [Fact]
    public void Numeric_IsFinite_RejectsNaNAndPositiveNegativeInfinity()
    {
        MethodInfo isFinite = RequireNumericIsFiniteMethod();

        Assert.False(InvokeIsFinite(isFinite, double.NaN));
        Assert.False(InvokeIsFinite(isFinite, double.PositiveInfinity));
        Assert.False(InvokeIsFinite(isFinite, double.NegativeInfinity));
    }

    [Fact]
    public void Guard_RequireFinite_ThrowsArgumentOutOfRangeException_WithCorrectParamName()
    {
        MethodInfo requireFinite = RequireGuardDoubleMethod("RequireFinite");

        AssertThrowsArgumentOutOfRange(requireFinite, new object[] { double.NaN, "evaluationX", null! }, "evaluationX");
        AssertThrowsArgumentOutOfRange(requireFinite, new object[] { double.PositiveInfinity, "evaluationX", null! }, "evaluationX");
        AssertThrowsArgumentOutOfRange(requireFinite, new object[] { double.NegativeInfinity, "evaluationX", null! }, "evaluationX");
    }

    [Fact]
    public void Guard_RequirePositiveFinite_RejectsNonPositiveAndNonFiniteValues()
    {
        MethodInfo requirePositiveFinite = RequireGuardDoubleMethod("RequirePositiveFinite");

        AssertThrowsArgumentOutOfRange(requirePositiveFinite, new object[] { 0.0, "speedMps", null! }, "speedMps");
        AssertThrowsArgumentOutOfRange(requirePositiveFinite, new object[] { -0.01, "speedMps", null! }, "speedMps");
        AssertThrowsArgumentOutOfRange(requirePositiveFinite, new object[] { double.NaN, "speedMps", null! }, "speedMps");
        AssertThrowsArgumentOutOfRange(requirePositiveFinite, new object[] { double.PositiveInfinity, "speedMps", null! }, "speedMps");
        AssertThrowsArgumentOutOfRange(requirePositiveFinite, new object[] { double.NegativeInfinity, "speedMps", null! }, "speedMps");
    }

    [Fact]
    public void Guard_RequireNonNegativeFinite_AllowsZeroAndPositive_RejectsNegativeAndNonFinite()
    {
        MethodInfo requireNonNegativeFinite = RequireGuardDoubleMethod("RequireNonNegativeFinite");

        Assert.Null(Record.Exception(() => requireNonNegativeFinite.Invoke(null, new object[] { 0.0, "distance", null! })));
        Assert.Null(Record.Exception(() => requireNonNegativeFinite.Invoke(null, new object[] { 42.5, "distance", null! })));

        AssertThrowsArgumentOutOfRange(requireNonNegativeFinite, new object[] { -0.0001, "distance", null! }, "distance");
        AssertThrowsArgumentOutOfRange(requireNonNegativeFinite, new object[] { double.NaN, "distance", null! }, "distance");
        AssertThrowsArgumentOutOfRange(requireNonNegativeFinite, new object[] { double.PositiveInfinity, "distance", null! }, "distance");
        AssertThrowsArgumentOutOfRange(requireNonNegativeFinite, new object[] { double.NegativeInfinity, "distance", null! }, "distance");
    }

    [Fact]
    public void Guard_RequireAtLeast_RejectsBelowMinimum_AndPreservesParamName()
    {
        MethodInfo requireAtLeast = RequireGuardRequireAtLeastMethod();

        AssertThrowsArgumentOutOfRange(
            requireAtLeast,
            new object[] { 1, 2, "arcLengthSamples", null! },
            "arcLengthSamples");

        Assert.Null(Record.Exception(() => requireAtLeast.Invoke(null, new object[] { 2, 2, "arcLengthSamples", null! })));
        Assert.Null(Record.Exception(() => requireAtLeast.Invoke(null, new object[] { 5, 2, "arcLengthSamples", null! })));
    }

    private static Assembly RequireCoreAssembly()
    {
        try
        {
            return Assembly.Load("Quantum.Core");
        }
        catch (Exception ex)
        {
            throw new Xunit.Sdk.XunitException(
                $"Expected assembly 'Quantum.Core' to load for Core guard tests, but load failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static Type RequireCoreType(string fullName)
    {
        Assembly coreAssembly = RequireCoreAssembly();
        Type? type = coreAssembly.GetType(fullName);

        Assert.True(type is not null, $"Expected type '{fullName}' to exist in Quantum.Core.");
        return type!;
    }

    private static MethodInfo RequireNumericIsFiniteMethod()
    {
        Type numericType = RequireCoreType("Quantum.Core.Numeric");
        MethodInfo? method = numericType.GetMethod(
            "IsFinite",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(double) },
            modifiers: null);

        Assert.True(method is not null, "Expected method: Numeric.IsFinite(double).");
        Assert.Equal(typeof(bool), method!.ReturnType);
        return method!;
    }

    private static MethodInfo RequireGuardDoubleMethod(string methodName)
    {
        Type guardType = RequireCoreType("Quantum.Core.Guard");
        MethodInfo? method = guardType.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(double), typeof(string), typeof(string) },
            modifiers: null);

        Assert.True(method is not null, $"Expected method: Guard.{methodName}(double value, string paramName, string? message = null).");
        return method!;
    }

    private static MethodInfo RequireGuardRequireAtLeastMethod()
    {
        Type guardType = RequireCoreType("Quantum.Core.Guard");
        MethodInfo? method = guardType.GetMethod(
            "RequireAtLeast",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(int), typeof(int), typeof(string), typeof(string) },
            modifiers: null);

        Assert.True(method is not null, "Expected method: Guard.RequireAtLeast(int value, int minInclusive, string paramName, string? message = null).");
        return method!;
    }

    private static bool InvokeIsFinite(MethodInfo method, double value)
    {
        object? result = method.Invoke(null, new object[] { value });

        Assert.True(result is bool, "Expected Numeric.IsFinite(double) to return a bool.");
        return (bool)result!;
    }

    private static void AssertThrowsArgumentOutOfRange(MethodInfo method, object[] args, string expectedParamName)
    {
        TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, args));
        ArgumentOutOfRangeException inner = Assert.IsType<ArgumentOutOfRangeException>(thrown.InnerException);
        Assert.Equal(expectedParamName, inner.ParamName);
    }
}
