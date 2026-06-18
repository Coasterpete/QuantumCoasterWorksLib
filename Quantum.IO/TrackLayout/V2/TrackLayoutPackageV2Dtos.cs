using System;
using System.Text.Json.Serialization;

namespace Quantum.IO.TrackLayout.V2
{
    /// <summary>
    /// Versioned backend-only JSON DTO for authored coaster track layouts.
    /// </summary>
    public sealed class TrackLayoutPackageV2Dto
    {
        public const string ContractName = "quantum.track_layout_package";

        public const int ContractVersion = 2;

        public string Contract { get; set; } = ContractName;

        public int Version { get; set; } = ContractVersion;

        public TrackLayoutMetadataV2Dto Metadata { get; set; } = new TrackLayoutMetadataV2Dto();

        public TrackStartPoseV2Dto StartPose { get; set; } = new TrackStartPoseV2Dto();

        public TrackLayoutSectionV2Dto[] Sections { get; set; } =
            Array.Empty<TrackLayoutSectionV2Dto>();

        public TrackBankingV2Dto? Banking { get; set; }

        public TrackHeartlineV2Dto? Heartline { get; set; }
    }

    public sealed class TrackLayoutMetadataV2Dto
    {
        public string Units { get; set; } = "meters";

        public string? SourceName { get; set; }

        public string? LayoutId { get; set; }
    }

    public sealed class TrackStartPoseV2Dto
    {
        public TrackLayoutVector3dV2Dto Position { get; set; } = new TrackLayoutVector3dV2Dto();

        public TrackLayoutVector3dV2Dto Tangent { get; set; } =
            new TrackLayoutVector3dV2Dto { X = 1.0 };

        public TrackLayoutVector3dV2Dto Normal { get; set; } =
            new TrackLayoutVector3dV2Dto { Y = 1.0 };

        public TrackLayoutVector3dV2Dto Binormal { get; set; } =
            new TrackLayoutVector3dV2Dto { Z = 1.0 };
    }

    public sealed class TrackLayoutVector3dV2Dto
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double Z { get; set; }
    }

    public sealed class TrackLayoutSectionV2Dto
    {
        public string Kind { get; set; } = string.Empty;

        public string Id { get; set; } = string.Empty;

        public double Length { get; set; }

        public double RollRadians { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Radius { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? StartCurvature { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? EndCurvature { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? InterpolationMode { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Degree { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TrackLayoutVector3dV2Dto[]? ControlPoints { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double[]? Weights { get; set; }
    }

    public sealed class TrackBankingV2Dto
    {
        public TrackBankingKeyV2Dto[] Keys { get; set; } =
            Array.Empty<TrackBankingKeyV2Dto>();
    }

    public sealed class TrackBankingKeyV2Dto
    {
        public double Distance { get; set; }

        public double RollRadians { get; set; }

        public string InterpolationToNext { get; set; } =
            TrackLayoutPackageV2Vocabulary.BankingInterpolationConstant;
    }

    public sealed class TrackHeartlineV2Dto
    {
        public string Kind { get; set; } =
            TrackLayoutPackageV2Vocabulary.HeartlineKindConstantOffset;

        public string DistanceDomain { get; set; } =
            TrackLayoutPackageV2Vocabulary.HeartlineDistanceDomainCenterlineStation;

        public string AxisSource { get; set; } =
            TrackLayoutPackageV2Vocabulary.HeartlineAxisSourceSampledFrame;

        public double NormalOffset { get; set; }

        public double LateralOffset { get; set; }
    }
}
