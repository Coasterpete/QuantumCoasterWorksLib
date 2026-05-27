using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using Quantum.IO.DebugViewport.V1;

namespace Quantum.Debug
{
    public static class DebugViewportSnapshotV1SvgCommand
    {
        private const double DefaultCanvasWidth = 960.0;
        private const double DefaultCanvasHeight = 540.0;
        private const double Padding = 48.0;
        private const double HeaderHeight = 72.0;
        private const double MinimumWorldSpan = 1.0;
        private const double FrameTickWorldLength = 1.5;
        private const int MaxFrameTicks = 16;

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public static int Run(string snapshotJsonPath, string? outputSvgPath = null)
        {
            return Run(snapshotJsonPath, outputSvgPath, Console.Out);
        }

        public static int Run(string snapshotJsonPath, string? outputSvgPath, TextWriter output)
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

            string resolvedInputPath;
            DebugViewportSnapshotV1Dto dto;
            try
            {
                resolvedInputPath = Path.GetFullPath(snapshotJsonPath);
                string json = File.ReadAllText(resolvedInputPath);
                dto = DebugViewportSnapshotV1Json.Deserialize(json);
            }
            catch (Exception ex) when (IsReadOrParseException(ex))
            {
                output.WriteLine("Failed to read DebugViewportSnapshotV1 JSON.");
                output.WriteLine(ex.Message);
                return 1;
            }

            IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics =
                DebugViewportSnapshotV1Validator.Validate(dto);
            if (diagnostics.Count > 0)
            {
                output.WriteLine("DebugViewportSnapshotV1 validation failed; SVG was not written.");
                for (int i = 0; i < diagnostics.Count; i++)
                {
                    DebugViewportSnapshotV1ValidationDiagnostic diagnostic = diagnostics[i];
                    output.WriteLine(
                        "- " +
                        diagnostic.Code +
                        " at " +
                        diagnostic.Path +
                        ": " +
                        diagnostic.Message);
                }

                return 1;
            }

            string resolvedOutputPath = ResolveOutputPath(resolvedInputPath, outputSvgPath);
            string? parentDirectory = Path.GetDirectoryName(resolvedOutputPath);
            if (!string.IsNullOrEmpty(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            string svg = BuildSvg(dto, Path.GetFileName(resolvedInputPath));
            File.WriteAllText(resolvedOutputPath, svg, Utf8NoBom);
            output.WriteLine($"Wrote DebugViewportSnapshotV1 SVG preview to '{resolvedOutputPath}'.");
            return 0;
        }

        internal static string BuildSvg(DebugViewportSnapshotV1Dto dto, string? sourceFileName = null)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            Projection projection = Projection.Create(dto);
            var builder = new StringBuilder();

            builder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            builder.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"960\" height=\"540\" viewBox=\"0 0 960 540\" role=\"img\" aria-labelledby=\"title desc\">");
            builder.AppendLine("  <title id=\"title\">Quantum DebugViewportSnapshotV1 Technical Preview</title>");
            builder.AppendLine("  <desc id=\"desc\">Simple top-down centerline preview generated from renderer-neutral backend debug data.</desc>");
            builder.AppendLine("  <rect width=\"960\" height=\"540\" fill=\"#f8fafc\" />");
            builder.AppendLine("  <text x=\"24\" y=\"30\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"18\" font-weight=\"600\" fill=\"#111827\">DebugViewportSnapshotV1 Technical Preview</text>");

            string metadata = BuildMetadataLine(dto, sourceFileName);
            builder.AppendLine(
                "  <text x=\"24\" y=\"54\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"12\" fill=\"#475569\">" +
                Escape(metadata) +
                "</text>");

            AppendGrid(builder);
            AppendCenterline(builder, dto, projection);
            AppendFrameTicks(builder, dto, projection);
            AppendLegend(builder);

            builder.AppendLine("</svg>");
            return builder.ToString();
        }

