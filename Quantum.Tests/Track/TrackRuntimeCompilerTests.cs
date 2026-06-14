using System.Collections;
using System.Reflection;
using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using TrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class TrackRuntimeCompilerTests
{
    private const double Tolerance = 1e-8;

    [Fact]
    public void Compile_DefaultOptions_ReturnsSuccessfulRuntime()
    {
        TrackDocument document = CreateLineDocument(10.0);

        TrackRuntimeCompileResult result = TrackRuntimeCompiler.Compile(document);

        Assert.True(result.Success);
        Assert.False(result.HasErrors);
        Assert.NotNull(result.Runtime);
        Assert.Empty(result.Diagnostics);
        Assert.Same(TrackSamplingOptions.Default, result.Runtime.SamplingOptions);
        Assert.Equal(1, result.Runtime.SegmentCount);
        AssertNear(10.0, result.Runtime.TotalLength);
    }

    [Fact]
    public void DefaultCompilerRuntime_MatchesConstructorAndLiveEvaluator()
    {
        TrackDocument document = CreateLineDocument(10.0);
        CompiledTrackRuntime compiled = TrackRuntimeCompiler.Compile(document).Runtime!;
        var compilerEvaluator = new TrackEvaluator(compiled);
        var constructorEvaluator = new TrackEvaluator(new CompiledTrackRuntime(document));
        var liveEvaluator = new TrackEvaluator(document);

        foreach (double distance in new[] { -2.0, 0.0, 3.25, 10.0, 20.0 })
        {
            TrackFrame expected = liveEvaluator.EvaluateFrameAtDistance(distance);
            AssertFrameNear(expected, compilerEvaluator.EvaluateFrameAtDistance(distance));
            AssertFrameNear(expected, constructorEvaluator.EvaluateFrameAtDistance(distance));
        }
    }

    [Fact]
    public void Compile_CustomOptions_AreCapturedUsedAndCompiledOnlyOnce()
    {
        var curve = new CountingLineCurve(10.0);
        var document = new TrackDocument(new[]
        {
            new StraightSegment(10.0, spline: curve)
        });
        var options = new TrackSamplingOptions(
            arcLengthSamples: 4,
            arcLengthTolerance: 1e-3,
            transportSamplesPerSegment: 3);

        TrackRuntimeCompileResult result = TrackRuntimeCompiler.Compile(document, options);

        Assert.True(result.Success);
        Assert.Same(options, result.Runtime!.SamplingOptions);
        Assert.Equal(4, result.Runtime.SamplingOptions.ArcLengthSamples);
        Assert.Equal(1e-3, result.Runtime.SamplingOptions.ArcLengthTolerance);
        Assert.Equal(3, result.Runtime.SamplingOptions.TransportSamplesPerSegment);
        Assert.Equal(17, curve.EvaluateCount);
        Assert.Equal(5, curve.TangentCount);

        new TrackEvaluator(result.Runtime).EvaluateFrameAtDistance(4.0);

        Assert.Equal(22, curve.EvaluateCount);
        Assert.Equal(10, curve.TangentCount);
    }

    [Fact]
    public void Compile_EmptyTrack_ReturnsWarningAndSuccessfulZeroLengthRuntime()
    {
        TrackRuntimeCompileResult result = TrackRuntimeCompiler.Compile(new TrackDocument());

        Assert.True(result.Success);
        Assert.NotNull(result.Runtime);
        Assert.Equal(0, result.Runtime.SegmentCount);
        Assert.Equal(0.0, result.Runtime.TotalLength);
        TrackRuntimeDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        AssertDiagnostic(
            diagnostic,
            TrackRuntimeDiagnosticCode.EmptyTrack,
            TrackRuntimeDiagnosticSeverity.Warning,
            segmentIndex: null,
            segmentId: null,
            splineParameter: null);
    }

    public static IEnumerable<object[]> ErrorDiagnosticCases()
    {
        yield return Case(
            TrackRuntimeDiagnosticCode.NullSegment,
            new TrackDocument(new TrackSegment[] { null! }),
            expectedSegmentIndex: 0,
            expectedSegmentId: null,
            expectedParameter: null);
        yield return Case(
            TrackRuntimeDiagnosticCode.InvalidDeclaredLength,
            new TrackDocument(new[] { new StraightSegment(0.0, "length") }),
            0,
            "length",
            null);
        yield return Case(
            TrackRuntimeDiagnosticCode.InvalidRoll,
            new TrackDocument(new[]
            {
                new StraightSegment(1.0, "roll", rollRadians: double.NaN)
            }),
            0,
            "roll",
            null);
        yield return Case(
            TrackRuntimeDiagnosticCode.SplineMeasurementFailed,
            new TrackDocument(new[]
            {
                new StraightSegment(1.0, "measurement", spline: new ThrowingEvaluateCurve())
            }),
            0,
            "measurement",
            null);
        yield return Case(
            TrackRuntimeDiagnosticCode.InvalidMeasuredLength,
            new TrackDocument(new[]
            {
                new StraightSegment(1.0, "measured", spline: new ZeroLengthCurve())
            }),
            0,
            "measured",
            null);
        yield return Case(
            TrackRuntimeDiagnosticCode.ReportedArcLengthMismatch,
            new TrackDocument(new[]
            {
                new StraightSegment(11.0, "reported", spline: new ReportedLengthCurve(10.0, 11.0))
            }),
            0,
            "reported",
            null);
        yield return Case(
            TrackRuntimeDiagnosticCode.DeclaredLengthMismatch,
            new TrackDocument(new[]
            {
                new StraightSegment(11.0, "declared", spline: new CountingLineCurve(10.0))
            }),
            0,
            "declared",
            null);
        yield return Case(
            TrackRuntimeDiagnosticCode.SplineTangentEvaluationFailed,
            new TrackDocument(new[]
            {
                new StraightSegment(10.0, "tangent-throw", spline: new ThrowingTangentCurve(10.0))
            }),
            0,
            "tangent-throw",
            0.0);
        yield return Case(
            TrackRuntimeDiagnosticCode.InvalidSplineTangent,
            new TrackDocument(new[]
            {
                new StraightSegment(10.0, "tangent-value", spline: new InvalidTangentCurve(10.0))
            }),
            0,
            "tangent-value",
            0.0);
    }

    [Theory]
    [MemberData(nameof(ErrorDiagnosticCases))]
    public void Compile_ErrorDiagnostics_HaveExpectedCodeSeverityAndLocation(
        TrackRuntimeDiagnosticCode expectedCode,
        TrackDocument document,
        int? expectedSegmentIndex,
        string? expectedSegmentId,
        double? expectedParameter)
    {
        TrackRuntimeCompileResult result = TrackRuntimeCompiler.Compile(document);

        Assert.False(result.Success);
        Assert.True(result.HasErrors);
        Assert.Null(result.Runtime);
        TrackRuntimeDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        AssertDiagnostic(
            diagnostic,
            expectedCode,
            TrackRuntimeDiagnosticSeverity.Error,
            expectedSegmentIndex,
            expectedSegmentId,
            expectedParameter);
    }

    [Fact]
    public void Compile_SamplingCapacityExceeded_IsGlobalError()
    {
        TrackDocument document = CreateLineDocument(1.0, includeSpline: false);
        var options = new TrackSamplingOptions(
            arcLengthSamples: 1,
            arcLengthTolerance: 1e-4,
            transportSamplesPerSegment: int.MaxValue);

        TrackRuntimeCompileResult result = TrackRuntimeCompiler.Compile(document, options);

        Assert.False(result.Success);
        Assert.Null(result.Runtime);
        AssertDiagnostic(
            Assert.Single(result.Diagnostics),
            TrackRuntimeDiagnosticCode.SamplingCapacityExceeded,
            TrackRuntimeDiagnosticSeverity.Error,
            null,
            null,
            null);
    }

    [Fact]
    public void Compile_FailedResultDiagnostics_AreReadOnly()
    {
        TrackRuntimeCompileResult result = TrackRuntimeCompiler.Compile(
            new TrackDocument(new[] { new StraightSegment(0.0) }));

        Assert.False(result.Success);
        Assert.Null(result.Runtime);
        Assert.IsAssignableFrom<IList>(result.Diagnostics);
        IList diagnostics = (IList)result.Diagnostics;
        Assert.True(diagnostics.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => diagnostics.Clear());
    }

    [Fact]
    public void Compile_ReportsDeterministicFirstErrorInSegmentOrder()
    {
        var document = new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(0.0, "first"),
            new StraightSegment(1.0, "second", rollRadians: double.NaN),
            null!
        });

        TrackRuntimeCompileResult first = TrackRuntimeCompiler.Compile(document);
        TrackRuntimeCompileResult second = TrackRuntimeCompiler.Compile(document);

        TrackRuntimeDiagnostic firstDiagnostic = Assert.Single(first.Diagnostics);
        TrackRuntimeDiagnostic secondDiagnostic = Assert.Single(second.Diagnostics);
        Assert.Equal(TrackRuntimeDiagnosticCode.InvalidDeclaredLength, firstDiagnostic.Code);
        Assert.Equal(0, firstDiagnostic.SegmentIndex);
        Assert.Equal(firstDiagnostic.Code, secondDiagnostic.Code);
        Assert.Equal(firstDiagnostic.Message, secondDiagnostic.Message);
        Assert.Equal(firstDiagnostic.SegmentIndex, secondDiagnostic.SegmentIndex);
    }

    [Fact]
    public void CompilerAndOptionsAwareRuntime_RejectNullArguments()
    {
        TrackDocument document = CreateLineDocument(1.0);
        TrackSamplingOptions options = TrackSamplingOptions.Default;

        Assert.Equal(
            "document",
            Assert.Throws<ArgumentNullException>(() => TrackRuntimeCompiler.Compile(null!)).ParamName);
        Assert.Equal(
            "document",
            Assert.Throws<ArgumentNullException>(
                () => TrackRuntimeCompiler.Compile(null!, options)).ParamName);
        Assert.Equal(
            "options",
            Assert.Throws<ArgumentNullException>(
                () => TrackRuntimeCompiler.Compile(document, null!)).ParamName);
        Assert.Equal(
            "options",
            Assert.Throws<ArgumentNullException>(
                () => new CompiledTrackRuntime(document, null!)).ParamName);
    }

    [Theory]
    [InlineData(0, 1e-4, 1, "arcLengthSamples")]
    [InlineData(1, 0.0, 1, "arcLengthTolerance")]
    [InlineData(1, double.NaN, 1, "arcLengthTolerance")]
    [InlineData(1, 1e-4, 0, "transportSamplesPerSegment")]
    public void SamplingOptions_RejectInvalidValues(
        int arcLengthSamples,
        double arcLengthTolerance,
        int transportSamplesPerSegment,
        string expectedParameter)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new TrackSamplingOptions(
                arcLengthSamples,
                arcLengthTolerance,
                transportSamplesPerSegment));

        Assert.Equal(expectedParameter, exception.ParamName);
    }

    [Fact]
    public void RuntimeCompilerPublicApi_StaysOnBackendDomainBoundary()
    {
        Type[] types =
        {
            typeof(TrackRuntimeCompiler),
            typeof(TrackRuntimeCompileResult),
            typeof(TrackRuntimeDiagnostic),
            typeof(TrackRuntimeDiagnosticCode),
            typeof(TrackRuntimeDiagnosticSeverity),
            typeof(TrackSamplingOptions)
        };

        Assert.NotNull(typeof(CompiledTrackRuntime).GetConstructor(
            new[] { typeof(TrackDocument), typeof(TrackSamplingOptions) }));
        Assert.Equal(
            typeof(TrackSamplingOptions),
            typeof(CompiledTrackRuntime).GetProperty(
                nameof(CompiledTrackRuntime.SamplingOptions))!.PropertyType);

        foreach (Type type in types)
        {
            Assert.True(type.IsPublic || type.IsNestedPublic);
            Assert.DoesNotContain(
                type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static),
                member => MemberExposesForbiddenType(member));
        }
    }

    [Fact]
    public void LegacyRuntimeConstructor_PreservesValidationExceptionShape()
    {
        var measurementDocument = new TrackDocument(new[]
        {
            new StraightSegment(1.0, spline: new ThrowingEvaluateCurve())
        });
        var tangentDocument = new TrackDocument(new[]
        {
            new StraightSegment(10.0, spline: new ThrowingTangentCurve(10.0))
        });

        InvalidOperationException measurement = Assert.Throws<InvalidOperationException>(
            () => new CompiledTrackRuntime(measurementDocument));
        InvalidOperationException tangent = Assert.Throws<InvalidOperationException>(
            () => new CompiledTrackRuntime(tangentDocument));

        Assert.Contains("spline could not be measured", measurement.Message);
        Assert.IsType<TestCurveException>(measurement.InnerException);
        Assert.Contains("invalid tangent at t=0", tangent.Message);
        Assert.IsType<TestCurveException>(tangent.InnerException);
    }

    private static object[] Case(
        TrackRuntimeDiagnosticCode code,
        TrackDocument document,
        int? expectedSegmentIndex,
        string? expectedSegmentId,
        double? expectedParameter)
    {
        return new object[]
        {
            code,
            document,
            expectedSegmentIndex!,
            expectedSegmentId!,
            expectedParameter!
        };
    }

    private static TrackDocument CreateLineDocument(double length, bool includeSpline = true)
    {
        IParamCurve? spline = includeSpline ? new CountingLineCurve(length) : null;
        return new TrackDocument(new[]
        {
            new StraightSegment(length, "line", spline: spline)
        });
    }

    private static void AssertDiagnostic(
        TrackRuntimeDiagnostic diagnostic,
        TrackRuntimeDiagnosticCode code,
        TrackRuntimeDiagnosticSeverity severity,
        int? segmentIndex,
        string? segmentId,
        double? splineParameter)
    {
        Assert.Equal(code, diagnostic.Code);
        Assert.Equal(severity, diagnostic.Severity);
        Assert.False(string.IsNullOrWhiteSpace(diagnostic.Message));
        Assert.Equal(segmentIndex, diagnostic.SegmentIndex);
        Assert.Equal(segmentId, diagnostic.SegmentId);
        Assert.Equal(splineParameter, diagnostic.SplineParameter);
        Assert.Equal(splineParameter, diagnostic.Parameter);
        Assert.Equal(splineParameter, diagnostic.LocalT);
    }

    private static bool MemberExposesForbiddenType(MemberInfo member)
    {
        switch (member)
        {
            case ConstructorInfo constructor:
                return constructor.GetParameters().Any(
                    parameter => IsForbidden(parameter.ParameterType));
            case PropertyInfo property:
                return IsForbidden(property.PropertyType);
            case MethodInfo method:
                return IsForbidden(method.ReturnType) || method.GetParameters().Any(
                    parameter => IsForbidden(parameter.ParameterType));
            default:
                return false;
        }
    }

    private static bool IsForbidden(Type type)
    {
        string typeNamespace = type.Namespace ?? string.Empty;
        if (typeNamespace == "Quantum.Splines" ||
            typeNamespace.StartsWith("UnityEngine", StringComparison.Ordinal) ||
            typeNamespace.StartsWith("UnityEditor", StringComparison.Ordinal))
        {
            return true;
        }

        if (type.IsGenericType && type.GetGenericArguments().Any(IsForbidden))
        {
            return true;
        }

        return type.HasElementType && IsForbidden(type.GetElementType()!);
    }

    private static void AssertFrameNear(TrackFrame expected, TrackFrame actual)
    {
        AssertNear(expected.Distance, actual.Distance);
        AssertVectorNear(expected.Position, actual.Position);
        AssertVectorNear(expected.Tangent, actual.Tangent);
        AssertVectorNear(expected.Normal, actual.Normal);
        AssertVectorNear(expected.Binormal, actual.Binormal);
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual)
    {
        AssertNear(expected.X, actual.X);
        AssertNear(expected.Y, actual.Y);
        AssertNear(expected.Z, actual.Z);
    }

    private static void AssertNear(double expected, double actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, Tolerance);
    }

    private class CountingLineCurve : IParamCurve
    {
        private readonly double _length;

        public CountingLineCurve(double length)
        {
            _length = length;
        }

        public int EvaluateCount { get; private set; }

        public int TangentCount { get; private set; }

        public virtual Vector3d Evaluate(double t)
        {
            EvaluateCount++;
            return new Vector3d(_length * t, 0.0, 0.0);
        }

        public virtual Vector3d Tangent(double t)
        {
            TangentCount++;
            return Vector3d.UnitX;
        }
    }

    private sealed class ThrowingEvaluateCurve : IParamCurve
    {
        public Vector3d Evaluate(double t)
        {
            throw new TestCurveException();
        }

        public Vector3d Tangent(double t)
        {
            return Vector3d.UnitX;
        }
    }

    private sealed class ZeroLengthCurve : IParamCurve
    {
        public Vector3d Evaluate(double t)
        {
            return Vector3d.Zero;
        }

        public Vector3d Tangent(double t)
        {
            return Vector3d.UnitX;
        }
    }

    private sealed class ReportedLengthCurve : IArcLengthCurve
    {
        private readonly double _measuredLength;

        public ReportedLengthCurve(double measuredLength, double reportedLength)
        {
            _measuredLength = measuredLength;
            Length = reportedLength;
        }

        public double Length { get; }

        public Vector3d Evaluate(double t)
        {
            return new Vector3d(_measuredLength * t, 0.0, 0.0);
        }

        public Vector3d Tangent(double t)
        {
            return Vector3d.UnitX;
        }

        public Vector3d EvaluateByLength(double s)
        {
            return new Vector3d(s, 0.0, 0.0);
        }

        public Vector3d TangentByLength(double s)
        {
            return Vector3d.UnitX;
        }
    }

    private sealed class ThrowingTangentCurve : CountingLineCurve
    {
        public ThrowingTangentCurve(double length)
            : base(length)
        {
        }

        public override Vector3d Tangent(double t)
        {
            throw new TestCurveException();
        }
    }

    private sealed class InvalidTangentCurve : CountingLineCurve
    {
        public InvalidTangentCurve(double length)
            : base(length)
        {
        }

        public override Vector3d Tangent(double t)
        {
            return Vector3d.Zero;
        }
    }

    private sealed class TestCurveException : Exception
    {
    }
}
