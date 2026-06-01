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
    public static class DebugViewportSnapshotBrowserCommand
    {
        public const string CommandName = "debug-viewport-snapshot-v1-browser";

        internal const string DefaultRelativeArtifactDirectory = "artifacts/debug-viewport";
        internal const string DefaultFileName = "browser.html";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private static readonly JsonSerializerOptions PayloadJsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public static int Run(string? artifactDirectory = null, string? outputHtmlPath = null)
        {
            return Run(artifactDirectory, outputHtmlPath, Console.Out);
        }

        public static int Run(string? artifactDirectory, string? outputHtmlPath, TextWriter output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            string resolvedArtifactDirectory = ResolveArtifactDirectory(artifactDirectory);
            string resolvedOutputHtmlPath = ResolveOutputPath(resolvedArtifactDirectory, outputHtmlPath);

            Directory.CreateDirectory(resolvedArtifactDirectory);

            IReadOnlyList<SnapshotBrowserEntry> entries = CollectEntries(resolvedArtifactDirectory);
            string html = BuildHtml(
                resolvedArtifactDirectory,
                resolvedOutputHtmlPath,
                entries,
                DateTimeOffset.UtcNow);

            string? parentDirectory = Path.GetDirectoryName(resolvedOutputHtmlPath);
            if (!string.IsNullOrEmpty(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            File.WriteAllText(resolvedOutputHtmlPath, html, Utf8NoBom);
            output.WriteLine($"Wrote DebugViewportSnapshotV1 browser viewer to '{resolvedOutputHtmlPath}'.");
            return 0;
        }

        private static string ResolveArtifactDirectory(string? artifactDirectory)
        {
            if (string.IsNullOrWhiteSpace(artifactDirectory))
            {
                return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, DefaultRelativeArtifactDirectory));
            }

            return Path.GetFullPath(artifactDirectory);
        }

        private static string ResolveOutputPath(string artifactDirectory, string? outputHtmlPath)
        {
            if (string.IsNullOrWhiteSpace(outputHtmlPath))
            {
                return Path.Combine(artifactDirectory, DefaultFileName);
            }

            return Path.GetFullPath(outputHtmlPath);
        }

        private static IReadOnlyList<SnapshotBrowserEntry> CollectEntries(string artifactDirectory)
        {
            var entries = new List<SnapshotBrowserEntry>();

            foreach (string snapshotPath in Directory.EnumerateFiles(artifactDirectory, "*.json", SearchOption.AllDirectories))
            {
                var snapshotFile = new FileInfo(snapshotPath);
                entries.Add(SnapshotBrowserEntry.Read(snapshotFile, artifactDirectory));
            }

            entries.Sort(CompareEntries);
            return entries;
        }

        private static int CompareEntries(SnapshotBrowserEntry left, SnapshotBrowserEntry right)
        {
            return string.Compare(left.SortKey, right.SortKey, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildHtml(
            string artifactDirectory,
            string outputHtmlPath,
            IReadOnlyList<SnapshotBrowserEntry> entries,
            DateTimeOffset generatedAtUtc)
        {
            var payload = new SnapshotBrowserPayload
            {
                GeneratedAtUtc = FormatTimestamp(generatedAtUtc),
                ArtifactDirectory = ToDisplayPath(Path.GetRelativePath(Environment.CurrentDirectory, artifactDirectory)),
                OutputPath = ToDisplayPath(Path.GetRelativePath(Environment.CurrentDirectory, outputHtmlPath)),
                Entries = entries
            };

            string payloadJson = EscapeJsonForHtml(JsonSerializer.Serialize(payload, PayloadJsonOptions));
            var builder = new StringBuilder();

            builder.AppendLine("<!doctype html>");
            builder.AppendLine("<html lang=\"en\">");
            builder.AppendLine("<head>");
            builder.AppendLine("  <meta charset=\"utf-8\">");
            builder.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            builder.AppendLine("  <title>Quantum DebugViewportSnapshotV1 Browser Viewer</title>");
            AppendStyles(builder);
            builder.AppendLine("</head>");
            builder.AppendLine("<body>");
            builder.AppendLine("  <main>");
            builder.AppendLine("    <header class=\"page-header\">");
            builder.AppendLine("      <p class=\"eyebrow\">Artifact-first browser viewer</p>");
            builder.AppendLine("      <h1>DebugViewportSnapshotV1 Browser Viewer</h1>");
            builder.AppendLine("      <p class=\"lede\">Backend-only local debug aid for visually inspecting renderer-neutral snapshot JSON. It is not a final editor, frontend, renderer, or contract change.</p>");
            builder.AppendLine("      <dl class=\"run-summary\">");
            AppendSummaryItem(builder, "Generated", payload.GeneratedAtUtc);
            AppendSummaryItem(builder, "Directory", payload.ArtifactDirectory);
            AppendSummaryItem(builder, "Snapshots", entries.Count.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("      </dl>");
            builder.AppendLine("    </header>");
            builder.AppendLine("    <section class=\"viewer-shell\" aria-label=\"Debug viewport snapshot inspector\">");
            builder.AppendLine("      <aside class=\"controls\">");
            builder.AppendLine("        <label class=\"field-label\" for=\"snapshotSelect\">Snapshot</label>");
            builder.AppendLine("        <select id=\"snapshotSelect\"></select>");
            builder.AppendLine("        <label class=\"field-label\" for=\"fileInput\">Load local JSON</label>");
            builder.AppendLine("        <input id=\"fileInput\" type=\"file\" accept=\"application/json,.json\">");
            builder.AppendLine("        <section class=\"animation-panel\" aria-labelledby=\"animation-title\">");
            builder.AppendLine("          <h2 id=\"animation-title\">Animation</h2>");
            builder.AppendLine("          <div class=\"animation-controls\">");
            builder.AppendLine("            <button id=\"playPauseButton\" type=\"button\">Play</button>");
            builder.AppendLine("            <input id=\"timelineSlider\" type=\"range\" min=\"0\" max=\"1000\" step=\"1\" value=\"0\" aria-label=\"Animation timeline\">");
            builder.AppendLine("          </div>");
            builder.AppendLine("          <p id=\"timelineReadout\">Animation unavailable</p>");
            builder.AppendLine("        </section>");
            builder.AppendLine("        <fieldset class=\"layer-list\">");
            builder.AppendLine("          <legend>Layers</legend>");
            AppendLayerToggle(builder, "centerline", "Centerline samples");
            AppendLayerToggle(builder, "distances", "Distance labels/ticks");
            AppendLayerToggle(builder, "curvature", "Curvature/radius diagnostics");
            AppendLayerToggle(builder, "curvatureColor", "Curvature colorization");
            AppendLayerToggle(builder, "frames", "Frame axes");
            AppendLayerToggle(builder, "debugLines", "Debug lines");
            AppendLayerToggle(builder, "boxes", "Train boxes");
            AppendLayerToggle(builder, "bogies", "Bogie markers");
            AppendLayerToggle(builder, "wheels", "Wheel markers");
            builder.AppendLine("        </fieldset>");
            builder.AppendLine("        <section class=\"measurement-panel\" aria-labelledby=\"measurement-title\">");
            builder.AppendLine("          <h2 id=\"measurement-title\">Measurement</h2>");
            builder.AppendLine("          <dl id=\"measurementList\"></dl>");
            builder.AppendLine("        </section>");
            builder.AppendLine("        <section class=\"metadata-panel\" aria-labelledby=\"metadata-title\">");
            builder.AppendLine("          <h2 id=\"metadata-title\">Metadata</h2>");
            builder.AppendLine("          <dl id=\"metadataList\"></dl>");
            builder.AppendLine("        </section>");
            builder.AppendLine("      </aside>");
            builder.AppendLine("      <section class=\"viewport-panel\" aria-labelledby=\"viewport-title\">");
            builder.AppendLine("        <div class=\"viewport-header\">");
            builder.AppendLine("          <div>");
            builder.AppendLine("            <h2 id=\"viewport-title\">Top-down X/Z Inspection</h2>");
            builder.AppendLine("            <p id=\"statusLine\">Select a snapshot to render centerline, distance labels, curvature/radius diagnostics, frames, debug lines, train boxes, bogies, and wheels.</p>");
            builder.AppendLine("          </div>");
            builder.AppendLine("          <p class=\"axis-note\">Projection: X/Z top-down, Y available in metadata and raw JSON.</p>");
            builder.AppendLine("        </div>");
            builder.AppendLine("        <svg id=\"viewport\" role=\"img\" aria-label=\"DebugViewportSnapshotV1 top-down browser inspection view\"></svg>");
            builder.AppendLine("      </section>");
            builder.AppendLine("    </section>");
            builder.AppendLine("  </main>");
            builder.AppendLine("  <script id=\"snapshot-data\" type=\"application/json\">");
            builder.AppendLine(payloadJson);
            builder.AppendLine("  </script>");
            AppendScript(builder);
            builder.AppendLine("</body>");
            builder.AppendLine("</html>");
            return builder.ToString();
        }

        private static void AppendLayerToggle(StringBuilder builder, string layerName, string label)
        {
            builder.AppendLine(
                "          <label><input type=\"checkbox\" data-layer=\"" +
                WebUtility.HtmlEncode(layerName) +
                "\" checked> " +
                Escape(label) +
                "</label>");
        }

        private static void AppendSummaryItem(StringBuilder builder, string label, string value)
        {
            builder.AppendLine("        <div><dt>" + Escape(label) + "</dt><dd>" + Escape(value) + "</dd></div>");
        }

        private static void AppendStyles(StringBuilder builder)
        {
            builder.AppendLine("  <style>");
            builder.AppendLine("    * { box-sizing: border-box; }");
            builder.AppendLine("    body { margin: 0; font-family: Segoe UI, Arial, sans-serif; color: #18212f; background: #f6f8fb; }");
            builder.AppendLine("    main { width: min(1440px, calc(100% - 32px)); margin: 0 auto; padding: 26px 0 40px; }");
            builder.AppendLine("    .page-header { margin-bottom: 18px; }");
            builder.AppendLine("    .eyebrow { margin: 0 0 6px; color: #0f766e; font-size: 12px; font-weight: 700; text-transform: uppercase; letter-spacing: 0; }");
            builder.AppendLine("    h1 { margin: 0 0 9px; font-size: 26px; line-height: 1.2; }");
            builder.AppendLine("    h2 { margin: 0; font-size: 16px; line-height: 1.25; }");
            builder.AppendLine("    p { line-height: 1.45; }");
            builder.AppendLine("    .lede { max-width: 980px; margin: 0 0 12px; color: #526173; }");
            builder.AppendLine("    .run-summary { display: grid; grid-template-columns: repeat(auto-fit, minmax(190px, 1fr)); gap: 8px; max-width: 980px; margin: 14px 0 0; }");
            builder.AppendLine("    .run-summary div, .metadata-panel dd, .measurement-panel dd { min-width: 0; overflow-wrap: anywhere; }");
            builder.AppendLine("    .run-summary div { padding: 9px 11px; border: 1px solid #cbd5e1; border-radius: 8px; background: #ffffff; }");
            builder.AppendLine("    dt { margin: 0 0 4px; color: #697789; font-size: 12px; font-weight: 700; text-transform: uppercase; letter-spacing: 0; }");
            builder.AppendLine("    dd { margin: 0; color: #18212f; font-size: 13px; }");
            builder.AppendLine("    .viewer-shell { display: grid; grid-template-columns: minmax(260px, 320px) minmax(0, 1fr); gap: 16px; align-items: start; }");
            builder.AppendLine("    .controls, .viewport-panel { border: 1px solid #cbd5e1; border-radius: 8px; background: #ffffff; }");
            builder.AppendLine("    .controls { padding: 14px; }");
            builder.AppendLine("    .field-label { display: block; margin: 0 0 6px; color: #334155; font-size: 13px; font-weight: 700; }");
            builder.AppendLine("    select, input[type=file] { width: 100%; min-height: 36px; margin: 0 0 14px; font: 13px Segoe UI, Arial, sans-serif; }");
            builder.AppendLine("    button { min-height: 34px; padding: 6px 12px; border: 1px solid #0f766e; border-radius: 8px; color: #ffffff; background: #0f766e; font: 700 13px Segoe UI, Arial, sans-serif; cursor: pointer; }");
            builder.AppendLine("    button:disabled { border-color: #cbd5e1; color: #64748b; background: #f1f5f9; cursor: not-allowed; }");
            builder.AppendLine("    .animation-panel { margin: 0 0 14px; padding: 10px 12px 12px; border: 1px solid #d8e0eb; border-radius: 8px; }");
            builder.AppendLine("    .animation-panel h2 { margin-bottom: 9px; color: #334155; font-size: 13px; }");
            builder.AppendLine("    .animation-controls { display: flex; gap: 9px; align-items: center; }");
            builder.AppendLine("    #timelineSlider { flex: 1 1 auto; min-width: 0; margin: 0; accent-color: #0f766e; }");
            builder.AppendLine("    #timelineReadout { margin: 8px 0 0; color: #526173; font-size: 12px; }");
            builder.AppendLine("    .layer-list { margin: 0 0 14px; padding: 10px 12px 12px; border: 1px solid #d8e0eb; border-radius: 8px; }");
            builder.AppendLine("    .layer-list legend { padding: 0 5px; color: #334155; font-size: 13px; font-weight: 700; }");
            builder.AppendLine("    .layer-list label { display: flex; align-items: center; gap: 8px; min-height: 28px; color: #253244; font-size: 13px; }");
            builder.AppendLine("    .metadata-panel, .measurement-panel { border-top: 1px solid #e2e8f0; padding-top: 13px; }");
            builder.AppendLine("    .measurement-panel { margin-bottom: 14px; }");
            builder.AppendLine("    .metadata-panel h2, .measurement-panel h2 { margin-bottom: 9px; }");
            builder.AppendLine("    .metadata-panel dl, .measurement-panel dl { display: grid; grid-template-columns: minmax(82px, 108px) minmax(0, 1fr); gap: 8px 10px; margin: 0; }");
            builder.AppendLine("    .viewport-panel { min-width: 0; overflow: hidden; }");
            builder.AppendLine("    .viewport-header { display: flex; gap: 12px; align-items: start; justify-content: space-between; padding: 14px 16px; border-bottom: 1px solid #e2e8f0; }");
            builder.AppendLine("    .viewport-header p { margin: 5px 0 0; color: #526173; font-size: 13px; }");
            builder.AppendLine("    .axis-note { flex: 0 0 auto; max-width: 260px; text-align: right; }");
            builder.AppendLine("    #viewport { display: block; width: 100%; min-height: 620px; background: #fbfcfe; }");
            builder.AppendLine("    .plot-bg { fill: #fbfcfe; }");
            builder.AppendLine("    .grid-line { stroke: #e4e9f0; stroke-width: 1; }");
            builder.AppendLine("    .centerline-path { fill: none; stroke: #0f766e; stroke-width: 3; stroke-linecap: round; stroke-linejoin: round; }");
            builder.AppendLine("    .sample-point { fill: #ffffff; stroke: #0f766e; stroke-width: 2; cursor: crosshair; }");
            builder.AppendLine("    .sample-point.is-hovered { fill: #fef3c7; stroke: #d97706; stroke-width: 3; }");
            builder.AppendLine("    .sample-point.is-selected { fill: #0f766e; stroke: #0f172a; stroke-width: 3; }");
            builder.AppendLine("    .distance-tick { stroke: #334155; stroke-width: 1.5; stroke-linecap: round; }");
            builder.AppendLine("    .distance-label { fill: #334155; stroke: #fbfcfe; stroke-width: 3; paint-order: stroke; stroke-linejoin: round; font: 11px Segoe UI, Arial, sans-serif; }");
            builder.AppendLine("    .curvature-segment { stroke: #64748b; stroke-width: 6; stroke-linecap: round; opacity: 0.58; }");
            builder.AppendLine("    .curvature-point { fill: #ffffff; stroke: #64748b; stroke-width: 2; cursor: crosshair; opacity: 0.95; }");
            builder.AppendLine("    .curvature-point.is-hovered { stroke: #d97706; stroke-width: 3; }");
            builder.AppendLine("    .curvature-point.is-selected { stroke: #0f172a; stroke-width: 3; }");
            builder.AppendLine("    .curvature-low { stroke: #16a34a; }");
            builder.AppendLine("    .curvature-moderate { stroke: #eab308; }");
            builder.AppendLine("    .curvature-high { stroke: #dc2626; }");
            builder.AppendLine("    .curvature-point.curvature-low { fill: #dcfce7; }");
            builder.AppendLine("    .curvature-point.curvature-moderate { fill: #fef9c3; }");
            builder.AppendLine("    .curvature-point.curvature-high { fill: #fee2e2; }");
            builder.AppendLine("    .radius-label { fill: #7f1d1d; stroke: #fbfcfe; stroke-width: 4; paint-order: stroke; stroke-linejoin: round; font: 12px Segoe UI, Arial, sans-serif; font-weight: 700; }");
            builder.AppendLine("    .frame-tangent { stroke: #2563eb; }");
            builder.AppendLine("    .frame-normal { stroke: #d97706; }");
            builder.AppendLine("    .frame-binormal { stroke: #7c3aed; }");
            builder.AppendLine("    .frame-axis { stroke-width: 1.6; stroke-linecap: round; }");
            builder.AppendLine("    .debug-line { stroke: #475569; stroke-width: 1.8; stroke-dasharray: 5 4; stroke-linecap: round; }");
            builder.AppendLine("    .debug-line.frame-axis-tangent { stroke: #2563eb; }");
            builder.AppendLine("    .debug-line.frame-axis-normal { stroke: #d97706; }");
            builder.AppendLine("    .debug-line.frame-axis-binormal { stroke: #7c3aed; }");
            builder.AppendLine("    .train-box { fill: rgba(15, 118, 110, 0.14); stroke: #0f766e; stroke-width: 2; }");
            builder.AppendLine("    .train-label { fill: #0f766e; font: 12px Segoe UI, Arial, sans-serif; }");
            builder.AppendLine("    .bogie-marker { fill: #ffffff; stroke: #be123c; stroke-width: 2; }");
            builder.AppendLine("    .wheel-marker { fill: #111827; stroke: #ffffff; stroke-width: 1; }");
            builder.AppendLine("    .empty-message { fill: #9a3412; font: 14px Segoe UI, Arial, sans-serif; }");
            builder.AppendLine("    @media (max-width: 820px) { main { width: min(100% - 20px, 1440px); padding-top: 18px; } .viewer-shell { grid-template-columns: 1fr; } .viewport-header { display: block; } .axis-note { text-align: left; max-width: none; } #viewport { min-height: 480px; } }");
            builder.AppendLine("  </style>");
        }

        private static void AppendScript(StringBuilder builder)
        {
            builder.AppendLine("  <script>");
            builder.AppendLine("    (function () {");
            builder.AppendLine("      'use strict';");
            builder.AppendLine("      const SVG_NS = 'http://www.w3.org/2000/svg';");
            builder.AppendLine("      const WIDTH = 1120;");
            builder.AppendLine("      const HEIGHT = 680;");
            builder.AppendLine("      const PAD = 48;");
            builder.AppendLine("      const payload = JSON.parse(document.getElementById('snapshot-data').textContent);");
            builder.AppendLine("      const entries = Array.isArray(payload.entries) ? payload.entries : [];");
            builder.AppendLine("      const select = document.getElementById('snapshotSelect');");
            builder.AppendLine("      const metadataList = document.getElementById('metadataList');");
            builder.AppendLine("      const measurementList = document.getElementById('measurementList');");
            builder.AppendLine("      const statusLine = document.getElementById('statusLine');");
            builder.AppendLine("      const viewport = document.getElementById('viewport');");
            builder.AppendLine("      const fileInput = document.getElementById('fileInput');");
            builder.AppendLine("      const playPauseButton = document.getElementById('playPauseButton');");
            builder.AppendLine("      const timelineSlider = document.getElementById('timelineSlider');");
            builder.AppendLine("      const timelineReadout = document.getElementById('timelineReadout');");
            builder.AppendLine("      let currentEntry = null;");
            builder.AppendLine("      let selectedSampleIndex = null;");
            builder.AppendLine("      let animationProgress = 0;");
            builder.AppendLine("      let animationPlaying = false;");
            builder.AppendLine("      let animationFrameHandle = null;");
            builder.AppendLine("      let lastAnimationTimestamp = null;");
            builder.AppendLine();
            builder.AppendLine("      function asArray(value) { return Array.isArray(value) ? value : []; }");
            builder.AppendLine("      function finite(value, fallback) { const number = Number(value); return Number.isFinite(number) ? number : fallback; }");
            builder.AppendLine("      function cssToken(value) { return String(value || '').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '') || 'unknown'; }");
            builder.AppendLine("      function optionalNumber(value) { if (value === null || value === undefined || value === '') { return null; } const number = Number(value); return Number.isFinite(number) ? number : null; }");
            builder.AppendLine("      function vec(value) { return { x: finite(value && value.x, 0), y: finite(value && value.y, 0), z: finite(value && value.z, 0) }; }");
            builder.AppendLine("      function hasPosition(value) { return !!value && optionalNumber(value.x) !== null && optionalNumber(value.y) !== null && optionalNumber(value.z) !== null; }");
            builder.AppendLine("      function framePosition(frame) { return vec(frame && frame.position); }");
            builder.AppendLine("      function layerVisible(name) { const input = document.querySelector('[data-layer=\"' + name + '\"]'); return !input || input.checked; }");
            builder.AppendLine("      function svg(name, attrs) {");
            builder.AppendLine("        const element = document.createElementNS(SVG_NS, name);");
            builder.AppendLine("        Object.keys(attrs || {}).forEach(function (key) { element.setAttribute(key, attrs[key]); });");
            builder.AppendLine("        return element;");
            builder.AppendLine("      }");
            builder.AppendLine("      function clear(element) { while (element.firstChild) { element.removeChild(element.firstChild); } }");
            builder.AppendLine("      function appendText(group, x, y, text, className) {");
            builder.AppendLine("        const element = svg('text', { x: x.toFixed(1), y: y.toFixed(1), class: className || '' });");
            builder.AppendLine("        element.textContent = text;");
            builder.AppendLine("        group.appendChild(element);");
            builder.AppendLine("      }");
            builder.AppendLine("      function normalizeTopDown(direction, fallback) {");
            builder.AppendLine("        const value = vec(direction);");
            builder.AppendLine("        const length = Math.hypot(value.x, value.z);");
            builder.AppendLine("        if (length < 1e-9) { return fallback; }");
            builder.AppendLine("        return { x: value.x / length, z: value.z / length };");
            builder.AppendLine("      }");
            builder.AppendLine("      function addLocal(frame, localX, localY, localZ) {");
            builder.AppendLine("        const p = framePosition(frame);");
            builder.AppendLine("        const t = vec(frame && frame.tangent);");
            builder.AppendLine("        const n = vec(frame && frame.normal);");
            builder.AppendLine("        const b = vec(frame && frame.binormal);");
            builder.AppendLine("        return {");
            builder.AppendLine("          x: p.x + t.x * localX + n.x * localY + b.x * localZ,");
            builder.AppendLine("          y: p.y + t.y * localX + n.y * localY + b.y * localZ,");
            builder.AppendLine("          z: p.z + t.z * localX + n.z * localY + b.z * localZ");
            builder.AppendLine("        };");
            builder.AppendLine("      }");
            builder.AppendLine("      function boxCorners(box) {");
            builder.AppendLine("        const frame = box && box.frame;");
            builder.AppendLine("        const center = framePosition(frame);");
            builder.AppendLine("        const tangent = normalizeTopDown(frame && frame.tangent, { x: 1, z: 0 });");
            builder.AppendLine("        let binormal = normalizeTopDown(frame && frame.binormal, { x: -tangent.z, z: tangent.x });");
            builder.AppendLine("        const halfLength = finite(box && box.size && box.size.length, 0) * 0.5;");
            builder.AppendLine("        const halfWidth = finite(box && box.size && box.size.width, 0) * 0.5;");
            builder.AppendLine("        return [");
            builder.AppendLine("          { x: center.x + tangent.x * halfLength + binormal.x * halfWidth, z: center.z + tangent.z * halfLength + binormal.z * halfWidth },");
            builder.AppendLine("          { x: center.x + tangent.x * halfLength - binormal.x * halfWidth, z: center.z + tangent.z * halfLength - binormal.z * halfWidth },");
            builder.AppendLine("          { x: center.x - tangent.x * halfLength - binormal.x * halfWidth, z: center.z - tangent.z * halfLength - binormal.z * halfWidth },");
            builder.AppendLine("          { x: center.x - tangent.x * halfLength + binormal.x * halfWidth, z: center.z - tangent.z * halfLength + binormal.z * halfWidth }");
            builder.AppendLine("        ];");
            builder.AppendLine("      }");
            builder.AppendLine("      function trainBogies(snapshot) {");
            builder.AppendLine("        const markers = [];");
            builder.AppendLine("        asArray(snapshot && snapshot.trainPose && snapshot.trainPose.cars).forEach(function (car, carIndex) {");
            builder.AppendLine("          [['front', car && car.frontBogie], ['rear', car && car.rearBogie]].forEach(function (pair) {");
            builder.AppendLine("            const bogie = pair[1] && pair[1].bogie;");
            builder.AppendLine("            if (bogie && bogie.frame) { markers.push({ label: 'car ' + carIndex + ' ' + pair[0] + ' bogie', frame: bogie.frame }); }");
            builder.AppendLine("          });");
            builder.AppendLine("        });");
            builder.AppendLine("        return markers;");
            builder.AppendLine("      }");
            builder.AppendLine("      function trainWheels(snapshot) {");
            builder.AppendLine("        const markers = [];");
            builder.AppendLine("        asArray(snapshot && snapshot.trainPose && snapshot.trainPose.cars).forEach(function (car) {");
            builder.AppendLine("          [car && car.frontBogie, car && car.rearBogie].forEach(function (bogieWithWheels) {");
            builder.AppendLine("            asArray(bogieWithWheels && bogieWithWheels.wheels).forEach(function (wheel) {");
            builder.AppendLine("              if (wheel && wheel.frame) {");
            builder.AppendLine("                markers.push(addLocal(wheel.frame, finite(wheel.localOffsetX, 0), finite(wheel.localOffsetY, 0), finite(wheel.localOffsetZ, 0)));");
            builder.AppendLine("              }");
            builder.AppendLine("            });");
            builder.AppendLine("          });");
            builder.AppendLine("        });");
            builder.AppendLine("        return markers;");
            builder.AppendLine("      }");
            builder.AppendLine("      function sampleFromPosition(distance, position, index, source) {");
            builder.AppendLine("        if (!hasPosition(position)) { return null; }");
            builder.AppendLine("        return { index: index === undefined ? null : index, distance: optionalNumber(distance), position: vec(position), derived: false, source: source || null };");
            builder.AppendLine("      }");
            builder.AppendLine("      function rawDistanceSamples(snapshot) {");
            builder.AppendLine("        const centerline = asArray(snapshot && snapshot.centerlinePoints).map(function (point) { return sampleFromPosition(point && point.distance, point && point.position, undefined, point); }).filter(Boolean);");
            builder.AppendLine("        if (centerline.length > 0) { return centerline; }");
            builder.AppendLine("        return asArray(snapshot && snapshot.frames).map(function (frame) { return sampleFromPosition(frame && frame.distance, frame && frame.position, undefined, frame); }).filter(Boolean);");
            builder.AppendLine("      }");
            builder.AppendLine("      function resolveSampleDistances(samples) {");
            builder.AppendLine("        let cumulative = 0;");
            builder.AppendLine("        let previous = null;");
            builder.AppendLine("        return samples.map(function (sample) {");
            builder.AppendLine("          if (previous) { cumulative += Math.hypot(sample.position.x - previous.x, sample.position.y - previous.y, sample.position.z - previous.z); }");
            builder.AppendLine("          previous = sample.position;");
            builder.AppendLine("          const value = sample.distance === null ? cumulative : sample.distance;");
            builder.AppendLine("          return { index: sample.index, distance: value, position: sample.position, derived: sample.distance === null, source: sample.source || null };");
            builder.AppendLine("        });");
            builder.AppendLine("      }");
            builder.AppendLine("      function distanceSamples(snapshot) {");
            builder.AppendLine("        return resolveSampleDistances(rawDistanceSamples(snapshot));");
            builder.AppendLine("      }");
            builder.AppendLine("      function centerlineInspectionSamples(snapshot) {");
            builder.AppendLine("        const samples = asArray(snapshot && snapshot.centerlinePoints).map(function (point, index) { return sampleFromPosition(point && point.distance, point && point.position, index, point); }).filter(Boolean);");
            builder.AppendLine("        return resolveSampleDistances(samples);");
            builder.AppendLine("      }");
            builder.AppendLine("      function fieldNumber(source, names) {");
            builder.AppendLine("        if (!source) { return null; }");
            builder.AppendLine("        for (let i = 0; i < names.length; i += 1) {");
            builder.AppendLine("          const value = optionalNumber(source[names[i]]);");
            builder.AppendLine("          if (value !== null) { return value; }");
            builder.AppendLine("        }");
            builder.AppendLine("        return null;");
            builder.AppendLine("      }");
            builder.AppendLine("      function firstFieldNumber(sources, names) {");
            builder.AppendLine("        for (let i = 0; i < sources.length; i += 1) {");
            builder.AppendLine("          const value = fieldNumber(sources[i], names);");
            builder.AppendLine("          if (value !== null) { return value; }");
            builder.AppendLine("        }");
            builder.AppendLine("        return null;");
            builder.AppendLine("      }");
            builder.AppendLine("      function radiusFromCurvature(curvature) {");
            builder.AppendLine("        if (curvature === null || curvature === undefined) { return null; }");
            builder.AppendLine("        const magnitude = Math.abs(curvature);");
            builder.AppendLine("        return magnitude > 1e-9 ? 1 / magnitude : Infinity;");
            builder.AppendLine("      }");
            builder.AppendLine("      function frameForSample(snapshot, index, distance) {");
            builder.AppendLine("        const frames = asArray(snapshot && snapshot.frames);");
            builder.AppendLine("        if (index !== null && index !== undefined && frames[index]) { return frames[index]; }");
            builder.AppendLine("        const target = optionalNumber(distance);");
            builder.AppendLine("        if (target === null) { return null; }");
            builder.AppendLine("        let best = null;");
            builder.AppendLine("        let bestDelta = Infinity;");
            builder.AppendLine("        frames.forEach(function (frame) {");
            builder.AppendLine("          const frameDistance = optionalNumber(frame && frame.distance);");
            builder.AppendLine("          if (frameDistance === null) { return; }");
            builder.AppendLine("          const delta = Math.abs(frameDistance - target);");
            builder.AppendLine("          if (delta < bestDelta) { best = frame; bestDelta = delta; }");
            builder.AppendLine("        });");
            builder.AppendLine("        return best;");
            builder.AppendLine("      }");
            builder.AppendLine("      function explicitCurvatureForSample(sample, frame) {");
            builder.AppendLine("        const sampleSource = sample && sample.source;");
            builder.AppendLine("        const sources = [sampleSource, frame, sampleSource && sampleSource.diagnostics, frame && frame.diagnostics];");
            builder.AppendLine("        const curvature = firstFieldNumber(sources, ['curvature', 'curvatureMagnitude', 'curvature1PerMeter']);");
            builder.AppendLine("        const radius = firstFieldNumber(sources, ['radius', 'turnRadius', 'curvatureRadius']);");
            builder.AppendLine("        if (curvature !== null) {");
            builder.AppendLine("          const magnitude = Math.abs(curvature);");
            builder.AppendLine("          const resolvedRadius = radius !== null && Math.abs(radius) > 1e-9 ? Math.abs(radius) : radiusFromCurvature(magnitude);");
            builder.AppendLine("          return { curvature: magnitude, radius: resolvedRadius, source: 'explicit' };");
            builder.AppendLine("        }");
            builder.AppendLine("        if (radius !== null && Math.abs(radius) > 1e-9) {");
            builder.AppendLine("          return { curvature: 1 / Math.abs(radius), radius: Math.abs(radius), source: 'explicit' };");
            builder.AppendLine("        }");
            builder.AppendLine("        return null;");
            builder.AppendLine("      }");
            builder.AppendLine("      function subtract3(a, b) { return { x: a.x - b.x, y: a.y - b.y, z: a.z - b.z }; }");
            builder.AppendLine("      function cross3(a, b) { return { x: a.y * b.z - a.z * b.y, y: a.z * b.x - a.x * b.z, z: a.x * b.y - a.y * b.x }; }");
            builder.AppendLine("      function length3(value) { return Math.hypot(value.x, value.y, value.z); }");
            builder.AppendLine("      function triangleCurvature(a, b, c) {");
            builder.AppendLine("        const ab = subtract3(b, a);");
            builder.AppendLine("        const bc = subtract3(c, b);");
            builder.AppendLine("        const ca = subtract3(a, c);");
            builder.AppendLine("        const abLength = length3(ab);");
            builder.AppendLine("        const bcLength = length3(bc);");
            builder.AppendLine("        const caLength = length3(ca);");
            builder.AppendLine("        const denominator = abLength * bcLength * caLength;");
            builder.AppendLine("        if (denominator < 1e-9) { return null; }");
            builder.AppendLine("        const ac = subtract3(c, a);");
            builder.AppendLine("        const crossLength = length3(cross3(ab, ac));");
            builder.AppendLine("        const curvature = 2 * crossLength / denominator;");
            builder.AppendLine("        return Number.isFinite(curvature) ? curvature : null;");
            builder.AppendLine("      }");
            builder.AppendLine("      function deriveSampleCurvature(samples, index) {");
            builder.AppendLine("        if (samples.length < 3) { return null; }");
            builder.AppendLine("        let leftIndex = index - 1;");
            builder.AppendLine("        let middleIndex = index;");
            builder.AppendLine("        let rightIndex = index + 1;");
            builder.AppendLine("        if (index <= 0) { leftIndex = 0; middleIndex = 1; rightIndex = 2; }");
            builder.AppendLine("        if (index >= samples.length - 1) { leftIndex = samples.length - 3; middleIndex = samples.length - 2; rightIndex = samples.length - 1; }");
            builder.AppendLine("        const curvature = triangleCurvature(samples[leftIndex].position, samples[middleIndex].position, samples[rightIndex].position);");
            builder.AppendLine("        if (curvature === null) { return null; }");
            builder.AppendLine("        return { curvature, radius: radiusFromCurvature(curvature), source: 'derived' };");
            builder.AppendLine("      }");
            builder.AppendLine("      function curvatureInspectionSamples(snapshot) {");
            builder.AppendLine("        const samples = centerlineInspectionSamples(snapshot);");
            builder.AppendLine("        return samples.map(function (sample, index) {");
            builder.AppendLine("          const frame = frameForSample(snapshot, sample.index, sample.distance);");
            builder.AppendLine("          const diagnostics = explicitCurvatureForSample(sample, frame) || deriveSampleCurvature(samples, index);");
            builder.AppendLine("          return Object.assign({}, sample, {");
            builder.AppendLine("            curvature: diagnostics ? diagnostics.curvature : null,");
            builder.AppendLine("            radius: diagnostics ? diagnostics.radius : null,");
            builder.AppendLine("            curvatureSource: diagnostics ? diagnostics.source : 'unavailable'");
            builder.AppendLine("          });");
            builder.AppendLine("        });");
            builder.AppendLine("      }");
            builder.AppendLine("      function sampleByIndexFromSamples(samples, index) {");
            builder.AppendLine("        if (index === null || index === undefined) { return null; }");
            builder.AppendLine("        return samples.find(function (sample) { return sample.index === index; }) || null;");
            builder.AppendLine("      }");
            builder.AppendLine("      function distanceMarkerIndexes(samples) {");
            builder.AppendLine("        if (samples.length === 0) { return []; }");
            builder.AppendLine("        const maxLabels = 11;");
            builder.AppendLine("        const step = Math.max(1, Math.ceil((samples.length - 1) / (maxLabels - 1)));");
            builder.AppendLine("        const indexes = [];");
            builder.AppendLine("        for (let i = 0; i < samples.length; i += step) { indexes.push(i); }");
            builder.AppendLine("        const last = samples.length - 1;");
            builder.AppendLine("        if (indexes[indexes.length - 1] !== last) { indexes.push(last); }");
            builder.AppendLine("        return indexes;");
            builder.AppendLine("      }");
            builder.AppendLine("      function formatDistance(value) {");
            builder.AppendLine("        const magnitude = Math.abs(value);");
            builder.AppendLine("        if (magnitude >= 100) { return value.toFixed(0) + ' m'; }");
            builder.AppendLine("        if (magnitude >= 10) { return value.toFixed(1) + ' m'; }");
            builder.AppendLine("        return value.toFixed(2) + ' m';");
            builder.AppendLine("      }");
            builder.AppendLine("      function formatNumber(value) { return finite(value, 0).toFixed(3); }");
            builder.AppendLine("      function formatCurvature(value) {");
            builder.AppendLine("        if (value === null || value === undefined || !Number.isFinite(value)) { return '<unavailable>'; }");
            builder.AppendLine("        const magnitude = Math.abs(value);");
            builder.AppendLine("        return (magnitude < 0.0001 ? value.toExponential(2) : value.toFixed(4)) + ' 1/m';");
            builder.AppendLine("      }");
            builder.AppendLine("      function formatRadius(value) {");
            builder.AppendLine("        if (value === null || value === undefined) { return '<unavailable>'; }");
            builder.AppendLine("        if (!Number.isFinite(value)) { return 'straight'; }");
            builder.AppendLine("        return formatDistance(value);");
            builder.AppendLine("      }");
            builder.AppendLine("      function curvatureLevel(curvature) {");
            builder.AppendLine("        const magnitude = Math.abs(finite(curvature, 0));");
            builder.AppendLine("        if (magnitude >= 0.05) { return 'high'; }");
            builder.AppendLine("        if (magnitude >= 0.015) { return 'moderate'; }");
            builder.AppendLine("        return 'low';");
            builder.AppendLine("      }");
            builder.AppendLine("      function curvatureClass(sample) { return 'curvature-' + curvatureLevel(sample && sample.curvature); }");
            builder.AppendLine("      function curvatureTitle(sample) {");
            builder.AppendLine("        if (!sample || sample.curvature === null) { return 'curvature unavailable'; }");
            builder.AppendLine("        return 'curvature ' + formatCurvature(sample.curvature) + ', radius ' + formatRadius(sample.radius) + ' (' + sample.curvatureSource + ')';");
            builder.AppendLine("      }");
            builder.AppendLine("      function summarizeCurvature(samples) {");
            builder.AppendLine("        const available = samples.filter(function (sample) { return sample.curvature !== null; });");
            builder.AppendLine("        if (available.length === 0) { return 'unavailable'; }");
            builder.AppendLine("        let maxCurvature = 0;");
            builder.AppendLine("        let explicitCount = 0;");
            builder.AppendLine("        available.forEach(function (sample) {");
            builder.AppendLine("          maxCurvature = Math.max(maxCurvature, Math.abs(sample.curvature));");
            builder.AppendLine("          if (sample.curvatureSource === 'explicit') { explicitCount += 1; }");
            builder.AppendLine("        });");
            builder.AppendLine("        const sourceText = explicitCount > 0 ? explicitCount + ' explicit' : 'derived';");
            builder.AppendLine("        return available.length + '/' + samples.length + ' samples, max ' + formatCurvature(maxCurvature) + ', min R=' + formatRadius(radiusFromCurvature(maxCurvature)) + ', ' + sourceText;");
            builder.AppendLine("      }");
            AppendAnimationScript(builder);
            builder.AppendLine("      function markerNormal(samples, index, project) {");
            builder.AppendLine("        const previous = samples[Math.max(0, index - 1)];");
            builder.AppendLine("        const next = samples[Math.min(samples.length - 1, index + 1)];");
            builder.AppendLine("        if (!previous || !next || previous === next) { return { x: 0, y: -1 }; }");
            builder.AppendLine("        const a = project.point(previous.position);");
            builder.AppendLine("        const b = project.point(next.position);");
            builder.AppendLine("        const dx = b.x - a.x;");
            builder.AppendLine("        const dy = b.y - a.y;");
            builder.AppendLine("        const length = Math.hypot(dx, dy);");
            builder.AppendLine("        if (length < 1e-9) { return { x: 0, y: -1 }; }");
            builder.AppendLine("        return { x: -dy / length, y: dx / length };");
            builder.AppendLine("      }");
            builder.AppendLine("      function collectBounds(snapshot) {");
            builder.AppendLine("        const points = [];");
            builder.AppendLine("        function push(value) { const p = vec(value); points.push({ x: p.x, z: p.z }); }");
            builder.AppendLine("        asArray(snapshot && snapshot.centerlinePoints).forEach(function (point) { push(point && point.position); });");
            builder.AppendLine("        asArray(snapshot && snapshot.frames).forEach(function (frame) { push(frame && frame.position); });");
            builder.AppendLine("        asArray(snapshot && snapshot.lines).forEach(function (line) { push(line && line.start); push(line && line.end); });");
            builder.AppendLine("        asArray(snapshot && snapshot.boxes).forEach(function (box) { boxCorners(box).forEach(function (corner) { points.push(corner); }); });");
            builder.AppendLine("        trainBogies(snapshot).forEach(function (marker) { push(marker.frame && marker.frame.position); });");
            builder.AppendLine("        trainWheels(snapshot).forEach(function (marker) { points.push({ x: marker.x, z: marker.z }); });");
            builder.AppendLine("        const train = animatedTrain(snapshot, animationProgress);");
            builder.AppendLine("        train.boxes.forEach(function (box) { boxCorners(box).forEach(function (corner) { points.push(corner); }); });");
            builder.AppendLine("        train.bogies.forEach(function (marker) { push(marker.frame && marker.frame.position); });");
            builder.AppendLine("        train.wheels.forEach(function (marker) { points.push({ x: marker.x, z: marker.z }); });");
            builder.AppendLine("        if (points.length === 0) { points.push({ x: -1, z: -1 }, { x: 1, z: 1 }); }");
            builder.AppendLine("        let minX = Infinity, maxX = -Infinity, minZ = Infinity, maxZ = -Infinity;");
            builder.AppendLine("        points.forEach(function (point) { minX = Math.min(minX, point.x); maxX = Math.max(maxX, point.x); minZ = Math.min(minZ, point.z); maxZ = Math.max(maxZ, point.z); });");
            builder.AppendLine("        if (maxX - minX < 1) { const cx = (minX + maxX) * 0.5; minX = cx - 0.5; maxX = cx + 0.5; }");
            builder.AppendLine("        if (maxZ - minZ < 1) { const cz = (minZ + maxZ) * 0.5; minZ = cz - 0.5; maxZ = cz + 0.5; }");
            builder.AppendLine("        return { minX, maxX, minZ, maxZ };");
            builder.AppendLine("      }");
            builder.AppendLine("      function projector(snapshot) {");
            builder.AppendLine("        const bounds = collectBounds(snapshot);");
            builder.AppendLine("        const spanX = bounds.maxX - bounds.minX;");
            builder.AppendLine("        const spanZ = bounds.maxZ - bounds.minZ;");
            builder.AppendLine("        const scale = Math.min((WIDTH - PAD * 2) / spanX, (HEIGHT - PAD * 2) / spanZ);");
            builder.AppendLine("        const offsetX = PAD + (WIDTH - PAD * 2 - spanX * scale) * 0.5;");
            builder.AppendLine("        const offsetY = PAD + (HEIGHT - PAD * 2 - spanZ * scale) * 0.5;");
            builder.AppendLine("        return {");
            builder.AppendLine("          scale,");
            builder.AppendLine("          point: function (value) { const p = vec(value); return { x: offsetX + (p.x - bounds.minX) * scale, y: offsetY + (bounds.maxZ - p.z) * scale }; },");
            builder.AppendLine("          raw: function (value) { return { x: offsetX + (value.x - bounds.minX) * scale, y: offsetY + (bounds.maxZ - value.z) * scale }; }");
            builder.AppendLine("        };");
            builder.AppendLine("      }");
            builder.AppendLine("      function drawGrid(group) {");
            builder.AppendLine("        for (let i = 0; i <= 4; i += 1) {");
            builder.AppendLine("          const x = PAD + (WIDTH - PAD * 2) * i / 4;");
            builder.AppendLine("          const y = PAD + (HEIGHT - PAD * 2) * i / 4;");
            builder.AppendLine("          group.appendChild(svg('line', { class: 'grid-line', x1: x, y1: PAD, x2: x, y2: HEIGHT - PAD }));");
            builder.AppendLine("          group.appendChild(svg('line', { class: 'grid-line', x1: PAD, y1: y, x2: WIDTH - PAD, y2: y }));");
            builder.AppendLine("        }");
            builder.AppendLine("      }");
            builder.AppendLine("      function drawCenterline(group, snapshot, project) {");
            builder.AppendLine("        const samples = curvatureInspectionSamples(snapshot);");
            builder.AppendLine("        if (samples.length > 1) {");
            builder.AppendLine("          const polyline = samples.map(function (sample) { const p = project.point(sample.position); return p.x.toFixed(1) + ',' + p.y.toFixed(1); }).join(' ');");
            builder.AppendLine("          group.appendChild(svg('polyline', { class: 'centerline-path', points: polyline }));");
            builder.AppendLine("        }");
            builder.AppendLine("        samples.forEach(function (sample) {");
            builder.AppendLine("          const p = project.point(sample.position);");
            builder.AppendLine("          const className = 'sample-point inspectable-sample-point' + (selectedSampleIndex === sample.index ? ' is-selected' : '');");
            builder.AppendLine("          const circle = svg('circle', { class: className, 'data-sample-index': String(sample.index), cx: p.x.toFixed(1), cy: p.y.toFixed(1), r: selectedSampleIndex === sample.index ? 6 : 4, tabindex: '0', role: 'button', 'aria-label': 'centerline sample ' + sample.index });");
            builder.AppendLine("          const title = svg('title');");
            builder.AppendLine("          title.textContent = 'sample ' + sample.index + ' s=' + formatDistance(sample.distance) + ', ' + curvatureTitle(sample);");
            builder.AppendLine("          circle.appendChild(title);");
            builder.AppendLine("          wireSampleInspection(circle, sample, snapshot);");
            builder.AppendLine("          group.appendChild(circle);");
            builder.AppendLine("        });");
            builder.AppendLine("      }");
            builder.AppendLine("      function drawDistanceMarkers(group, snapshot, project) {");
            builder.AppendLine("        const samples = distanceSamples(snapshot);");
            builder.AppendLine("        distanceMarkerIndexes(samples).forEach(function (index) {");
            builder.AppendLine("          const sample = samples[index];");
            builder.AppendLine("          const p = project.point(sample.position);");
            builder.AppendLine("          const normal = markerNormal(samples, index, project);");
            builder.AppendLine("          const tickHalf = 7;");
            builder.AppendLine("          const labelOffset = 18;");
            builder.AppendLine("          group.appendChild(svg('line', { class: 'distance-tick', x1: (p.x - normal.x * tickHalf).toFixed(1), y1: (p.y - normal.y * tickHalf).toFixed(1), x2: (p.x + normal.x * tickHalf).toFixed(1), y2: (p.y + normal.y * tickHalf).toFixed(1) }));");
            builder.AppendLine("          appendText(group, p.x + normal.x * labelOffset, p.y + normal.y * labelOffset - 3, 's=' + formatDistance(sample.distance), 'distance-label');");
            builder.AppendLine("        });");
            builder.AppendLine("      }");
            builder.AppendLine("      function drawCurvature(group, snapshot, project) {");
            builder.AppendLine("        const samples = curvatureInspectionSamples(snapshot);");
            builder.AppendLine("        const colorize = layerVisible('curvatureColor');");
            builder.AppendLine("        for (let i = 1; i < samples.length; i += 1) {");
            builder.AppendLine("          const previous = samples[i - 1];");
            builder.AppendLine("          const sample = samples[i];");
            builder.AppendLine("          const start = project.point(previous.position);");
            builder.AppendLine("          const end = project.point(sample.position);");
            builder.AppendLine("          const segmentCurvature = Math.max(Math.abs(finite(previous.curvature, 0)), Math.abs(finite(sample.curvature, 0)));");
            builder.AppendLine("          const className = 'curvature-segment' + (colorize ? ' curvature-' + curvatureLevel(segmentCurvature) : '');");
            builder.AppendLine("          group.appendChild(svg('line', { class: className, x1: start.x.toFixed(1), y1: start.y.toFixed(1), x2: end.x.toFixed(1), y2: end.y.toFixed(1) }));");
            builder.AppendLine("        }");
            builder.AppendLine("        samples.forEach(function (sample) {");
            builder.AppendLine("          const p = project.point(sample.position);");
            builder.AppendLine("          const className = 'curvature-point inspectable-sample-point' + (colorize ? ' ' + curvatureClass(sample) : '') + (selectedSampleIndex === sample.index ? ' is-selected' : '');");
            builder.AppendLine("          const circle = svg('circle', { class: className, 'data-sample-index': String(sample.index), cx: p.x.toFixed(1), cy: p.y.toFixed(1), r: selectedSampleIndex === sample.index ? 6 : 4, tabindex: '0', role: 'button', 'aria-label': 'curvature sample ' + sample.index });");
            builder.AppendLine("          const title = svg('title');");
            builder.AppendLine("          title.textContent = 'sample ' + sample.index + ' s=' + formatDistance(sample.distance) + ', ' + curvatureTitle(sample);");
            builder.AppendLine("          circle.appendChild(title);");
            builder.AppendLine("          wireSampleInspection(circle, sample, snapshot);");
            builder.AppendLine("          group.appendChild(circle);");
            builder.AppendLine("        });");
            builder.AppendLine("        const selected = sampleByIndexFromSamples(samples, selectedSampleIndex);");
            builder.AppendLine("        if (selected && selected.curvature !== null) {");
            builder.AppendLine("          const index = samples.indexOf(selected);");
            builder.AppendLine("          const p = project.point(selected.position);");
            builder.AppendLine("          const normal = markerNormal(samples, index, project);");
            builder.AppendLine("          appendText(group, p.x + normal.x * 32, p.y + normal.y * 32 - 4, 'R=' + formatRadius(selected.radius), 'radius-label');");
            builder.AppendLine("        }");
            builder.AppendLine("      }");
            builder.AppendLine("      function drawAxis(group, frame, direction, className, project) {");
            builder.AppendLine("        const origin = framePosition(frame);");
            builder.AppendLine("        const d = vec(direction);");
            builder.AppendLine("        const length = Math.hypot(d.x, d.z);");
            builder.AppendLine("        const start = project.point(origin);");
            builder.AppendLine("        let end;");
            builder.AppendLine("        if (length < 1e-9) {");
            builder.AppendLine("          end = { x: start.x, y: start.y - 15 };");
            builder.AppendLine("        } else {");
            builder.AppendLine("          const worldLength = Math.max(1.0, 26 / project.scale);");
            builder.AppendLine("          end = project.point({ x: origin.x + d.x / length * worldLength, y: origin.y, z: origin.z + d.z / length * worldLength });");
            builder.AppendLine("        }");
            builder.AppendLine("        group.appendChild(svg('line', { class: 'frame-axis ' + className, x1: start.x.toFixed(1), y1: start.y.toFixed(1), x2: end.x.toFixed(1), y2: end.y.toFixed(1) }));");
            builder.AppendLine("      }");
            builder.AppendLine("      function drawFrames(group, snapshot, project) {");
            builder.AppendLine("        const frames = asArray(snapshot.frames);");
            builder.AppendLine("        const step = Math.max(1, Math.ceil(frames.length / 24));");
            builder.AppendLine("        for (let i = 0; i < frames.length; i += step) {");
            builder.AppendLine("          const frame = frames[i];");
            builder.AppendLine("          drawAxis(group, frame, frame.tangent, 'frame-tangent', project);");
            builder.AppendLine("          drawAxis(group, frame, frame.normal, 'frame-normal', project);");
            builder.AppendLine("          drawAxis(group, frame, frame.binormal, 'frame-binormal', project);");
            builder.AppendLine("        }");
            builder.AppendLine("      }");
            builder.AppendLine("      function drawDebugLines(group, snapshot, project) {");
            builder.AppendLine("        asArray(snapshot.lines).forEach(function (line) {");
            builder.AppendLine("          const start = project.point(line.start);");
            builder.AppendLine("          const end = project.point(line.end);");
            builder.AppendLine("          const kind = cssToken(line.kind || 'diagnostic.line');");
            builder.AppendLine("          group.appendChild(svg('line', { class: 'debug-line ' + kind, x1: start.x.toFixed(1), y1: start.y.toFixed(1), x2: end.x.toFixed(1), y2: end.y.toFixed(1) }));");
            builder.AppendLine("        });");
            builder.AppendLine("      }");
            builder.AppendLine("      function drawBoxes(group, snapshot, project, boxes) {");
            builder.AppendLine("        const sourceBoxes = boxes || asArray(snapshot.boxes);");
            builder.AppendLine("        sourceBoxes.forEach(function (box) {");
            builder.AppendLine("          const points = boxCorners(box).map(function (corner) { const p = project.raw(corner); return p.x.toFixed(1) + ',' + p.y.toFixed(1); }).join(' ');");
            builder.AppendLine("          group.appendChild(svg('polygon', { class: 'train-box', points }));");
            builder.AppendLine("          if (box.label) { const p = project.point(box.frame && box.frame.position); appendText(group, p.x + 6, p.y - 6, box.label, 'train-label'); }");
            builder.AppendLine("        });");
            builder.AppendLine("      }");
            builder.AppendLine("      function drawBogies(group, snapshot, project, bogies) {");
            builder.AppendLine("        const sourceBogies = bogies || trainBogies(snapshot);");
            builder.AppendLine("        sourceBogies.forEach(function (marker) { const p = project.point(marker.frame.position); group.appendChild(svg('circle', { class: 'bogie-marker', cx: p.x.toFixed(1), cy: p.y.toFixed(1), r: 6 })); });");
            builder.AppendLine("      }");
            builder.AppendLine("      function drawWheels(group, snapshot, project, wheels) {");
            builder.AppendLine("        const sourceWheels = wheels || trainWheels(snapshot);");
            builder.AppendLine("        sourceWheels.forEach(function (marker) { const p = project.point(marker); group.appendChild(svg('circle', { class: 'wheel-marker', cx: p.x.toFixed(1), cy: p.y.toFixed(1), r: 3.5 })); });");
            builder.AppendLine("      }");
            builder.AppendLine("      function metric(label, value) {");
            builder.AppendLine("        const dt = document.createElement('dt');");
            builder.AppendLine("        const dd = document.createElement('dd');");
            builder.AppendLine("        dt.textContent = label;");
            builder.AppendLine("        dd.textContent = value;");
            builder.AppendLine("        metadataList.appendChild(dt);");
            builder.AppendLine("        metadataList.appendChild(dd);");
            builder.AppendLine("      }");
            builder.AppendLine("      function measurement(label, value) {");
            builder.AppendLine("        const dt = document.createElement('dt');");
            builder.AppendLine("        const dd = document.createElement('dd');");
            builder.AppendLine("        dt.textContent = label;");
            builder.AppendLine("        dd.textContent = value;");
            builder.AppendLine("        measurementList.appendChild(dt);");
            builder.AppendLine("        measurementList.appendChild(dd);");
            builder.AppendLine("      }");
            builder.AppendLine("      function renderMeasurement(sample, mode) {");
            builder.AppendLine("        clear(measurementList);");
            builder.AppendLine("        if (!sample) {");
            builder.AppendLine("          measurement('Sample', 'None');");
            builder.AppendLine("          measurement('Station', '<unselected>');");
            builder.AppendLine("          measurement('Position', '<unselected>');");
            builder.AppendLine("          measurement('Curvature', '<unselected>');");
            builder.AppendLine("          measurement('Radius', '<unselected>');");
            builder.AppendLine("          return;");
            builder.AppendLine("        }");
            builder.AppendLine("        measurement('Mode', mode);");
            builder.AppendLine("        measurement('Index', String(sample.index));");
            builder.AppendLine("        measurement('Station', formatDistance(sample.distance) + (sample.derived ? ' (derived)' : ''));");
            builder.AppendLine("        measurement('X', formatNumber(sample.position.x));");
            builder.AppendLine("        measurement('Y', formatNumber(sample.position.y));");
            builder.AppendLine("        measurement('Z', formatNumber(sample.position.z));");
            builder.AppendLine("        measurement('Curvature', formatCurvature(sample.curvature));");
            builder.AppendLine("        measurement('Radius', formatRadius(sample.radius));");
            builder.AppendLine("        measurement('Curvature source', sample.curvatureSource || '<unavailable>');");
            builder.AppendLine("      }");
            builder.AppendLine("      function sampleByIndex(snapshot, index) {");
            builder.AppendLine("        return sampleByIndexFromSamples(curvatureInspectionSamples(snapshot), index);");
            builder.AppendLine("      }");
            builder.AppendLine("      function renderSelectedMeasurement(snapshot) {");
            builder.AppendLine("        const sample = sampleByIndex(snapshot, selectedSampleIndex);");
            builder.AppendLine("        renderMeasurement(sample, sample ? 'Selected' : 'None');");
            builder.AppendLine("      }");
            builder.AppendLine("      function selectSample(sample, element) {");
            builder.AppendLine("        selectedSampleIndex = sample.index;");
            builder.AppendLine("        render(currentEntry);");
            builder.AppendLine("        const selected = currentEntry && currentEntry.snapshot ? sampleByIndex(currentEntry.snapshot, selectedSampleIndex) : sample;");
            builder.AppendLine("        renderMeasurement(selected || sample, 'Selected');");
            builder.AppendLine("        statusLine.textContent = 'Selected centerline sample ' + sample.index + ' at s=' + formatDistance(sample.distance) + '.';");
            builder.AppendLine("      }");
            builder.AppendLine("      function wireSampleInspection(element, sample, snapshot) {");
            builder.AppendLine("        function inspect() { element.classList.add('is-hovered'); renderMeasurement(sample, 'Hover'); }");
            builder.AppendLine("        function clearInspect() { element.classList.remove('is-hovered'); renderSelectedMeasurement(snapshot); }");
            builder.AppendLine("        element.addEventListener('mouseenter', inspect);");
            builder.AppendLine("        element.addEventListener('mouseleave', clearInspect);");
            builder.AppendLine("        element.addEventListener('focus', inspect);");
            builder.AppendLine("        element.addEventListener('blur', clearInspect);");
            builder.AppendLine("        element.addEventListener('click', function () { selectSample(sample, element); });");
            builder.AppendLine("        element.addEventListener('keydown', function (event) {");
            builder.AppendLine("          if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); selectSample(sample, element); }");
            builder.AppendLine("        });");
            builder.AppendLine("      }");
            builder.AppendLine("      function renderMetadata(entry) {");
            builder.AppendLine("        clear(metadataList);");
            builder.AppendLine("        if (!entry || !entry.snapshot) {");
            builder.AppendLine("          metric('Status', entry && entry.error ? entry.error : 'No DebugViewportSnapshotV1 snapshots found.');");
            builder.AppendLine("          return;");
            builder.AppendLine("        }");
            builder.AppendLine("        const snapshot = entry.snapshot;");
            builder.AppendLine("        const metadata = snapshot.metadata || {};");
            builder.AppendLine("        const bogieCount = trainBogies(snapshot).length;");
            builder.AppendLine("        const wheelCount = trainWheels(snapshot).length;");
            builder.AppendLine("        const distanceLabelCount = distanceMarkerIndexes(distanceSamples(snapshot)).length;");
            builder.AppendLine("        const trainAnimation = animatedTrain(snapshot, animationProgress);");
            builder.AppendLine("        const trainPoseCarCount = asArray(snapshot.trainPose && snapshot.trainPose.cars).length;");
            builder.AppendLine("        const curvatureSummary = summarizeCurvature(curvatureInspectionSamples(snapshot));");
            builder.AppendLine("        metric('Source', metadata.sourceFixtureName || entry.sourcePath || '<unspecified>');");
            builder.AppendLine("        metric('Contract', snapshot.contract || '<missing>');");
            builder.AppendLine("        metric('Version', String(snapshot.version));");
            builder.AppendLine("        metric('Units', metadata.units || '<unknown>');");
            builder.AppendLine("        metric('Animation', trainAnimation.supported ? 'Available' : 'Unavailable');");
            builder.AppendLine("        metric('TrainPose', snapshot.trainPose ? 'Present' : 'Absent');");
            builder.AppendLine("        metric('Train cars', String(trainPoseCarCount));");
            builder.AppendLine("        metric('Centerline', String(asArray(snapshot.centerlinePoints).length));");
            builder.AppendLine("        metric('Distance ticks', String(distanceLabelCount));");
            builder.AppendLine("        metric('Curvature', curvatureSummary);");
            builder.AppendLine("        metric('Frames', String(asArray(snapshot.frames).length));");
            builder.AppendLine("        metric('Lines', String(asArray(snapshot.lines).length));");
            builder.AppendLine("        metric('Boxes', String(asArray(snapshot.boxes).length));");
            builder.AppendLine("        metric('Bogies', String(bogieCount));");
            builder.AppendLine("        metric('Wheels', String(wheelCount));");
            builder.AppendLine("      }");
            builder.AppendLine("      function render(entry) {");
            builder.AppendLine("        const entryChanged = currentEntry !== entry;");
            builder.AppendLine("        currentEntry = entry;");
            builder.AppendLine("        if (entryChanged) { selectedSampleIndex = null; animationProgress = 0; stopAnimationClock(); }");
            builder.AppendLine("        viewport.setAttribute('viewBox', '0 0 ' + WIDTH + ' ' + HEIGHT);");
            builder.AppendLine("        clear(viewport);");
            builder.AppendLine("        viewport.appendChild(svg('rect', { class: 'plot-bg', x: 0, y: 0, width: WIDTH, height: HEIGHT }));");
            builder.AppendLine("        const grid = svg('g', { class: 'grid' });");
            builder.AppendLine("        viewport.appendChild(grid);");
            builder.AppendLine("        drawGrid(grid);");
            builder.AppendLine("        renderMetadata(entry);");
            builder.AppendLine("        if (!entry || !entry.snapshot) {");
            builder.AppendLine("          updateAnimationControls(null, null);");
            builder.AppendLine("          renderMeasurement(null, 'None');");
            builder.AppendLine("          appendText(viewport, PAD, PAD + 24, 'No valid DebugViewportSnapshotV1 JSON snapshots are embedded.', 'empty-message');");
            builder.AppendLine("          statusLine.textContent = entry && entry.error ? entry.error : 'No snapshot selected.';");
            builder.AppendLine("          return;");
            builder.AppendLine("        }");
            builder.AppendLine("        const snapshot = entry.snapshot;");
            builder.AppendLine("        const trainAnimation = animatedTrain(snapshot, animationProgress);");
            builder.AppendLine("        const project = projector(snapshot);");
            builder.AppendLine("        if (layerVisible('debugLines')) { drawDebugLines(viewport, snapshot, project); }");
            builder.AppendLine("        if (layerVisible('curvature')) { drawCurvature(viewport, snapshot, project); }");
            builder.AppendLine("        if (layerVisible('centerline')) { drawCenterline(viewport, snapshot, project); }");
            builder.AppendLine("        if (layerVisible('distances')) { drawDistanceMarkers(viewport, snapshot, project); }");
            builder.AppendLine("        if (layerVisible('frames')) { drawFrames(viewport, snapshot, project); }");
            builder.AppendLine("        if (layerVisible('boxes')) { drawBoxes(viewport, snapshot, project, trainAnimation.supported ? trainAnimation.boxes : null); }");
            builder.AppendLine("        if (layerVisible('bogies')) { drawBogies(viewport, snapshot, project, trainAnimation.supported ? trainAnimation.bogies : null); }");
            builder.AppendLine("        if (layerVisible('wheels')) { drawWheels(viewport, snapshot, project, trainAnimation.supported ? trainAnimation.wheels : null); }");
            builder.AppendLine("        renderSelectedMeasurement(snapshot);");
            builder.AppendLine("        updateAnimationControls(snapshot, trainAnimation);");
            builder.AppendLine("        statusLine.textContent = (entry.label || entry.sourcePath || 'Snapshot') + ' rendered from embedded or local DebugViewportSnapshotV1 JSON.' + (trainAnimation.supported ? ' Train animation preview is available.' : '');");
            builder.AppendLine("      }");
            builder.AppendLine("      function populateSelect() {");
            builder.AppendLine("        clear(select);");
            builder.AppendLine("        entries.forEach(function (entry, index) {");
            builder.AppendLine("          const option = document.createElement('option');");
            builder.AppendLine("          option.value = String(index);");
            builder.AppendLine("          option.textContent = entry.label || entry.sourcePath || ('snapshot ' + index);");
            builder.AppendLine("          option.disabled = !entry.snapshot;");
            builder.AppendLine("          select.appendChild(option);");
            builder.AppendLine("        });");
            builder.AppendLine("        const firstValidIndex = entries.findIndex(function (entry) { return !!entry.snapshot; });");
            builder.AppendLine("        if (firstValidIndex >= 0) { select.value = String(firstValidIndex); render(entries[firstValidIndex]); } else { select.disabled = true; render(entries[0] || null); }");
            builder.AppendLine("      }");
            builder.AppendLine("      select.addEventListener('change', function () { render(entries[Number(select.value)]); });");
            builder.AppendLine("      document.querySelectorAll('[data-layer]').forEach(function (input) { input.addEventListener('change', function () { render(currentEntry); }); });");
            builder.AppendLine("      playPauseButton.addEventListener('click', function () { if (animationPlaying) { pauseAnimation(); } else { startAnimation(); } });");
            builder.AppendLine("      timelineSlider.addEventListener('input', function () { pauseAnimation(); setAnimationProgress(Number(timelineSlider.value) / 1000, true); });");
            builder.AppendLine("      fileInput.addEventListener('change', function () {");
            builder.AppendLine("        const file = fileInput.files && fileInput.files[0];");
            builder.AppendLine("        if (!file) { return; }");
            builder.AppendLine("        const reader = new FileReader();");
            builder.AppendLine("        reader.onload = function () {");
            builder.AppendLine("          try {");
            builder.AppendLine("            const snapshot = JSON.parse(String(reader.result));");
            builder.AppendLine("            const entry = { label: file.name, sourcePath: file.name, snapshot, error: null };");
            builder.AppendLine("            entries.push(entry);");
            builder.AppendLine("            const option = document.createElement('option');");
            builder.AppendLine("            option.value = String(entries.length - 1);");
            builder.AppendLine("            option.textContent = file.name;");
            builder.AppendLine("            select.disabled = false;");
            builder.AppendLine("            select.appendChild(option);");
            builder.AppendLine("            select.value = option.value;");
            builder.AppendLine("            render(entry);");
            builder.AppendLine("          } catch (error) {");
            builder.AppendLine("            render({ label: file.name, sourcePath: file.name, snapshot: null, error: 'Failed to parse local JSON: ' + error.message });");
            builder.AppendLine("          }");
            builder.AppendLine("        };");
            builder.AppendLine("        reader.readAsText(file);");
            builder.AppendLine("      });");
            builder.AppendLine("      populateSelect();");
            builder.AppendLine("    }());");
            builder.AppendLine("  </script>");
        }

        private static void AppendAnimationScript(StringBuilder builder)
        {
            builder.Append(
"""
      function positiveNumber(value) {
        const number = optionalNumber(value);
        return number !== null && number > 0 ? number : null;
      }
      function firstPositive() {
        for (let i = 0; i < arguments.length; i += 1) {
          const value = positiveNumber(arguments[i]);
          if (value !== null) { return value; }
        }
        return 1;
      }
      function normalize3(value, fallback) {
        const vector = vec(value);
        const length = Math.hypot(vector.x, vector.y, vector.z);
        if (length < 1e-9) { return fallback; }
        return { x: vector.x / length, y: vector.y / length, z: vector.z / length };
      }
      function lerpNumber(a, b, t) { return a + (b - a) * t; }
      function lerpVector(a, b, t) {
        return {
          x: lerpNumber(a.x, b.x, t),
          y: lerpNumber(a.y, b.y, t),
          z: lerpNumber(a.z, b.z, t)
        };
      }
      function directionBetween(a, b, fallback) {
        return normalize3({ x: b.x - a.x, y: b.y - a.y, z: b.z - a.z }, fallback);
      }
      function fallbackBinormal(tangent) {
        return normalize3({ x: -tangent.z, y: 0, z: tangent.x }, { x: 0, y: 0, z: 1 });
      }
      function frameSampleFromFrame(frame, index) {
        const sample = sampleFromPosition(frame && frame.distance, frame && frame.position, index);
        if (!sample) { return null; }
        return {
          index: sample.index,
          distance: sample.distance,
          position: sample.position,
          tangent: vec(frame && frame.tangent),
          normal: vec(frame && frame.normal),
          binormal: vec(frame && frame.binormal),
          derived: sample.derived
        };
      }
      function resolveFrameSampleDistances(samples) {
        let cumulative = 0;
        let previous = null;
        return samples.map(function (sample) {
          if (previous) { cumulative += Math.hypot(sample.position.x - previous.x, sample.position.y - previous.y, sample.position.z - previous.z); }
          previous = sample.position;
          const value = sample.distance === null ? cumulative : sample.distance;
          return Object.assign({}, sample, { distance: value, derived: sample.distance === null });
        });
      }
      function normalizeFrameSample(sample, index, samples) {
        const previous = samples[Math.max(0, index - 1)] || sample;
        const next = samples[Math.min(samples.length - 1, index + 1)] || sample;
        const fallbackTangent = directionBetween(previous.position, next.position, { x: 1, y: 0, z: 0 });
        const tangent = normalize3(sample.tangent, fallbackTangent);
        const normal = normalize3(sample.normal, { x: 0, y: 1, z: 0 });
        const binormal = normalize3(sample.binormal, fallbackBinormal(tangent));
        return Object.assign({}, sample, { tangent, normal, binormal });
      }
      function generatedAnimationFrameSamples(snapshot) {
        const samples = distanceSamples(snapshot);
        if (samples.length < 2) { return []; }
        return samples.map(function (sample, index) {
          const previous = samples[Math.max(0, index - 1)];
          const next = samples[Math.min(samples.length - 1, index + 1)];
          const tangent = directionBetween(previous.position, next.position, { x: 1, y: 0, z: 0 });
          return {
            index: sample.index,
            distance: sample.distance,
            position: sample.position,
            tangent,
            normal: { x: 0, y: 1, z: 0 },
            binormal: fallbackBinormal(tangent),
            derived: true
          };
        });
      }
      function animationTrackSamples(snapshot) {
        const frameSamples = resolveFrameSampleDistances(asArray(snapshot && snapshot.frames).map(frameSampleFromFrame).filter(Boolean));
        if (frameSamples.length > 1) {
          return frameSamples.map(function (sample, index) { return normalizeFrameSample(sample, index, frameSamples); });
        }
        return generatedAnimationFrameSamples(snapshot);
      }
      function animationDistanceRange(samples) {
        if (samples.length < 2) { return { start: 0, end: 0, length: 0 }; }
        const start = finite(samples[0].distance, 0);
        const end = finite(samples[samples.length - 1].distance, start);
        return { start, end, length: Math.max(0, end - start) };
      }
      function wrapTrackDistance(distance, range) {
        const value = finite(distance, range.start);
        if (range.length < 1e-9) { return range.start; }
        const offset = ((value - range.start) % range.length + range.length) % range.length;
        return range.start + offset;
      }
      function interpolateAnimationFrame(samples, distance) {
        const range = animationDistanceRange(samples);
        const target = wrapTrackDistance(distance, range);
        for (let i = 0; i < samples.length - 1; i += 1) {
          const a = samples[i];
          const b = samples[i + 1];
          if (target <= b.distance || i === samples.length - 2) {
            const span = b.distance - a.distance;
            const t = span < 1e-9 ? 0 : Math.max(0, Math.min(1, (target - a.distance) / span));
            const segmentTangent = directionBetween(a.position, b.position, normalize3(a.tangent, { x: 1, y: 0, z: 0 }));
            const tangent = normalize3(lerpVector(a.tangent, b.tangent, t), segmentTangent);
            return {
              distance: target,
              position: lerpVector(a.position, b.position, t),
              tangent,
              normal: normalize3(lerpVector(a.normal, b.normal, t), { x: 0, y: 1, z: 0 }),
              binormal: normalize3(lerpVector(a.binormal, b.binormal, t), fallbackBinormal(tangent))
            };
          }
        }
        return samples[samples.length - 1];
      }
      function inferBoxSpacing(boxes) {
        const distances = boxes.map(function (box) { return optionalNumber(box && box.frame && box.frame.distance); }).filter(function (value) { return value !== null; });
        if (distances.length < 2) { return null; }
        let total = 0;
        for (let i = 1; i < distances.length; i += 1) { total += Math.abs(distances[i] - distances[i - 1]); }
        return total / (distances.length - 1);
      }
      function animationTrainLayout(snapshot) {
        const trainPose = snapshot && snapshot.trainPose;
        const definition = (trainPose && trainPose.definition) || {};
        const boxes = asArray(snapshot && snapshot.boxes).filter(function (box) { return !!box && !!box.size; });
        const cars = asArray(trainPose && trainPose.cars);
        const definitionCarCount = Math.max(0, Math.floor(finite(definition.carCount, 0)));
        const carCount = Math.max(boxes.length, cars.length, definitionCarCount);
        if (carCount <= 0) {
          return { carCount: 0, boxes, hasBogies: false, carGeometry: { length: 4.5, width: 1.8, height: 2.1 }, carSpacing: 5, bogieSpacing: 2.5, wheelLayout: null };
        }
        const firstBox = boxes[0] || {};
        const geometry = definition.carGeometry || {};
        const carGeometry = {
          length: firstPositive(geometry.length, firstBox.size && firstBox.size.length, 4.5),
          width: firstPositive(geometry.width, firstBox.size && firstBox.size.width, 1.8),
          height: firstPositive(geometry.height, firstBox.size && firstBox.size.height, 2.1)
        };
        const carSpacing = firstPositive(definition.carSpacing, inferBoxSpacing(boxes), carGeometry.length * 1.25);
        const bogieSpacing = firstPositive(definition.bogieLayout && definition.bogieLayout.bogieSpacing, carGeometry.length * 0.58);
        const wheelLayout = definition.wheelLayout || null;
        const hasBogies = cars.length > 0 || !!definition.bogieLayout;
        return { carCount, boxes, hasBogies, carGeometry, carSpacing, bogieSpacing, wheelLayout };
      }
      function animationCarLabel(layout, carIndex) {
        const box = layout.boxes[carIndex];
        return box && box.label ? box.label : 'car-' + carIndex;
      }
      function wheelOffsets(wheelLayout) {
        if (!wheelLayout) { return []; }
        const wheelCount = Math.max(0, Math.floor(finite(wheelLayout.wheelCountPerBogie, 0)));
        if (wheelCount <= 0) { return []; }
        const axleCount = Math.ceil(wheelCount / 2);
        const centeredAxleOffset = (axleCount - 1) * 0.5;
        const axleSpacing = finite(wheelLayout.axleSpacing, 0);
        const sideOffsetMagnitude = firstPositive(wheelLayout.wheelWidth, 0.25) * 0.5;
        const offsets = [];
        for (let i = 0; i < wheelCount; i += 1) {
          const axleIndex = Math.floor(i / 2);
          offsets.push({
            x: (axleIndex - centeredAxleOffset) * axleSpacing,
            y: (i % 2 === 0 ? -1 : 1) * sideOffsetMagnitude,
            z: 0
          });
        }
        return offsets;
      }
      function animatedTrain(snapshot, progress) {
        const samples = animationTrackSamples(snapshot);
        const layout = animationTrainLayout(snapshot);
        if (samples.length < 2 || layout.carCount <= 0) {
          return { supported: false, boxes: [], bogies: [], wheels: [], leadDistance: null, trackStart: 0, trackLength: 0 };
        }
        const range = animationDistanceRange(samples);
        if (range.length < 1e-9) {
          return { supported: false, boxes: [], bogies: [], wheels: [], leadDistance: null, trackStart: range.start, trackLength: 0 };
        }
        const baseLead = optionalNumber(snapshot && snapshot.trainPose && snapshot.trainPose.leadDistance);
        const leadDistance = (baseLead === null ? range.start : baseLead) + Math.max(0, Math.min(1, finite(progress, 0))) * range.length;
        const boxes = [];
        const bogies = [];
        const wheels = [];
        const offsets = wheelOffsets(layout.wheelLayout);
        for (let carIndex = 0; carIndex < layout.carCount; carIndex += 1) {
          const bodyDistance = leadDistance - carIndex * layout.carSpacing;
          const bodyFrame = interpolateAnimationFrame(samples, bodyDistance);
          boxes.push({ role: 'train.body', label: animationCarLabel(layout, carIndex), frame: bodyFrame, size: layout.carGeometry });
          if (layout.hasBogies) {
            [
              { label: 'car ' + carIndex + ' front bogie', distance: bodyDistance + layout.bogieSpacing * 0.5 },
              { label: 'car ' + carIndex + ' rear bogie', distance: bodyDistance - layout.bogieSpacing * 0.5 }
            ].forEach(function (bogie) {
              const bogieFrame = interpolateAnimationFrame(samples, bogie.distance);
              bogies.push({ label: bogie.label, frame: bogieFrame });
              offsets.forEach(function (offset) { wheels.push(addLocal(bogieFrame, offset.x, offset.y, offset.z)); });
            });
          }
        }
        return { supported: true, boxes, bogies, wheels, leadDistance, trackStart: range.start, trackLength: range.length };
      }
      function stopAnimationClock() {
        animationPlaying = false;
        lastAnimationTimestamp = null;
        if (animationFrameHandle !== null) {
          cancelAnimationFrame(animationFrameHandle);
          animationFrameHandle = null;
        }
      }
      function updateAnimationControls(snapshot, train) {
        const currentTrain = train || animatedTrain(snapshot, animationProgress);
        if (!currentTrain.supported) { stopAnimationClock(); }
        playPauseButton.disabled = !currentTrain.supported;
        timelineSlider.disabled = !currentTrain.supported;
        playPauseButton.textContent = animationPlaying ? 'Pause' : 'Play';
        timelineSlider.value = String(Math.round(animationProgress * 1000));
        timelineReadout.textContent = currentTrain.supported
          ? 'Lead ' + formatDistance(wrapTrackDistance(currentTrain.leadDistance, { start: currentTrain.trackStart, end: currentTrain.trackStart + currentTrain.trackLength, length: currentTrain.trackLength })) + ' / loop ' + formatDistance(currentTrain.trackLength)
          : 'Animation unavailable';
      }
      function setAnimationProgress(value, shouldRender) {
        animationProgress = Math.max(0, Math.min(1, finite(value, 0)));
        timelineSlider.value = String(Math.round(animationProgress * 1000));
        if (shouldRender && currentEntry) { render(currentEntry); } else { updateAnimationControls(currentEntry && currentEntry.snapshot, null); }
      }
      function stepAnimation(timestamp) {
        if (!animationPlaying || !currentEntry) { stopAnimationClock(); updateAnimationControls(currentEntry && currentEntry.snapshot, null); return; }
        if (lastAnimationTimestamp === null) { lastAnimationTimestamp = timestamp; }
        const elapsed = Math.max(0, timestamp - lastAnimationTimestamp);
        lastAnimationTimestamp = timestamp;
        animationProgress = (animationProgress + elapsed / 12000) % 1;
        timelineSlider.value = String(Math.round(animationProgress * 1000));
        render(currentEntry);
        animationFrameHandle = requestAnimationFrame(stepAnimation);
      }
      function startAnimation() {
        const currentTrain = animatedTrain(currentEntry && currentEntry.snapshot, animationProgress);
        if (!currentTrain.supported) { updateAnimationControls(currentEntry && currentEntry.snapshot, currentTrain); return; }
        animationPlaying = true;
        lastAnimationTimestamp = null;
        updateAnimationControls(currentEntry && currentEntry.snapshot, currentTrain);
        animationFrameHandle = requestAnimationFrame(stepAnimation);
      }
      function pauseAnimation() {
        stopAnimationClock();
        updateAnimationControls(currentEntry && currentEntry.snapshot, null);
      }
""");
        }

        private static string FormatTimestamp(DateTimeOffset value)
        {
            return value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        }

        private static string ToDisplayPath(string path)
        {
            return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }

        private static string Escape(string value)
        {
            return WebUtility.HtmlEncode(value);
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

        private sealed class SnapshotBrowserPayload
        {
            public string GeneratedAtUtc { get; set; } = string.Empty;

            public string ArtifactDirectory { get; set; } = string.Empty;

            public string OutputPath { get; set; } = string.Empty;

            public IReadOnlyList<SnapshotBrowserEntry> Entries { get; set; } = Array.Empty<SnapshotBrowserEntry>();
        }

        private sealed class SnapshotBrowserEntry
        {
            public string Label { get; set; } = string.Empty;

            public string SourcePath { get; set; } = string.Empty;

            public string SortKey { get; set; } = string.Empty;

            public JsonElement? Snapshot { get; set; }

            public string? Error { get; set; }

            public static SnapshotBrowserEntry Read(FileInfo snapshotFile, string artifactDirectory)
            {
                if (snapshotFile == null)
                {
                    throw new ArgumentNullException(nameof(snapshotFile));
                }

                string sourcePath = ToDisplayPath(Path.GetRelativePath(artifactDirectory, snapshotFile.FullName));
                string stem = NormalizeStem(Path.GetFileNameWithoutExtension(snapshotFile.Name));
                string label = CreateLabel(stem);

                try
                {
                    string json = File.ReadAllText(snapshotFile.FullName);
                    _ = DebugViewportSnapshotV1Json.Deserialize(json);
                    using JsonDocument document = JsonDocument.Parse(json);

                    return new SnapshotBrowserEntry
                    {
                        Label = label,
                        SourcePath = sourcePath,
                        SortKey = CreateSortKey(stem, sourcePath),
                        Snapshot = document.RootElement.Clone()
                    };
                }
                catch (Exception ex) when (IsReadOrParseException(ex))
                {
                    return new SnapshotBrowserEntry
                    {
                        Label = label,
                        SourcePath = sourcePath,
                        SortKey = CreateSortKey(stem, sourcePath),
                        Error = "Skipped " + sourcePath + ": " + ex.Message
                    };
                }
            }

            private static string NormalizeStem(string stem)
            {
                if (stem.EndsWith(".snapshot", StringComparison.OrdinalIgnoreCase))
                {
                    return stem.Substring(0, stem.Length - ".snapshot".Length);
                }

                return stem;
            }

            private static string CreateLabel(string stem)
            {
                if (string.Equals(stem, "DebugViewportSnapshotV1.sample", StringComparison.OrdinalIgnoreCase))
                {
                    return "Built-in sample";
                }

                if (string.Equals(stem, "DebugViewportSnapshotV1.banking-profile.sample", StringComparison.OrdinalIgnoreCase))
                {
                    return "BankingProfile train-pose sample";
                }

                if (stem.StartsWith("Milestone7.synthetic.", StringComparison.OrdinalIgnoreCase))
                {
                    string fixtureName = stem.Substring("Milestone7.synthetic.".Length).Replace('_', ' ');
                    return "Milestone 7 synthetic " + fixtureName;
                }

                return stem.Replace('.', ' ').Replace('_', ' ');
            }

            private static string CreateSortKey(string stem, string sourcePath)
            {
                if (string.Equals(stem, "DebugViewportSnapshotV1.sample", StringComparison.OrdinalIgnoreCase))
                {
                    return "0:0:" + sourcePath;
                }

                if (string.Equals(stem, "DebugViewportSnapshotV1.banking-profile.sample", StringComparison.OrdinalIgnoreCase))
                {
                    return "0:1:" + sourcePath;
                }

                if (stem.StartsWith("Milestone7.synthetic.", StringComparison.OrdinalIgnoreCase))
                {
                    return "1:" + sourcePath;
                }

                return "2:" + sourcePath;
            }
        }
    }
}
