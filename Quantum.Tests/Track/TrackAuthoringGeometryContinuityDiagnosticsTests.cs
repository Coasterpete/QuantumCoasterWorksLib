using System.Reflection;
using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class TrackAuthoringGeometryContinuityDiagnosticsTests
{
    private const double TestTolerance = 1e-7;

    [Fact]
    public void PublicContract_UsesRequiredKindsAndDefaultTolerances()
    {
        Assert.Equal(
            new[]
            {
                TrackAuthoringGeometryContinuityDiagnosticKind.PositionDiscontinuity,
                TrackAuthoringGeometryContinuityDiagnosticKind.TangentDiscontinuity,
                TrackAuthoringGeometryContinuityDiagnosticKind.CurvatureVectorDiscontinuity,
                TrackAuthoringGeometryContinuityDiagnosticKind.RollDiscontinuity
            },
            Enum.GetValues<TrackAuthoringGeometryContinuityDiagnosticKind>());

        TrackAuthoringGeometryContinuityTolerances tolerances =
            TrackAuthoringGeometryContinuityTolerances.Default;
        Assert.Equal(1e-7, tolerances.PositionTolerance);
        Assert.Equal(1e-7, tolerances.TangentAngleToleranceRadians);
        Assert.Equal(1e-4, tolerances.CurvatureVectorTolerance);
        Assert.Equal(1e-9, tolerances.RollToleranceRadians);

        Assert.Equal(
            typeof(TrackAuthoringGeometryContinuityReport),
            typeof(TrackAuthoringGeometryContinuityDiagnostics).GetMethod(
                nameof(TrackAuthoringGeometryContinuityDiagnostics.Analyze),
                new[] { typeof(TrackAuthoringCompilation) })?.ReturnType);
        Assert.Equal(
            typeof(TrackAuthoringGeometryContinuityReport),
            typeof(TrackAuthoringGeometryContinuityDiagnostics).GetMethod(
                nameof(TrackAuthoringGeometryContinuityDiagnostics.Analyze),
                new[]
                {
                    typeof(TrackAuthoringCompilation),
                    typeof(TrackAuthoringGeometryContinuityTolerances)
                })?.ReturnType);
    }

    [Fact]
    public void Analyze_DefinitionAndCompilationOverloadsProduceEquivalentReports()
    {
        TrackAuthoringDefinition definition =
            CreateMixedSpatialDefinition(CreateArbitraryStartPose());
        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(definition);
        var tolerances = new TrackAuthoringGeometryContinuityTolerances(
            1e-8,
            1e-8,
            1e-5,
            1e-8);

        TrackAuthoringGeometryContinuityReport definitionReport =
            TrackAuthoringGeometryContinuityDiagnostics.Analyze(definition, tolerances);
        TrackAuthoringGeometryContinuityReport compilationReport =
            TrackAuthoringGeometryContinuityDiagnostics.Analyze(compilation, tolerances);

        Assert.Equal(definitionReport.Boundaries, compilationReport.Boundaries);
        Assert.Equal(definitionReport.Diagnostics, compilationReport.Diagnostics);
        Assert.Equal(definitionReport.Tolerances, compilationReport.Tolerances);
    }

    [Fact]
    public void Analyze_MixedStraightSpatialArcSpatialStraight_ProducesOrderedBoundaries()
    {
        TrackAuthoringDefinition definition = CreateMixedSpatialDefinition(TrackStartPose.Identity);

        TrackAuthoringGeometryContinuityReport report =
            TrackAuthoringGeometryContinuityDiagnostics.Analyze(definition);

        Assert.Equal(definition.Sections.Count - 1, report.BoundaryCount);
        Assert.Equal(new[] { 0, 1, 2, 3 }, report.Boundaries.Select(boundary => boundary.BoundaryIndex));
        Assert.Equal(new[] { 0, 1, 2, 3 }, report.Boundaries.Select(boundary => boundary.PreviousSectionIndex));
        Assert.Equal(new[] { 1, 2, 3, 4 }, report.Boundaries.Select(boundary => boundary.NextSectionIndex));
        Assert.Equal(
            new[] { "entry", "spatial-up", "arc", "spatial-down" },
            report.Boundaries.Select(boundary => boundary.PreviousSectionId));
        Assert.Equal(
            new[] { "spatial-up", "arc", "spatial-down", "exit" },
            report.Boundaries.Select(boundary => boundary.NextSectionId));

        double station = 0.0;
        for (int i = 0; i < report.BoundaryCount; i++)
        {
            station += definition.Sections[i].Length;
            AssertNear(station, report.Boundaries[i].Station);
            Assert.Equal(report.Boundaries[i].Station, report.Boundaries[i].StationDistance);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Analyze_MixedLayout_PositionAndTangentsAreContinuous(bool arbitraryStartPose)
    {
        TrackStartPose pose = arbitraryStartPose ? CreateArbitraryStartPose() : TrackStartPose.Identity;
        TrackAuthoringGeometryContinuityReport report =
            TrackAuthoringGeometryContinuityDiagnostics.Analyze(CreateMixedSpatialDefinition(pose));

        Assert.All(report.Boundaries, boundary =>
        {
            AssertVectorNear(Vector3d.Zero, boundary.PositionGap);
            Assert.InRange(boundary.PositionGapMagnitude, 0.0, 1e-12);
            Assert.InRange(boundary.TangentAngleRadians, 0.0, 1e-12);
        });
        Assert.DoesNotContain(
            report.Diagnostics,
            diagnostic => diagnostic.Kind ==
                TrackAuthoringGeometryContinuityDiagnosticKind.PositionDiscontinuity);
        Assert.DoesNotContain(
            report.Diagnostics,
            diagnostic => diagnostic.Kind ==
                TrackAuthoringGeometryContinuityDiagnosticKind.TangentDiscontinuity);
    }

    [Fact]
    public void Analyze_StraightToArc_ReportsKnownCurvatureVectorDiscontinuity()
    {
        const double radius = 20.0;
        var definition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            new StraightSectionDefinition("straight", 4.0),
            new ConstantCurvatureSectionDefinition("arc", 5.0, radius)
        });

        TrackAuthoringGeometryContinuityReport report =
            TrackAuthoringGeometryContinuityDiagnostics.Analyze(definition);

        TrackAuthoringGeometryContinuityDiagnostic diagnostic = Assert.Single(report.Diagnostics);
        TrackAuthoringGeometryContinuityBoundary boundary = Assert.Single(report.Boundaries);
        Assert.Equal(
            TrackAuthoringGeometryContinuityDiagnosticKind.CurvatureVectorDiscontinuity,
            diagnostic.Kind);
        AssertVectorNear(Vector3d.Zero, boundary.PreviousEndCurvatureVector, 1e-10);
        Assert.InRange(
            System.Math.Abs(boundary.NextStartCurvatureVector.Length - (1.0 / radius)),
            0.0,
            1e-8);
        Assert.InRange(System.Math.Abs(diagnostic.MeasuredValue - (1.0 / radius)), 0.0, 1e-8);
    }

    [Fact]
    public void Analyze_ContinuousTransitionToArc_CurvatureVectorsAreWithinDefaultTolerance()
    {
        const double curvature = 0.05;
        var definition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            new CurvatureTransitionSectionDefinition("transition", 6.0, 0.0, curvature),
            new ConstantCurvatureSectionDefinition("arc", 5.0, radius: 1.0 / curvature)
        });

        TrackAuthoringGeometryContinuityReport report =
            TrackAuthoringGeometryContinuityDiagnostics.Analyze(definition);
        TrackAuthoringGeometryContinuityBoundary boundary = Assert.Single(report.Boundaries);

        Assert.InRange(
            boundary.CurvatureVectorDeltaMagnitude,
            0.0,
            TrackAuthoringGeometryContinuityTolerances.Default.CurvatureVectorTolerance);
        Assert.DoesNotContain(
            report.Diagnostics,
            diagnostic => diagnostic.Kind ==
                TrackAuthoringGeometryContinuityDiagnosticKind.CurvatureVectorDiscontinuity);
    }

    [Fact]
    public void Analyze_SpatialCurvatureVectorsAreFiniteThreeDimensionalValues()
    {
        TrackAuthoringGeometryContinuityReport report =
            TrackAuthoringGeometryContinuityDiagnostics.Analyze(
                CreateMixedSpatialDefinition(CreateArbitraryStartPose()));

        Assert.All(report.Boundaries, boundary =>
        {
            AssertFinite(boundary.PreviousEndCurvatureVector);
            AssertFinite(boundary.NextStartCurvatureVector);
            AssertFinite(boundary.CurvatureVectorDelta);
        });
        Assert.Contains(
            report.Boundaries,
            boundary => System.Math.Abs(boundary.PreviousEndCurvatureVector.Z) > 1e-6 ||
                        System.Math.Abs(boundary.NextStartCurvatureVector.Z) > 1e-6);
    }

    [Fact]
    public void Analyze_CompilationOverloadUsesSuppliedCompiledDocument()
    {
        TrackAuthoringCompilation compilation = CreateSyntheticDiscontinuousCompilation();

        TrackAuthoringGeometryContinuityReport report =
            TrackAuthoringGeometryContinuityDiagnostics.Analyze(compilation);

        Assert.Contains(
            report.Diagnostics,
            diagnostic => diagnostic.Kind ==
                TrackAuthoringGeometryContinuityDiagnosticKind.PositionDiscontinuity);
        Assert.Contains(
            report.Diagnostics,
            diagnostic => diagnostic.Kind ==
                TrackAuthoringGeometryContinuityDiagnosticKind.TangentDiscontinuity);
        TrackAuthoringGeometryContinuityBoundary boundary = Assert.Single(report.Boundaries);
        AssertVectorNear(new Vector3d(1.0, 0.0, 0.0), boundary.PositionGap);
        AssertNear(System.Math.PI * 0.5, boundary.TangentAngleRadians);
    }

    [Fact]
    public void Analyze_WrapsRollAndTreatsFullTurnsAsEquivalent()
    {
        var fullTurnDefinition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            new StraightSectionDefinition("before", 2.0, rollRadians: -0.25),
            new StraightSectionDefinition(
                "after",
                3.0,
                rollRadians: (2.0 * System.Math.PI) - 0.25)
        });
        var wrappedDefinition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            new StraightSectionDefinition("before", 2.0),
            new StraightSectionDefinition("after", 3.0, rollRadians: 1.5 * System.Math.PI)
        });

        TrackAuthoringGeometryContinuityReport fullTurnReport =
            TrackAuthoringGeometryContinuityDiagnostics.Analyze(fullTurnDefinition);
        TrackAuthoringGeometryContinuityReport wrappedReport =
            TrackAuthoringGeometryContinuityDiagnostics.Analyze(wrappedDefinition);

        Assert.Equal(0.0, Assert.Single(fullTurnReport.Boundaries).RollDeltaRadians, 12);
        Assert.DoesNotContain(
            fullTurnReport.Diagnostics,
            diagnostic => diagnostic.Kind ==
                TrackAuthoringGeometryContinuityDiagnosticKind.RollDiscontinuity);
        Assert.Equal(-0.5 * System.Math.PI, Assert.Single(wrappedReport.Boundaries).RollDeltaRadians, 12);
    }

    [Fact]
    public void Analyze_CompilationOverloadAppliesCustomTolerances()
    {
        TrackAuthoringCompilation compilation = CreateSyntheticDiscontinuousCompilation();
        TrackAuthoringGeometryContinuityBoundary measured = Assert.Single(
            TrackAuthoringGeometryContinuityDiagnostics.Analyze(
                compilation,
                new TrackAuthoringGeometryContinuityTolerances(0.0, 0.0, 0.0, 0.0))
            .Boundaries);
        var exactTolerances = new TrackAuthoringGeometryContinuityTolerances(
            measured.PositionGapMagnitude,
            measured.TangentAngleRadians,
            measured.CurvatureVectorDeltaMagnitude,
            measured.AbsoluteRollDeltaRadians);

        TrackAuthoringGeometryContinuityReport report =
            TrackAuthoringGeometryContinuityDiagnostics.Analyze(
                compilation,
                exactTolerances);

        Assert.Empty(report.Diagnostics);
        Assert.Equal(exactTolerances, report.Tolerances);
    }

    [Fact]
    public void Analyze_CompilationOverloadOrdersDiagnosticsByBoundaryThenGeometryKind()
    {
        TrackAuthoringGeometryContinuityReport report =
            TrackAuthoringGeometryContinuityDiagnostics.Analyze(
                CreateSyntheticDiscontinuousCompilation(),
                new TrackAuthoringGeometryContinuityTolerances(0.0, 0.0, 0.0, 0.0));

        Assert.Equal(
            new[]
            {
                TrackAuthoringGeometryContinuityDiagnosticKind.PositionDiscontinuity,
                TrackAuthoringGeometryContinuityDiagnosticKind.TangentDiscontinuity,
                TrackAuthoringGeometryContinuityDiagnosticKind.CurvatureVectorDiscontinuity,
                TrackAuthoringGeometryContinuityDiagnosticKind.RollDiscontinuity
            },
            report.Diagnostics.Select(diagnostic => diagnostic.Kind));
    }

    [Fact]
    public void Analyze_SingleSectionReturnsEmptyReadOnlyCollections()
    {
        var definition = new TrackAuthoringDefinition(new[]
        {
            new StraightSectionDefinition("only", 2.0)
        });

        TrackAuthoringGeometryContinuityReport report =
            TrackAuthoringGeometryContinuityDiagnostics.Analyze(definition);
        IList<TrackAuthoringGeometryContinuityBoundary> boundaries =
            Assert.IsAssignableFrom<IList<TrackAuthoringGeometryContinuityBoundary>>(report.Boundaries);
        IList<TrackAuthoringGeometryContinuityDiagnostic> diagnostics =
            Assert.IsAssignableFrom<IList<TrackAuthoringGeometryContinuityDiagnostic>>(report.Diagnostics);

        Assert.Empty(report.Boundaries);
        Assert.Empty(report.Diagnostics);
        Assert.False(report.HasDiagnostics);
        Assert.True(boundaries.IsReadOnly);
        Assert.True(diagnostics.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => boundaries.Add(default));
        Assert.Throws<NotSupportedException>(() => diagnostics.Add(default));
    }

    [Fact]
    public void Analyze_RepeatedAnalysisIsDeterministic()
    {
        TrackAuthoringDefinition definition = CreateMixedSpatialDefinition(CreateArbitraryStartPose());
        var tolerances = new TrackAuthoringGeometryContinuityTolerances(1e-8, 1e-8, 1e-5, 1e-8);

        TrackAuthoringGeometryContinuityReport first =
            TrackAuthoringGeometryContinuityDiagnostics.Analyze(definition, tolerances);
        TrackAuthoringGeometryContinuityReport second =
            TrackAuthoringGeometryContinuityDiagnostics.Analyze(definition, tolerances);

        Assert.Equal(first.Boundaries, second.Boundaries);
        Assert.Equal(first.Diagnostics, second.Diagnostics);
        Assert.Equal(first.Tolerances, second.Tolerances);
    }

    [Fact]
    public void Analyze_NullDefinitionThrows()
    {
        Assert.Throws<ArgumentNullException>(
            () => TrackAuthoringGeometryContinuityDiagnostics.Analyze(
                (TrackAuthoringDefinition)null!));
        Assert.Throws<ArgumentNullException>(() =>
            TrackAuthoringGeometryContinuityDiagnostics.Analyze(
                (TrackAuthoringDefinition)null!,
                TrackAuthoringGeometryContinuityTolerances.Default));
    }

    [Fact]
    public void Analyze_NullCompilationThrowsClearly()
    {
        ArgumentNullException defaultException = Assert.Throws<ArgumentNullException>(
            () => TrackAuthoringGeometryContinuityDiagnostics.Analyze(
                (TrackAuthoringCompilation)null!));
        ArgumentNullException customException = Assert.Throws<ArgumentNullException>(() =>
            TrackAuthoringGeometryContinuityDiagnostics.Analyze(
                (TrackAuthoringCompilation)null!,
                TrackAuthoringGeometryContinuityTolerances.Default));

        Assert.Equal("compilation", defaultException.ParamName);
        Assert.Equal("compilation", customException.ParamName);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Tolerances_RejectNegativeOrNonFiniteValues(double invalidTolerance)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TrackAuthoringGeometryContinuityTolerances(invalidTolerance, 0.0, 0.0, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TrackAuthoringGeometryContinuityTolerances(0.0, invalidTolerance, 0.0, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TrackAuthoringGeometryContinuityTolerances(0.0, 0.0, invalidTolerance, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TrackAuthoringGeometryContinuityTolerances(0.0, 0.0, 0.0, invalidTolerance));
    }

    [Fact]
    public void PublicApi_ExposesNoSplineGSharkUnityOrUiTypes()
    {
        Type[] types =
        {
            typeof(TrackAuthoringGeometryContinuityDiagnostics),
            typeof(TrackAuthoringGeometryContinuityReport),
            typeof(TrackAuthoringGeometryContinuityBoundary),
            typeof(TrackAuthoringGeometryContinuityDiagnostic),
            typeof(TrackAuthoringGeometryContinuityDiagnosticKind),
            typeof(TrackAuthoringGeometryContinuityTolerances),
            typeof(TrackAuthoringCompilation)
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

    private static TrackAuthoringCompilation CreateSyntheticDiscontinuousCompilation()
    {
        var definition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            new StraightSectionDefinition("previous", 1.0),
            new StraightSectionDefinition("next", 1.0, rollRadians: 0.3)
        });
        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(definition);
        compilation.Document.Segments[0] = new StraightSegment(
            1.0,
            "previous",
            spline: new SyntheticArcCurve(
                Vector3d.Zero,
                Vector3d.UnitX,
                Vector3d.UnitY,
                length: 1.0,
                curvature: 0.0));
        compilation.Document.Segments[1] = new CurvedSegment(
            1.0,
            "next",
            spline: new SyntheticArcCurve(
                new Vector3d(2.0, 0.0, 0.0),
                Vector3d.UnitY,
                new Vector3d(-1.0, 0.0, 0.0),
                length: 1.0,
                curvature: 0.2),
            rollRadians: 0.3);
        return compilation;
    }

    private static TrackAuthoringDefinition CreateMixedSpatialDefinition(TrackStartPose startPose)
    {
        SpatialSectionDefinition firstSpatial = CreateSpatial(
            "spatial-up",
            new[]
            {
                Vector3d.Zero,
                new Vector3d(2.0, 0.0, 0.0),
                new Vector3d(4.0, 1.4, 2.0),
                new Vector3d(6.0, 2.0, 3.5)
            });
        SpatialSectionDefinition secondSpatial = CreateSpatial(
            "spatial-down",
            new[]
            {
                Vector3d.Zero,
                new Vector3d(1.5, 0.0, 0.0),
                new Vector3d(3.0, -1.2, 1.0),
                new Vector3d(5.0, -2.0, 0.5)
            });

        return new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            new StraightSectionDefinition("entry", 4.0),
            firstSpatial,
            new ConstantCurvatureSectionDefinition("arc", 5.0, 12.0),
            secondSpatial,
            new StraightSectionDefinition("exit", 3.0)
        }, startPose);
    }

    private static SpatialSectionDefinition CreateSpatial(
        string id,
        IReadOnlyList<Vector3d> controlPoints)
    {
        const int degree = 3;
        var points = controlPoints.ToList();
        var weights = Enumerable.Repeat(1.0, points.Count).ToList();
        var curve = new GSharkNurbsCurveAdapter(points, weights, degree);
        TrackSamplingOptions samplingOptions = TrackSamplingOptions.Default;
        double length = new ArcLengthLUT(
            curve,
            samplingOptions.ArcLengthSamples,
            samplingOptions.ArcLengthTolerance).TotalLength;
        return new SpatialSectionDefinition(id, length, points, degree, weights);
    }

    private static TrackStartPose CreateArbitraryStartPose()
    {
        double inverseSqrtThree = 1.0 / System.Math.Sqrt(3.0);
        double inverseSqrtTwo = 1.0 / System.Math.Sqrt(2.0);
        double inverseSqrtSix = 1.0 / System.Math.Sqrt(6.0);

        return new TrackStartPose(
            new Vector3d(10.0, -3.0, 5.0),
            new Vector3d(inverseSqrtThree, inverseSqrtThree, inverseSqrtThree),
            new Vector3d(-inverseSqrtTwo, inverseSqrtTwo, 0.0),
            new Vector3d(-inverseSqrtSix, -inverseSqrtSix, 2.0 * inverseSqrtSix));
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
            typeName.StartsWith("GShark", StringComparison.Ordinal) ||
            typeName.StartsWith("Unity", StringComparison.Ordinal) ||
            typeName.StartsWith("Avalonia", StringComparison.Ordinal) ||
            typeName.StartsWith("OpenTK", StringComparison.Ordinal) ||
            typeName.StartsWith("Silk.NET", StringComparison.Ordinal),
            $"{owner.Name} exposes forbidden public type {typeName}.");
    }

    private static void AssertFinite(Vector3d vector)
    {
        Assert.True(double.IsFinite(vector.X));
        Assert.True(double.IsFinite(vector.Y));
        Assert.True(double.IsFinite(vector.Z));
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual)
    {
        AssertVectorNear(expected, actual, TestTolerance);
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual, double tolerance)
    {
        Assert.InRange(System.Math.Abs(expected.X - actual.X), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(expected.Y - actual.Y), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(expected.Z - actual.Z), 0.0, tolerance);
    }

    private static void AssertNear(double expected, double actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, TestTolerance);
    }

    private sealed class SyntheticArcCurve : IArcLengthCurve
    {
        private readonly Vector3d _start;
        private readonly Vector3d _startTangent;
        private readonly Vector3d _curvatureDirection;
        private readonly double _curvature;

        public SyntheticArcCurve(
            Vector3d start,
            Vector3d startTangent,
            Vector3d curvatureDirection,
            double length,
            double curvature)
        {
            _start = start;
            _startTangent = startTangent;
            _curvatureDirection = curvatureDirection;
            Length = length;
            _curvature = curvature;
        }

        public double Length { get; }

        public Vector3d Evaluate(double t)
        {
            return EvaluateByLength(Length * t);
        }

        public Vector3d Tangent(double t)
        {
            return TangentByLength(Length * t);
        }

        public Vector3d EvaluateByLength(double s)
        {
            if (_curvature == 0.0)
            {
                return _start + (_startTangent * s);
            }

            double heading = _curvature * s;
            return _start +
                   (_startTangent * (System.Math.Sin(heading) / _curvature)) +
                   (_curvatureDirection * ((1.0 - System.Math.Cos(heading)) / _curvature));
        }

        public Vector3d TangentByLength(double s)
        {
            double heading = _curvature * s;
            return (_startTangent * System.Math.Cos(heading)) +
                   (_curvatureDirection * System.Math.Sin(heading));
        }
    }
}
