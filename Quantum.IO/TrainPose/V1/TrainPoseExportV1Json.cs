using System;
using System.Text.Json;

namespace Quantum.IO.TrainPose.V1
{
    /// <summary>
    /// JSON reader/writer for the TrainPoseExportV1 contract.
    /// </summary>
    public static class TrainPoseExportV1Json
    {
        private static readonly JsonSerializerOptions CompactOptions = CreateOptions(indented: false);
        private static readonly JsonSerializerOptions IndentedOptions = CreateOptions(indented: true);

        /// <summary>
        /// Serializes a TrainPoseExportV1 DTO using camelCase JSON properties.
        /// </summary>
        public static string Serialize(TrainPoseExportV1Dto dto, bool indented = false)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            return JsonSerializer.Serialize(dto, indented ? IndentedOptions : CompactOptions);
        }

        /// <summary>
        /// Deserializes a TrainPoseExportV1 DTO and enforces contract identity/version.
        /// </summary>
        public static TrainPoseExportV1Dto Deserialize(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            TrainPoseExportV1Dto? dto;

            try
            {
                dto = JsonSerializer.Deserialize<TrainPoseExportV1Dto>(json, CompactOptions);
            }
            catch (JsonException ex)
            {
                throw new JsonException("Failed to deserialize TrainPoseExportV1Dto: malformed JSON.", ex);
            }

            if (dto == null)
            {
                throw new JsonException("Failed to deserialize TrainPoseExportV1Dto: JSON payload was null.");
            }

            if (!string.Equals(dto.Contract, TrainPoseExportV1Dto.ContractName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Invalid TrainPoseExportV1Dto contract '{dto.Contract ?? "<null>"}'. Expected '{TrainPoseExportV1Dto.ContractName}'.");
            }

            if (dto.Version != TrainPoseExportV1Dto.ContractVersion)
            {
                throw new InvalidOperationException(
                    $"Invalid TrainPoseExportV1Dto version '{dto.Version}'. Expected '{TrainPoseExportV1Dto.ContractVersion}'.");
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
