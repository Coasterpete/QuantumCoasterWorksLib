using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using Quantum.IO.DistanceInspection.V1;

namespace Quantum.Debug
{
    public static class DistanceInspectionBrowserCommand
    {
        public const string CommandName = "distance-inspection-browser";

        internal const string DefaultRelativeOutputPath =
            "artifacts/track/distance-inspection.browser.html";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public static int Run(string? outputPath = null)
        {
            return Run(outputPath, Console.Out);
        }

        public static int Run(string? outputPath, TextWriter output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            DistanceInspectionSnapshotV1Dto dto = DistanceInspectionJsonCommand.BuildSample();
            return WriteHtml(dto, outputPath, output);
        }

        public static int Run(string inputJsonPath, string outputHtmlPath)
        {
            return Run(inputJsonPath, outputHtmlPath, Console.Out);
        }

        public static int Run(string inputJsonPath, string outputHtmlPath, TextWriter output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            DistanceInspectionSnapshotV1Dto dto;

            try
            {
                string resolvedInputJsonPath = Path.GetFullPath(inputJsonPath);
                string json = File.ReadAllText(resolvedInputJsonPath);
                dto = DistanceInspectionSnapshotV1Json.Deserialize(json);
            }
            catch (Exception ex) when (IsReadOrParseException(ex))
            {
                output.WriteLine("Failed to read DistanceInspectionSnapshotV1 JSON.");
                output.WriteLine(ex.Message);
                return 1;
            }

            return WriteHtml(dto, outputHtmlPath, output);
        }

