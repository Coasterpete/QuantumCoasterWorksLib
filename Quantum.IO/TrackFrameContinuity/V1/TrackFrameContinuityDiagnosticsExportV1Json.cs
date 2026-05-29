using System;
using System.Text.Json;

namespace Quantum.IO.TrackFrameContinuity.V1
{
    /// <summary>
    /// JSON reader/writer for the TrackFrameContinuityDiagnosticsExportV1 contract.
    /// </summary>
    public static class TrackFrameContinuityDiagnosticsExportV1Json
    {
        private static readonly JsonSerializerOptions CompactOptions = CreateOptions(indented: false);
        private static readonly JsonSerializerOptions IndentedOptions = CreateOptions(indented: true);

        public static string Serialize(TrackFrameContinuityDiagnosticsExportV1Dto dto, bool indented = false)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            return JsonSerializer.Serialize(dto, indented ? IndentedOptions : CompactOptions);
        }

        public static TrackFrameContinuityDiagnosticsExportV1Dto Deserialize(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            TrackFrameContinuityDiagnosticsExportV1Dto? dto;

            try
            {
                dto = JsonSerializer.Deserialize<TrackFrameContinuityDiagnosticsExportV1Dto>(
                    json,
                    CompactOptions);
            }
            catch (JsonException ex)
            {
                throw new JsonException(
                    "Failed to deserialize TrackFrameContinuityDiagnosticsExportV1Dto: malformed JSON.",
                    ex);
            }

            if (dto == null)
            {
                throw new JsonException(
                    "Failed to deserialize TrackFrameContinuityDiagnosticsExportV1Dto: JSON payload was null.");
            }

            if (!string.Equals(
                dto.Contract,
                TrackFrameContinuityDiagnosticsExportV1Dto.ContractName,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Invalid TrackFrameContinuityDiagnosticsExportV1Dto contract '{dto.Contract ?? "<null>"}'. Expected '{TrackFrameContinuityDiagnosticsExportV1Dto.ContractName}'.");
            }

            if (dto.Version != TrackFrameContinuityDiagnosticsExportV1Dto.ContractVersion)
            {
                throw new InvalidOperationException(
                    $"Invalid TrackFrameContinuityDiagnosticsExportV1Dto version '{dto.Version}'. Expected '{TrackFrameContinuityDiagnosticsExportV1Dto.ContractVersion}'.");
            }

            return dto;
        }

        private static JsonSerializerOptions CreateOptions(bool indented)
        {
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = indented
            };
        }
    }
}
