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
            builder.AppendLine("        <fieldset class=\"layer-list\">");
            builder.AppendLine("          <legend>Layers</legend>");
            AppendLayerToggle(builder, "centerline", "Centerline samples");
            AppendLayerToggle(builder, "frames", "Frame axes");
            AppendLayerToggle(builder, "debugLines", "Debug lines");
            AppendLayerToggle(builder, "boxes", "Train boxes");
            AppendLayerToggle(builder, "bogies", "Bogie markers");
            AppendLayerToggle(builder, "wheels", "Wheel markers");
            builder.AppendLine("        </fieldset>");
            builder.AppendLine("        <section class=\"metadata-panel\" aria-labelledby=\"metadata-title\">");
            builder.AppendLine("          <h2 id=\"metadata-title\">Metadata</h2>");
            builder.AppendLine("          <dl id=\"metadataList\"></dl>");
            builder.AppendLine("        </section>");
            builder.AppendLine("      </aside>");
            builder.AppendLine("      <section class=\"viewport-panel\" aria-labelledby=\"viewport-title\">");
            builder.AppendLine("        <div class=\"viewport-header\">");
            builder.AppendLine("          <div>");
            builder.AppendLine("            <h2 id=\"viewport-title\">Top-down X/Z Inspection</h2>");
            builder.AppendLine("            <p id=\"statusLine\">Select a snapshot to render centerline, frames, debug lines, train boxes, bogies, and wheels.</p>");
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
            builder.AppendLine("    .run-summary div, .metadata-panel dd { min-width: 0; overflow-wrap: anywhere; }");
            builder.AppendLine("    .run-summary div { padding: 9px 11px; border: 1px solid #cbd5e1; border-radius: 8px; background: #ffffff; }");
            builder.AppendLine("    dt { margin: 0 0 4px; color: #697789; font-size: 12px; font-weight: 700; text-transform: uppercase; letter-spacing: 0; }");
            builder.AppendLine("    dd { margin: 0; color: #18212f; font-size: 13px; }");
            builder.AppendLine("    .viewer-shell { display: grid; grid-template-columns: minmax(260px, 320px) minmax(0, 1fr); gap: 16px; align-items: start; }");
            builder.AppendLine("    .controls, .viewport-panel { border: 1px solid #cbd5e1; border-radius: 8px; background: #ffffff; }");
            builder.AppendLine("    .controls { padding: 14px; }");
            builder.AppendLine("    .field-label { display: block; margin: 0 0 6px; color: #334155; font-size: 13px; font-weight: 700; }");
            builder.AppendLine("    select, input[type=file] { width: 100%; min-height: 36px; margin: 0 0 14px; font: 13px Segoe UI, Arial, sans-serif; }");
            builder.AppendLine("    .layer-list { margin: 0 0 14px; padding: 10px 12px 12px; border: 1px solid #d8e0eb; border-radius: 8px; }");
            builder.AppendLine("    .layer-list legend { padding: 0 5px; color: #334155; font-size: 13px; font-weight: 700; }");
            builder.AppendLine("    .layer-list label { display: flex; align-items: center; gap: 8px; min-height: 28px; color: #253244; font-size: 13px; }");
            builder.AppendLine("    .metadata-panel { border-top: 1px solid #e2e8f0; padding-top: 13px; }");
            builder.AppendLine("    .metadata-panel h2 { margin-bottom: 9px; }");
            builder.AppendLine("    .metadata-panel dl { display: grid; grid-template-columns: minmax(82px, 108px) minmax(0, 1fr); gap: 8px 10px; margin: 0; }");
            builder.AppendLine("    .viewport-panel { min-width: 0; overflow: hidden; }");
            builder.AppendLine("    .viewport-header { display: flex; gap: 12px; align-items: start; justify-content: space-between; padding: 14px 16px; border-bottom: 1px solid #e2e8f0; }");
            builder.AppendLine("    .viewport-header p { margin: 5px 0 0; color: #526173; font-size: 13px; }");
            builder.AppendLine("    .axis-note { flex: 0 0 auto; max-width: 260px; text-align: right; }");
            builder.AppendLine("    #viewport { display: block; width: 100%; min-height: 620px; background: #fbfcfe; }");
            builder.AppendLine("    .plot-bg { fill: #fbfcfe; }");
            builder.AppendLine("    .grid-line { stroke: #e4e9f0; stroke-width: 1; }");
            builder.AppendLine("    .centerline-path { fill: none; stroke: #0f766e; stroke-width: 3; stroke-linecap: round; stroke-linejoin: round; }");
            builder.AppendLine("    .sample-point { fill: #ffffff; stroke: #0f766e; stroke-width: 2; }");
            builder.AppendLine("    .frame-tangent { stroke: #2563eb; }");
            builder.AppendLine("    .frame-normal { stroke: #d97706; }");
            builder.AppendLine("    .frame-binormal { stroke: #7c3aed; }");
            builder.AppendLine("    .frame-axis { stroke-width: 1.6; stroke-linecap: round; }");
            builder.AppendLine("    .debug-line { stroke: #475569; stroke-width: 1.8; stroke-dasharray: 5 4; stroke-linecap: round; }");
            builder.AppendLine("    .debug-line.tangent { stroke: #2563eb; }");
            builder.AppendLine("    .debug-line.normal { stroke: #d97706; }");
            builder.AppendLine("    .debug-line.binormal { stroke: #7c3aed; }");
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
            builder.AppendLine("      const statusLine = document.getElementById('statusLine');");
            builder.AppendLine("      const viewport = document.getElementById('viewport');");
            builder.AppendLine("      const fileInput = document.getElementById('fileInput');");
            builder.AppendLine("      let currentEntry = null;");
            builder.AppendLine();
            builder.AppendLine("      function asArray(value) { return Array.isArray(value) ? value : []; }");
            builder.AppendLine("      function finite(value, fallback) { const number = Number(value); return Number.isFinite(number) ? number : fallback; }");
            builder.AppendLine("      function vec(value) { return { x: finite(value && value.x, 0), y: finite(value && value.y, 0), z: finite(value && value.z, 0) }; }");
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
            builder.AppendLine("      function collectBounds(snapshot) {");
            builder.AppendLine("        const points = [];");
            builder.AppendLine("        function push(value) { const p = vec(value); points.push({ x: p.x, z: p.z }); }");
            builder.AppendLine("        asArray(snapshot && snapshot.centerlinePoints).forEach(function (point) { push(point && point.position); });");
            builder.AppendLine("        asArray(snapshot && snapshot.frames).forEach(function (frame) { push(frame && frame.position); });");
            builder.AppendLine("        asArray(snapshot && snapshot.lines).forEach(function (line) { push(line && line.start); push(line && line.end); });");
            builder.AppendLine("        asArray(snapshot && snapshot.boxes).forEach(function (box) { boxCorners(box).forEach(function (corner) { points.push(corner); }); });");
            builder.AppendLine("        trainBogies(snapshot).forEach(function (marker) { push(marker.frame && marker.frame.position); });");
            builder.AppendLine("        trainWheels(snapshot).forEach(function (marker) { points.push({ x: marker.x, z: marker.z }); });");
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
            builder.AppendLine("        const points = asArray(snapshot.centerlinePoints);");
            builder.AppendLine("        if (points.length > 1) {");
            builder.AppendLine("          const polyline = points.map(function (point) { const p = project.point(point.position); return p.x.toFixed(1) + ',' + p.y.toFixed(1); }).join(' ');");
            builder.AppendLine("          group.appendChild(svg('polyline', { class: 'centerline-path', points: polyline }));");
            builder.AppendLine("        }");
            builder.AppendLine("        points.forEach(function (point) { const p = project.point(point.position); group.appendChild(svg('circle', { class: 'sample-point', cx: p.x.toFixed(1), cy: p.y.toFixed(1), r: 4 })); });");
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
            builder.AppendLine("          const kind = String(line.kind || '').toLowerCase();");
            builder.AppendLine("          group.appendChild(svg('line', { class: 'debug-line ' + kind, x1: start.x.toFixed(1), y1: start.y.toFixed(1), x2: end.x.toFixed(1), y2: end.y.toFixed(1) }));");
            builder.AppendLine("        });");
            builder.AppendLine("      }");
            builder.AppendLine("      function drawBoxes(group, snapshot, project) {");
            builder.AppendLine("        asArray(snapshot.boxes).forEach(function (box) {");
            builder.AppendLine("          const points = boxCorners(box).map(function (corner) { const p = project.raw(corner); return p.x.toFixed(1) + ',' + p.y.toFixed(1); }).join(' ');");
            builder.AppendLine("          group.appendChild(svg('polygon', { class: 'train-box', points }));");
            builder.AppendLine("          if (box.label) { const p = project.point(box.frame && box.frame.position); appendText(group, p.x + 6, p.y - 6, box.label, 'train-label'); }");
            builder.AppendLine("        });");
            builder.AppendLine("      }");
            builder.AppendLine("      function drawBogies(group, snapshot, project) {");
            builder.AppendLine("        trainBogies(snapshot).forEach(function (marker) { const p = project.point(marker.frame.position); group.appendChild(svg('circle', { class: 'bogie-marker', cx: p.x.toFixed(1), cy: p.y.toFixed(1), r: 6 })); });");
            builder.AppendLine("      }");
            builder.AppendLine("      function drawWheels(group, snapshot, project) {");
            builder.AppendLine("        trainWheels(snapshot).forEach(function (marker) { const p = project.point(marker); group.appendChild(svg('circle', { class: 'wheel-marker', cx: p.x.toFixed(1), cy: p.y.toFixed(1), r: 3.5 })); });");
            builder.AppendLine("      }");
            builder.AppendLine("      function metric(label, value) {");
            builder.AppendLine("        const dt = document.createElement('dt');");
            builder.AppendLine("        const dd = document.createElement('dd');");
            builder.AppendLine("        dt.textContent = label;");
            builder.AppendLine("        dd.textContent = value;");
            builder.AppendLine("        metadataList.appendChild(dt);");
            builder.AppendLine("        metadataList.appendChild(dd);");
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
            builder.AppendLine("        metric('Source', metadata.sourceFixtureName || entry.sourcePath || '<unspecified>');");
            builder.AppendLine("        metric('Contract', snapshot.contract || '<missing>');");
            builder.AppendLine("        metric('Version', String(snapshot.version));");
            builder.AppendLine("        metric('Units', metadata.units || '<unknown>');");
            builder.AppendLine("        metric('Centerline', String(asArray(snapshot.centerlinePoints).length));");
            builder.AppendLine("        metric('Frames', String(asArray(snapshot.frames).length));");
            builder.AppendLine("        metric('Lines', String(asArray(snapshot.lines).length));");
            builder.AppendLine("        metric('Boxes', String(asArray(snapshot.boxes).length));");
            builder.AppendLine("        metric('Bogies', String(bogieCount));");
            builder.AppendLine("        metric('Wheels', String(wheelCount));");
            builder.AppendLine("      }");
            builder.AppendLine("      function render(entry) {");
            builder.AppendLine("        currentEntry = entry;");
            builder.AppendLine("        viewport.setAttribute('viewBox', '0 0 ' + WIDTH + ' ' + HEIGHT);");
            builder.AppendLine("        clear(viewport);");
            builder.AppendLine("        viewport.appendChild(svg('rect', { class: 'plot-bg', x: 0, y: 0, width: WIDTH, height: HEIGHT }));");
            builder.AppendLine("        const grid = svg('g', { class: 'grid' });");
            builder.AppendLine("        viewport.appendChild(grid);");
            builder.AppendLine("        drawGrid(grid);");
            builder.AppendLine("        renderMetadata(entry);");
            builder.AppendLine("        if (!entry || !entry.snapshot) {");
            builder.AppendLine("          appendText(viewport, PAD, PAD + 24, 'No valid DebugViewportSnapshotV1 JSON snapshots are embedded.', 'empty-message');");
            builder.AppendLine("          statusLine.textContent = entry && entry.error ? entry.error : 'No snapshot selected.';");
            builder.AppendLine("          return;");
            builder.AppendLine("        }");
            builder.AppendLine("        const snapshot = entry.snapshot;");
            builder.AppendLine("        const project = projector(snapshot);");
            builder.AppendLine("        if (layerVisible('debugLines')) { drawDebugLines(viewport, snapshot, project); }");
            builder.AppendLine("        if (layerVisible('centerline')) { drawCenterline(viewport, snapshot, project); }");
            builder.AppendLine("        if (layerVisible('frames')) { drawFrames(viewport, snapshot, project); }");
            builder.AppendLine("        if (layerVisible('boxes')) { drawBoxes(viewport, snapshot, project); }");
            builder.AppendLine("        if (layerVisible('bogies')) { drawBogies(viewport, snapshot, project); }");
            builder.AppendLine("        if (layerVisible('wheels')) { drawWheels(viewport, snapshot, project); }");
            builder.AppendLine("        statusLine.textContent = (entry.label || entry.sourcePath || 'Snapshot') + ' rendered from embedded or local DebugViewportSnapshotV1 JSON.';");
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

            public DebugViewportSnapshotV1Dto? Snapshot { get; set; }

            public string? Error { get; set; }

            public static SnapshotBrowserEntry Read(FileInfo snapshotFile, string artifactDirectory)
            {
                if (snapshotFile == null)
                {
                    throw new ArgumentNullException(nameof(snapshotFile));
                }

                string sourcePath = ToDisplayPath(Path.GetRelativePath(artifactDirectory, snapshotFile.FullName));
                string label = CreateLabel(Path.GetFileNameWithoutExtension(snapshotFile.Name));

                try
                {
                    string json = File.ReadAllText(snapshotFile.FullName);
                    DebugViewportSnapshotV1Dto dto = DebugViewportSnapshotV1Json.Deserialize(json);

                    return new SnapshotBrowserEntry
                    {
                        Label = label,
                        SourcePath = sourcePath,
                        SortKey = CreateSortKey(label, sourcePath),
                        Snapshot = dto
                    };
                }
                catch (Exception ex) when (IsReadOrParseException(ex))
                {
                    return new SnapshotBrowserEntry
                    {
                        Label = label,
                        SourcePath = sourcePath,
                        SortKey = CreateSortKey(label, sourcePath),
                        Error = "Skipped " + sourcePath + ": " + ex.Message
                    };
                }
            }

            private static string CreateLabel(string stem)
            {
                if (stem.EndsWith(".snapshot", StringComparison.OrdinalIgnoreCase))
                {
                    stem = stem.Substring(0, stem.Length - ".snapshot".Length);
                }

                if (string.Equals(stem, "DebugViewportSnapshotV1.sample", StringComparison.OrdinalIgnoreCase))
                {
                    return "Built-in sample";
                }

                if (stem.StartsWith("Milestone7.synthetic.", StringComparison.OrdinalIgnoreCase))
                {
                    string fixtureName = stem.Substring("Milestone7.synthetic.".Length).Replace('_', ' ');
                    return "Milestone 7 synthetic " + fixtureName;
                }

                return stem.Replace('.', ' ').Replace('_', ' ');
            }

            private static string CreateSortKey(string label, string sourcePath)
            {
                if (string.Equals(label, "Built-in sample", StringComparison.OrdinalIgnoreCase))
                {
                    return "0:" + sourcePath;
                }

                if (label.StartsWith("Milestone 7 synthetic ", StringComparison.OrdinalIgnoreCase))
                {
                    return "1:" + sourcePath;
                }

                return "2:" + sourcePath;
            }
        }
    }
}
