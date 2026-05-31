using System;
using System.Text.Json;

namespace Quantum.IO.TransportedFrameComparison.V1
{
    /// <summary>
    /// JSON reader/writer for the TransportedFrameComparisonDiagnosticsExportV1 contract.
    /// </summary>
    public static class TransportedFrameComparisonDiagnosticsExportV1Json
    {
        private static readonly JsonSerializerOptions CompactOptions = CreateOptions(indented: false);
        private static readonly JsonSerializerOptions IndentedOptions = CreateOptions(indented: true);

        public static string Serialize(TransportedFrameComparisonDiagnosticsExportV1Dto dto, bool indented = false)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            return JsonSerializer.Serialize(dto, indented ? IndentedOptions : CompactOptions);
        }

        public static TransportedFrameComparisonDiagnosticsExportV1Dto Deserialize(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            TransportedFrameComparisonDiagnosticsExportV1Dto? dto;

            try
            {
                dto = JsonSerializer.Deserialize<TransportedFrameComparisonDiagnosticsExportV1Dto>(
                    json,
                    CompactOptions);
            }
            catch (JsonException ex)
            {
                throw new JsonException(
                    "Failed to deserialize TransportedFrameComparisonDiagnosticsExportV1Dto: malformed JSON.",
                    ex);
            }

            if (dto == null)
            {
                throw new JsonException(
                    "Failed to deserialize TransportedFrameComparisonDiagnosticsExportV1Dto: JSON payload was null.");
            }

            if (!string.Equals(
                dto.Contract,
                TransportedFrameComparisonDiagnosticsExportV1Dto.ContractName,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Invalid TransportedFrameComparisonDiagnosticsExportV1Dto contract '{dto.Contract ?? "<null>"}'. Expected '{TransportedFrameComparisonDiagnosticsExportV1Dto.ContractName}'.");
            }

            if (dto.Version != TransportedFrameComparisonDiagnosticsExportV1Dto.ContractVersion)
            {
                throw new InvalidOperationException(
                    $"Invalid TransportedFrameComparisonDiagnosticsExportV1Dto version '{dto.Version}'. Expected '{TransportedFrameComparisonDiagnosticsExportV1Dto.ContractVersion}'.");
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
