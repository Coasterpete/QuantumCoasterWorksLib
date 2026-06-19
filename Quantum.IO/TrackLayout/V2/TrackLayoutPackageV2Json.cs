using System;
using System.Collections.Generic;
using System.Globalization;
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

        public static TrackLayoutPackageV2ImportResult Import(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            try
            {
                return TrackLayoutPackageV2Mapper.Import(Deserialize(json));
            }
            catch (JsonException ex)
            {
                return TrackLayoutPackageV2ImportResult.Failure(
                    new[]
                    {
                        new TrackLayoutPackageV2ValidationDiagnostic(
                            TrackLayoutPackageV2ValidationCode.MalformedJson,
                            "json",
                            CreateMalformedJsonMessage(ex))
                    });
            }
        }

        private static string CreateMalformedJsonMessage(JsonException exception)
        {
            JsonException detail = exception.InnerException as JsonException ?? exception;
            string message = exception.Message;
            if (!ReferenceEquals(detail, exception))
            {
                message += " JSON parser detail: " + detail.Message;
            }

            string context = CreateJsonExceptionContext(detail);
            if (context.Length != 0)
            {
                message += " " + context;
            }

            return message;
        }

        private static string CreateJsonExceptionContext(JsonException exception)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(exception.Path))
            {
                parts.Add("path '" + exception.Path + "'");
            }

            if (exception.LineNumber.HasValue)
            {
                parts.Add("line " + exception.LineNumber.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (exception.BytePositionInLine.HasValue)
            {
                parts.Add(
                    "byte position " +
                    exception.BytePositionInLine.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (parts.Count == 0)
            {
                return string.Empty;
            }

            return "Context: " + string.Join(", ", parts) + ".";
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
