using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Quantum.IO.DebugViewport.V1;

namespace Quantum.Debug
{
    public static class DebugViewportSnapshotV1ValidateCommand
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public static int Run(string snapshotJsonPath)
        {
            return Run(snapshotJsonPath, Console.Out);
        }

        public static int Run(string snapshotJsonPath, TextWriter output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (string.IsNullOrWhiteSpace(snapshotJsonPath))
            {
                output.WriteLine("snapshotJsonPath is required.");
                return 1;
            }

            DebugViewportSnapshotV1Dto dto;
            try
            {
                string resolvedPath = Path.GetFullPath(snapshotJsonPath);
                string json = File.ReadAllText(resolvedPath);
                dto = JsonSerializer.Deserialize<DebugViewportSnapshotV1Dto>(json, JsonOptions) ??
                    throw new JsonException("JSON payload was null.");
            }
            catch (Exception ex) when (IsReadOrParseException(ex))
            {
                output.WriteLine("Failed to read DebugViewportSnapshotV1 JSON.");
                output.WriteLine(ex.Message);
                return 1;
            }

            IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics =
                DebugViewportSnapshotV1Validator.Validate(dto);

            PrintSummary(output, dto, diagnostics);
            return diagnostics.Count == 0 ? 0 : 1;
        }

        private static void PrintSummary(
            TextWriter output,
            DebugViewportSnapshotV1Dto dto,
            IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics)
        {
            output.WriteLine("DebugViewportSnapshotV1 validation summary");
            output.WriteLine("contract: " + FormatText(dto.Contract));
            output.WriteLine("version: " + dto.Version.ToString(CultureInfo.InvariantCulture));
            output.WriteLine("units: " + FormatText(dto.Metadata?.Units));
            output.WriteLine("sourceFixtureName: " + FormatText(dto.Metadata?.SourceFixtureName));
            output.WriteLine("centerlineCount: " + FormatCount(dto.CenterlinePoints));
            output.WriteLine("frameCount: " + FormatCount(dto.Frames));
            output.WriteLine("lineCount: " + FormatCount(dto.Lines));
            output.WriteLine("boxCount: " + FormatCount(dto.Boxes));
            output.WriteLine("trainPose: " + (dto.TrainPose == null ? "absent" : "present"));

            if (diagnostics.Count == 0)
            {
                output.WriteLine("validation: PASS");
                return;
            }

            output.WriteLine(
                "validation: FAIL (" +
                diagnostics.Count.ToString(CultureInfo.InvariantCulture) +
                " issue(s))");

            foreach (DebugViewportSnapshotV1ValidationDiagnostic diagnostic in diagnostics)
            {
                output.WriteLine(
                    "- " +
                    diagnostic.Code +
                    " at " +
                    diagnostic.Path +
                    ": " +
                    diagnostic.Message);
            }
        }

        private static string FormatText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<missing>" : value;
        }

        private static string FormatCount<T>(T[]? values)
        {
            return values == null ? "<missing>" : values.Length.ToString(CultureInfo.InvariantCulture);
        }

        private static bool IsReadOrParseException(Exception ex)
        {
            return ex is IOException ||
                   ex is UnauthorizedAccessException ||
                   ex is ArgumentException ||
                   ex is NotSupportedException ||
                   ex is JsonException;
        }
    }
}
