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
            builder.AppendLine("    @media (max-width: 760px) { main { width: min(100% - 20px, 1040px); padding-top: 18px; } .section-header { align-items: flex-start; } .section-body { grid-template-columns: 1fr; } .section-index { text-align: left; } }");
            builder.AppendLine("  </style>");
        }

        private static void AppendSummary(StringBuilder builder, string label, string value)
        {
            builder.AppendLine("        <div><dt>" + Escape(label) + "</dt><dd>" + Escape(value) + "</dd></div>");
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
    }
}
