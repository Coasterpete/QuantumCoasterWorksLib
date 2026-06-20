using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Quantum.Debug;
using Quantum.IO.DebugViewport.V1;
using Quantum.IO.TrackLayout.V2;
using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;

namespace Quantum.Tests;

public sealed class DebugViewportSnapshotV1FromTrackLayoutPackageV2CommandTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void Export_MinimalValidV2Package_ProducesValidSnapshot()
    {
        DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportResult result =
            DebugViewportSnapshotV1FromTrackLayoutPackageV2Command.Export(CreateMinimalDto());

        Assert.True(result.Success, FormatDiagnostics(result));
        Assert.NotNull(result.Snapshot);
        DebugViewportSnapshotV1Dto snapshot = result.Snapshot!;
        AssertValidSnapshot(snapshot);

        Assert.Equal(DebugViewportSnapshotV1Dto.ContractName, snapshot.Contract);
        Assert.Equal(DebugViewportSnapshotV1Dto.ContractVersion, snapshot.Version);
        Assert.Equal("meters", snapshot.Metadata.Units);
        Assert.Equal("Minimal V2 layout", snapshot.Metadata.SourceFixtureName);
        Assert.Equal(5, snapshot.Metadata.SampleCount);
        Assert.Equal(5, snapshot.CenterlinePoints.Length);
        Assert.Equal(5, snapshot.Frames.Length);
        Assert.Equal(3, snapshot.Lines.Length);
        Assert.Equal(2, snapshot.Boxes.Length);
        Assert.NotNull(snapshot.TrainPose);
        Assert.Equal(2, snapshot.TrainPose!.Cars.Length);
        Assert.Equal(DebugViewportSnapshotV1Vocabulary.TrainBodyRole, snapshot.Boxes[0].Role);
        AssertDoubleNear(0.0, snapshot.CenterlinePoints[0].Distance);
        AssertDoubleNear(12.0, snapshot.CenterlinePoints[^1].Distance);
        AssertDoubleNear(12.0, snapshot.CenterlinePoints[^1].Position.X);
    }

    [Fact]
    public void Run_WithExplicitOutputPath_WritesSnapshotFromV2Json()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string inputPath = Path.Combine(tempDirectory, "minimal-v2.json");
        string outputPath = Path.Combine(tempDirectory, "minimal-v2.snapshot.json");
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        try
        {
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllText(inputPath, TrackLayoutPackageV2Json.Serialize(CreateMinimalDto(), indented: true));

            int exitCode = DebugViewportSnapshotV1FromTrackLayoutPackageV2Command.Run(
                inputPath,
                outputPath,
                writer);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));
            DebugViewportSnapshotV1Dto snapshot =
                DebugViewportSnapshotV1Json.Deserialize(File.ReadAllText(outputPath));
            AssertValidSnapshot(snapshot);
            Assert.Contains("Wrote TrackLayoutPackageV2 DebugViewportSnapshotV1 snapshot", writer.ToString());
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void Export_RepresentativeSectionKinds_AppearInSampledOutput()
    {
        DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportResult result =
            DebugViewportSnapshotV1FromTrackLayoutPackageV2Command.Export(CreateRepresentativeSectionsDto());

        Assert.True(result.Success, FormatDiagnostics(result));
        Assert.NotNull(result.Snapshot);
        DebugViewportSnapshotV1Dto snapshot = result.Snapshot!;
        AssertValidSnapshot(snapshot);

        Assert.Contains(snapshot.CenterlinePoints, point => IsNear(point.Distance, 6.0));
        Assert.Contains(snapshot.CenterlinePoints, point => IsNear(point.Distance, 18.0));
        Assert.Contains(snapshot.CenterlinePoints, point => IsNear(point.Distance, 24.0));
        Assert.Contains(snapshot.CenterlinePoints, point => IsNear(point.Distance, 33.0));
        Assert.True(snapshot.CenterlinePoints.Max(point => point.Position.Y) > 0.25);
        Assert.True(snapshot.CenterlinePoints.Max(point => System.Math.Abs(point.Position.Z)) > 0.25);
        Assert.Equal(3, snapshot.Lines.Length);
        Assert.NotNull(snapshot.TrainPose);
    }

    [Fact]
    public void Export_BankedHeartline_UsesProfileBackedBoxesAndDiagnosticLines()
    {
        TrackLayoutPackageV2Dto dto = CreateMinimalDto();
        dto.Metadata.SourceName = "Banked heartline V2 snapshot";
        dto.Banking = new TrackBankingV2Dto
        {
            Keys = new[]
            {
                new TrackBankingKeyV2Dto
                {
                    Distance = 0.0,
                    RollRadians = System.Math.PI * 0.5,
                    InterpolationToNext = TrackLayoutPackageV2Vocabulary.BankingInterpolationConstant
                },
                new TrackBankingKeyV2Dto
                {
                    Distance = 12.0,
                    RollRadians = System.Math.PI * 0.5,
                    InterpolationToNext = TrackLayoutPackageV2Vocabulary.BankingInterpolationConstant
                }
            }
        };
        dto.Heartline = new TrackHeartlineV2Dto
        {
            Kind = TrackLayoutPackageV2Vocabulary.HeartlineKindConstantOffset,
            DistanceDomain = TrackLayoutPackageV2Vocabulary.HeartlineDistanceDomainCenterlineStation,
            AxisSource = TrackLayoutPackageV2Vocabulary.HeartlineAxisSourceSampledFrame,
            NormalOffset = 1.0,
            LateralOffset = 0.0
        };

        DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportResult result =
            DebugViewportSnapshotV1FromTrackLayoutPackageV2Command.Export(dto);

        Assert.True(result.Success, FormatDiagnostics(result));
        Assert.NotNull(result.Snapshot);
        DebugViewportSnapshotV1Dto snapshot = result.Snapshot!;
        AssertValidSnapshot(snapshot);

        Assert.Contains(snapshot.Lines, line => line.Kind == DebugViewportSnapshotV1Vocabulary.DiagnosticLineKind);
        Assert.All(snapshot.Boxes, box => Assert.Equal(
            DebugViewportSnapshotV1Vocabulary.TrainBodyBankingProfileRole,
            box.Role));
    }

    [Fact]
    public void Export_InvalidV2Package_ReturnsValidationDiagnostics()
    {
        TrackLayoutPackageV2Dto dto = CreateMinimalDto();
        dto.Sections = Array.Empty<TrackLayoutSectionV2Dto>();

        DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportResult result =
            DebugViewportSnapshotV1FromTrackLayoutPackageV2Command.Export(dto);

        Assert.False(result.Success);
        Assert.Null(result.Snapshot);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "TrackLayoutPackageV2.EmptySections" &&
            diagnostic.Path == "sections");
    }

    [Fact]
    public void Export_ValidV2PackageWithUnsupportedRuntimeGeometry_FailsPredictably()
    {
        TrackLayoutPackageV2Dto dto = CreateMinimalDto();
        dto.Metadata.SourceName = "Declared spatial length mismatch";
        dto.Sections = new[]
        {
            new TrackLayoutSectionV2Dto
            {
                Kind = TrackLayoutPackageV2Vocabulary.SpatialSectionKind,
                Id = "spatial-length-mismatch",
                Length = 6.0,
                RollRadians = 0.0,
                Degree = 3,
                ControlPoints = new[]
                {
                    Vector(0.0, 0.0, 0.0),
                    Vector(1.0, 0.0, 0.0),
                    Vector(2.0, 0.0, 0.0),
                    Vector(3.0, 0.0, 0.0)
                },
                Weights = new[] { 1.0, 1.0, 1.0, 1.0 }
            }
        };

        Assert.Empty(TrackLayoutPackageV2Validator.Validate(dto));

        DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportResult result =
            DebugViewportSnapshotV1FromTrackLayoutPackageV2Command.Export(dto);

        Assert.False(result.Success);
        Assert.Null(result.Snapshot);
        DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportDiagnostic diagnostic =
            Assert.Single(result.Diagnostics);
        Assert.Equal("TrackLayoutPackageV2.CompilationFailed", diagnostic.Code);
        Assert.Equal("track", diagnostic.Path);
        Assert.Contains("does not match measured geometric length", diagnostic.Message);
    }

    private static TrackLayoutPackageV2Dto CreateMinimalDto()
    {
        return new TrackLayoutPackageV2Dto
        {
            Metadata = new TrackLayoutMetadataV2Dto
            {
                Units = "meters",
                SourceName = "Minimal V2 layout",
                LayoutId = null
            },
            Sections = new[]
            {
                new TrackLayoutSectionV2Dto
                {
                    Kind = TrackLayoutPackageV2Vocabulary.StraightSectionKind,
                    Id = "entry",
                    Length = 12.0,
                    RollRadians = 0.0
                }
            },
            Banking = null,
            Heartline = null
        };
    }

    private static TrackLayoutPackageV2Dto CreateRepresentativeSectionsDto()
    {
        const double spatialLength = 9.0;
        TrackLayoutVector3dV2Dto[] spatialControlPoints = CreateNormalizedSpatialControlPoints(
            spatialLength);

        return new TrackLayoutPackageV2Dto
        {
            Metadata = new TrackLayoutMetadataV2Dto
            {
                Units = "meters",
                SourceName = "Representative V2 section snapshot",
                LayoutId = "layout.m148.pr2.representative"
            },
            Sections = new[]
            {
                new TrackLayoutSectionV2Dto
                {
                    Kind = TrackLayoutPackageV2Vocabulary.StraightSectionKind,
                    Id = "entry",
                    Length = 6.0,
                    RollRadians = 0.0
                },
                new TrackLayoutSectionV2Dto
                {
                    Kind = TrackLayoutPackageV2Vocabulary.ConstantCurvatureSectionKind,
                    Id = "arc",
                    Length = 12.0,
                    RollRadians = 0.0,
                    Radius = 20.0
                },
                new TrackLayoutSectionV2Dto
                {
                    Kind = TrackLayoutPackageV2Vocabulary.CurvatureTransitionSectionKind,
                    Id = "transition",
                    Length = 6.0,
                    RollRadians = 0.0,
                    StartCurvature = 1.0 / 20.0,
                    EndCurvature = 0.0,
                    InterpolationMode = TrackLayoutPackageV2Vocabulary.CurvatureInterpolationLinear
                },
                new TrackLayoutSectionV2Dto
                {
                    Kind = TrackLayoutPackageV2Vocabulary.SpatialSectionKind,
                    Id = "spatial",
                    Length = spatialLength,
                    RollRadians = 0.0,
                    Degree = 3,
                    ControlPoints = spatialControlPoints,
                    Weights = Enumerable.Repeat(1.0, spatialControlPoints.Length).ToArray()
                }
            },
            Banking = null,
            Heartline = null
        };
    }

    private static TrackLayoutVector3dV2Dto[] CreateNormalizedSpatialControlPoints(double targetLength)
    {
        Vector3d[] points =
        {
            Vector3d.Zero,
            new Vector3d(2.0, 0.0, 0.0),
            new Vector3d(4.0, 0.0, 0.0),
            new Vector3d(6.0, 1.5, 1.2),
            new Vector3d(8.0, 2.0, 3.0)
        };

        for (int i = 0; i < 3; i++)
        {
            double measuredLength = MeasureSpatialLength(points);
            double scale = targetLength / measuredLength;
            points = points.Select(point => point * scale).ToArray();
        }

        return points.Select(point => Vector(point.X, point.Y, point.Z)).ToArray();
    }

    private static double MeasureSpatialLength(Vector3d[] points)
    {
        var curve = new GSharkNurbsCurveAdapter(
            points.ToList(),
            Enumerable.Repeat(1.0, points.Length).ToList(),
            degree: 3);
        return new ArcLengthLUT(
            curve,
            TrackSamplingOptions.DefaultArcLengthSamples,
            TrackSamplingOptions.DefaultArcLengthTolerance).TotalLength;
    }

    private static TrackLayoutVector3dV2Dto Vector(double x, double y, double z)
    {
        return new TrackLayoutVector3dV2Dto
        {
            X = x,
            Y = y,
            Z = z
        };
    }

    private static void AssertValidSnapshot(DebugViewportSnapshotV1Dto snapshot)
    {
        bool isValid = DebugViewportSnapshotV1Validator.TryValidate(
            snapshot,
            out IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics);

        Assert.True(
            isValid,
            string.Join(Environment.NewLine, diagnostics.Select(d => $"{d.Code} {d.Path}: {d.Message}")));
        Assert.Empty(diagnostics);
    }

    private static bool IsNear(double actual, double expected)
    {
        return System.Math.Abs(actual - expected) <= Tolerance;
    }

    private static void AssertDoubleNear(double expected, double actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, Tolerance);
    }

    private static string FormatDiagnostics(
        DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportResult result)
    {
        return string.Join(
            Environment.NewLine,
            result.Diagnostics.Select(diagnostic =>
                diagnostic.Code + " " + diagnostic.Path + ": " + diagnostic.Message));
    }

    private static string CreateTempDirectoryPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "QuantumCoasterWorks.DebugViewportSnapshotV1FromTrackLayoutPackageV2CommandTests",
            Guid.NewGuid().ToString("N"));
    }

    private static void DeleteDirectoryIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
