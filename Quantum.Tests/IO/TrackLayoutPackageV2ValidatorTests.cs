using System;
using System.Collections.Generic;
using System.Linq;
using Quantum.IO.TrackLayout.V2;

namespace Quantum.Tests;

public sealed class TrackLayoutPackageV2ValidatorTests
{
    [Fact]
    public void DefaultDto_IsInvalidOnlyBecauseSectionsIsEmpty()
    {
        var dto = new TrackLayoutPackageV2Dto();

        bool isValid = TrackLayoutPackageV2Validator.TryValidate(
            dto,
            out IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> diagnostics);

        Assert.False(isValid);
        TrackLayoutPackageV2ValidationDiagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(TrackLayoutPackageV2ValidationCode.EmptySections, diagnostic.Code);
        Assert.Equal("sections", diagnostic.Path);
    }

    [Fact]
    public void MinimalValidDto_ValidatesCleanly()
    {
        TrackLayoutPackageV2Dto dto = CreateMinimalDto();

        AssertValid(dto);
    }

    [Fact]
    public void ConstantHeartlineRepresentativeDto_ValidatesCleanly()
    {
        TrackLayoutPackageV2Dto dto = CreateConstantHeartlineDto();

        AssertValid(dto);
    }

    [Fact]
    public void ValidationCodeValues_AreStable()
    {
        Assert.Equal(0, (int)TrackLayoutPackageV2ValidationCode.InvalidContract);
        Assert.Equal(24, (int)TrackLayoutPackageV2ValidationCode.InvalidBankingDomain);
        Assert.Equal(25, (int)TrackLayoutPackageV2ValidationCode.InvalidHeartlineKind);
        Assert.Equal(26, (int)TrackLayoutPackageV2ValidationCode.InvalidHeartlineDistanceDomain);
        Assert.Equal(27, (int)TrackLayoutPackageV2ValidationCode.InvalidHeartlineAxisSource);
        Assert.Equal(28, (int)TrackLayoutPackageV2ValidationCode.MalformedJson);
        Assert.Equal(29, (int)TrackLayoutPackageV2ValidationCode.MappingFailed);
    }

    [Fact]
    public void Validator_CatchesMissingMetadataAndBlankUnits()
    {
        TrackLayoutPackageV2Dto missingMetadata = CreateMinimalDto();
        missingMetadata.Metadata = null!;

        TrackLayoutPackageV2ValidationDiagnostic metadataDiagnostic = AssertDiagnostic(
            TrackLayoutPackageV2Validator.Validate(missingMetadata),
            TrackLayoutPackageV2ValidationCode.MissingMetadata,
            "metadata");

        Assert.Equal("Metadata object is required.", metadataDiagnostic.Message);

        TrackLayoutPackageV2Dto blankUnits = CreateMinimalDto();
        blankUnits.Metadata.Units = " ";

        AssertDiagnostic(
            TrackLayoutPackageV2Validator.Validate(blankUnits),
            TrackLayoutPackageV2ValidationCode.MissingRequiredField,
            "metadata.units");
    }

