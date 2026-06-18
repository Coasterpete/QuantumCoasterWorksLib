using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Quantum.IO.BankingProfile.V1;

namespace Quantum.Debug
{
    public static class BankingProfileBrowserCommand
    {
        public const string CommandName = "banking-profile-browser";

        internal const string DefaultFileName = "banking-profile.browser.html";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private static readonly JsonSerializerOptions PayloadJsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public static int Run(string? diagnosticsJsonPath = null, string? outputHtmlPath = null)
        {
            return Run(diagnosticsJsonPath, outputHtmlPath, Console.Out);
        }

        public static int Run(string? diagnosticsJsonPath, string? outputHtmlPath, TextWriter output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            string resolvedDiagnosticsJsonPath = ResolveDiagnosticsJsonPath(diagnosticsJsonPath);
            string resolvedOutputHtmlPath = ResolveOutputPath(resolvedDiagnosticsJsonPath, outputHtmlPath);

            if (!File.Exists(resolvedDiagnosticsJsonPath))
            {
                output.WriteLine("BankingProfile diagnostics JSON was not found.");
                output.WriteLine("Expected: " + resolvedDiagnosticsJsonPath);
                output.WriteLine(
                    "Generate it with: dotnet run --project Quantum.Debug -- banking-profile-diagnostics " +
                    ToDisplayPath(Path.GetRelativePath(Environment.CurrentDirectory, resolvedDiagnosticsJsonPath)));
                return 1;
            }

            JsonElement artifactRoot;

            try
            {
                string json = File.ReadAllText(resolvedDiagnosticsJsonPath);
                _ = BankingProfileDiagnosticsExportV1Json.Deserialize(json);

                using JsonDocument document = JsonDocument.Parse(json);
                artifactRoot = document.RootElement.Clone();
            }
            catch (Exception ex) when (IsReadOrParseException(ex))
            {
                output.WriteLine("Failed to read BankingProfile diagnostics JSON.");
                output.WriteLine(ex.Message);
                return 1;
            }

            string html = BuildHtml(resolvedDiagnosticsJsonPath, resolvedOutputHtmlPath, artifactRoot);

            string? parentDirectory = Path.GetDirectoryName(resolvedOutputHtmlPath);
            if (!string.IsNullOrEmpty(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            File.WriteAllText(resolvedOutputHtmlPath, html, Utf8NoBom);
            output.WriteLine($"Wrote BankingProfile browser viewer to '{resolvedOutputHtmlPath}'.");
            return 0;
        }

        private static string ResolveDiagnosticsJsonPath(string? diagnosticsJsonPath)
        {
            if (string.IsNullOrWhiteSpace(diagnosticsJsonPath))
            {
                return Path.GetFullPath(
                    Path.Combine(Environment.CurrentDirectory, BankingProfileDiagnosticsCommand.DefaultRelativeOutputPath));
            }

            return Path.GetFullPath(diagnosticsJsonPath);
        }

        private static string ResolveOutputPath(string diagnosticsJsonPath, string? outputHtmlPath)
        {
            if (string.IsNullOrWhiteSpace(outputHtmlPath))
            {
                string? parentDirectory = Path.GetDirectoryName(diagnosticsJsonPath);
                return Path.Combine(parentDirectory ?? Environment.CurrentDirectory, DefaultFileName);
            }

            return Path.GetFullPath(outputHtmlPath);
        }

        private static string BuildHtml(
            string diagnosticsJsonPath,
            string outputHtmlPath,
            JsonElement artifactRoot)
        {
            var payload = new BankingProfileBrowserPayload
            {
                SourcePath = ToViewerRelativeDisplayPath(diagnosticsJsonPath, outputHtmlPath),
                OutputPath = ToViewerRelativeDisplayPath(outputHtmlPath, outputHtmlPath),
                Artifact = artifactRoot
            };

            string payloadJson = EscapeJsonForHtml(JsonSerializer.Serialize(payload, PayloadJsonOptions));
            var builder = new StringBuilder();

            builder.AppendLine("<!doctype html>");
            builder.AppendLine("<html lang=\"en\">");
            builder.AppendLine("<head>");
            builder.AppendLine("  <meta charset=\"utf-8\">");
            builder.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            builder.AppendLine("  <title>Quantum BankingProfile Browser Viewer</title>");
            AppendStyles(builder);
            builder.AppendLine("</head>");
            builder.AppendLine("<body>");
            builder.AppendLine("  <main>");
            builder.AppendLine("    <header class=\"page-header\">");
            builder.AppendLine("      <p class=\"eyebrow\">Quantum Debug</p>");
            builder.AppendLine("      <h1>BankingProfile Browser Viewer</h1>");
            builder.AppendLine("      <p class=\"lede\">Static local diagnostics for BankingProfile roll samples. It does not change TrackFrame, TrackEvaluator, DebugViewportSnapshotV1, TrainPoseExportV1, or runtime banking behavior.</p>");
            builder.AppendLine("      <dl class=\"run-summary\" id=\"runSummary\"></dl>");
            builder.AppendLine("    </header>");
            builder.AppendLine("    <section class=\"viewer-shell\" aria-label=\"BankingProfile diagnostics inspector\">");
            builder.AppendLine("      <aside class=\"controls\">");
            builder.AppendLine("        <label class=\"field-label\" for=\"fileInput\">Load local JSON</label>");
            builder.AppendLine("        <input id=\"fileInput\" type=\"file\" accept=\"application/json,.json\">");
            builder.AppendLine("        <section class=\"summary-panel\" aria-labelledby=\"metadata-title\">");
            builder.AppendLine("          <h2 id=\"metadata-title\">Profile Metadata</h2>");
            builder.AppendLine("          <dl id=\"metadataList\"></dl>");
            builder.AppendLine("        </section>");
            builder.AppendLine("        <section class=\"summary-panel\" aria-labelledby=\"summary-title\">");
            builder.AppendLine("          <h2 id=\"summary-title\">Summary Metrics</h2>");
            builder.AppendLine("          <dl id=\"summaryMetrics\"></dl>");
            builder.AppendLine("        </section>");
            builder.AppendLine("        <section class=\"legend-panel\" aria-labelledby=\"legend-title\">");
            builder.AppendLine("          <h2 id=\"legend-title\">Roll Slope Severity</h2>");
            builder.AppendLine("          <div class=\"legend-row\"><span class=\"severity-dot severity-zero\"></span><span>0.02 rad/m or less</span></div>");
            builder.AppendLine("          <div class=\"legend-row\"><span class=\"severity-dot severity-low\"></span><span>0.02 to 0.05 rad/m</span></div>");
            builder.AppendLine("          <div class=\"legend-row\"><span class=\"severity-dot severity-moderate\"></span><span>0.05 to 0.10 rad/m</span></div>");
            builder.AppendLine("          <div class=\"legend-row\"><span class=\"severity-dot severity-high\"></span><span>0.10 rad/m or more</span></div>");
            builder.AppendLine("        </section>");
            builder.AppendLine("      </aside>");
            builder.AppendLine("      <section class=\"content-panel\" aria-labelledby=\"report-title\">");
            builder.AppendLine("        <div class=\"content-header\">");
            builder.AppendLine("          <div>");
            builder.AppendLine("            <h2 id=\"report-title\">BankingProfile Diagnostics</h2>");
            builder.AppendLine("            <p id=\"statusLine\"></p>");
            builder.AppendLine("          </div>");
            builder.AppendLine("          <p class=\"axis-note\">Distance is meters. Roll is radians and degrees.</p>");
            builder.AppendLine("        </div>");
            builder.AppendLine("        <section class=\"chart-panel\" aria-labelledby=\"roll-chart-title\">");
            builder.AppendLine("          <h3 id=\"roll-chart-title\">Roll Angle vs Station Distance</h3>");
            builder.AppendLine("          <svg id=\"rollChart\" role=\"img\" aria-label=\"Roll angle versus station distance\"></svg>");
            builder.AppendLine("        </section>");
            builder.AppendLine("        <section class=\"chart-panel\" aria-labelledby=\"slope-chart-title\">");
            builder.AppendLine("          <h3 id=\"slope-chart-title\">Roll Slope vs Station Distance</h3>");
            builder.AppendLine("          <svg id=\"slopeChart\" role=\"img\" aria-label=\"Roll slope versus station distance\"></svg>");
            builder.AppendLine("        </section>");
            builder.AppendLine("        <section class=\"table-panel\" aria-labelledby=\"table-title\">");
            builder.AppendLine("          <h3 id=\"table-title\">Roll Samples</h3>");
            builder.AppendLine("          <div class=\"table-scroll\">");
            builder.AppendLine("            <table>");
            builder.AppendLine("              <thead>");
            builder.AppendLine("                <tr>");
            builder.AppendLine("                  <th>Index</th>");
            builder.AppendLine("                  <th>Distance</th>");
            builder.AppendLine("                  <th>Roll Radians</th>");
            builder.AppendLine("                  <th>Roll Degrees</th>");
            builder.AppendLine("                  <th>Roll Slope</th>");
            builder.AppendLine("                  <th>Interpolation</th>");
            builder.AppendLine("                  <th>Source</th>");
            builder.AppendLine("                  <th>Interval</th>");
            builder.AppendLine("                </tr>");
            builder.AppendLine("              </thead>");
            builder.AppendLine("              <tbody id=\"sampleRows\"></tbody>");
            builder.AppendLine("            </table>");
            builder.AppendLine("          </div>");
            builder.AppendLine("        </section>");
            builder.AppendLine("      </section>");
            builder.AppendLine("    </section>");
            builder.AppendLine("  </main>");
            builder.AppendLine("  <script id=\"banking-profile-data\" type=\"application/json\">");
            builder.AppendLine(payloadJson);
            builder.AppendLine("  </script>");
            AppendScript(builder);
            builder.AppendLine("</body>");
            builder.AppendLine("</html>");
            return builder.ToString();
        }

        private static void AppendStyles(StringBuilder builder)
        {
            builder.AppendLine("  <style>");
            builder.AppendLine("    * { box-sizing: border-box; }");
            builder.AppendLine("    body { margin: 0; font-family: Segoe UI, Arial, sans-serif; color: #172033; background: #f7f8fb; }");
            builder.AppendLine("    main { width: min(1360px, calc(100% - 32px)); margin: 0 auto; padding: 24px 0 36px; }");
            builder.AppendLine("    .page-header { margin-bottom: 16px; }");
            builder.AppendLine("    .eyebrow { margin: 0 0 5px; color: #0f766e; font-size: 12px; font-weight: 700; text-transform: uppercase; letter-spacing: 0; }");
            builder.AppendLine("    h1 { margin: 0 0 9px; font-size: 26px; line-height: 1.2; }");
            builder.AppendLine("    h2 { margin: 0; font-size: 17px; line-height: 1.25; }");
            builder.AppendLine("    h3 { margin: 0 0 10px; font-size: 14px; line-height: 1.25; }");
            builder.AppendLine("    p { line-height: 1.45; }");
            builder.AppendLine("    .lede { max-width: 1000px; margin: 0 0 12px; color: #526173; }");
            builder.AppendLine("    .run-summary { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 8px; max-width: 980px; margin: 12px 0 0; }");
            builder.AppendLine("    .run-summary div { min-width: 0; padding: 9px 11px; border: 1px solid #cbd5e1; border-radius: 8px; background: #ffffff; overflow-wrap: anywhere; }");
            builder.AppendLine("    dt { margin: 0 0 4px; color: #627086; font-size: 12px; font-weight: 700; text-transform: uppercase; letter-spacing: 0; }");
            builder.AppendLine("    dd { margin: 0; color: #172033; font-size: 13px; }");
            builder.AppendLine("    .viewer-shell { display: grid; grid-template-columns: minmax(260px, 320px) minmax(0, 1fr); gap: 16px; align-items: start; }");
            builder.AppendLine("    .controls, .content-panel { border: 1px solid #cbd5e1; border-radius: 8px; background: #ffffff; }");
            builder.AppendLine("    .controls { padding: 14px; }");
            builder.AppendLine("    .field-label { display: block; margin: 0 0 6px; color: #334155; font-size: 13px; font-weight: 700; }");
            builder.AppendLine("    input[type=file] { width: 100%; min-height: 36px; margin: 0 0 14px; font: 13px Segoe UI, Arial, sans-serif; }");
            builder.AppendLine("    .summary-panel, .legend-panel { border-top: 1px solid #e2e8f0; padding-top: 13px; }");
            builder.AppendLine("    .summary-panel { margin-bottom: 14px; }");
            builder.AppendLine("    .summary-panel h2, .legend-panel h2 { margin-bottom: 9px; }");
            builder.AppendLine("    #metadataList, #summaryMetrics { display: grid; grid-template-columns: minmax(100px, 132px) minmax(0, 1fr); gap: 8px 10px; margin: 0; }");
            builder.AppendLine("    #metadataList dd, #summaryMetrics dd { min-width: 0; overflow-wrap: anywhere; }");
            builder.AppendLine("    .legend-row { display: flex; align-items: center; gap: 8px; min-height: 24px; color: #334155; font-size: 13px; }");
            builder.AppendLine("    .content-panel { min-width: 0; overflow: hidden; }");
            builder.AppendLine("    .content-header { display: flex; gap: 12px; align-items: start; justify-content: space-between; padding: 14px 16px; border-bottom: 1px solid #e2e8f0; }");
            builder.AppendLine("    .content-header p { margin: 5px 0 0; color: #526173; font-size: 13px; }");
            builder.AppendLine("    .axis-note { flex: 0 0 auto; max-width: 250px; text-align: right; }");
            builder.AppendLine("    .chart-panel, .table-panel { padding: 14px 16px 16px; }");
            builder.AppendLine("    .chart-panel { border-bottom: 1px solid #e2e8f0; }");
            builder.AppendLine("    #rollChart, #slopeChart { display: block; width: 100%; min-height: 260px; background: #fbfcfe; border: 1px solid #e2e8f0; border-radius: 8px; }");
            builder.AppendLine("    .chart-grid { stroke: #e4e9f0; stroke-width: 1; }");
            builder.AppendLine("    .axis-line { stroke: #64748b; stroke-width: 1.2; }");
            builder.AppendLine("    .chart-label { fill: #334155; font: 12px Segoe UI, Arial, sans-serif; }");
            builder.AppendLine("    .chart-path { fill: none; stroke: #0f766e; stroke-width: 2.5; stroke-linecap: round; stroke-linejoin: round; }");
            builder.AppendLine("    .slope-path { stroke: #2563eb; }");
            builder.AppendLine("    .sample-point { fill: #ffffff; stroke: #0f766e; stroke-width: 2; }");
            builder.AppendLine("    .slope-point { fill: #ffffff; stroke: #2563eb; stroke-width: 2; }");
            builder.AppendLine("    .key-marker { stroke: #7c3aed; stroke-width: 1.4; stroke-dasharray: 4 4; }");
            builder.AppendLine("    .transition-marker { stroke: #d97706; stroke-width: 1.4; stroke-dasharray: 2 5; }");
            builder.AppendLine("    .marker-label { fill: #475569; font: 11px Segoe UI, Arial, sans-serif; }");
            builder.AppendLine("    .severity-dot { display: inline-block; width: 11px; height: 11px; border-radius: 999px; border: 1px solid rgba(15, 23, 42, 0.18); vertical-align: -1px; }");
            builder.AppendLine("    .severity-zero { fill: #cbd5e1; background: #cbd5e1; }");
            builder.AppendLine("    .severity-low { fill: #16a34a; background: #16a34a; }");
            builder.AppendLine("    .severity-moderate { fill: #d97706; background: #d97706; }");
            builder.AppendLine("    .severity-high { fill: #dc2626; background: #dc2626; }");
            builder.AppendLine("    .table-scroll { max-height: 540px; overflow: auto; border: 1px solid #e2e8f0; border-radius: 8px; }");
            builder.AppendLine("    table { width: 100%; border-collapse: collapse; font-size: 12px; }");
            builder.AppendLine("    th, td { padding: 7px 9px; border-bottom: 1px solid #e2e8f0; text-align: right; white-space: nowrap; }");
            builder.AppendLine("    th { position: sticky; top: 0; z-index: 1; color: #334155; background: #f8fafc; font-weight: 700; }");
            builder.AppendLine("    th:first-child, td:first-child, th:nth-child(6), td:nth-child(6), th:nth-child(7), td:nth-child(7), th:nth-child(8), td:nth-child(8) { text-align: left; }");
            builder.AppendLine("    tr:hover td { background: #f8fafc; }");
            builder.AppendLine("    .empty-message { padding: 20px; color: #64748b; }");
            builder.AppendLine("    @media (max-width: 860px) { main { width: min(100% - 20px, 1360px); padding-top: 18px; } .viewer-shell { grid-template-columns: 1fr; } .content-header { display: block; } .axis-note { text-align: left; max-width: none; } th, td { padding: 7px; } }");
            builder.AppendLine("  </style>");
        }

        private static void AppendScript(StringBuilder builder)
        {
            builder.AppendLine("  <script>");
            builder.AppendLine("""
    (function () {
      'use strict';
      const CONTRACT = 'quantum.banking_profile_diagnostics';
      const VERSION = 1;
      let payload = JSON.parse(document.getElementById('banking-profile-data').textContent);
      let artifact = payload.artifact;
      const fileInput = document.getElementById('fileInput');
      const runSummary = document.getElementById('runSummary');
      const metadataList = document.getElementById('metadataList');
      const summaryMetrics = document.getElementById('summaryMetrics');
      const reportTitle = document.getElementById('report-title');
      const statusLine = document.getElementById('statusLine');
      const rollChart = document.getElementById('rollChart');
      const slopeChart = document.getElementById('slopeChart');
      const sampleRows = document.getElementById('sampleRows');

      function text(value) {
        return document.createTextNode(value == null || value === '' ? 'n/a' : String(value));
      }
      function clear(element) {
        while (element.firstChild) {
          element.removeChild(element.firstChild);
        }
      }
      function finite(value, fallback) {
        const number = Number(value);
        return Number.isFinite(number) ? number : fallback;
      }
      function formatNumber(value, digits) {
        const number = finite(value, null);
        return number === null ? 'n/a' : number.toFixed(digits);
      }
      function formatDistance(value) {
        return formatNumber(value, 3) + ' m';
      }
      function formatRadians(value) {
        return formatNumber(value, 6) + ' rad';
      }
      function formatDegrees(value) {
        return formatNumber(value, 3) + ' deg';
      }
      function formatSlope(value) {
        return value == null ? 'n/a' : formatNumber(value, 6) + ' rad/m';
      }
      function addDefinition(list, label, value) {
        const dt = document.createElement('dt');
        const dd = document.createElement('dd');
        dt.appendChild(text(label));
        dd.appendChild(text(value));
        list.appendChild(dt);
        list.appendChild(dd);
      }
      function addSummaryCard(label, value) {
        const wrapper = document.createElement('div');
        const dt = document.createElement('dt');
        const dd = document.createElement('dd');
        dt.appendChild(text(label));
        dd.appendChild(text(value));
        wrapper.appendChild(dt);
        wrapper.appendChild(dd);
        runSummary.appendChild(wrapper);
      }
      function samples() {
        return Array.isArray(artifact && artifact.samples) ? artifact.samples : [];
      }
      function summary() {
        return artifact && artifact.summaryMetrics ? artifact.summaryMetrics : {};
      }
      function metadata() {
        return artifact && artifact.metadata ? artifact.metadata : {};
      }
      function severityForSlope(value) {
        const magnitude = Math.abs(finite(value, 0));
        if (magnitude >= 0.10) { return 'high'; }
        if (magnitude >= 0.05) { return 'moderate'; }
        if (magnitude > 0.02) { return 'low'; }
        return 'zero';
      }
      function severityDot(value) {
        const dot = document.createElement('span');
        dot.className = 'severity-dot severity-' + severityForSlope(value);
        dot.setAttribute('title', 'Roll slope: ' + formatSlope(value));
        return dot;
      }
      function verifyArtifact(candidate) {
        if (!candidate || candidate.contract !== CONTRACT || candidate.version !== VERSION) {
          throw new Error('Expected ' + CONTRACT + ' v' + VERSION + '.');
        }
        if (!candidate.metadata || !candidate.summaryMetrics || !Array.isArray(candidate.samples)) {
          throw new Error('Expected BankingProfile diagnostics metadata, summaryMetrics, and samples.');
        }
      }
      function renderRunSummary() {
        const s = summary();
        clear(runSummary);
        addSummaryCard('Source', payload.sourcePath || 'local file');
        addSummaryCard('Contract', artifact.contract || 'n/a');
        addSummaryCard('Sample count', String(s.sampleCount || 0));
        addSummaryCard('Max slope', formatSlope(s.maxAbsoluteRollSlopeRadPerMeter));
      }
      function renderMetadata() {
        const m = metadata();
        clear(metadataList);
        addDefinition(metadataList, 'Source', m.sourceName || 'n/a');
        addDefinition(metadataList, 'Keys', String(m.profileKeyCount || 0));
        addDefinition(metadataList, 'Units', m.units || 'n/a');
        addDefinition(metadataList, 'Distance', m.distanceUnit || 'n/a');
        addDefinition(metadataList, 'Roll angle', m.rollAngleUnits || 'n/a');
        addDefinition(metadataList, 'Roll slope', m.rollSlopeUnit || 'n/a');
      }
      function renderSummary() {
        const s = summary();
        clear(summaryMetrics);
        addDefinition(summaryMetrics, 'Samples', String(s.sampleCount || 0));
        addDefinition(summaryMetrics, 'Min roll rad', formatRadians(s.minRollRadians));
        addDefinition(summaryMetrics, 'Max roll rad', formatRadians(s.maxRollRadians));
        addDefinition(summaryMetrics, 'Min roll deg', formatDegrees(s.minRollDegrees));
        addDefinition(summaryMetrics, 'Max roll deg', formatDegrees(s.maxRollDegrees));
        addDefinition(summaryMetrics, 'Max slope', formatSlope(s.maxAbsoluteRollSlopeRadPerMeter));
      }
      function svgElement(name) {
        return document.createElementNS('http://www.w3.org/2000/svg', name);
      }
      function chartRanges(points, valueKey) {
        let minX = Number.POSITIVE_INFINITY;
        let maxX = Number.NEGATIVE_INFINITY;
        let minY = Number.POSITIVE_INFINITY;
        let maxY = Number.NEGATIVE_INFINITY;
        points.forEach(function (point) {
          const x = finite(point.distance, null);
          const y = finite(point[valueKey], null);
          if (x == null || y == null) { return; }
          minX = Math.min(minX, x);
          maxX = Math.max(maxX, x);
          minY = Math.min(minY, y);
          maxY = Math.max(maxY, y);
        });
        if (!Number.isFinite(minX) || !Number.isFinite(maxX)) {
          minX = 0;
          maxX = 1;
        }
        if (!Number.isFinite(minY) || !Number.isFinite(maxY)) {
          minY = 0;
          maxY = 1;
        }
        if (minX === maxX) {
          minX -= 0.5;
          maxX += 0.5;
        }
        if (minY === maxY) {
          const pad = Math.max(Math.abs(minY) * 0.1, 0.1);
          minY -= pad;
          maxY += pad;
        }
        const yPad = (maxY - minY) * 0.12;
        return { minX: minX, maxX: maxX, minY: minY - yPad, maxY: maxY + yPad };
      }
      function collectMarkers(points) {
        const keyMap = new Map();
        const transitions = [];
        points.forEach(function (sample, index) {
          const interval = sample.sourceInterval || {};
          [
            { keyIndex: interval.startKeyIndex, distance: interval.startDistance },
            { keyIndex: interval.endKeyIndex, distance: interval.endDistance }
          ].forEach(function (marker) {
            if (!Number.isFinite(Number(marker.distance))) { return; }
            const id = String(marker.keyIndex) + ':' + String(marker.distance);
            if (!keyMap.has(id)) {
              keyMap.set(id, { keyIndex: marker.keyIndex, distance: Number(marker.distance) });
            }
          });
          if (index > 0) {
            const previous = points[index - 1];
            const changed =
              previous.interpolationMode !== sample.interpolationMode ||
              previous.sourceKind !== sample.sourceKind ||
              (previous.sourceInterval && sample.sourceInterval &&
                previous.sourceInterval.endKeyIndex !== sample.sourceInterval.endKeyIndex);
            if (changed && Number.isFinite(Number(sample.distance))) {
              transitions.push({
                distance: Number(sample.distance),
                mode: sample.interpolationMode || 'n/a',
                sourceKind: sample.sourceKind || 'n/a'
              });
            }
          }
        });
        return {
          keys: Array.from(keyMap.values()).sort(function (a, b) { return a.distance - b.distance; }),
          transitions: transitions
        };
      }
      function drawChart(svg, points, valueKey, options) {
        clear(svg);
        const width = 880;
        const height = 260;
        const padLeft = 66;
        const padRight = 20;
        const padTop = 22;
        const padBottom = 42;
        const plotWidth = width - padLeft - padRight;
        const plotHeight = height - padTop - padBottom;
        const validPoints = points.filter(function (point) {
          return Number.isFinite(Number(point.distance)) && Number.isFinite(Number(point[valueKey]));
        });
        const ranges = chartRanges(validPoints, valueKey);
        const markers = collectMarkers(points);
        svg.setAttribute('viewBox', '0 0 ' + width + ' ' + height);

        function xFor(distance) {
          return padLeft + ((distance - ranges.minX) / (ranges.maxX - ranges.minX)) * plotWidth;
        }
        function yFor(value) {
          return padTop + (1 - ((value - ranges.minY) / (ranges.maxY - ranges.minY))) * plotHeight;
        }
        function addLine(x1, y1, x2, y2, className) {
          const line = svgElement('line');
          line.setAttribute('x1', String(x1));
          line.setAttribute('y1', String(y1));
          line.setAttribute('x2', String(x2));
          line.setAttribute('y2', String(y2));
          line.setAttribute('class', className);
          svg.appendChild(line);
          return line;
        }
        function addLabel(x, y, value, anchor) {
          const label = svgElement('text');
          label.setAttribute('x', String(x));
          label.setAttribute('y', String(y));
          label.setAttribute('class', 'chart-label');
          if (anchor) {
            label.setAttribute('text-anchor', anchor);
          }
          label.textContent = value;
          svg.appendChild(label);
        }

        for (let i = 0; i <= 4; i++) {
          const x = padLeft + (plotWidth * i / 4);
          const y = padTop + (plotHeight * i / 4);
          addLine(x, padTop, x, padTop + plotHeight, 'chart-grid');
          addLine(padLeft, y, padLeft + plotWidth, y, 'chart-grid');
          addLabel(x, height - 16, formatNumber(ranges.minX + (ranges.maxX - ranges.minX) * i / 4, 1), 'middle');
          addLabel(8, y + 4, formatNumber(ranges.maxY - (ranges.maxY - ranges.minY) * i / 4, options.digits), null);
        }
        addLine(padLeft, padTop + plotHeight, padLeft + plotWidth, padTop + plotHeight, 'axis-line');
        addLine(padLeft, padTop, padLeft, padTop + plotHeight, 'axis-line');
        addLabel(padLeft + plotWidth / 2, height - 2, 'station distance (m)', 'middle');
        addLabel(8, 14, options.yLabel, null);

        markers.keys.forEach(function (marker) {
          const x = xFor(marker.distance);
          addLine(x, padTop, x, padTop + plotHeight, 'key-marker');
          addLabel(x + 4, padTop + 12, 'key ' + marker.keyIndex, null);
        });
        markers.transitions.forEach(function (transition) {
          const x = xFor(transition.distance);
          addLine(x, padTop, x, padTop + plotHeight, 'transition-marker');
          addLabel(x + 4, padTop + plotHeight - 6, transition.mode, null);
        });

        if (validPoints.length > 0) {
          const path = svgElement('path');
          const d = validPoints.map(function (point, index) {
            const prefix = index === 0 ? 'M ' : 'L ';
            return prefix + formatNumber(xFor(Number(point.distance)), 3) + ' ' + formatNumber(yFor(Number(point[valueKey])), 3);
          }).join(' ');
          path.setAttribute('d', d);
          path.setAttribute('class', 'chart-path ' + options.pathClass);
          svg.appendChild(path);
        }

        validPoints.forEach(function (point) {
          const circle = svgElement('circle');
          circle.setAttribute('cx', String(xFor(Number(point.distance))));
          circle.setAttribute('cy', String(yFor(Number(point[valueKey]))));
          circle.setAttribute('r', '4');
          circle.setAttribute('class', options.pointClass);
          circle.setAttribute('data-sample-index', String(point.sampleIndex));
          circle.setAttribute('data-interpolation-mode', point.interpolationMode || '');
          circle.appendChild(svgElement('title')).textContent =
            'sample ' + point.sampleIndex + ', s=' + formatDistance(point.distance) + ', ' +
            options.yLabel + '=' + options.format(point[valueKey]) + ', mode=' + (point.interpolationMode || 'n/a');
          svg.appendChild(circle);
        });
      }
      function appendCell(row, value, alignLeft) {
        const cell = document.createElement('td');
        if (alignLeft) {
          cell.style.textAlign = 'left';
        }
        cell.appendChild(text(value));
        row.appendChild(cell);
        return cell;
      }
      function renderSamples() {
        const rows = samples();
        clear(sampleRows);
        rows.forEach(function (sample, index) {
          const interval = sample.sourceInterval || {};
          const row = document.createElement('tr');
          appendCell(row, sample.sampleIndex == null ? index : sample.sampleIndex, false);
          appendCell(row, formatDistance(sample.distance), false);
          appendCell(row, formatRadians(sample.rollRadians), false);
          appendCell(row, formatDegrees(sample.rollDegrees), false);
          const slopeCell = appendCell(row, '', false);
          slopeCell.appendChild(severityDot(sample.approximateRollSlopeRadPerMeter));
          slopeCell.appendChild(text(' ' + formatSlope(sample.approximateRollSlopeRadPerMeter)));
          appendCell(row, sample.interpolationMode || 'n/a', true);
          appendCell(row, sample.sourceKind || 'n/a', true);
          appendCell(
            row,
            '#' + interval.startKeyIndex + ' ' + formatDistance(interval.startDistance) +
              ' to #' + interval.endKeyIndex + ' ' + formatDistance(interval.endDistance),
            true);
          sampleRows.appendChild(row);
        });
        if (rows.length === 0) {
          const row = document.createElement('tr');
          const cell = document.createElement('td');
          cell.colSpan = 8;
          cell.className = 'empty-message';
          cell.appendChild(text('No BankingProfile samples are present.'));
          row.appendChild(cell);
          sampleRows.appendChild(row);
        }
      }
      function renderCharts() {
        const rows = samples();
        drawChart(
          rollChart,
          rows,
          'rollRadians',
          {
            yLabel: 'roll radians',
            digits: 3,
            pathClass: '',
            pointClass: 'sample-point',
            format: formatRadians
          });
        drawChart(
          slopeChart,
          rows,
          'approximateRollSlopeRadPerMeter',
          {
            yLabel: 'roll slope rad/m',
            digits: 4,
            pathClass: 'slope-path',
            pointClass: 'slope-point',
            format: formatSlope
          });
      }
      function renderAll() {
        try {
          verifyArtifact(artifact);
          const m = metadata();
          const s = summary();
          reportTitle.textContent = m.sourceName || 'BankingProfile Diagnostics';
          statusLine.textContent =
            'samples=' + (s.sampleCount || 0) +
            ', rollRad=[' + formatNumber(s.minRollRadians, 6) + ', ' + formatNumber(s.maxRollRadians, 6) + ']' +
            ', rollDeg=[' + formatNumber(s.minRollDegrees, 3) + ', ' + formatNumber(s.maxRollDegrees, 3) + ']' +
            ', maxSlope=' + formatSlope(s.maxAbsoluteRollSlopeRadPerMeter);
          renderRunSummary();
          renderMetadata();
          renderSummary();
          renderCharts();
          renderSamples();
        } catch (error) {
          clear(runSummary);
          clear(metadataList);
          clear(summaryMetrics);
          clear(sampleRows);
          clear(rollChart);
          clear(slopeChart);
          reportTitle.textContent = 'Invalid artifact';
          statusLine.textContent = error.message;
        }
      }
      fileInput.addEventListener('change', function () {
        const file = fileInput.files && fileInput.files[0];
        if (!file) { return; }
        const reader = new FileReader();
        reader.onload = function () {
          try {
            const parsed = JSON.parse(String(reader.result));
            verifyArtifact(parsed);
            artifact = parsed;
            payload.sourcePath = file.name;
            renderAll();
          } catch (error) {
            statusLine.textContent = error.message;
          }
        };
        reader.readAsText(file);
      });
      renderAll();
    }());
""");
            builder.AppendLine("  </script>");
        }

        private static string ToDisplayPath(string path)
        {
            return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }

        private static string ToViewerRelativeDisplayPath(string path, string outputHtmlPath)
        {
            string outputDirectory = Path.GetDirectoryName(outputHtmlPath) ?? Path.GetPathRoot(outputHtmlPath) ?? ".";
            return ToDisplayPath(Path.GetRelativePath(outputDirectory, path));
        }

        private static string EscapeJsonForHtml(string json)
        {
            return json.Replace("</", "<\\/", StringComparison.Ordinal);
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

        private sealed class BankingProfileBrowserPayload
        {
            public string SourcePath { get; set; } = string.Empty;

            public string OutputPath { get; set; } = string.Empty;

            public JsonElement Artifact { get; set; }
        }
    }
}
