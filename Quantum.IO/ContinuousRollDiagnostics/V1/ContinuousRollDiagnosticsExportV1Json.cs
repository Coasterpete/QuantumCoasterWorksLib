using System;
using System.Text.Json;

namespace Quantum.IO.ContinuousRollDiagnostics.V1
{
    /// <summary>
    /// JSON reader/writer for the ContinuousRollDiagnosticsExportV1 contract.
    /// </summary>
    public static class ContinuousRollDiagnosticsExportV1Json
    {
        private static readonly JsonSerializerOptions CompactOptions = CreateOptions(indented: false);
        private static readonly JsonSerializerOptions IndentedOptions = CreateOptions(indented: true);

        public static string Serialize(ContinuousRollDiagnosticsExportV1Dto dto, bool indented = false)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            return JsonSerializer.Serialize(dto, indented ? IndentedOptions : CompactOptions);
        }

        public static ContinuousRollDiagnosticsExportV1Dto Deserialize(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            ContinuousRollDiagnosticsExportV1Dto? dto;

            try
            {
                dto = JsonSerializer.Deserialize<ContinuousRollDiagnosticsExportV1Dto>(
                    json,
                    CompactOptions);
            }
            catch (JsonException ex)
            {
                throw new JsonException(
                    "Failed to deserialize ContinuousRollDiagnosticsExportV1Dto: malformed JSON.",
                    ex);
            }

            if (dto == null)
            {
                throw new JsonException(
                    "Failed to deserialize ContinuousRollDiagnosticsExportV1Dto: JSON payload was null.");
            }

            if (!string.Equals(
                dto.Contract,
                ContinuousRollDiagnosticsExportV1Dto.ContractName,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Invalid ContinuousRollDiagnosticsExportV1Dto contract '{dto.Contract ?? "<null>"}'. Expected '{ContinuousRollDiagnosticsExportV1Dto.ContractName}'.");
            }

            if (dto.Version != ContinuousRollDiagnosticsExportV1Dto.ContractVersion)
            {
                throw new InvalidOperationException(
                    $"Invalid ContinuousRollDiagnosticsExportV1Dto version '{dto.Version}'. Expected '{ContinuousRollDiagnosticsExportV1Dto.ContractVersion}'.");
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
