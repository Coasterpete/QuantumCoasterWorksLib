using System;
using System.Text.Json;
using Quantum.IO.TrainPose.V1;

namespace Quantum.IO.DebugViewport.V1
{
    /// <summary>
    /// JSON reader/writer for the DebugViewportSnapshotV1 contract.
    /// </summary>
    public static class DebugViewportSnapshotV1Json
    {
        private static readonly JsonSerializerOptions CompactOptions = CreateOptions(indented: false);
        private static readonly JsonSerializerOptions IndentedOptions = CreateOptions(indented: true);

        public static string Serialize(DebugViewportSnapshotV1Dto dto, bool indented = false)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            return JsonSerializer.Serialize(dto, indented ? IndentedOptions : CompactOptions);
        }

        public static DebugViewportSnapshotV1Dto Deserialize(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            DebugViewportSnapshotV1Dto? dto;

            try
            {
                dto = JsonSerializer.Deserialize<DebugViewportSnapshotV1Dto>(json, CompactOptions);
            }
            catch (JsonException ex)
            {
                throw new JsonException("Failed to deserialize DebugViewportSnapshotV1Dto: malformed JSON.", ex);
            }

            if (dto == null)
            {
                throw new JsonException("Failed to deserialize DebugViewportSnapshotV1Dto: JSON payload was null.");
            }

            if (!string.Equals(dto.Contract, DebugViewportSnapshotV1Dto.ContractName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Invalid DebugViewportSnapshotV1Dto contract '{dto.Contract ?? "<null>"}'. Expected '{DebugViewportSnapshotV1Dto.ContractName}'.");
            }

            if (dto.Version != DebugViewportSnapshotV1Dto.ContractVersion)
            {
                throw new InvalidOperationException(
                    $"Invalid DebugViewportSnapshotV1Dto version '{dto.Version}'. Expected '{DebugViewportSnapshotV1Dto.ContractVersion}'.");
            }

            ValidateNestedTrainPoseIdentity(dto.TrainPose);
            return dto;
        }

        private static void ValidateNestedTrainPoseIdentity(TrainPoseExportV1Dto? trainPose)
        {
            if (trainPose == null)
            {
                return;
            }

            if (!string.Equals(trainPose.Contract, TrainPoseExportV1Dto.ContractName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Invalid nested TrainPoseExportV1Dto contract '{trainPose.Contract ?? "<null>"}'. Expected '{TrainPoseExportV1Dto.ContractName}'.");
            }

            if (trainPose.Version != TrainPoseExportV1Dto.ContractVersion)
            {
                throw new InvalidOperationException(
                    $"Invalid nested TrainPoseExportV1Dto version '{trainPose.Version}'. Expected '{TrainPoseExportV1Dto.ContractVersion}'.");
            }
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