        private static int WriteHtml(
            DistanceInspectionSnapshotV1Dto dto,
            string? outputPath,
            TextWriter output)
        {
            string html = BuildHtml(dto);
            string resolvedOutputPath = ResolveOutputPath(outputPath);
            string? parentDirectory = Path.GetDirectoryName(resolvedOutputPath);

            if (!string.IsNullOrEmpty(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            File.WriteAllText(resolvedOutputPath, html, Utf8NoBom);
            output.WriteLine($"Wrote distance inspection browser preview to '{resolvedOutputPath}'.");
            return 0;
        }

        private static string BuildHtml(DistanceInspectionSnapshotV1Dto dto)
        {
            var builder = new StringBuilder();

            builder.AppendLine("<!doctype html>");
            builder.AppendLine("<html lang=\"en\">");
            builder.AppendLine("<head>");
            builder.AppendLine("  <meta charset=\"utf-8\">");
            builder.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            builder.AppendLine("  <title>Quantum Distance Inspection Browser Preview</title>");
            AppendStyles(builder);
            builder.AppendLine("</head>");
            builder.AppendLine("<body>");
            builder.AppendLine("  <main>");
            builder.AppendLine("    <header class=\"page-header\">");
            builder.AppendLine("      <p class=\"eyebrow\">Quantum Debug</p>");
            builder.AppendLine("      <h1>Distance Inspection Browser Preview</h1>");
            builder.AppendLine("      <dl class=\"summary-grid\">");
            AppendSummary(builder, "Contract", dto.Contract);
            AppendSummary(builder, "Version", dto.Version.ToString(CultureInfo.InvariantCulture));
            AppendSummary(builder, "Inspected distance", FormatNumber(dto.Distance) + " m");
            AppendSummary(builder, "Sections", dto.Sections.Length.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("      </dl>");
            builder.AppendLine("    </header>");
            AppendTimeline(builder, dto);
            builder.AppendLine("    <section class=\"sections\" aria-label=\"Ordered distance inspection sections\">");

            for (int i = 0; i < dto.Sections.Length; i++)
            {
                AppendSection(builder, dto.Sections[i], i);
            }

            builder.AppendLine("    </section>");
            builder.AppendLine("  </main>");
            builder.AppendLine("</body>");
            builder.AppendLine("</html>");
            return builder.ToString();
        }

        private static void AppendStyles(StringBuilder builder)
        {
            builder.AppendLine("  <style>");
            builder.AppendLine("    * { box-sizing: border-box; }");
            builder.AppendLine("    body { margin: 0; font-family: Segoe UI, Arial, sans-serif; color: #1f2937; background: #f6f7f9; }");
            builder.AppendLine("    main { width: min(1040px, calc(100% - 32px)); margin: 0 auto; padding: 24px 0 36px; }");
            builder.AppendLine("    .page-header { margin-bottom: 16px; }");
            builder.AppendLine("    .eyebrow { margin: 0 0 5px; color: #0f766e; font-size: 12px; font-weight: 700; text-transform: uppercase; letter-spacing: 0; }");
            builder.AppendLine("    h1 { margin: 0 0 12px; font-size: 26px; line-height: 1.2; }");
            builder.AppendLine("    h2 { margin: 0; font-size: 18px; line-height: 1.25; }");
            builder.AppendLine("    h3 { margin: 0 0 8px; font-size: 14px; line-height: 1.25; }");
            builder.AppendLine("    .summary-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 8px; margin: 0; }");
            builder.AppendLine("    .summary-grid div, .section-card { border: 1px solid #cbd5e1; border-radius: 8px; background: #ffffff; }");
            builder.AppendLine("    .summary-grid div { min-width: 0; padding: 9px 11px; overflow-wrap: anywhere; }");
            builder.AppendLine("    dt { margin: 0 0 4px; color: #64748b; font-size: 12px; font-weight: 700; text-transform: uppercase; letter-spacing: 0; }");
            builder.AppendLine("    dd { margin: 0; color: #1f2937; font-size: 13px; }");
            builder.AppendLine("    .timeline-panel { margin: 0 0 16px; border: 1px solid #cbd5e1; border-radius: 8px; background: #ffffff; overflow: hidden; }");
            builder.AppendLine("    .timeline-header { display: flex; gap: 12px; align-items: flex-end; justify-content: space-between; padding: 13px 15px; border-bottom: 1px solid #e2e8f0; background: #fbfcfe; }");
            builder.AppendLine("    .timeline-header p { margin: 3px 0 0; color: #64748b; font-size: 13px; }");
            builder.AppendLine("    .timeline-rows { display: grid; }");
            builder.AppendLine("    .timeline-row { display: grid; grid-template-columns: minmax(90px, 130px) minmax(74px, 96px) minmax(0, 1fr); gap: 12px; align-items: center; padding: 10px 15px; border-bottom: 1px solid #e2e8f0; }");
            builder.AppendLine("    .timeline-row:last-child { border-bottom: 0; }");
            builder.AppendLine("    .timeline-kind { min-width: 0; color: #1f2937; font-size: 13px; font-weight: 700; overflow-wrap: anywhere; }");
            builder.AppendLine("    .timeline-range { color: #475569; font-size: 12px; font-variant-numeric: tabular-nums; white-space: nowrap; }");
            builder.AppendLine("    .timeline-track { position: relative; height: 28px; border: 1px solid #cbd5e1; border-radius: 8px; background: linear-gradient(90deg, #f8fafc, #eef6f4); overflow: hidden; }");
            builder.AppendLine("    .timeline-track::before { content: ''; position: absolute; left: 50%; top: 0; bottom: 0; width: 1px; background: #dbe3ea; }");
            builder.AppendLine("    .timeline-bar { position: absolute; top: 9px; height: 10px; border-radius: 999px; background: #2563eb; }");
            builder.AppendLine("    .timeline-cursor { position: absolute; top: 3px; bottom: 3px; width: 2px; transform: translateX(-1px); background: #dc2626; box-shadow: 0 0 0 1px rgba(220, 38, 38, 0.18); }");
            builder.AppendLine("    .timeline-empty { padding: 12px 15px; color: #64748b; font-size: 13px; }");
            builder.AppendLine("    .sections { display: grid; gap: 12px; }");
            builder.AppendLine("    .section-card { overflow: hidden; }");
            builder.AppendLine("    .section-header { display: flex; gap: 12px; align-items: center; justify-content: space-between; padding: 13px 15px; border-bottom: 1px solid #e2e8f0; background: #fbfcfe; }");
            builder.AppendLine("    .section-index { flex: 0 0 auto; min-width: 58px; color: #64748b; font-size: 12px; font-weight: 700; text-align: right; }");
            builder.AppendLine("    .section-body { display: grid; grid-template-columns: minmax(220px, 280px) minmax(0, 1fr); gap: 16px; padding: 15px; }");
            builder.AppendLine("    .section-facts { display: grid; grid-template-columns: minmax(92px, 116px) minmax(0, 1fr); gap: 8px 10px; align-content: start; margin: 0; }");
            builder.AppendLine("    .section-facts dd { min-width: 0; overflow-wrap: anywhere; }");
            builder.AppendLine("    .diagnostic-badge { display: inline-flex; align-items: center; min-height: 20px; padding: 2px 8px; border: 1px solid transparent; border-radius: 999px; font-size: 12px; font-weight: 700; line-height: 1.2; }");
            builder.AppendLine("    .diagnostic-none { color: #166534; background: #dcfce7; border-color: #bbf7d0; }");
            builder.AppendLine("    .diagnostic-attention { color: #92400e; background: #fef3c7; border-color: #fde68a; }");
            builder.AppendLine("    .channels { margin: 0; padding-left: 18px; color: #334155; font-size: 13px; line-height: 1.6; }");
            builder.AppendLine("    .table-scroll { overflow: auto; border: 1px solid #e2e8f0; border-radius: 8px; }");
            builder.AppendLine("    table { width: 100%; border-collapse: collapse; font-size: 13px; }");
            builder.AppendLine("    th, td { padding: 8px 10px; border-bottom: 1px solid #e2e8f0; text-align: left; white-space: nowrap; }");
            builder.AppendLine("    th { color: #334155; background: #f8fafc; font-weight: 700; }");
            builder.AppendLine("    tr:last-child td { border-bottom: 0; }");
            builder.AppendLine("    td:nth-child(2) { text-align: right; font-variant-numeric: tabular-nums; }");
            builder.AppendLine("    @media (max-width: 760px) { main { width: min(100% - 20px, 1040px); padding-top: 18px; } .timeline-header { align-items: flex-start; flex-direction: column; } .timeline-row { grid-template-columns: 1fr; gap: 6px; } .section-header { align-items: flex-start; } .section-body { grid-template-columns: 1fr; } .section-index { text-align: left; } }");
            builder.AppendLine("  </style>");
        }

        private static void AppendSummary(StringBuilder builder, string label, string value)
        {
            builder.AppendLine("        <div><dt>" + Escape(label) + "</dt><dd>" + Escape(value) + "</dd></div>");
        }

        private static void AppendTimeline(StringBuilder builder, DistanceInspectionSnapshotV1Dto dto)
        {
            TimelineScale scale = CreateTimelineScale(dto);

            builder.AppendLine("    <section class=\"timeline-panel\" aria-label=\"Distance inspection timeline\">");
            builder.AppendLine("      <div class=\"timeline-header\">");
            builder.AppendLine("        <div>");
            builder.AppendLine("          <h2>Visual Timeline</h2>");
            builder.AppendLine(
                "          <p>Timeline range [" +
                Escape(FormatNumber(scale.Min)) +
                ", " +
                Escape(FormatNumber(scale.Max)) +
                "] m</p>");
            builder.AppendLine("        </div>");
            builder.AppendLine(
                "        <p>Inspected distance " +
                Escape(FormatNumber(dto.Distance)) +
                " m</p>");
            builder.AppendLine("      </div>");
            builder.AppendLine("      <div class=\"timeline-rows\">");

            if (dto.Sections.Length == 0)
            {
                builder.AppendLine("        <div class=\"timeline-empty\">No active sections</div>");
            }
            else
            {
                for (int i = 0; i < dto.Sections.Length; i++)
                {
                    AppendTimelineRow(builder, dto.Sections[i], dto.Distance, scale);
                }
            }

            builder.AppendLine("      </div>");
            builder.AppendLine("    </section>");
        }

        private static void AppendTimelineRow(
            StringBuilder builder,
            DistanceInspectionSectionV1Dto section,
            double inspectedDistance,
            TimelineScale scale)
        {
            string rangeText = "[" + FormatNumber(section.StartX) + ", " + FormatNumber(section.EndX) + "]";
            double rangeStart = System.Math.Min(section.StartX, section.EndX);
            double rangeEnd = System.Math.Max(section.StartX, section.EndX);
            double rangeStartPercent = ToTimelinePercent(rangeStart, scale);
            double rangeEndPercent = ToTimelinePercent(rangeEnd, scale);
            double barLeft = ClampPercent(System.Math.Min(rangeStartPercent, rangeEndPercent));
            double barWidth = ClampPercent(System.Math.Max(rangeStartPercent, rangeEndPercent) - barLeft);
            double cursorLeft = ToTimelinePercent(inspectedDistance, scale);
            string label = section.Kind + " section " + rangeText + ", inspected distance " +
                FormatNumber(inspectedDistance) + " m";

            builder.AppendLine("        <div class=\"timeline-row\">");
            builder.AppendLine("          <div class=\"timeline-kind\">" + Escape(section.Kind) + "</div>");
            builder.AppendLine("          <div class=\"timeline-range\">" + Escape(rangeText) + "</div>");
            builder.AppendLine("          <div class=\"timeline-track\" aria-label=\"" + Escape(label) + "\">");
            builder.AppendLine(
                "            <div class=\"timeline-bar\" style=\"left: " +
                FormatPercent(barLeft) +
                "%; width: " +
                FormatPercent(barWidth) +
                "%;\"></div>");
            builder.AppendLine(
                "            <div class=\"timeline-cursor\" style=\"left: " +
                FormatPercent(cursorLeft) +
                "%;\"></div>");
            builder.AppendLine("          </div>");
            builder.AppendLine("        </div>");
        }

        private static void AppendSection(
            StringBuilder builder,
            DistanceInspectionSectionV1Dto section,
            int index)
        {
            builder.AppendLine("      <article class=\"section-card\">");
            builder.AppendLine("        <div class=\"section-header\">");
            builder.AppendLine("          <h2>" + Escape(section.Kind) + " section</h2>");
            builder.AppendLine("          <span class=\"section-index\">Section " + (index + 1).ToString(CultureInfo.InvariantCulture) + "</span>");
            builder.AppendLine("        </div>");
            builder.AppendLine("        <div class=\"section-body\">");
            builder.AppendLine("          <dl class=\"section-facts\">");
            AppendFact(builder, "Kind", section.Kind);
            AppendFact(builder, "Domain", section.Domain);
            AppendFact(builder, "Range", "[" + FormatNumber(section.StartX) + ", " + FormatNumber(section.EndX) + "]");
            AppendDiagnosticFact(builder, section.Diagnostic);
            builder.AppendLine("          </dl>");
            builder.AppendLine("          <div>");
            builder.AppendLine("            <h3>Channels</h3>");
            AppendChannels(builder, section.Channels);
            builder.AppendLine("            <h3>Channel Values</h3>");
            AppendChannelValues(builder, section.ChannelValues);
            builder.AppendLine("          </div>");
            builder.AppendLine("        </div>");
            builder.AppendLine("      </article>");
        }

        private static void AppendFact(StringBuilder builder, string label, string value)
        {
            builder.AppendLine("            <dt>" + Escape(label) + "</dt><dd>" + Escape(value) + "</dd>");
        }

        private static void AppendDiagnosticFact(StringBuilder builder, string diagnostic)
        {
            string className = string.Equals(diagnostic, "None", StringComparison.Ordinal)
                ? "diagnostic-badge diagnostic-none"
                : "diagnostic-badge diagnostic-attention";

            builder.AppendLine(
                "            <dt>Diagnostic</dt><dd><span class=\"" +
                className +
                "\">" +
                Escape(diagnostic) +
                "</span></dd>");
        }

        private static void AppendChannels(StringBuilder builder, string[] channels)
        {
            builder.AppendLine("            <ul class=\"channels\">");

            if (channels.Length == 0)
            {
                builder.AppendLine("              <li>None</li>");
            }
            else
            {
                for (int i = 0; i < channels.Length; i++)
                {
                    builder.AppendLine("              <li>" + Escape(channels[i]) + "</li>");
                }
            }

            builder.AppendLine("            </ul>");
        }

        private static void AppendChannelValues(
            StringBuilder builder,
            DistanceInspectionChannelValueV1Dto[] channelValues)
        {
            builder.AppendLine("            <div class=\"table-scroll\">");
            builder.AppendLine("              <table>");
            builder.AppendLine("                <thead>");
            builder.AppendLine("                  <tr><th>Channel</th><th>Value</th></tr>");
            builder.AppendLine("                </thead>");
            builder.AppendLine("                <tbody>");

            if (channelValues.Length == 0)
            {
                builder.AppendLine("                  <tr><td colspan=\"2\">No channel values</td></tr>");
            }
            else
            {
                for (int i = 0; i < channelValues.Length; i++)
                {
                    DistanceInspectionChannelValueV1Dto channelValue = channelValues[i];
                    builder.AppendLine(
                        "                  <tr><td>" +
                        Escape(channelValue.Channel) +
                        "</td><td>" +
                        Escape(FormatNumber(channelValue.Value)) +
                        "</td></tr>");
                }
            }

            builder.AppendLine("                </tbody>");
            builder.AppendLine("              </table>");
            builder.AppendLine("            </div>");
        }

        private static string ResolveOutputPath(string? outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, DefaultRelativeOutputPath));
            }

            return Path.GetFullPath(outputPath);
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string FormatPercent(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static TimelineScale CreateTimelineScale(DistanceInspectionSnapshotV1Dto dto)
        {
            double min = dto.Distance;
            double max = dto.Distance;
            bool hasSection = false;

            for (int i = 0; i < dto.Sections.Length; i++)
            {
                DistanceInspectionSectionV1Dto section = dto.Sections[i];
                double rangeStart = System.Math.Min(section.StartX, section.EndX);
                double rangeEnd = System.Math.Max(section.StartX, section.EndX);

                if (!hasSection)
                {
                    min = rangeStart;
                    max = rangeEnd;
                    hasSection = true;
                }
                else
                {
                    min = System.Math.Min(min, rangeStart);
                    max = System.Math.Max(max, rangeEnd);
                }
            }

            return new TimelineScale(min, max);
        }

        private static double ToTimelinePercent(double value, TimelineScale scale)
        {
            double span = scale.Max - scale.Min;

            if (!(span > 0.0))
            {
                return 0.0;
            }

            return ClampPercent((value - scale.Min) / span * 100.0);
        }

        private static double ClampPercent(double value)
        {
            if (double.IsNaN(value) || double.IsNegativeInfinity(value))
            {
                return 0.0;
            }

            if (double.IsPositiveInfinity(value))
            {
                return 100.0;
            }

            return System.Math.Min(100.0, System.Math.Max(0.0, value));
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

        private readonly struct TimelineScale
        {
            public TimelineScale(double min, double max)
            {
                Min = min;
                Max = max;
            }

            public double Min { get; }

            public double Max { get; }
        }
    }
}
