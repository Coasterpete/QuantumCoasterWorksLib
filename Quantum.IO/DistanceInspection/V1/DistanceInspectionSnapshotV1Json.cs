using System;
using System.Text.Json;

namespace Quantum.IO.DistanceInspection.V1
{
    /// <summary>
    /// JSON reader/writer for the DistanceInspectionSnapshotV1 contract.
    /// </summary>
    public static class DistanceInspectionSnapshotV1Json
    {
        private static readonly JsonSerializerOptions CompactOptions = CreateOptions(indented: false);
        private static readonly JsonSerializerOptions IndentedOptions = CreateOptions(indented: true);

        public static string Serialize(DistanceInspectionSnapshotV1Dto dto, bool indented = false)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            return JsonSerializer.Serialize(dto, indented ? IndentedOptions : CompactOptions);
        }

        public static DistanceInspectionSnapshotV1Dto Deserialize(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            DistanceInspectionSnapshotV1Dto? dto;

            try
            {
                dto = JsonSerializer.Deserialize<DistanceInspectionSnapshotV1Dto>(
                    json,
                    CompactOptions);
            }
            catch (JsonException ex)
            {
                throw new JsonException(
                    "Failed to deserialize DistanceInspectionSnapshotV1Dto: malformed JSON.",
                    ex);
            }

            if (dto == null)
            {
                throw new JsonException(
                    "Failed to deserialize DistanceInspectionSnapshotV1Dto: JSON payload was null.");
            }

            if (!string.Equals(
                dto.Contract,
                DistanceInspectionSnapshotV1Dto.ContractName,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Invalid DistanceInspectionSnapshotV1Dto contract '{dto.Contract ?? "<null>"}'. Expected '{DistanceInspectionSnapshotV1Dto.ContractName}'.");
            }

            if (dto.Version != DistanceInspectionSnapshotV1Dto.ContractVersion)
            {
                throw new InvalidOperationException(
                    $"Invalid DistanceInspectionSnapshotV1Dto version '{dto.Version}'. Expected '{DistanceInspectionSnapshotV1Dto.ContractVersion}'.");
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
