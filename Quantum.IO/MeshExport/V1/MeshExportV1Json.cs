using System;
using System.Text.Json;

namespace Quantum.IO.MeshExport.V1
{
    /// <summary>
    /// JSON reader/writer for the MeshExportV1 contract.
    /// </summary>
    public static class MeshExportV1Json
    {
        private static readonly JsonSerializerOptions CompactOptions = CreateOptions(indented: false);
        private static readonly JsonSerializerOptions IndentedOptions = CreateOptions(indented: true);

        public static string Serialize(MeshExportV1Dto dto, bool indented = false)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            return JsonSerializer.Serialize(dto, indented ? IndentedOptions : CompactOptions);
        }

        public static MeshExportV1Dto Deserialize(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            MeshExportV1Dto? dto;

            try
            {
                dto = JsonSerializer.Deserialize<MeshExportV1Dto>(json, CompactOptions);
            }
            catch (JsonException ex)
            {
                throw new JsonException("Failed to deserialize MeshExportV1Dto: malformed JSON.", ex);
            }

            if (dto == null)
            {
                throw new JsonException("Failed to deserialize MeshExportV1Dto: JSON payload was null.");
            }

            if (!string.Equals(dto.Contract, MeshExportV1Dto.ContractName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Invalid MeshExportV1Dto contract '{dto.Contract ?? "<null>"}'. Expected '{MeshExportV1Dto.ContractName}'.");
            }

            if (dto.Version != MeshExportV1Dto.ContractVersion)
            {
                throw new InvalidOperationException(
                    $"Invalid MeshExportV1Dto version '{dto.Version}'. Expected '{MeshExportV1Dto.ContractVersion}'.");
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