        private static string ResolveOutputPath(string resolvedInputPath, string? outputSvgPath)
        {
            if (!string.IsNullOrWhiteSpace(outputSvgPath))
            {
                return Path.GetFullPath(outputSvgPath);
            }

            string inputDirectory = Path.GetDirectoryName(resolvedInputPath) ?? Environment.CurrentDirectory;
            string inputFileName = Path.GetFileNameWithoutExtension(resolvedInputPath);
            if (string.IsNullOrWhiteSpace(inputFileName))
            {
                inputFileName = "DebugViewportSnapshotV1";
            }

            return Path.GetFullPath(Path.Combine(inputDirectory, inputFileName + ".svg"));
        }

        private static string BuildMetadataLine(DebugViewportSnapshotV1Dto dto, string? sourceFileName)
        {
            DebugViewportMetadataV1Dto? metadata = dto.Metadata;
            string source = string.IsNullOrWhiteSpace(metadata?.SourceFixtureName)
                ? "<unspecified source>"
                : metadata!.SourceFixtureName!;
            string units = string.IsNullOrWhiteSpace(metadata?.Units) ? "<unknown units>" : metadata!.Units;
            string file = string.IsNullOrWhiteSpace(sourceFileName) ? "<memory>" : sourceFileName!;

            return "source: " +
                   source +
                   " | file: " +
                   file +
                   " | units: " +
                   units +
                   " | centerline points: " +
                   FormatCount(dto.CenterlinePoints) +
                   " | frames: " +
                   FormatCount(dto.Frames);
        }

        private static void AppendGrid(StringBuilder builder)
        {
            builder.AppendLine("  <rect x=\"48\" y=\"72\" width=\"864\" height=\"420\" fill=\"#ffffff\" stroke=\"#cbd5e1\" stroke-width=\"1\" />");

            for (int i = 1; i < 4; i++)
            {
                double x = Padding + ((DefaultCanvasWidth - Padding * 2.0) * i / 4.0);
                builder.AppendLine(
                    "  <line x1=\"" +
                    Format(x) +
                    "\" y1=\"72\" x2=\"" +
                    Format(x) +
                    "\" y2=\"492\" stroke=\"#e2e8f0\" stroke-width=\"1\" />");
            }

            for (int i = 1; i < 4; i++)
            {
                double y = HeaderHeight + ((DefaultCanvasHeight - HeaderHeight - Padding) * i / 4.0);
                builder.AppendLine(
                    "  <line x1=\"48\" y1=\"" +
                    Format(y) +
                    "\" x2=\"912\" y2=\"" +
                    Format(y) +
                    "\" stroke=\"#e2e8f0\" stroke-width=\"1\" />");
            }

            builder.AppendLine(
                "  <text x=\"54\" y=\"486\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"11\" fill=\"#64748b\">top-down X/Z preview</text>");
        }

