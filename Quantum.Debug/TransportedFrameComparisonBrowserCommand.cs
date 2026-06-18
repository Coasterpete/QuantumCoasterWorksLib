using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Quantum.IO.TransportedFrameComparison.V1;

namespace Quantum.Debug
{
    public static class TransportedFrameComparisonBrowserCommand
    {
        public const string CommandName = "transported-frame-comparison-browser";

        internal const string DefaultFileName = "transported-frame-comparison.browser.html";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private static readonly JsonSerializerOptions PayloadJsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public static int Run(string? comparisonJsonPath = null, string? outputHtmlPath = null)
        {
            return Run(comparisonJsonPath, outputHtmlPath, Console.Out);
        }

        public static int Run(string? comparisonJsonPath, string? outputHtmlPath, TextWriter output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            string resolvedComparisonJsonPath = ResolveComparisonJsonPath(comparisonJsonPath);
            string resolvedOutputHtmlPath = ResolveOutputPath(resolvedComparisonJsonPath, outputHtmlPath);

            if (!File.Exists(resolvedComparisonJsonPath))
            {
                output.WriteLine("Transported frame comparison JSON was not found.");
                output.WriteLine("Expected: " + resolvedComparisonJsonPath);
                output.WriteLine(
                    "Generate it with: dotnet run --project Quantum.Debug -- transported-frame-comparison " +
                    ToDisplayPath(Path.GetRelativePath(Environment.CurrentDirectory, resolvedComparisonJsonPath)));
                return 1;
            }

            JsonElement artifactRoot;

            try
            {
                string json = File.ReadAllText(resolvedComparisonJsonPath);
                _ = TransportedFrameComparisonDiagnosticsExportV1Json.Deserialize(json);

                using JsonDocument document = JsonDocument.Parse(json);
                artifactRoot = document.RootElement.Clone();
            }
            catch (Exception ex) when (IsReadOrParseException(ex))
            {
                output.WriteLine("Failed to read transported frame comparison JSON.");
                output.WriteLine(ex.Message);
                return 1;
            }

            string html = BuildHtml(resolvedComparisonJsonPath, resolvedOutputHtmlPath, artifactRoot);

            string? parentDirectory = Path.GetDirectoryName(resolvedOutputHtmlPath);
            if (!string.IsNullOrEmpty(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            File.WriteAllText(resolvedOutputHtmlPath, html, Utf8NoBom);
            output.WriteLine($"Wrote transported frame comparison browser viewer to '{resolvedOutputHtmlPath}'.");
            return 0;
        }

        private static string ResolveComparisonJsonPath(string? comparisonJsonPath)
        {
            if (string.IsNullOrWhiteSpace(comparisonJsonPath))
            {
                return Path.GetFullPath(
                    Path.Combine(Environment.CurrentDirectory, TransportedFrameComparisonCommand.DefaultRelativeOutputPath));
            }

            return Path.GetFullPath(comparisonJsonPath);
        }

        private static string ResolveOutputPath(string comparisonJsonPath, string? outputHtmlPath)
        {
            if (string.IsNullOrWhiteSpace(outputHtmlPath))
            {
                string? parentDirectory = Path.GetDirectoryName(comparisonJsonPath);
                return Path.Combine(parentDirectory ?? Environment.CurrentDirectory, DefaultFileName);
            }

            return Path.GetFullPath(outputHtmlPath);
        }

        private static string BuildHtml(
            string comparisonJsonPath,
            string outputHtmlPath,
            JsonElement artifactRoot)
        {
            var payload = new TransportedFrameComparisonBrowserPayload
            {
                SourcePath = ToViewerRelativeDisplayPath(comparisonJsonPath, outputHtmlPath),
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
            builder.AppendLine("  <title>Quantum Transported Frame Comparison Browser Viewer</title>");
            AppendStyles(builder);
            builder.AppendLine("</head>");
            builder.AppendLine("<body>");
            builder.AppendLine("  <main>");
            builder.AppendLine("    <header class=\"page-header\">");
            builder.AppendLine("      <p class=\"eyebrow\">Quantum Debug</p>");
            builder.AppendLine("      <h1>Transported Frame Comparison</h1>");
            builder.AppendLine("      <dl class=\"run-summary\" id=\"runSummary\"></dl>");
            builder.AppendLine("    </header>");
            builder.AppendLine("    <section class=\"viewer-shell\" aria-label=\"Transported frame comparison inspector\">");
            builder.AppendLine("      <aside class=\"controls\">");
            builder.AppendLine("        <label class=\"field-label\" for=\"reportSelect\">Report</label>");
            builder.AppendLine("        <select id=\"reportSelect\"></select>");
            builder.AppendLine("        <label class=\"field-label\" for=\"fileInput\">Load local JSON</label>");
            builder.AppendLine("        <input id=\"fileInput\" type=\"file\" accept=\"application/json,.json\">");
            builder.AppendLine("        <section class=\"summary-panel\" aria-labelledby=\"summary-title\">");
            builder.AppendLine("          <h2 id=\"summary-title\">Summary Metrics</h2>");
            builder.AppendLine("          <dl id=\"summaryMetrics\"></dl>");
            builder.AppendLine("        </section>");
            builder.AppendLine("        <section class=\"legend-panel\" aria-labelledby=\"legend-title\">");
            builder.AppendLine("          <h2 id=\"legend-title\">Delta Severity</h2>");
            builder.AppendLine("          <div class=\"legend-row\"><span class=\"severity-dot severity-zero\"></span><span>0.1 degrees or less</span></div>");
            builder.AppendLine("          <div class=\"legend-row\"><span class=\"severity-dot severity-low\"></span><span>0.1 to 5 degrees</span></div>");
            builder.AppendLine("          <div class=\"legend-row\"><span class=\"severity-dot severity-moderate\"></span><span>5 to 30 degrees</span></div>");
            builder.AppendLine("          <div class=\"legend-row\"><span class=\"severity-dot severity-high\"></span><span>30 degrees or more</span></div>");
            builder.AppendLine("        </section>");
            builder.AppendLine("      </aside>");
            builder.AppendLine("      <section class=\"content-panel\" aria-labelledby=\"report-title\">");
            builder.AppendLine("        <div class=\"content-header\">");
            builder.AppendLine("          <div>");
            builder.AppendLine("            <h2 id=\"report-title\">Report</h2>");
            builder.AppendLine("            <p id=\"statusLine\"></p>");
            builder.AppendLine("          </div>");
            builder.AppendLine("          <p class=\"axis-note\">Delta metrics are degrees.</p>");
            builder.AppendLine("        </div>");
            builder.AppendLine("        <section class=\"chart-panel\" aria-labelledby=\"chart-title\">");
            builder.AppendLine("          <h3 id=\"chart-title\">Normal, Binormal, Frame, Matrix Severity</h3>");
            builder.AppendLine("          <svg id=\"severityChart\" role=\"img\" aria-label=\"Per-sample delta severity chart\"></svg>");
            builder.AppendLine("        </section>");
            builder.AppendLine("        <section class=\"table-panel\" aria-labelledby=\"table-title\">");
            builder.AppendLine("          <h3 id=\"table-title\">Per-sample Delta Table</h3>");
            builder.AppendLine("          <div class=\"table-scroll\">");
            builder.AppendLine("            <table>");
            builder.AppendLine("              <thead>");
            builder.AppendLine("                <tr>");
            builder.AppendLine("                  <th>Index</th>");
            builder.AppendLine("                  <th>Distance</th>");
            builder.AppendLine("                  <th>Tangent</th>");
            builder.AppendLine("                  <th>Normal</th>");
            builder.AppendLine("                  <th>Binormal</th>");
            builder.AppendLine("                  <th>Frame</th>");
            builder.AppendLine("                  <th>Roll</th>");
            builder.AppendLine("                  <th>Abs Roll</th>");
            builder.AppendLine("                  <th>Matrix</th>");
            builder.AppendLine("                </tr>");
            builder.AppendLine("              </thead>");
            builder.AppendLine("              <tbody id=\"sampleRows\"></tbody>");
            builder.AppendLine("            </table>");
            builder.AppendLine("          </div>");
            builder.AppendLine("        </section>");
            builder.AppendLine("      </section>");
            builder.AppendLine("    </section>");
            builder.AppendLine("  </main>");
            builder.AppendLine("  <script id=\"comparison-data\" type=\"application/json\">");
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
            builder.AppendLine("    h1 { margin: 0 0 10px; font-size: 26px; line-height: 1.2; }");
            builder.AppendLine("    h2 { margin: 0; font-size: 17px; line-height: 1.25; }");
            builder.AppendLine("    h3 { margin: 0 0 10px; font-size: 14px; line-height: 1.25; }");
            builder.AppendLine("    p { line-height: 1.45; }");
            builder.AppendLine("    .run-summary { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 8px; max-width: 980px; margin: 12px 0 0; }");
            builder.AppendLine("    .run-summary div { min-width: 0; padding: 9px 11px; border: 1px solid #cbd5e1; border-radius: 8px; background: #ffffff; overflow-wrap: anywhere; }");
            builder.AppendLine("    dt { margin: 0 0 4px; color: #627086; font-size: 12px; font-weight: 700; text-transform: uppercase; letter-spacing: 0; }");
            builder.AppendLine("    dd { margin: 0; color: #172033; font-size: 13px; }");
            builder.AppendLine("    .viewer-shell { display: grid; grid-template-columns: minmax(260px, 320px) minmax(0, 1fr); gap: 16px; align-items: start; }");
            builder.AppendLine("    .controls, .content-panel { border: 1px solid #cbd5e1; border-radius: 8px; background: #ffffff; }");
            builder.AppendLine("    .controls { padding: 14px; }");
            builder.AppendLine("    .field-label { display: block; margin: 0 0 6px; color: #334155; font-size: 13px; font-weight: 700; }");
            builder.AppendLine("    select, input[type=file] { width: 100%; min-height: 36px; margin: 0 0 14px; font: 13px Segoe UI, Arial, sans-serif; }");
            builder.AppendLine("    .summary-panel, .legend-panel { border-top: 1px solid #e2e8f0; padding-top: 13px; }");
            builder.AppendLine("    .summary-panel { margin-bottom: 14px; }");
            builder.AppendLine("    .summary-panel h2, .legend-panel h2 { margin-bottom: 9px; }");
            builder.AppendLine("    #summaryMetrics { display: grid; grid-template-columns: minmax(100px, 124px) minmax(0, 1fr); gap: 8px 10px; margin: 0; }");
            builder.AppendLine("    #summaryMetrics dd { min-width: 0; overflow-wrap: anywhere; }");
            builder.AppendLine("    .legend-row { display: flex; align-items: center; gap: 8px; min-height: 24px; color: #334155; font-size: 13px; }");
            builder.AppendLine("    .content-panel { min-width: 0; overflow: hidden; }");
            builder.AppendLine("    .content-header { display: flex; gap: 12px; align-items: start; justify-content: space-between; padding: 14px 16px; border-bottom: 1px solid #e2e8f0; }");
            builder.AppendLine("    .content-header p { margin: 5px 0 0; color: #526173; font-size: 13px; }");
            builder.AppendLine("    .axis-note { flex: 0 0 auto; max-width: 220px; text-align: right; }");
            builder.AppendLine("    .chart-panel, .table-panel { padding: 14px 16px 16px; }");
            builder.AppendLine("    .chart-panel { border-bottom: 1px solid #e2e8f0; }");
            builder.AppendLine("    #severityChart { display: block; width: 100%; min-height: 168px; background: #fbfcfe; border: 1px solid #e2e8f0; border-radius: 8px; }");
            builder.AppendLine("    .chart-label { fill: #334155; font: 12px Segoe UI, Arial, sans-serif; }");
            builder.AppendLine("    .severity-cell { stroke: #ffffff; stroke-width: 1; }");
            builder.AppendLine("    .severity-dot { display: inline-block; width: 11px; height: 11px; border-radius: 999px; border: 1px solid rgba(15, 23, 42, 0.18); vertical-align: -1px; }");
            builder.AppendLine("    .severity-zero { fill: #cbd5e1; background: #cbd5e1; }");
            builder.AppendLine("    .severity-low { fill: #16a34a; background: #16a34a; }");
            builder.AppendLine("    .severity-moderate { fill: #d97706; background: #d97706; }");
            builder.AppendLine("    .severity-high { fill: #dc2626; background: #dc2626; }");
            builder.AppendLine("    .table-scroll { max-height: 540px; overflow: auto; border: 1px solid #e2e8f0; border-radius: 8px; }");
            builder.AppendLine("    table { width: 100%; border-collapse: collapse; font-size: 12px; }");
            builder.AppendLine("    th, td { padding: 7px 9px; border-bottom: 1px solid #e2e8f0; text-align: right; white-space: nowrap; }");
            builder.AppendLine("    th { position: sticky; top: 0; z-index: 1; color: #334155; background: #f8fafc; font-weight: 700; }");
            builder.AppendLine("    th:first-child, td:first-child { text-align: left; }");
            builder.AppendLine("    tr:hover td { background: #f8fafc; }");
            builder.AppendLine("    .empty-message { padding: 20px; color: #64748b; }");
            builder.AppendLine("    @media (max-width: 820px) { main { width: min(100% - 20px, 1360px); padding-top: 18px; } .viewer-shell { grid-template-columns: 1fr; } .content-header { display: block; } .axis-note { text-align: left; max-width: none; } th, td { padding: 7px; } }");
            builder.AppendLine("  </style>");
        }

        private static void AppendScript(StringBuilder builder)
        {
            builder.AppendLine("  <script>");
            builder.AppendLine("""
    (function () {
      'use strict';
      const CONTRACT = 'quantum.transported_frame_comparison_diagnostics';
      const VERSION = 1;
      const METRICS = [
        { key: 'normalDegrees', label: 'Normal' },
        { key: 'binormalDegrees', label: 'Binormal' },
        { key: 'frameDegrees', label: 'Frame' },
        { key: 'matrixOrientationDegrees', label: 'Matrix' }
      ];
      let payload = JSON.parse(document.getElementById('comparison-data').textContent);
      let artifact = payload.artifact;
      let selectedReportIndex = 0;
      const reportSelect = document.getElementById('reportSelect');
      const fileInput = document.getElementById('fileInput');
      const runSummary = document.getElementById('runSummary');
      const summaryMetrics = document.getElementById('summaryMetrics');
      const reportTitle = document.getElementById('report-title');
      const statusLine = document.getElementById('statusLine');
      const severityChart = document.getElementById('severityChart');
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
      function formatDegrees(value) {
        return formatNumber(value, 3) + ' deg';
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
      function reportList() {
        return Array.isArray(artifact && artifact.reports) ? artifact.reports : [];
      }
      function severityFor(value) {
        const magnitude = Math.abs(finite(value, 0));
        if (magnitude >= 30) { return 'high'; }
        if (magnitude >= 5) { return 'moderate'; }
        if (magnitude > 0.1) { return 'low'; }
        return 'zero';
      }
      function severityDot(metric, value) {
        const dot = document.createElement('span');
        dot.className = 'severity-dot severity-' + severityFor(value);
        dot.setAttribute('data-metric', metric);
        dot.setAttribute('title', metric + ': ' + formatDegrees(value));
        return dot;
      }
      function metricSummaryText(metric) {
        return formatDegrees(metric && metric.maxAbsolute) + ' max / ' +
          formatDegrees(metric && metric.averageAbsolute) + ' avg';
      }
      function verifyArtifact(candidate) {
        if (!candidate || candidate.contract !== CONTRACT || candidate.version !== VERSION) {
          throw new Error('Expected ' + CONTRACT + ' v' + VERSION + '.');
        }
        if (!Array.isArray(candidate.reports)) {
          throw new Error('Expected reports array.');
        }
      }
      function renderRunSummary() {
        const metadata = artifact.metadata || {};
        clear(runSummary);
        addSummaryCard('Source', payload.sourcePath || 'local file');
        addSummaryCard('Contract', artifact.contract || 'n/a');
        addSummaryCard('Reports', String((artifact.reports || []).length));
        addSummaryCard('Units', metadata.units || 'meters');
      }
      function renderReportOptions() {
        const reports = reportList();
        clear(reportSelect);
        reports.forEach(function (report, index) {
          const option = document.createElement('option');
          option.value = String(index);
          option.appendChild(text(report.sourceName || 'Report ' + index));
          reportSelect.appendChild(option);
        });
        if (selectedReportIndex >= reports.length) {
          selectedReportIndex = 0;
        }
        reportSelect.value = String(selectedReportIndex);
      }
      function renderSummary(report) {
        const summary = report.summaryMetrics || {};
        clear(summaryMetrics);
        addDefinition(summaryMetrics, 'Fixture', report.sourceName || 'n/a');
        addDefinition(summaryMetrics, 'Track length', formatDistance(report.trackLength));
        addDefinition(summaryMetrics, 'Samples', summary.sampleCount == null ? '0' : String(summary.sampleCount));
        addDefinition(summaryMetrics, 'Stateless issues', String(summary.statelessContinuityIssueCount || 0));
        addDefinition(summaryMetrics, 'Transported issues', String(summary.transportedContinuityIssueCount || 0));
        addDefinition(summaryMetrics, 'Normal', metricSummaryText(summary.normalDegrees));
        addDefinition(summaryMetrics, 'Binormal', metricSummaryText(summary.binormalDegrees));
        addDefinition(summaryMetrics, 'Frame', metricSummaryText(summary.frameDegrees));
        addDefinition(summaryMetrics, 'Matrix', metricSummaryText(summary.matrixOrientationDegrees));
      }
      function svgElement(name) {
        return document.createElementNS('http://www.w3.org/2000/svg', name);
      }
      function renderSeverityChart(report) {
        const samples = Array.isArray(report.samples) ? report.samples : [];
        const width = Math.max(460, 116 + Math.max(samples.length, 1) * 22);
        const height = 36 + METRICS.length * 28;
        clear(severityChart);
        severityChart.setAttribute('viewBox', '0 0 ' + width + ' ' + height);
        METRICS.forEach(function (metric, rowIndex) {
          const y = 26 + rowIndex * 28;
          const label = svgElement('text');
          label.setAttribute('x', '12');
          label.setAttribute('y', String(y + 15));
          label.setAttribute('class', 'chart-label');
          label.textContent = metric.label;
          severityChart.appendChild(label);
          samples.forEach(function (sample, sampleIndex) {
            const rect = svgElement('rect');
            rect.setAttribute('x', String(104 + sampleIndex * 22));
            rect.setAttribute('y', String(y));
            rect.setAttribute('width', '20');
            rect.setAttribute('height', '20');
            rect.setAttribute('rx', '3');
            rect.setAttribute('class', 'severity-cell severity-' + severityFor(sample[metric.key]));
            rect.setAttribute('data-sample-index', String(sample.sampleIndex == null ? sampleIndex : sample.sampleIndex));
            rect.setAttribute('data-metric', metric.key);
            rect.appendChild(document.createElementNS('http://www.w3.org/2000/svg', 'title'))
              .textContent = metric.label + ' sample ' + (sample.sampleIndex == null ? sampleIndex : sample.sampleIndex) + ': ' + formatDegrees(sample[metric.key]);
            severityChart.appendChild(rect);
          });
        });
      }
      function appendNumberCell(row, value, metric) {
        const cell = document.createElement('td');
        if (metric) {
          cell.appendChild(severityDot(metric, value));
          cell.appendChild(text(' '));
        }
        cell.appendChild(text(formatDegrees(value)));
        row.appendChild(cell);
      }
      function renderSamples(report) {
        const samples = Array.isArray(report.samples) ? report.samples : [];
        clear(sampleRows);
        samples.forEach(function (sample, index) {
          const row = document.createElement('tr');
          const indexCell = document.createElement('td');
          indexCell.appendChild(text(sample.sampleIndex == null ? index : sample.sampleIndex));
          row.appendChild(indexCell);
          const distanceCell = document.createElement('td');
          distanceCell.appendChild(text(formatDistance(sample.distance)));
          row.appendChild(distanceCell);
          appendNumberCell(row, sample.tangentDegrees, null);
          appendNumberCell(row, sample.normalDegrees, 'normalDegrees');
          appendNumberCell(row, sample.binormalDegrees, 'binormalDegrees');
          appendNumberCell(row, sample.frameDegrees, 'frameDegrees');
          appendNumberCell(row, sample.rollDegrees, null);
          appendNumberCell(row, sample.absoluteRollDegrees, null);
          appendNumberCell(row, sample.matrixOrientationDegrees, 'matrixOrientationDegrees');
          sampleRows.appendChild(row);
        });
        if (samples.length === 0) {
          const row = document.createElement('tr');
          const cell = document.createElement('td');
          cell.colSpan = 9;
          cell.className = 'empty-message';
          cell.appendChild(text('No sample deltas are present.'));
          row.appendChild(cell);
          sampleRows.appendChild(row);
        }
      }
      function renderSelectedReport() {
        const reports = reportList();
        const report = reports[selectedReportIndex];
        if (!report) {
          reportTitle.textContent = 'No reports';
          statusLine.textContent = 'No transported frame comparison reports are embedded.';
          clear(summaryMetrics);
          clear(severityChart);
          clear(sampleRows);
          return;
        }
        const summary = report.summaryMetrics || {};
        reportTitle.textContent = report.sourceName || 'Report ' + selectedReportIndex;
        statusLine.textContent = 'samples=' + (summary.sampleCount || 0) +
          ', normalMax=' + formatDegrees(summary.normalDegrees && summary.normalDegrees.maxAbsolute) +
          ', matrixMax=' + formatDegrees(summary.matrixOrientationDegrees && summary.matrixOrientationDegrees.maxAbsolute);
        renderSummary(report);
        renderSeverityChart(report);
        renderSamples(report);
      }
      function renderAll() {
        try {
          verifyArtifact(artifact);
          renderRunSummary();
          renderReportOptions();
          renderSelectedReport();
        } catch (error) {
          clear(runSummary);
          clear(summaryMetrics);
          clear(sampleRows);
          clear(severityChart);
          reportTitle.textContent = 'Invalid artifact';
          statusLine.textContent = error.message;
        }
      }
      reportSelect.addEventListener('change', function () {
        selectedReportIndex = Number(reportSelect.value) || 0;
        renderSelectedReport();
      });
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
            selectedReportIndex = 0;
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

        private sealed class TransportedFrameComparisonBrowserPayload
        {
            public string SourcePath { get; set; } = string.Empty;

            public string OutputPath { get; set; } = string.Empty;

            public JsonElement Artifact { get; set; }
        }
    }
}
