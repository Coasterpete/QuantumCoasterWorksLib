using System;
using System.Collections.Generic;
using System.Linq;
using Quantum.IO.TrackLayout.V2;
using Quantum.Math;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class TrackLayoutPackageV2MapperTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void JsonImport_MinimalValidV2Package_MapsCompilesAndEvaluates()
    {
        string json = TrackLayoutPackageV2Json.Serialize(CreateMinimalDto());

        TrackLayoutPackageV2ImportResult result = TrackLayoutPackageV2Json.Import(json);

        Assert.True(result.Success);
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Definition);
        Assert.False(result.HeartlineOffset.HasValue);
        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(
            result.Definition!);
        var evaluator = new TrackEvaluator(compilation.Document);
        TrackFrame frame = evaluator.EvaluateFrameAtDistance(6.0);

        Assert.Equal(12.0, compilation.TotalLength);
        Assert.NotNull(compilation.Runtime);
        AssertVectorNear(new Vector3d(6.0, 0.0, 0.0), frame.Position);
        AssertVectorNear(Vector3d.UnitX, frame.Tangent);
    }

    [Fact]
    public void Import_RepresentativeSections_MapsSupportedAuthoringSectionKinds()
    {
        TrackLayoutPackageV2Dto dto = CreateRepresentativeDto();
        dto.Banking = null;

        TrackLayoutPackageV2ImportResult result = TrackLayoutPackageV2Mapper.Import(dto);

        Assert.True(result.Success);
        Assert.NotNull(result.Definition);
        IReadOnlyList<GeometricSectionDefinition> sections = result.Definition!.Sections;
        Assert.Equal(4, sections.Count);

        StraightSectionDefinition straight = Assert.IsType<StraightSectionDefinition>(sections[0]);
        Assert.Equal("entry", straight.Id);
        Assert.Equal(10.0, straight.Length);
        Assert.Equal(0.1, straight.RollRadians);

        ConstantCurvatureSectionDefinition arc =
            Assert.IsType<ConstantCurvatureSectionDefinition>(sections[1]);
        Assert.Equal("turn", arc.Id);
        Assert.Equal(-30.0, arc.Radius);

        CurvatureTransitionSectionDefinition transition =
            Assert.IsType<CurvatureTransitionSectionDefinition>(sections[2]);
        Assert.Equal("transition", transition.Id);
        Assert.Equal(0.02, transition.StartCurvature);
        Assert.Equal(-0.01, transition.EndCurvature);
        Assert.Equal(CurvatureTransitionInterpolationMode.Linear, transition.InterpolationMode);

        SpatialSectionDefinition spatial = Assert.IsType<SpatialSectionDefinition>(sections[3]);
        Assert.Equal("spatial", spatial.Id);
        Assert.Equal(3, spatial.Degree);
        Assert.Equal(4, spatial.ControlPoints.Count);
        AssertVectorNear(new Vector3d(6.0, 0.0, 0.0), spatial.ControlPoints[3]);
        Assert.Equal(new[] { 1.0, 1.0, 1.0, 1.0 }, spatial.Weights.ToArray());

        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(
            result.Definition);
        Assert.Equal(36.0, compilation.TotalLength);
        Assert.NotNull(compilation.Runtime);
    }

    [Fact]
    public void Import_BankingAndHeartline_MapsToBackendOptInRuntimeContracts()
    {
        TrackLayoutPackageV2Dto dto = CreateBankedHeartlineDto();

        TrackLayoutPackageV2ImportResult result = TrackLayoutPackageV2Mapper.Import(dto);

        Assert.True(result.Success);
        Assert.NotNull(result.Definition);
        Assert.NotNull(result.Definition!.Banking);
        Assert.True(result.HeartlineOffset.HasValue);

        Assert.Equal(2, result.Definition.Banking!.Keys.Count);
        Assert.Equal(BankingProfileInterpolationMode.Constant, result.Definition.Banking.Keys[0].InterpolationToNext);
        HeartlineOffset heartlineOffset = result.HeartlineOffset!.Value;
        AssertDoubleNear(1.5, heartlineOffset.NormalOffsetMeters);
        AssertDoubleNear(-0.25, heartlineOffset.LateralOffsetMeters);

        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(
            result.Definition);
        var evaluator = new TrackEvaluator(compilation.Document);
        HeartlineFrame heartlineFrame = Assert.Single(HeartlineSampler.SampleAtDistances(
            evaluator,
            compilation.BankingProfile,
            heartlineOffset,
            new[] { 5.0 }));

        AssertVectorNear(new Vector3d(5.0, 0.25, 1.5), heartlineFrame.Position);
    }

    [Fact]
    public void Import_InvalidV2Package_ReturnsValidationDiagnosticsWithoutDefinition()
    {
        TrackLayoutPackageV2Dto dto = CreateMinimalDto();
        dto.Sections = Array.Empty<TrackLayoutSectionV2Dto>();

        TrackLayoutPackageV2ImportResult result = TrackLayoutPackageV2Mapper.Import(dto);

        Assert.False(result.Success);
        Assert.Null(result.Definition);
        Assert.False(result.HeartlineOffset.HasValue);
        AssertDiagnostic(
            result.Diagnostics,
            TrackLayoutPackageV2ValidationCode.EmptySections,
            "sections");
    }

    [Fact]
    public void Import_DuplicateUnknownAndInvalidSections_AreRejectedByValidation()
    {
        TrackLayoutPackageV2Dto duplicate = CreateMinimalDto();
        duplicate.Sections = new[]
        {
            CreateStraightSection("entry", 2.0),
            CreateStraightSection("entry", 3.0)
        };

        TrackLayoutPackageV2Dto unknown = CreateMinimalDto();
        unknown.Sections = new[]
        {
            new TrackLayoutSectionV2Dto
            {
                Kind = "legacy",
                Id = "legacy",
                Length = 1.0,
                RollRadians = 0.0
            }
        };

        TrackLayoutPackageV2Dto invalid = CreateMinimalDto();
        invalid.Sections = new[]
        {
            CreateStraightSection("invalid-length", 0.0)
        };

        TrackLayoutPackageV2ImportResult duplicateResult = TrackLayoutPackageV2Mapper.Import(duplicate);
        TrackLayoutPackageV2ImportResult unknownResult = TrackLayoutPackageV2Mapper.Import(unknown);
        TrackLayoutPackageV2ImportResult invalidResult = TrackLayoutPackageV2Mapper.Import(invalid);

        AssertRejectedByValidation(
            duplicateResult,
            TrackLayoutPackageV2ValidationCode.DuplicateSectionId,
            "sections[1].id");
        AssertRejectedByValidation(
            unknownResult,
            TrackLayoutPackageV2ValidationCode.UnknownSectionKind,
            "sections[0].kind");
        AssertRejectedByValidation(
            invalidResult,
            TrackLayoutPackageV2ValidationCode.NonPositiveLength,
            "sections[0].length");
    }

    [Fact]
    public void JsonImport_MalformedJson_ReturnsDiagnosticWithoutDefinition()
    {
        TrackLayoutPackageV2ImportResult result = TrackLayoutPackageV2Json.Import("{");

        Assert.False(result.Success);
        Assert.Null(result.Definition);
        Assert.False(result.HeartlineOffset.HasValue);
        TrackLayoutPackageV2ValidationDiagnostic diagnostic = AssertDiagnostic(
            result.Diagnostics,
            TrackLayoutPackageV2ValidationCode.MalformedJson,
            "json");
        Assert.Contains(
            "Failed to deserialize TrackLayoutPackageV2Dto",
            diagnostic.Message,
            StringComparison.Ordinal);
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
                CreateStraightSection("entry", 12.0)
            },
            Banking = null,
            Heartline = null
        };
    }

    private static TrackLayoutPackageV2Dto CreateRepresentativeDto()
    {
        return new TrackLayoutPackageV2Dto
        {
            Metadata = new TrackLayoutMetadataV2Dto
            {
                Units = "meters",
                SourceName = "representative",
                LayoutId = "layout.m148.import-representative"
            },
            StartPose = new TrackStartPoseV2Dto
            {
                Position = new TrackLayoutVector3dV2Dto { X = 1.0, Y = 2.0, Z = 3.0 },
                Tangent = new TrackLayoutVector3dV2Dto { X = 0.0, Y = 1.0, Z = 0.0 },
                Normal = new TrackLayoutVector3dV2Dto { X = 0.0, Y = 0.0, Z = 1.0 },
                Binormal = new TrackLayoutVector3dV2Dto { X = 1.0, Y = 0.0, Z = 0.0 }
            },
            Sections = new[]
            {
                new TrackLayoutSectionV2Dto
                {
                    Kind = TrackLayoutPackageV2Vocabulary.StraightSectionKind,
                    Id = "entry",
                    Length = 10.0,
                    RollRadians = 0.1
                },
                new TrackLayoutSectionV2Dto
                {
                    Kind = TrackLayoutPackageV2Vocabulary.ConstantCurvatureSectionKind,
                    Id = "turn",
                    Length = 12.0,
                    RollRadians = -0.2,
                    Radius = -30.0
                },
                new TrackLayoutSectionV2Dto
                {
                    Kind = TrackLayoutPackageV2Vocabulary.CurvatureTransitionSectionKind,
                    Id = "transition",
                    Length = 8.0,
                    RollRadians = 0.05,
                    StartCurvature = 0.02,
                    EndCurvature = -0.01,
                    InterpolationMode = TrackLayoutPackageV2Vocabulary.CurvatureInterpolationLinear
                },
                new TrackLayoutSectionV2Dto
                {
                    Kind = TrackLayoutPackageV2Vocabulary.SpatialSectionKind,
                    Id = "spatial",
                    Length = 6.0,
                    RollRadians = 0.25,
                    Degree = 3,
                    ControlPoints = new[]
                    {
                        new TrackLayoutVector3dV2Dto { X = 0.0, Y = 0.0, Z = 0.0 },
                        new TrackLayoutVector3dV2Dto { X = 2.0, Y = 0.0, Z = 0.0 },
                        new TrackLayoutVector3dV2Dto { X = 4.0, Y = 0.0, Z = 0.0 },
                        new TrackLayoutVector3dV2Dto { X = 6.0, Y = 0.0, Z = 0.0 }
                    },
                    Weights = new[] { 1.0, 1.0, 1.0, 1.0 }
                }
            },
            Banking = new TrackBankingV2Dto
            {
                Keys = new[]
                {
                    new TrackBankingKeyV2Dto
                    {
                        Distance = 0.0,
                        RollRadians = 0.0,
                        InterpolationToNext = TrackLayoutPackageV2Vocabulary.BankingInterpolationLinear
                    },
                    new TrackBankingKeyV2Dto
                    {
                        Distance = 10.0,
                        RollRadians = 0.2,
                        InterpolationToNext = TrackLayoutPackageV2Vocabulary.BankingInterpolationSmoothStep
                    },
                    new TrackBankingKeyV2Dto
                    {
                        Distance = 22.0,
                        RollRadians = -0.35,
                        InterpolationToNext = TrackLayoutPackageV2Vocabulary.BankingInterpolationConstant
                    },
                    new TrackBankingKeyV2Dto
                    {
                        Distance = 36.0,
                        RollRadians = 0.1,
                        InterpolationToNext = TrackLayoutPackageV2Vocabulary.BankingInterpolationConstant
                    }
                }
            },
            Heartline = null
        };
    }

    private static TrackLayoutPackageV2Dto CreateBankedHeartlineDto()
    {
        return new TrackLayoutPackageV2Dto
        {
            Metadata = new TrackLayoutMetadataV2Dto
            {
                Units = "meters",
                SourceName = "Banked heartline V2 import",
                LayoutId = "layout.m148.banked-heartline"
            },
            Sections = new[]
            {
                CreateStraightSection("entry", 10.0)
            },
            Banking = new TrackBankingV2Dto
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
                        Distance = 10.0,
                        RollRadians = System.Math.PI * 0.5,
                        InterpolationToNext = TrackLayoutPackageV2Vocabulary.BankingInterpolationConstant
                    }
                }
            },
            Heartline = new TrackHeartlineV2Dto
            {
                Kind = TrackLayoutPackageV2Vocabulary.HeartlineKindConstantOffset,
                DistanceDomain = TrackLayoutPackageV2Vocabulary.HeartlineDistanceDomainCenterlineStation,
                AxisSource = TrackLayoutPackageV2Vocabulary.HeartlineAxisSourceSampledFrame,
                NormalOffset = 1.5,
                LateralOffset = -0.25
            }
        };
    }

    private static TrackLayoutSectionV2Dto CreateStraightSection(string id, double length)
    {
        return new TrackLayoutSectionV2Dto
        {
            Kind = TrackLayoutPackageV2Vocabulary.StraightSectionKind,
            Id = id,
            Length = length,
            RollRadians = 0.0
        };
    }

    private static void AssertRejectedByValidation(
        TrackLayoutPackageV2ImportResult result,
        TrackLayoutPackageV2ValidationCode code,
        string path)
    {
        Assert.False(result.Success);
        Assert.Null(result.Definition);
        Assert.False(result.HeartlineOffset.HasValue);
        AssertDiagnostic(result.Diagnostics, code, path);
    }

    private static TrackLayoutPackageV2ValidationDiagnostic AssertDiagnostic(
        IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> diagnostics,
        TrackLayoutPackageV2ValidationCode code,
        string path)
    {
        TrackLayoutPackageV2ValidationDiagnostic? diagnostic = diagnostics.FirstOrDefault(
            d => d.Code == code && d.Path == path);

        Assert.NotNull(diagnostic);
        return diagnostic!;
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual)
    {
        AssertDoubleNear(expected.X, actual.X);
        AssertDoubleNear(expected.Y, actual.Y);
        AssertDoubleNear(expected.Z, actual.Z);
    }

    private static void AssertDoubleNear(double expected, double actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, Tolerance);
    }
}