    [Fact]
    public void Validator_CatchesWrongContractAndVersion()
    {
        TrackLayoutPackageV2Dto dto = CreateMinimalDto();
        dto.Contract = "quantum.track_layout_package.v1";
        dto.Version = 1;

        IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> diagnostics =
            TrackLayoutPackageV2Validator.Validate(dto);

        Assert.Equal(2, diagnostics.Count);
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.InvalidContract,
            "contract");
        TrackLayoutPackageV2ValidationDiagnostic version = AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.InvalidVersion,
            "version");
        Assert.Equal(1.0, version.Value);
        Assert.Equal(2.0, version.Expected);
    }

    [Fact]
    public void Validator_CatchesMissingAndEmptySections()
    {
        TrackLayoutPackageV2Dto missing = CreateMinimalDto();
        missing.Sections = null!;

        AssertDiagnostic(
            TrackLayoutPackageV2Validator.Validate(missing),
            TrackLayoutPackageV2ValidationCode.MissingSections,
            "sections");

        TrackLayoutPackageV2Dto empty = CreateMinimalDto();
        empty.Sections = Array.Empty<TrackLayoutSectionV2Dto>();

        AssertDiagnostic(
            TrackLayoutPackageV2Validator.Validate(empty),
            TrackLayoutPackageV2ValidationCode.EmptySections,
            "sections");
    }

    [Fact]
    public void Validator_CatchesNullSectionAndBlankSectionIdentity()
    {
        TrackLayoutPackageV2Dto dto = CreateMinimalDto();
        dto.Sections = new[]
        {
            null!,
            new TrackLayoutSectionV2Dto
            {
                Kind = " ",
                Id = " ",
                Length = 1.0,
                RollRadians = 0.0
            }
        };

        IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> diagnostics =
            TrackLayoutPackageV2Validator.Validate(dto);

        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.MissingObject,
            "sections[0]");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.MissingRequiredField,
            "sections[1].kind");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.MissingSectionId,
            "sections[1].id");
    }

    [Fact]
    public void Validator_CatchesDuplicateSectionIdsUsingOrdinalComparison()
    {
        TrackLayoutPackageV2Dto duplicate = CreateMinimalDto();
        duplicate.Sections = new[]
        {
            CreateStraightSection("entry", 2.0),
            CreateStraightSection("entry", 3.0)
        };

        AssertDiagnostic(
            TrackLayoutPackageV2Validator.Validate(duplicate),
            TrackLayoutPackageV2ValidationCode.DuplicateSectionId,
            "sections[1].id");

        TrackLayoutPackageV2Dto ordinalDistinct = CreateMinimalDto();
        ordinalDistinct.Sections = new[]
        {
            CreateStraightSection("entry", 2.0),
            CreateStraightSection("ENTRY", 3.0)
        };

        AssertValid(ordinalDistinct);
    }

    [Fact]
    public void Validator_CatchesInvalidStartPose()
    {
        AssertDiagnostic(
            ValidateStartPose(p => p.Position.X = double.NaN),
            TrackLayoutPackageV2ValidationCode.NonFiniteNumber,
            "startPose.position.x");
        AssertDiagnostic(
            ValidateStartPose(p => p.Tangent = new TrackLayoutVector3dV2Dto()),
            TrackLayoutPackageV2ValidationCode.InvalidStartPoseBasis,
            "startPose.tangent");
        AssertDiagnostic(
            ValidateStartPose(p => p.Tangent = new TrackLayoutVector3dV2Dto { X = 2.0 }),
            TrackLayoutPackageV2ValidationCode.InvalidStartPoseBasis,
            "startPose.tangent");
        AssertDiagnostic(
            ValidateStartPose(
                p =>
                {
                    p.Tangent = new TrackLayoutVector3dV2Dto { X = 1.0 };
                    p.Normal = new TrackLayoutVector3dV2Dto { X = 1.0 };
                    p.Binormal = new TrackLayoutVector3dV2Dto { Z = 1.0 };
                }),
            TrackLayoutPackageV2ValidationCode.InvalidStartPoseBasis,
            "startPose.tangentNormalDot");
        AssertDiagnostic(
            ValidateStartPose(p => p.Binormal = new TrackLayoutVector3dV2Dto { Z = -1.0 }),
            TrackLayoutPackageV2ValidationCode.InvalidStartPoseBasis,
            "startPose.handedness");
    }

    [Fact]
    public void Validator_CatchesInvalidSectionCommonFields()
    {
        TrackLayoutPackageV2Dto dto = CreateMinimalDto();
        dto.Sections = new[]
        {
            CreateStraightSection("nonfinite-length", double.NaN),
            CreateStraightSection("zero-length", 0.0),
            new TrackLayoutSectionV2Dto
            {
                Kind = TrackLayoutPackageV2Vocabulary.StraightSectionKind,
                Id = "nonfinite-roll",
                Length = 1.0,
                RollRadians = double.PositiveInfinity
            }
        };

        IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> diagnostics =
            TrackLayoutPackageV2Validator.Validate(dto);

        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.NonFiniteNumber,
            "sections[0].length");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.NonPositiveLength,
            "sections[1].length");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.NonFiniteNumber,
            "sections[2].rollRadians");
    }

    [Fact]
    public void Validator_CatchesInvalidPerKindSectionFields()
    {
        TrackLayoutPackageV2Dto dto = CreateRepresentativeDto();
        dto.Banking = null;
        dto.Sections[0].Radius = 10.0;
        dto.Sections[1].Radius = 0.0;
        dto.Sections[2].StartCurvature = double.NaN;
        dto.Sections[2].EndCurvature = null;
        dto.Sections[2].InterpolationMode = "smoothStep";
        dto.Sections = dto.Sections.Concat(new[]
        {
            new TrackLayoutSectionV2Dto
            {
                Kind = "legacy",
                Id = "legacy",
                Length = 1.0,
                RollRadians = 0.0
            },
            new TrackLayoutSectionV2Dto
            {
                Kind = TrackLayoutPackageV2Vocabulary.ConstantCurvatureSectionKind,
                Id = "missing-radius",
                Length = 1.0,
                RollRadians = 0.0
            }
        }).ToArray();

        IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> diagnostics =
            TrackLayoutPackageV2Validator.Validate(dto);

        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.UnexpectedSectionField,
            "sections[0].radius");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.InvalidRadius,
            "sections[1].radius");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.NonFiniteNumber,
            "sections[2].startCurvature");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.MissingRequiredField,
            "sections[2].endCurvature");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.InvalidCurvatureInterpolation,
            "sections[2].interpolationMode");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.UnknownSectionKind,
            "sections[4].kind");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.MissingRequiredField,
            "sections[5].radius");
    }

    [Fact]
    public void Validator_CatchesInvalidSpatialDegreeControlPointsWeightsAndStartContract()
    {
        TrackLayoutPackageV2Dto dto = CreateMinimalDto();
        dto.Sections = new[]
        {
            new TrackLayoutSectionV2Dto
            {
                Kind = TrackLayoutPackageV2Vocabulary.SpatialSectionKind,
                Id = "spatial-invalid-shape",
                Length = 1.0,
                RollRadians = 0.0,
                Degree = 0,
                ControlPoints = Array.Empty<TrackLayoutVector3dV2Dto>(),
                Weights = new[] { 0.0 }
            },
            new TrackLayoutSectionV2Dto
            {
                Kind = TrackLayoutPackageV2Vocabulary.SpatialSectionKind,
                Id = "spatial-invalid-start",
                Length = 1.0,
                RollRadians = 0.0,
                Degree = 1,
                ControlPoints = new[]
                {
                    new TrackLayoutVector3dV2Dto(),
                    new TrackLayoutVector3dV2Dto { Y = 1.0 }
                },
                Weights = new[] { 1.0, 1.0 }
            },
            new TrackLayoutSectionV2Dto
            {
                Kind = TrackLayoutPackageV2Vocabulary.SpatialSectionKind,
                Id = "spatial-nonfinite-control-point",
                Length = 1.0,
                RollRadians = 0.0,
                Degree = 1,
                ControlPoints = new[]
                {
                    new TrackLayoutVector3dV2Dto(),
                    new TrackLayoutVector3dV2Dto { X = 1.0, Y = double.NaN }
                },
                Weights = new[] { 1.0, 1.0 }
            }
        };

        IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> diagnostics =
            TrackLayoutPackageV2Validator.Validate(dto);

        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.InvalidSpatialDegree,
            "sections[0].degree");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.InvalidSpatialControlPoints,
            "sections[0].controlPoints");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.InvalidSpatialWeights,
            "sections[0].weights");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.InvalidSpatialWeights,
            "sections[0].weights[0]");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.InvalidSpatialStartContract,
            "sections[1].controlPoints[1]");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.NonFiniteNumber,
            "sections[2].controlPoints[1].y");
    }

    [Fact]
    public void Validator_CatchesInvalidBankingCountOrderDomainAndInterpolation()
    {
        TrackLayoutPackageV2Dto shortBanking = CreateMinimalDto();
        shortBanking.Banking = new TrackBankingV2Dto
        {
            Keys = new[]
            {
                new TrackBankingKeyV2Dto
                {
                    Distance = 0.0,
                    RollRadians = 0.0,
                    InterpolationToNext = TrackLayoutPackageV2Vocabulary.BankingInterpolationConstant
                }
            }
        };

        AssertDiagnostic(
            TrackLayoutPackageV2Validator.Validate(shortBanking),
            TrackLayoutPackageV2ValidationCode.InvalidBankingKeyCount,
            "banking.keys");

        TrackLayoutPackageV2Dto invalid = CreateRepresentativeDto();
        invalid.Banking!.Keys[0].Distance = 1.0;
        invalid.Banking.Keys[1].Distance = 0.5;
        invalid.Banking.Keys[2].RollRadians = double.NaN;
        invalid.Banking.Keys[2].InterpolationToNext = "legacyCubic";
        invalid.Banking.Keys[3].Distance = 35.0;

        IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> diagnostics =
            TrackLayoutPackageV2Validator.Validate(invalid);

        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.InvalidBankingDomain,
            "banking.keys[0].distance");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.InvalidBankingKeyOrder,
            "banking.keys[1].distance");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.NonFiniteNumber,
            "banking.keys[2].rollRadians");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.InvalidBankingInterpolation,
            "banking.keys[2].interpolationToNext");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.InvalidBankingDomain,
            "banking.keys[3].distance");

        TrackLayoutPackageV2Dto nonfiniteDistance = CreateRepresentativeDto();
        nonfiniteDistance.Banking!.Keys[1].Distance = double.NaN;

        AssertDiagnostic(
            TrackLayoutPackageV2Validator.Validate(nonfiniteDistance),
            TrackLayoutPackageV2ValidationCode.NonFiniteNumber,
            "banking.keys[1].distance");
    }

    [Fact]
    public void Validator_AllowsNullBankingAndNullHeartline()
    {
        TrackLayoutPackageV2Dto dto = CreateMinimalDto();
        dto.Banking = null;
        dto.Heartline = null;

        AssertValid(dto);
    }

    [Fact]
    public void Validator_CatchesInvalidHeartlineVocabularyAndNonFiniteOffsets()
    {
        TrackLayoutPackageV2Dto dto = CreateMinimalDto();
        dto.Heartline = new TrackHeartlineV2Dto
        {
            Kind = "dynamic",
            DistanceDomain = "worldDistance",
            AxisSource = "worldUp",
            NormalOffset = double.NaN,
            LateralOffset = double.NegativeInfinity
        };

        IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> diagnostics =
            TrackLayoutPackageV2Validator.Validate(dto);

        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.InvalidHeartlineKind,
            "heartline.kind");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.InvalidHeartlineDistanceDomain,
            "heartline.distanceDomain");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.InvalidHeartlineAxisSource,
            "heartline.axisSource");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.NonFiniteNumber,
            "heartline.normalOffset");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.NonFiniteNumber,
            "heartline.lateralOffset");
    }

    [Fact]
    public void Validator_DiagnosticPaths_RemainStableForRepresentativeFailures()
    {
        TrackLayoutPackageV2Dto dto = CreateConstantHeartlineDto();
        dto.StartPose.Tangent = new TrackLayoutVector3dV2Dto { X = 2.0 };
        dto.Sections[0].Radius = 10.0;
        dto.Sections = dto.Sections.Concat(new[]
        {
            new TrackLayoutSectionV2Dto
            {
                Kind = TrackLayoutPackageV2Vocabulary.SpatialSectionKind,
                Id = "bad-spatial",
                Length = 1.0,
                RollRadians = 0.0,
                Degree = 1,
                ControlPoints = new[]
                {
                    new TrackLayoutVector3dV2Dto(),
                    new TrackLayoutVector3dV2Dto { Y = 1.0 }
                },
                Weights = new[] { 1.0, 1.0 }
            }
        }).ToArray();
        dto.Banking!.Keys[1].Distance = 0.0;
        dto.Heartline!.Kind = "dynamic";

        IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> diagnostics =
            TrackLayoutPackageV2Validator.Validate(dto);

        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.InvalidStartPoseBasis,
            "startPose.tangent");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.UnexpectedSectionField,
            "sections[0].radius");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.InvalidSpatialStartContract,
            "sections[4].controlPoints[1]");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.InvalidBankingKeyOrder,
            "banking.keys[1].distance");
        AssertDiagnostic(
            diagnostics,
            TrackLayoutPackageV2ValidationCode.InvalidHeartlineKind,
            "heartline.kind");
    }

    private static IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> ValidateStartPose(
        Action<TrackStartPoseV2Dto> mutate)
    {
        TrackLayoutPackageV2Dto dto = CreateMinimalDto();
        mutate(dto.StartPose);
        return TrackLayoutPackageV2Validator.Validate(dto);
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

    private static TrackLayoutPackageV2Dto CreateConstantHeartlineDto()
    {
        TrackLayoutPackageV2Dto dto = CreateRepresentativeDto();
        dto.Metadata.SourceName = "Constant heartline V2 layout";
        dto.Metadata.LayoutId = "layout.m147.constant-heartline";
        dto.Heartline = new TrackHeartlineV2Dto
        {
            Kind = TrackLayoutPackageV2Vocabulary.HeartlineKindConstantOffset,
            DistanceDomain = TrackLayoutPackageV2Vocabulary.HeartlineDistanceDomainCenterlineStation,
            AxisSource = TrackLayoutPackageV2Vocabulary.HeartlineAxisSourceSampledFrame,
            NormalOffset = 1.1,
            LateralOffset = 0.0
        };

        return dto;
    }

    private static TrackLayoutPackageV2Dto CreateRepresentativeDto()
    {
        return new TrackLayoutPackageV2Dto
        {
            Metadata = new TrackLayoutMetadataV2Dto
            {
                Units = "meters",
                SourceName = "representative",
                LayoutId = "layout.m147.representative"
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

    private static void AssertValid(TrackLayoutPackageV2Dto dto)
    {
        IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> diagnostics =
            TrackLayoutPackageV2Validator.Validate(dto);

        Assert.Empty(diagnostics);
        Assert.True(
            TrackLayoutPackageV2Validator.TryValidate(
                dto,
                out IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> tryDiagnostics));
        Assert.Empty(tryDiagnostics);
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
}
