using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Quantum.IO.TrackLayout.V1
{
    /// <summary>
    /// JSON reader/writer for the TrackLayoutPackageV1 authored-layout contract.
    /// </summary>
    public static class TrackLayoutPackageV1Json
    {
        private static readonly JsonSerializerOptions CompactOptions = CreateOptions(indented: false);
        private static readonly JsonSerializerOptions IndentedOptions = CreateOptions(indented: true);

        public static string Serialize(TrackLayoutPackageV1Dto dto, bool indented = false)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            return JsonSerializer.Serialize(dto, indented ? IndentedOptions : CompactOptions);
        }

        public static TrackLayoutPackageV1Dto Deserialize(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            TrackLayoutPackageV1Dto? dto;

            try
            {
                dto = JsonSerializer.Deserialize<TrackLayoutPackageV1Dto>(json, CompactOptions);
            }
            catch (JsonException ex)
            {
                throw new JsonException("Failed to deserialize TrackLayoutPackageV1Dto: malformed JSON.", ex);
            }

            if (dto == null)
            {
                throw new JsonException("Failed to deserialize TrackLayoutPackageV1Dto: JSON payload was null.");
            }

            return dto;
        }

        public static TrackLayoutPackageV1ImportResult Import(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            try
            {
                return TrackLayoutPackageV1Mapper.Import(Deserialize(json));
            }
            catch (JsonException ex)
            {
                return TrackLayoutPackageV1ImportResult.Failure(
                    new[]
                    {
                        new TrackLayoutPackageV1ValidationDiagnostic(
                            TrackLayoutPackageV1ValidationCode.MalformedJson,
                            "json",
                            ex.Message)
                    });
            }
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
