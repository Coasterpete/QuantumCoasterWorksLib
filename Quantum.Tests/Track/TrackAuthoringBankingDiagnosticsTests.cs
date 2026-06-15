using System.Reflection;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class TrackAuthoringBankingDiagnosticsTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void PublicContract_UsesRequiredKindsAndDefaultOptions()
    {
        Assert.Equal(
            new[]
            {
                TrackAuthoringBankingDiagnosticKind.StartEndpointMismatch,
                TrackAuthoringBankingDiagnosticKind.EndEndpointMismatch,
                TrackAuthoringBankingDiagnosticKind.RollDiscontinuity,
                TrackAuthoringBankingDiagnosticKind.RollSlope
            },
            Enum.GetValues<TrackAuthoringBankingDiagnosticKind>());
        Assert.Equal(
            new[]
            {
                TrackAuthoringBankingProfileSourceKind.ExplicitAuthored,
                TrackAuthoringBankingProfileSourceKind.SectionRollFallback
            },
            Enum.GetValues<TrackAuthoringBankingProfileSourceKind>());

        TrackAuthoringBankingDiagnosticsOptions options =
            TrackAuthoringBankingDiagnosticsOptions.Default;
        Assert.Equal(1e-9, options.EndpointDistanceTolerance);
        Assert.Equal(8, options.SamplesPerProfileInterval);
        Assert.Equal(ContinuousRollWrapMode.None, options.ContinuousRollOptions.WrapMode);
        AssertNear(ToRadians(45.0), options.ContinuousRollOptions.RollDeltaWarningThresholdRadians);
        AssertNear(ToRadians(45.0), options.ContinuousRollOptions.RollRateWarningThresholdRadPerMeter);

        Assert.Equal(
            typeof(TrackAuthoringBankingDiagnosticsReport),
            typeof(TrackAuthoringBankingDiagnostics).GetMethod(
                nameof(TrackAuthoringBankingDiagnostics.Analyze),
                new[] { typeof(TrackAuthoringDefinition) })?.ReturnType);
        Assert.Equal(
            typeof(TrackAuthoringBankingDiagnosticsReport),
            typeof(TrackAuthoringBankingDiagnostics).GetMethod(
                nameof(TrackAuthoringBankingDiagnostics.Analyze),
                new[] { typeof(TrackAuthoringCompilation) })?.ReturnType);
        Assert.NotNull(typeof(TrackAuthoringBankingDiagnostics).GetMethod(
            nameof(TrackAuthoringBankingDiagnostics.Analyze),
            new[]
            {
                typeof(TrackAuthoringDefinition),
                typeof(TrackAuthoringBankingDiagnosticsOptions)
            }));
        Assert.NotNull(typeof(TrackAuthoringBankingDiagnostics).GetMethod(
            nameof(TrackAuthoringBankingDiagnostics.Analyze),
            new[]
            {
                typeof(TrackAuthoringCompilation),
                typeof(TrackAuthoringBankingDiagnosticsOptions)
            }));
    }

    [Fact]
    public void Analyze_DefinitionAndCompilationOverloadsProduceEquivalentReports()
    {
        TrackAuthoringDefinition definition = CreateExplicitBankingDefinition(
            new BankingProfileKey(0.0, 0.0, BankingProfileInterpolationMode.Linear),
            new BankingProfileKey(4.0, 0.5, BankingProfileInterpolationMode.SmoothStep),
            new BankingProfileKey(10.0, -0.25));
        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(definition);
        var options = new TrackAuthoringBankingDiagnosticsOptions(
            endpointDistanceTolerance: 1e-8,
            samplesPerProfileInterval: 4,
            continuousRollOptions: ContinuousRollDiagnosticsOptions.NoWrap);

        TrackAuthoringBankingDiagnosticsReport definitionReport =
            TrackAuthoringBankingDiagnostics.Analyze(definition, options);
        TrackAuthoringBankingDiagnosticsReport compilationReport =
            TrackAuthoringBankingDiagnostics.Analyze(compilation, options);

        AssertReportsEquivalent(definitionReport, compilationReport);
    }

    [Fact]
    public void Analyze_FallbackProfileReportsSectionRollFallback()
    {
        TrackAuthoringBankingDiagnosticsReport report =
            TrackAuthoringBankingDiagnostics.Analyze(new TrackAuthoringDefinition(
                new GeometricSectionDefinition[]
                {
                    new StraightSectionDefinition("first", 4.0),
                    new StraightSectionDefinition("second", 6.0)
                }));

        Assert.Equal(TrackAuthoringBankingProfileSourceKind.SectionRollFallback, report.SourceKind);
        Assert.False(report.HasDiagnostics);
    }

    [Fact]
    public void Analyze_ExplicitProfileReportsExplicitAuthored()
    {
        TrackAuthoringBankingDiagnosticsReport report =
            TrackAuthoringBankingDiagnostics.Analyze(CreateExplicitBankingDefinition(
                new BankingProfileKey(0.0, 0.0),
                new BankingProfileKey(10.0, 0.25)));

        Assert.Equal(TrackAuthoringBankingProfileSourceKind.ExplicitAuthored, report.SourceKind);
        Assert.False(report.HasDiagnostics);
    }

    [Fact]
    public void Coverage_ReportsEndpointDistancesAndPassesForNormalCompiledProfiles()
    {
        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(
            new TrackAuthoringDefinition(new GeometricSectionDefinition[]
            {
                new StraightSectionDefinition("first", 4.0),
                new StraightSectionDefinition("second", 6.0)
            }));

        TrackAuthoringBankingDiagnosticsReport report =
            TrackAuthoringBankingDiagnostics.Analyze(compilation);

        Assert.Equal(0.0, report.Coverage.ExpectedStartDistance);
        Assert.Equal(0.0, report.Coverage.ActualStartDistance);
        Assert.Equal(10.0, report.Coverage.ExpectedEndDistance);
        Assert.Equal(10.0, report.Coverage.ActualEndDistance);
        Assert.Equal(10.0, report.Coverage.TotalLength);
        Assert.True(report.Coverage.StartsAtTrackStart);
        Assert.True(report.Coverage.EndsAtTrackEnd);
        Assert.True(report.Coverage.Passes);
        Assert.Empty(report.Diagnostics);
    }

    [Fact]
    public void Coverage_MismatchedEndpointsEmitAuthoringSpecificDiagnostics()
    {
        TrackAuthoringCompilation compilation = CreateCompilationWithBankingProfile(
            new BankingProfile(new[]
            {
                new BankingProfileKey(0.25, 0.0),
                new BankingProfileKey(9.75, 0.0)
            }));

        TrackAuthoringBankingDiagnosticsReport report =
            TrackAuthoringBankingDiagnostics.Analyze(compilation);

        Assert.False(report.Coverage.Passes);
        Assert.Contains(
            report.Diagnostics,
            diagnostic => diagnostic.Kind == TrackAuthoringBankingDiagnosticKind.StartEndpointMismatch &&
                diagnostic.ExpectedDistance == 0.0 &&
                diagnostic.ActualDistance == 0.25);
        Assert.Contains(
            report.Diagnostics,
            diagnostic => diagnostic.Kind == TrackAuthoringBankingDiagnosticKind.EndEndpointMismatch &&
                diagnostic.ExpectedDistance == 10.0 &&
                diagnostic.ActualDistance == 9.75);
    }

    [Fact]
    public void Analyze_ExplicitProfilePreservesUnwrappedValuesAndInterpolationModes()
    {
        double fullTurn = 2.0 * System.Math.PI;
        TrackAuthoringBankingDiagnosticsReport report =
            TrackAuthoringBankingDiagnostics.Analyze(CreateExplicitBankingDefinition(
                new BankingProfileKey(0.0, fullTurn + 0.25, BankingProfileInterpolationMode.Linear),
                new BankingProfileKey(4.0, fullTurn * 2.0, BankingProfileInterpolationMode.SmoothStep),
                new BankingProfileKey(10.0, -fullTurn - 0.5)));

        BankingProfileDiagnosticsSample firstKey = Assert.Single(
            report.Samples,
            sample => sample.Distance == 0.0);
        BankingProfileDiagnosticsSample middleKey = Assert.Single(
            report.Samples,
            sample => sample.Distance == 4.0);
        BankingProfileDiagnosticsSample lastKey = Assert.Single(
            report.Samples,
            sample => sample.Distance == 10.0);
        BankingProfileDiagnosticsSample linearInterior = Assert.Single(
            report.Samples,
            sample => sample.Distance == 0.5);
        BankingProfileDiagnosticsSample smoothInterior = Assert.Single(
            report.Samples,
            sample => sample.Distance == 4.75);

        AssertNear(fullTurn + 0.25, firstKey.RollRadians);
        AssertNear(fullTurn * 2.0, middleKey.RollRadians);
        AssertNear(-fullTurn - 0.5, lastKey.RollRadians);
        Assert.Equal(BankingProfileInterpolationMode.Linear, linearInterior.InterpolationMode);
        Assert.Equal(BankingProfileInterpolationMode.SmoothStep, smoothInterior.InterpolationMode);
    }

    [Fact]
    public void Analyze_AbruptConstantBankingStepEmitsRollDiscontinuity()
    {
        var options = new TrackAuthoringBankingDiagnosticsOptions(
            endpointDistanceTolerance: 1e-9,
            samplesPerProfileInterval: 1,
            continuousRollOptions: ContinuousRollDiagnosticsOptions.NoWrap);

        TrackAuthoringBankingDiagnosticsReport report =
            TrackAuthoringBankingDiagnostics.Analyze(CreateExplicitBankingDefinition(
                new BankingProfileKey(0.0, 0.0, BankingProfileInterpolationMode.Constant),
                new BankingProfileKey(10.0, ToRadians(120.0), BankingProfileInterpolationMode.Constant)),
                options);

        TrackAuthoringBankingDiagnostic diagnostic = Assert.Single(report.Diagnostics);
        Assert.Equal(TrackAuthoringBankingDiagnosticKind.RollDiscontinuity, diagnostic.Kind);
        Assert.Equal(0, diagnostic.StartSampleIndex);
        Assert.Equal(1, diagnostic.EndSampleIndex);
        AssertNear(ToRadians(120.0), diagnostic.RollDeltaRadians!.Value);
    }

    [Fact]
    public void Analyze_ExcessRollSlopeEmitsRollSlope()
    {
        var options = new TrackAuthoringBankingDiagnosticsOptions(
            endpointDistanceTolerance: 1e-9,
            samplesPerProfileInterval: 1,
            continuousRollOptions: ContinuousRollDiagnosticsOptions.NoWrap);

        TrackAuthoringBankingDiagnosticsReport report =
            TrackAuthoringBankingDiagnostics.Analyze(CreateExplicitBankingDefinition(
                new BankingProfileKey(0.0, 0.0, BankingProfileInterpolationMode.Linear),
                new BankingProfileKey(0.1, ToRadians(20.0), BankingProfileInterpolationMode.Constant),
                new BankingProfileKey(10.0, ToRadians(20.0))),
                options);

        TrackAuthoringBankingDiagnostic diagnostic = Assert.Single(report.Diagnostics);
        Assert.Equal(TrackAuthoringBankingDiagnosticKind.RollSlope, diagnostic.Kind);
        AssertNear(ToRadians(200.0), diagnostic.RollRateRadPerMeter!.Value);
        AssertNear(ToRadians(45.0), diagnostic.Tolerance);
    }

    [Fact]
    public void Analyze_DefaultNoWrapPreservesFullTurnUnwrappedBehavior()
    {
        double fullTurn = 2.0 * System.Math.PI;

        TrackAuthoringBankingDiagnosticsReport report =
            TrackAuthoringBankingDiagnostics.Analyze(CreateExplicitBankingDefinition(
                new BankingProfileKey(0.0, 0.0, BankingProfileInterpolationMode.Constant),
                new BankingProfileKey(10.0, fullTurn)));

        Assert.Equal(ContinuousRollWrapMode.None, report.ContinuousRollReport.Options.WrapMode);
        AssertNear(fullTurn, report.ContinuousRollSamples[^1].ContinuousRollRadians);
        Assert.Contains(
            report.Diagnostics,
            diagnostic => diagnostic.Kind == TrackAuthoringBankingDiagnosticKind.RollDiscontinuity);
    }

    [Fact]
    public void Analyze_ExplicitFullTurnWrapOptionsAreHonored()
    {
        double fullTurn = 2.0 * System.Math.PI;
        var options = new TrackAuthoringBankingDiagnosticsOptions(
            endpointDistanceTolerance: 1e-9,
            samplesPerProfileInterval: 1,
            continuousRollOptions: ContinuousRollDiagnosticsOptions.Default);

        TrackAuthoringBankingDiagnosticsReport report =
            TrackAuthoringBankingDiagnostics.Analyze(CreateExplicitBankingDefinition(
                new BankingProfileKey(0.0, 0.0, BankingProfileInterpolationMode.Linear),
                new BankingProfileKey(10.0, fullTurn)),
                options);

        Assert.Equal(ContinuousRollWrapMode.FullTurn, report.ContinuousRollReport.Options.WrapMode);
        Assert.Empty(report.Diagnostics);
        Assert.True(Assert.Single(report.ContinuousRollIntervals).UsedWrapAround);
        AssertNear(0.0, report.ContinuousRollSamples[^1].ContinuousRollRadians);
        AssertNear(fullTurn, report.Samples[^1].RollRadians);
    }

    [Fact]
    public void Analyze_DiagnosticCollectionsAreReadOnly()
    {
        TrackAuthoringBankingDiagnosticsReport report =
            TrackAuthoringBankingDiagnostics.Analyze(CreateExplicitBankingDefinition(
                new BankingProfileKey(0.0, 0.0, BankingProfileInterpolationMode.Constant),
                new BankingProfileKey(10.0, ToRadians(120.0))));

        IList<double> sampleDistances = Assert.IsAssignableFrom<IList<double>>(report.SampleDistances);
        IList<TrackAuthoringBankingDiagnostic> diagnostics =
            Assert.IsAssignableFrom<IList<TrackAuthoringBankingDiagnostic>>(report.Diagnostics);

        Assert.True(sampleDistances.IsReadOnly);
        Assert.True(diagnostics.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => sampleDistances.Add(99.0));
        Assert.Throws<NotSupportedException>(() => diagnostics.Add(default));
    }

    [Fact]
    public void Analyze_RepeatedAnalysisIsDeterministic()
    {
        TrackAuthoringDefinition definition = CreateExplicitBankingDefinition(
            new BankingProfileKey(0.0, 0.0, BankingProfileInterpolationMode.Linear),
            new BankingProfileKey(3.0, 0.5, BankingProfileInterpolationMode.SmoothStep),
            new BankingProfileKey(10.0, -0.25));

        TrackAuthoringBankingDiagnosticsReport first =
            TrackAuthoringBankingDiagnostics.Analyze(definition);
        TrackAuthoringBankingDiagnosticsReport second =
            TrackAuthoringBankingDiagnostics.Analyze(definition);

        AssertReportsEquivalent(first, second);
    }

    [Fact]
    public void Analyze_NullInputsThrowClearly()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TrackAuthoringBankingDiagnostics.Analyze((TrackAuthoringDefinition)null!));
        Assert.Throws<ArgumentNullException>(() =>
            TrackAuthoringBankingDiagnostics.Analyze(
                (TrackAuthoringDefinition)null!,
                TrackAuthoringBankingDiagnosticsOptions.Default));

        ArgumentNullException defaultException = Assert.Throws<ArgumentNullException>(() =>
            TrackAuthoringBankingDiagnostics.Analyze((TrackAuthoringCompilation)null!));
        ArgumentNullException customException = Assert.Throws<ArgumentNullException>(() =>
            TrackAuthoringBankingDiagnostics.Analyze(
                (TrackAuthoringCompilation)null!,
                TrackAuthoringBankingDiagnosticsOptions.Default));

        Assert.Equal("compilation", defaultException.ParamName);
        Assert.Equal("compilation", customException.ParamName);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Options_RejectInvalidEndpointTolerance(double endpointDistanceTolerance)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TrackAuthoringBankingDiagnosticsOptions(
                endpointDistanceTolerance,
                1,
                ContinuousRollDiagnosticsOptions.NoWrap));
    }

    [Fact]
    public void Options_RejectSamplesPerProfileIntervalLessThanOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TrackAuthoringBankingDiagnosticsOptions(
                endpointDistanceTolerance: 0.0,
                samplesPerProfileInterval: 0,
                continuousRollOptions: ContinuousRollDiagnosticsOptions.NoWrap));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrackAuthoringBankingDiagnostics.Analyze(
                CreateExplicitBankingDefinition(
                    new BankingProfileKey(0.0, 0.0),
                    new BankingProfileKey(10.0, 0.0)),
                default));
    }

    [Fact]
    public void PublicApi_ExposesNoUnityUiFvdIoRenderingOrSplineTypes()
    {
        Type[] types =
        {
            typeof(TrackAuthoringBankingDiagnostics),
            typeof(TrackAuthoringBankingDiagnosticsReport),
            typeof(TrackAuthoringBankingDiagnostic),
            typeof(TrackAuthoringBankingDiagnosticKind),
            typeof(TrackAuthoringBankingDiagnosticsOptions),
            typeof(TrackAuthoringBankingCoverage),
            typeof(TrackAuthoringBankingProfileSourceKind)
        };

        foreach (Type type in types)
        {
            IEnumerable<Type> exposedTypes = type.GetConstructors().SelectMany(
                    constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType))
                .Concat(type.GetProperties(
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                        BindingFlags.DeclaredOnly)
                    .Select(property => property.PropertyType))
                .Concat(type.GetMethods(
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                        BindingFlags.DeclaredOnly)
                    .SelectMany(method => method.GetParameters()
                        .Select(parameter => parameter.ParameterType)
                        .Append(method.ReturnType)));

            Assert.All(exposedTypes, exposedType => AssertAllowedPublicType(exposedType, type));
        }
    }

    private static TrackAuthoringDefinition CreateExplicitBankingDefinition(
        params BankingProfileKey[] keys)
    {
        return new TrackAuthoringDefinition(
            new[] { new StraightSectionDefinition("track", 10.0) },
            TrackStartPose.Identity,
            new TrackBankingDefinition(keys));
    }

    private static TrackAuthoringCompilation CreateCompilationWithBankingProfile(
        BankingProfile bankingProfile)
    {
        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(
            new TrackAuthoringDefinition(new[] { new StraightSectionDefinition("track", 10.0) }));
        return new TrackAuthoringCompilation(
            compilation.Definition,
            compilation.Document,
            compilation.Runtime,
            bankingProfile,
            compilation.ResolvedSections,
            compilation.TotalLength);
    }

    private static void AssertReportsEquivalent(
        TrackAuthoringBankingDiagnosticsReport expected,
        TrackAuthoringBankingDiagnosticsReport actual)
    {
        Assert.Equal(expected.SourceKind, actual.SourceKind);
        Assert.Equal(expected.Coverage, actual.Coverage);
        Assert.Equal(expected.SampleDistances, actual.SampleDistances);
        Assert.Equal(expected.Options, actual.Options);
        Assert.Equal(expected.Diagnostics, actual.Diagnostics);
        Assert.Equal(expected.DiagnosticCount, actual.DiagnosticCount);
        Assert.Equal(expected.HasDiagnostics, actual.HasDiagnostics);

        Assert.Equal(expected.Samples.Count, actual.Samples.Count);
        for (int i = 0; i < expected.Samples.Count; i++)
        {
            AssertBankingSample(expected.Samples[i], actual.Samples[i]);
        }

        Assert.Equal(expected.ContinuousRollSamples.Count, actual.ContinuousRollSamples.Count);
        for (int i = 0; i < expected.ContinuousRollSamples.Count; i++)
        {
            AssertContinuousRollSample(
                expected.ContinuousRollSamples[i],
                actual.ContinuousRollSamples[i]);
        }

        Assert.Equal(expected.ContinuousRollIntervals, actual.ContinuousRollIntervals);
        Assert.Equal(expected.ContinuousRollWarnings, actual.ContinuousRollWarnings);
    }

    private static void AssertBankingSample(
        BankingProfileDiagnosticsSample expected,
        BankingProfileDiagnosticsSample actual)
    {
        Assert.Equal(expected.SampleIndex, actual.SampleIndex);
        Assert.Equal(expected.Distance, actual.Distance);
        Assert.Equal(expected.RollRadians, actual.RollRadians);
        Assert.Equal(expected.RollDegrees, actual.RollDegrees);
        Assert.Equal(expected.InterpolationMode, actual.InterpolationMode);
        Assert.Equal(expected.SourceKind, actual.SourceKind);
        Assert.Equal(expected.SourceStartKeyIndex, actual.SourceStartKeyIndex);
        Assert.Equal(expected.SourceEndKeyIndex, actual.SourceEndKeyIndex);
        Assert.Equal(expected.SourceStartDistance, actual.SourceStartDistance);
        Assert.Equal(expected.SourceEndDistance, actual.SourceEndDistance);
        Assert.Equal(
            expected.ApproximateRollSlopeRadPerMeter,
            actual.ApproximateRollSlopeRadPerMeter);
    }

    private static void AssertContinuousRollSample(
        ContinuousRollDiagnosticsSample expected,
        ContinuousRollDiagnosticsSample actual)
    {
        Assert.Equal(expected.SampleIndex, actual.SampleIndex);
        Assert.Equal(expected.Distance, actual.Distance);
        Assert.Equal(expected.RollRadians, actual.RollRadians);
        Assert.Equal(expected.ContinuousRollRadians, actual.ContinuousRollRadians);
    }

    private static void AssertAllowedPublicType(Type type, Type owner)
    {
        if (type.IsGenericType)
        {
            foreach (Type argument in type.GetGenericArguments())
            {
                AssertAllowedPublicType(argument, owner);
            }
        }

        if (type.HasElementType && type.GetElementType() is Type elementType)
        {
            AssertAllowedPublicType(elementType, owner);
        }

        string typeName = type.FullName ?? type.Name;
        Assert.False(
            typeName.StartsWith("Quantum.Splines", StringComparison.Ordinal) ||
            typeName.StartsWith("Quantum.FVD", StringComparison.Ordinal) ||
            typeName.StartsWith("Quantum.IO", StringComparison.Ordinal) ||
            typeName.StartsWith("GShark", StringComparison.Ordinal) ||
            typeName.StartsWith("Unity", StringComparison.Ordinal) ||
            typeName.StartsWith("Avalonia", StringComparison.Ordinal) ||
            typeName.StartsWith("OpenTK", StringComparison.Ordinal) ||
            typeName.StartsWith("Silk.NET", StringComparison.Ordinal),
            $"{owner.Name} exposes forbidden public type {typeName}.");
    }

    private static double ToRadians(double degrees)
    {
        return degrees * System.Math.PI / 180.0;
    }

    private static void AssertNear(double expected, double actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, Tolerance);
    }
}
