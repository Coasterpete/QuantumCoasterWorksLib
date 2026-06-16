using System;
using System.Text.Json.Serialization;

namespace Quantum.IO.TrackLayout.V1
{
    /// <summary>
    /// Versioned backend-only JSON DTO for authored coaster track layouts.
    /// </summary>
    public sealed class TrackLayoutPackageV1Dto
    {
        public const string ContractName = "quantum.track_layout_package";

        public const int ContractVersion = 1;

        public string Contract { get; set; } = ContractName;

        public int Version { get; set; } = ContractVersion;

        public TrackLayoutMetadataV1Dto Metadata { get; set; } = new TrackLayoutMetadataV1Dto();

        public TrackStartPoseV1Dto StartPose { get; set; } = new TrackStartPoseV1Dto();

        public TrackLayoutSectionV1Dto[] Sections { get; set; } =
            Array.Empty<TrackLayoutSectionV1Dto>();

        public TrackBankingV1Dto? Banking { get; set; }
    }

    public sealed class TrackLayoutMetadataV1Dto
    {
        public string Units { get; set; } = "meters";

        public string? SourceName { get; set; }
    }

    public sealed class TrackStartPoseV1Dto
    {
        public TrackLayoutVector3dV1Dto Position { get; set; } = new TrackLayoutVector3dV1Dto();

        public TrackLayoutVector3dV1Dto Tangent { get; set; } =
            new TrackLayoutVector3dV1Dto { X = 1.0 };

        public TrackLayoutVector3dV1Dto Normal { get; set; } =
            new TrackLayoutVector3dV1Dto { Y = 1.0 };

        public TrackLayoutVector3dV1Dto Binormal { get; set; } =
            new TrackLayoutVector3dV1Dto { Z = 1.0 };
    }

    public sealed class TrackLayoutVector3dV1Dto
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double Z { get; set; }
    }

    public sealed class TrackLayoutSectionV1Dto
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
        public TrackLayoutVector3dV1Dto[]? ControlPoints { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double[]? Weights { get; set; }
    }

    public sealed class TrackBankingV1Dto
    {
        public TrackBankingKeyV1Dto[] Keys { get; set; } =
            Array.Empty<TrackBankingKeyV1Dto>();
    }

    public sealed class TrackBankingKeyV1Dto
    {
        public double Distance { get; set; }

        public double RollRadians { get; set; }

        public string InterpolationToNext { get; set; } =
            TrackLayoutPackageV1Vocabulary.BankingInterpolationConstant;
    }
}
