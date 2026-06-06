using System;

namespace Quantum.IO.DistanceInspection.V1
{
    /// <summary>
    /// Versioned UI-facing DTO for distance inspection snapshot handoff.
    /// </summary>
    public sealed class DistanceInspectionSnapshotV1Dto
    {
        public const string ContractName = "quantum.distance_inspection_snapshot";

        public const int ContractVersion = 1;

        public string Contract { get; set; } = ContractName;

        public int Version { get; set; } = ContractVersion;

        public double Distance { get; set; }

        public DistanceInspectionSectionV1Dto[] Sections { get; set; } =
            Array.Empty<DistanceInspectionSectionV1Dto>();
    }

    public sealed class DistanceInspectionSectionV1Dto
    {
        public string Kind { get; set; } = string.Empty;

        public string Domain { get; set; } = string.Empty;

        public double StartX { get; set; }

        public double EndX { get; set; }

        public string Diagnostic { get; set; } = string.Empty;

        public string[] Channels { get; set; } = Array.Empty<string>();

        public DistanceInspectionChannelValueV1Dto[] ChannelValues { get; set; } =
            Array.Empty<DistanceInspectionChannelValueV1Dto>();
    }

    public sealed class DistanceInspectionChannelValueV1Dto
    {
        public string Channel { get; set; } = string.Empty;

        public double Value { get; set; }
    }
}
