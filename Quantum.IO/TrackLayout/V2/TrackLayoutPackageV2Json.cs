using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Quantum.IO.TrackLayout.V2
{
    /// <summary>
    /// JSON reader/writer for the TrackLayoutPackageV2 authored-layout contract.
    /// </summary>
    public static class TrackLayoutPackageV2Json
    {
        private static readonly JsonSerializerOptions CompactOptions = CreateOptions(indented: false);
        private static readonly JsonSerializerOptions IndentedOptions = CreateOptions(indented: true);

        public static string Serialize(TrackLayoutPackageV2Dto dto, bool indented = false)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            return JsonSerializer.Serialize(dto, indented ? IndentedOptions : CompactOptions);
        }

        public static TrackLayoutPackageV2Dto Deserialize(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            TrackLayoutPackageV2Dto? dto;

            try
            {
                dto = JsonSerializer.Deserialize<TrackLayoutPackageV2Dto>(json, CompactOptions);
            }
            catch (JsonException ex)
            {
                throw new JsonException("Failed to deserialize TrackLayoutPackageV2Dto: malformed JSON.", ex);
            }

            if (dto == null)
            {
                throw new JsonException("Failed to deserialize TrackLayoutPackageV2Dto: JSON payload was null.");
            }

            return dto;
        }

        private static JsonSerializerOptions CreateOptions(bool indented)
        {
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = indented,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            };
        }
    }
}
