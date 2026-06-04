using System;
using System.Text.Json.Serialization;

namespace Quantum.IO.ContinuousRollDiagnostics.V1
{
    /// <summary>
    /// Versioned renderer-neutral export contract for backend continuous roll diagnostics.
    /// </summary>
    public sealed class ContinuousRollDiagnosticsExportV1Dto
    {
        public const string ContractName = "quantum.continuous_roll_diagnostics";

        public const int ContractVersion = 1;

        public string Contract { get; set; } = ContractName;

        public int Version { get; set; } = ContractVersion;

        public int SampleCount { get; set; }

        public double MaxRollRateRadiansPerMeter { get; set; }

        public double AverageRollRateRadiansPerMeter { get; set; }

        public bool WrapHandlingEnabled { get; set; }

        public int WarningCount { get; set; }

        public ContinuousRollDiagnosticsSampleV1Dto[] Samples { get; set; } =
            Array.Empty<ContinuousRollDiagnosticsSampleV1Dto>();
    }

    public sealed class ContinuousRollDiagnosticsSampleV1Dto
    {
        public double StationDistance { get; set; }

        public double RollRadians { get; set; }

        public double RollDegrees { get; set; }

        public double DeltaRadians { get; set; }

        public double DeltaDegrees { get; set; }

        public double RollRateRadiansPerMeter { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Warning { get; set; }
    }
}