        private static void AppendCenterline(
            StringBuilder builder,
            DebugViewportSnapshotV1Dto dto,
            Projection projection)
        {
            DebugViewportCenterlinePointV1Dto[] points = dto.CenterlinePoints ?? Array.Empty<DebugViewportCenterlinePointV1Dto>();
            if (points.Length == 0)
            {
                builder.AppendLine("  <text x=\"72\" y=\"120\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"14\" fill=\"#b91c1c\">No centerline points found.</text>");
                return;
            }

            builder.Append("  <polyline fill=\"none\" stroke=\"#0f766e\" stroke-width=\"3\" stroke-linecap=\"round\" stroke-linejoin=\"round\" points=\"");
            for (int i = 0; i < points.Length; i++)
            {
                SvgPoint point = projection.Project(points[i].Position);
                if (i > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(Format(point.X));
                builder.Append(',');
                builder.Append(Format(point.Y));
            }

            builder.AppendLine("\" />");

            for (int i = 0; i < points.Length; i++)
            {
                SvgPoint point = projection.Project(points[i].Position);
                builder.AppendLine(
                    "  <circle cx=\"" +
                    Format(point.X) +
                    "\" cy=\"" +
                    Format(point.Y) +
                    "\" r=\"3\" fill=\"#0f766e\" />");
            }
        }

        private static void AppendFrameTicks(
            StringBuilder builder,
            DebugViewportSnapshotV1Dto dto,
            Projection projection)
        {
            DebugViewportFrameV1Dto[] frames = dto.Frames ?? Array.Empty<DebugViewportFrameV1Dto>();
            if (frames.Length == 0)
            {
                return;
            }

            int step = System.Math.Max(1, (int)System.Math.Ceiling(frames.Length / (double)MaxFrameTicks));
            for (int i = 0; i < frames.Length; i += step)
            {
                DebugViewportFrameV1Dto frame = frames[i];
                AppendAxisTick(builder, projection, frame.Position, frame.Tangent, "#2563eb", FrameTickWorldLength);
                AppendAxisTick(builder, projection, frame.Position, frame.Binormal, "#7c3aed", FrameTickWorldLength * 0.8);
            }
        }

        private static void AppendAxisTick(
            StringBuilder builder,
            Projection projection,
            DebugViewportVector3dV1Dto origin,
            DebugViewportVector3dV1Dto direction,
            string stroke,
            double worldLength)
        {
            double length = System.Math.Sqrt(direction.X * direction.X + direction.Z * direction.Z);
            if (length <= 1e-9)
            {
                return;
            }

            var end = new DebugViewportVector3dV1Dto
            {
                X = origin.X + direction.X / length * worldLength,
                Y = origin.Y,
                Z = origin.Z + direction.Z / length * worldLength
            };

            SvgPoint startPoint = projection.Project(origin);
            SvgPoint endPoint = projection.Project(end);
            builder.AppendLine(
                "  <line x1=\"" +
                Format(startPoint.X) +
                "\" y1=\"" +
                Format(startPoint.Y) +
                "\" x2=\"" +
                Format(endPoint.X) +
                "\" y2=\"" +
                Format(endPoint.Y) +
                "\" stroke=\"" +
                stroke +
                "\" stroke-width=\"1.5\" stroke-linecap=\"round\" />");
        }

        private static void AppendLegend(StringBuilder builder)
        {
            builder.AppendLine("  <g font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"11\" fill=\"#475569\">");
            builder.AppendLine("    <line x1=\"720\" y1=\"32\" x2=\"752\" y2=\"32\" stroke=\"#0f766e\" stroke-width=\"3\" stroke-linecap=\"round\" />");
            builder.AppendLine("    <text x=\"760\" y=\"36\">centerline</text>");
            builder.AppendLine("    <line x1=\"720\" y1=\"50\" x2=\"752\" y2=\"50\" stroke=\"#2563eb\" stroke-width=\"1.5\" stroke-linecap=\"round\" />");
            builder.AppendLine("    <text x=\"760\" y=\"54\">tangent ticks</text>");
            builder.AppendLine("    <line x1=\"820\" y1=\"50\" x2=\"852\" y2=\"50\" stroke=\"#7c3aed\" stroke-width=\"1.5\" stroke-linecap=\"round\" />");
            builder.AppendLine("    <text x=\"860\" y=\"54\">binormal ticks</text>");
            builder.AppendLine("  </g>");
        }

        private static string FormatCount<T>(T[]? values)
        {
            return values == null ? "0" : values.Length.ToString(CultureInfo.InvariantCulture);
        }

        private static string Format(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Escape(string value)
        {
            return WebUtility.HtmlEncode(value);
        }

        private static bool IsReadOrParseException(Exception ex)
        {
            return ex is IOException ||
                   ex is UnauthorizedAccessException ||
                   ex is ArgumentException ||
                   ex is NotSupportedException ||
                   ex is JsonException ||
                   ex is InvalidOperationException;
        }

        private readonly struct SvgPoint
        {
            public SvgPoint(double x, double y)
            {
                X = x;
                Y = y;
            }

            public double X { get; }

            public double Y { get; }
        }

        private sealed class Projection
        {
            private readonly double _minX;
            private readonly double _minZ;
            private readonly double _scale;
            private readonly double _offsetX;
            private readonly double _offsetY;
            private readonly double _scaledDepth;

            private Projection(double minX, double minZ, double scale, double offsetX, double offsetY, double scaledDepth)
            {
                _minX = minX;
                _minZ = minZ;
                _scale = scale;
                _offsetX = offsetX;
                _offsetY = offsetY;
                _scaledDepth = scaledDepth;
            }

            public static Projection Create(DebugViewportSnapshotV1Dto dto)
            {
                double minX = double.PositiveInfinity;
                double maxX = double.NegativeInfinity;
                double minZ = double.PositiveInfinity;
                double maxZ = double.NegativeInfinity;

                IncludeCenterline(dto.CenterlinePoints, ref minX, ref maxX, ref minZ, ref maxZ);
                IncludeFrames(dto.Frames, ref minX, ref maxX, ref minZ, ref maxZ);
                IncludeLines(dto.Lines, ref minX, ref maxX, ref minZ, ref maxZ);

                if (double.IsPositiveInfinity(minX) || double.IsNegativeInfinity(maxX))
                {
                    minX = -0.5;
                    maxX = 0.5;
                    minZ = -0.5;
                    maxZ = 0.5;
                }

                ExpandDegenerateRange(ref minX, ref maxX);
                ExpandDegenerateRange(ref minZ, ref maxZ);

                double drawWidth = DefaultCanvasWidth - Padding * 2.0;
                double drawHeight = DefaultCanvasHeight - HeaderHeight - Padding;
                double worldWidth = maxX - minX;
                double worldDepth = maxZ - minZ;
                double scale = System.Math.Min(drawWidth / worldWidth, drawHeight / worldDepth);
                double offsetX = Padding + (drawWidth - worldWidth * scale) / 2.0;
                double offsetY = HeaderHeight + (drawHeight - worldDepth * scale) / 2.0;

                return new Projection(minX, minZ, scale, offsetX, offsetY, worldDepth * scale);
            }

            public SvgPoint Project(DebugViewportVector3dV1Dto vector)
            {
                double x = _offsetX + (vector.X - _minX) * _scale;
                double y = _offsetY + _scaledDepth - (vector.Z - _minZ) * _scale;
                return new SvgPoint(x, y);
            }

            private static void IncludeCenterline(
                DebugViewportCenterlinePointV1Dto[]? points,
                ref double minX,
                ref double maxX,
                ref double minZ,
                ref double maxZ)
            {
                if (points == null)
                {
                    return;
                }

                for (int i = 0; i < points.Length; i++)
                {
                    Include(points[i].Position, ref minX, ref maxX, ref minZ, ref maxZ);
                }
            }

            private static void IncludeFrames(
                DebugViewportFrameV1Dto[]? frames,
                ref double minX,
                ref double maxX,
                ref double minZ,
                ref double maxZ)
            {
                if (frames == null)
                {
                    return;
                }

                for (int i = 0; i < frames.Length; i++)
                {
                    Include(frames[i].Position, ref minX, ref maxX, ref minZ, ref maxZ);
                }
            }

            private static void IncludeLines(
                DebugViewportLineSegmentV1Dto[]? lines,
                ref double minX,
                ref double maxX,
                ref double minZ,
                ref double maxZ)
            {
                if (lines == null)
                {
                    return;
                }

                for (int i = 0; i < lines.Length; i++)
                {
                    Include(lines[i].Start, ref minX, ref maxX, ref minZ, ref maxZ);
                    Include(lines[i].End, ref minX, ref maxX, ref minZ, ref maxZ);
                }
            }

            private static void Include(
                DebugViewportVector3dV1Dto vector,
                ref double minX,
                ref double maxX,
                ref double minZ,
                ref double maxZ)
            {
                minX = System.Math.Min(minX, vector.X);
                maxX = System.Math.Max(maxX, vector.X);
                minZ = System.Math.Min(minZ, vector.Z);
                maxZ = System.Math.Max(maxZ, vector.Z);
            }

            private static void ExpandDegenerateRange(ref double min, ref double max)
            {
                double span = max - min;
                if (span >= MinimumWorldSpan)
                {
                    return;
                }

                double center = (min + max) * 0.5;
                min = center - MinimumWorldSpan * 0.5;
                max = center + MinimumWorldSpan * 0.5;
            }
        }
    }
}
