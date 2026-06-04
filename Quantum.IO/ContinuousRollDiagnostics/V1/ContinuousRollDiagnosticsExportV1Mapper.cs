using System;
using System.Collections.Generic;
using System.Globalization;
using Quantum.Track;
using SystemMath = System.Math;

namespace Quantum.IO.ContinuousRollDiagnostics.V1
{
    /// <summary>
    /// Maps ContinuousRollDiagnostics reports into a stable JSON DTO contract.
    /// </summary>
    public static class ContinuousRollDiagnosticsExportV1Mapper
    {
        private const double RadiansToDegrees = 180.0 / SystemMath.PI;

        public static ContinuousRollDiagnosticsExportV1Dto Export(
            ContinuousRollDiagnosticsReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            return new ContinuousRollDiagnosticsExportV1Dto
            {
                Contract = ContinuousRollDiagnosticsExportV1Dto.ContractName,
                Version = ContinuousRollDiagnosticsExportV1Dto.ContractVersion,
                SampleCount = report.Summary.SampleCount,
                MaxRollRateRadiansPerMeter = report.Summary.MaxAbsoluteRollRateRadPerMeter,
                AverageRollRateRadiansPerMeter = report.Summary.AverageAbsoluteRollRateRadPerMeter,
                WrapHandlingEnabled = report.Summary.WrapMode != ContinuousRollWrapMode.None,
                WarningCount = report.Summary.WarningCount,
                Samples = MapSamples(report)
            };
        }

        private static ContinuousRollDiagnosticsSampleV1Dto[] MapSamples(
            ContinuousRollDiagnosticsReport report)
        {
            var samples = new ContinuousRollDiagnosticsSampleV1Dto[report.Samples.Count];
            Dictionary<int, string> warningsByEndSampleIndex =
                BuildWarningTextByEndSampleIndex(report.Warnings);

            for (int i = 0; i < report.Samples.Count; i++)
            {
                ContinuousRollDiagnosticsSample sample = report.Samples[i];
                double deltaRadians = 0.0;
                double rollRateRadiansPerMeter = 0.0;

                if (i > 0)
                {
                    ContinuousRollDiagnosticsInterval interval = report.Intervals[i - 1];
                    deltaRadians = interval.RollDeltaRadians;
                    rollRateRadiansPerMeter = interval.RollRateRadPerMeter;
                }

                warningsByEndSampleIndex.TryGetValue(sample.SampleIndex, out string? warning);

                samples[i] = new ContinuousRollDiagnosticsSampleV1Dto
                {
                    StationDistance = sample.Distance,
                    RollRadians = sample.ContinuousRollRadians,
                    RollDegrees = sample.ContinuousRollDegrees,
                    DeltaRadians = deltaRadians,
                    DeltaDegrees = deltaRadians * RadiansToDegrees,
                    RollRateRadiansPerMeter = rollRateRadiansPerMeter,
                    Warning = warning
                };
            }

            return samples;
        }

        private static Dictionary<int, string> BuildWarningTextByEndSampleIndex(
            IReadOnlyList<ContinuousRollWarning> warnings)
        {
            var warningsByEndSampleIndex = new Dictionary<int, string>();

            for (int i = 0; i < warnings.Count; i++)
            {
                ContinuousRollWarning warning = warnings[i];
                int endSampleIndex = warning.Interval.EndSampleIndex;
                string warningText = FormatWarning(warning);

                if (warningsByEndSampleIndex.TryGetValue(endSampleIndex, out string? existing))
                {
                    warningsByEndSampleIndex[endSampleIndex] = existing + "; " + warningText;
                }
                else
                {
                    warningsByEndSampleIndex[endSampleIndex] = warningText;
                }
            }

            return warningsByEndSampleIndex;
        }

        private static string FormatWarning(ContinuousRollWarning warning)
        {
            if (warning.Kind == ContinuousRollWarningKind.RollRate)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "RollRate exceeded: samples={0}->{1}, actualRadiansPerMeter={2:G17}, thresholdRadiansPerMeter={3:G17}",
                    warning.Interval.StartSampleIndex,
                    warning.Interval.EndSampleIndex,
                    warning.ActualValue,
                    warning.ThresholdValue);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "RollDelta exceeded: samples={0}->{1}, actualRadians={2:G17}, thresholdRadians={3:G17}",
                warning.Interval.StartSampleIndex,
                warning.Interval.EndSampleIndex,
                warning.ActualValue,
                warning.ThresholdValue);
        }
    }
}
