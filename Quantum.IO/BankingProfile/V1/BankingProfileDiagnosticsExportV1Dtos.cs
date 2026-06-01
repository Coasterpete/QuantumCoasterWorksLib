using System;

namespace Quantum.IO.BankingProfile.V1
{
    /// <summary>
    /// Versioned backend-only export contract for BankingProfile roll diagnostics.
    /// </summary>
    public sealed class BankingProfileDiagnosticsExportV1Dto
    {
        public const string ContractName = "quantum.banking_profile_diagnostics";

        public const int ContractVersion = 1;

        public string Contract { get; set; } = ContractName;

        public int Version { get; set; } = ContractVersion;

        public bool BackendOnly { get; set; } = true;

        public BankingProfileDiagnosticsMetadataV1Dto Metadata { get; set; } =
            new BankingProfileDiagnosticsMetadataV1Dto();

        public BankingProfileDiagnosticsSummaryMetricsV1Dto SummaryMetrics { get; set; } =
            new BankingProfileDiagnosticsSummaryMetricsV1Dto();

        public BankingProfileDiagnosticsSampleV1Dto[] Samples { get; set; } =
            Array.Empty<BankingProfileDiagnosticsSampleV1Dto>();
    }

    public sealed class BankingProfileDiagnosticsMetadataV1Dto
    {
        public string Units { get; set; } = "meters,radians";

        public string? SourceName { get; set; }

        public int ProfileKeyCount { get; set; }

        public string DistanceUnit { get; set; } = "meters";

        public string RollAngleUnits { get; set; } = "radians,degrees";

        public string RollSlopeUnit { get; set; } = "radians_per_meter";
    }

    public sealed class BankingProfileDiagnosticsSummaryMetricsV1Dto
    {
        public int SampleCount { get; set; }

        public double MinRollRadians { get; set; }

        public double MaxRollRadians { get; set; }

        public double MinRollDegrees { get; set; }

        public double MaxRollDegrees { get; set; }

        public double MaxAbsoluteRollSlopeRadPerMeter { get; set; }
    }

    public sealed class BankingProfileDiagnosticsSampleV1Dto
    {
        public int SampleIndex { get; set; }

        public double Distance { get; set; }

        public double RollRadians { get; set; }

        public double RollDegrees { get; set; }

        public string InterpolationMode { get; set; } = string.Empty;

        public string SourceKind { get; set; } = string.Empty;

        public BankingProfileDiagnosticsSourceIntervalV1Dto SourceInterval { get; set; } =
            new BankingProfileDiagnosticsSourceIntervalV1Dto();

        public double? ApproximateRollSlopeRadPerMeter { get; set; }
    }

    public sealed class BankingProfileDiagnosticsSourceIntervalV1Dto
    {
        public int StartKeyIndex { get; set; }

        public int EndKeyIndex { get; set; }

        public double StartDistance { get; set; }

        public double EndDistance { get; set; }
    }
}
