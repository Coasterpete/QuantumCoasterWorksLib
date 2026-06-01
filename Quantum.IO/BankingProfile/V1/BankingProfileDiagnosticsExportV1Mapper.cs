using System;
using System.Collections.Generic;
using Quantum.Track;

namespace Quantum.IO.BankingProfile.V1
{
    /// <summary>
    /// Source data for building a BankingProfile diagnostics artifact.
    /// </summary>
    public sealed class BankingProfileDiagnosticsExportV1Source
    {
        public string Units { get; set; } = "meters,radians";

        public string? SourceName { get; set; }

        public int ProfileKeyCount { get; set; }

        public BankingProfileDiagnosticsReport? Report { get; set; }
    }

    /// <summary>
    /// Maps BankingProfileDiagnostics results into a stable JSON DTO contract.
    /// </summary>
    public static class BankingProfileDiagnosticsExportV1Mapper
    {
        public static BankingProfileDiagnosticsExportV1Dto Export(
            BankingProfileDiagnosticsExportV1Source source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            BankingProfileDiagnosticsReport report = source.Report ??
                throw new ArgumentException("BankingProfile diagnostics report cannot be null.", nameof(source));

            return new BankingProfileDiagnosticsExportV1Dto
            {
                Contract = BankingProfileDiagnosticsExportV1Dto.ContractName,
                Version = BankingProfileDiagnosticsExportV1Dto.ContractVersion,
                BackendOnly = true,
                Metadata = new BankingProfileDiagnosticsMetadataV1Dto
                {
                    Units = string.IsNullOrWhiteSpace(source.Units) ? "meters,radians" : source.Units,
                    SourceName = source.SourceName,
                    ProfileKeyCount = source.ProfileKeyCount,
                    DistanceUnit = "meters",
                    RollAngleUnits = "radians,degrees",
                    RollSlopeUnit = "radians_per_meter"
                },
                SummaryMetrics = MapSummary(report.Summary),
                Samples = MapSamples(report.Samples)
            };
        }

        private static BankingProfileDiagnosticsSummaryMetricsV1Dto MapSummary(
            BankingProfileDiagnosticsSummary summary)
        {
            return new BankingProfileDiagnosticsSummaryMetricsV1Dto
            {
                SampleCount = summary.SampleCount,
                MinRollRadians = summary.MinRollRadians,
                MaxRollRadians = summary.MaxRollRadians,
                MinRollDegrees = summary.MinRollDegrees,
                MaxRollDegrees = summary.MaxRollDegrees,
                MaxAbsoluteRollSlopeRadPerMeter = summary.MaxAbsoluteRollSlopeRadPerMeter
            };
        }

        private static BankingProfileDiagnosticsSampleV1Dto[] MapSamples(
            IReadOnlyList<BankingProfileDiagnosticsSample> samples)
        {
            var result = new BankingProfileDiagnosticsSampleV1Dto[samples.Count];

            for (int i = 0; i < samples.Count; i++)
            {
                BankingProfileDiagnosticsSample sample = samples[i];
                result[i] = new BankingProfileDiagnosticsSampleV1Dto
                {
                    SampleIndex = sample.SampleIndex,
                    Distance = sample.Distance,
                    RollRadians = sample.RollRadians,
                    RollDegrees = sample.RollDegrees,
                    InterpolationMode = sample.InterpolationMode.ToString(),
                    SourceKind = sample.SourceKind.ToString(),
                    SourceInterval = new BankingProfileDiagnosticsSourceIntervalV1Dto
                    {
                        StartKeyIndex = sample.SourceStartKeyIndex,
                        EndKeyIndex = sample.SourceEndKeyIndex,
                        StartDistance = sample.SourceStartDistance,
                        EndDistance = sample.SourceEndDistance
                    },
                    ApproximateRollSlopeRadPerMeter = sample.ApproximateRollSlopeRadPerMeter
                };
            }

            return result;
        }
    }
}
