using System;
using System.Text.Json;

namespace Quantum.IO.BankingProfile.V1
{
    /// <summary>
    /// JSON reader/writer for the BankingProfileDiagnosticsExportV1 contract.
    /// </summary>
    public static class BankingProfileDiagnosticsExportV1Json
    {
        private static readonly JsonSerializerOptions CompactOptions = CreateOptions(indented: false);
        private static readonly JsonSerializerOptions IndentedOptions = CreateOptions(indented: true);

        public static string Serialize(BankingProfileDiagnosticsExportV1Dto dto, bool indented = false)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            return JsonSerializer.Serialize(dto, indented ? IndentedOptions : CompactOptions);
        }

        public static BankingProfileDiagnosticsExportV1Dto Deserialize(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            BankingProfileDiagnosticsExportV1Dto? dto;

            try
            {
                dto = JsonSerializer.Deserialize<BankingProfileDiagnosticsExportV1Dto>(
                    json,
                    CompactOptions);
            }
            catch (JsonException ex)
            {
                throw new JsonException(
                    "Failed to deserialize BankingProfileDiagnosticsExportV1Dto: malformed JSON.",
                    ex);
            }

            if (dto == null)
            {
                throw new JsonException(
                    "Failed to deserialize BankingProfileDiagnosticsExportV1Dto: JSON payload was null.");
            }

            if (!string.Equals(
                dto.Contract,
                BankingProfileDiagnosticsExportV1Dto.ContractName,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Invalid BankingProfileDiagnosticsExportV1Dto contract '{dto.Contract ?? "<null>"}'. Expected '{BankingProfileDiagnosticsExportV1Dto.ContractName}'.");
            }

            if (dto.Version != BankingProfileDiagnosticsExportV1Dto.ContractVersion)
            {
                throw new InvalidOperationException(
                    $"Invalid BankingProfileDiagnosticsExportV1Dto version '{dto.Version}'. Expected '{BankingProfileDiagnosticsExportV1Dto.ContractVersion}'.");
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
