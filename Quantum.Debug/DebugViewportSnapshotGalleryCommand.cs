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
    public static class DebugViewportSnapshotGalleryCommand
    {
        public const string CommandName = "debug-viewport-snapshot-v1-gallery";

        internal const string DefaultRelativeArtifactDirectory = "artifacts/debug-viewport";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

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

            IReadOnlyList<SnapshotGalleryEntry> entries = CollectEntries(resolvedArtifactDirectory);
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
            output.WriteLine($"Wrote DebugViewportSnapshotV1 static gallery to '{resolvedOutputHtmlPath}'.");
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
                return Path.Combine(artifactDirectory, DebugViewportSnapshotPreviewIndex.GalleryFileName);
            }

            return Path.GetFullPath(outputHtmlPath);
        }

        private static IReadOnlyList<SnapshotGalleryEntry> CollectEntries(string artifactDirectory)
        {
            var entries = new List<SnapshotGalleryEntry>();
            var previewFilesByBasePath = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
            var usedPreviewPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string previewPath in Directory.EnumerateFiles(artifactDirectory, "*.svg", SearchOption.AllDirectories))
            {
                var previewFile = new FileInfo(previewPath);
                previewFilesByBasePath[Path.ChangeExtension(previewFile.FullName, null) ?? previewFile.FullName] = previewFile;
            }

            foreach (string snapshotPath in Directory.EnumerateFiles(artifactDirectory, "*.json", SearchOption.AllDirectories))
            {
                var snapshotFile = new FileInfo(snapshotPath);
                string basePath = Path.ChangeExtension(snapshotFile.FullName, null) ?? snapshotFile.FullName;
                previewFilesByBasePath.TryGetValue(basePath, out FileInfo? previewFile);

                if (previewFile != null)
                {
                    usedPreviewPaths.Add(previewFile.FullName);
                }

                entries.Add(SnapshotGalleryEntry.Create(snapshotFile, previewFile));
            }

            foreach (FileInfo previewFile in previewFilesByBasePath.Values)
            {
                if (!usedPreviewPaths.Contains(previewFile.FullName))
                {
                    entries.Add(SnapshotGalleryEntry.Create(snapshotFile: null, previewFile));
                }
            }

            entries.Sort(CompareEntries);
            return entries;
        }

        private static int CompareEntries(SnapshotGalleryEntry left, SnapshotGalleryEntry right)
        {
            return string.Compare(left.SortKey, right.SortKey, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildHtml(
            string artifactDirectory,
            string outputHtmlPath,
            IReadOnlyList<SnapshotGalleryEntry> entries,
            DateTimeOffset generatedAtUtc)
        {
            var builder = new StringBuilder();
            string linkBaseDirectory = Path.GetDirectoryName(outputHtmlPath) ?? artifactDirectory;
            string displayDirectory = ToDisplayPath(Path.GetRelativePath(Environment.CurrentDirectory, artifactDirectory));

            builder.AppendLine("<!doctype html>");
            builder.AppendLine("<html lang=\"en\">");
            builder.AppendLine("<head>");
            builder.AppendLine("  <meta charset=\"utf-8\">");
            builder.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            builder.AppendLine("  <title>Quantum DebugViewportSnapshotV1 Gallery</title>");
            AppendStyles(builder);
            builder.AppendLine("</head>");
            builder.AppendLine("<body>");
            builder.AppendLine("  <main>");
            builder.AppendLine("    <header class=\"page-header\">");
            builder.AppendLine("      <p class=\"eyebrow\">Static debug gallery</p>");
            builder.AppendLine("      <h1>Quantum DebugViewportSnapshotV1 Gallery</h1>");
            builder.AppendLine("      <p class=\"lede\">Generated technical debug previews from renderer-neutral backend snapshots. Use this local artifact page to scan centerline samples, stable frames, debug lines, and simple train boxes without Unity, a full editor, or a production renderer.</p>");
            builder.AppendLine("      <p class=\"artifact-note\">Generated <code>artifacts/debug-viewport</code> output is ignored by default; regenerate these files when the backend artifacts change.</p>");
            builder.AppendLine("      <dl class=\"run-summary\">");
            AppendSummaryItem(builder, "Generated", FormatTimestamp(generatedAtUtc));
            AppendSummaryItem(builder, "Directory", displayDirectory);
            AppendSummaryItem(builder, "Cards", entries.Count.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("      </dl>");
            builder.AppendLine("    </header>");
            AppendArtifactGuide(builder);
            builder.AppendLine("    <section class=\"gallery\" aria-labelledby=\"gallery-title\">");
            builder.AppendLine("      <div class=\"section-heading\">");
            builder.AppendLine("        <h2 id=\"gallery-title\">Generated Snapshots</h2>");
            builder.AppendLine("      </div>");

            if (entries.Count == 0)
            {
                builder.AppendLine("      <p class=\"empty-state\">No DebugViewportSnapshotV1 JSON snapshots or SVG previews were found.</p>");
            }
            else
            {
                builder.AppendLine("      <div class=\"snapshot-grid\">");
                for (int i = 0; i < entries.Count; i++)
                {
                    AppendSnapshotCard(builder, entries[i], linkBaseDirectory);
                }

                builder.AppendLine("      </div>");
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
            builder.AppendLine("    body { margin: 0; font-family: Segoe UI, Arial, sans-serif; color: #111827; background: #f8fafc; }");
            builder.AppendLine("    main { width: min(1280px, calc(100% - 32px)); margin: 0 auto; padding: 28px 0 40px; }");
            builder.AppendLine("    .page-header { margin-bottom: 22px; }");
            builder.AppendLine("    .eyebrow { margin: 0 0 6px; color: #0f766e; font-size: 13px; font-weight: 700; text-transform: uppercase; letter-spacing: 0; }");
            builder.AppendLine("    h1 { margin: 0 0 10px; font-size: 26px; line-height: 1.2; }");
            builder.AppendLine("    h2 { margin: 0; font-size: 17px; line-height: 1.25; }");
            builder.AppendLine("    h3 { margin: 0; font-size: 16px; line-height: 1.3; }");
            builder.AppendLine("    p { line-height: 1.45; }");
            builder.AppendLine("    code { font-family: Consolas, Menlo, monospace; font-size: 0.95em; }");
            builder.AppendLine("    .lede, .artifact-note { max-width: 960px; margin: 0 0 10px; color: #475569; }");
            builder.AppendLine("    .run-summary { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 8px; max-width: 960px; margin: 16px 0 0; }");
            builder.AppendLine("    .run-summary div, .metadata-grid div { min-width: 0; padding: 10px 12px; border: 1px solid #cbd5e1; border-radius: 8px; background: #ffffff; }");
            builder.AppendLine("    dt { margin: 0 0 4px; color: #64748b; font-size: 12px; font-weight: 700; text-transform: uppercase; letter-spacing: 0; }");
            builder.AppendLine("    dd { margin: 0; min-width: 0; overflow-wrap: anywhere; color: #111827; font-size: 13px; }");
            builder.AppendLine("    .artifact-guide { margin: 0 0 22px; border: 1px solid #cbd5e1; border-radius: 8px; background: #ffffff; overflow: hidden; }");
            builder.AppendLine("    .artifact-guide h2 { padding: 13px 14px; border-bottom: 1px solid #e2e8f0; }");
            builder.AppendLine("    .artifact-guide dl { display: grid; grid-template-columns: minmax(120px, 170px) 1fr; margin: 0; }");
            builder.AppendLine("    .artifact-guide dt, .artifact-guide dd { padding: 11px 14px; border-top: 1px solid #e2e8f0; }");
            builder.AppendLine("    .artifact-guide dt { color: #111827; }");
            builder.AppendLine("    .artifact-guide dd { color: #475569; }");
            builder.AppendLine("    .section-heading { margin: 0 0 12px; }");
            builder.AppendLine("    .snapshot-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(520px, 1fr)); gap: 18px; align-items: start; }");
            builder.AppendLine("    .snapshot-card { min-width: 0; margin: 0; padding: 14px; border: 1px solid #cbd5e1; border-radius: 8px; background: #ffffff; }");
            builder.AppendLine("    .snapshot-card header { display: flex; gap: 12px; align-items: start; justify-content: space-between; margin-bottom: 12px; }");
            builder.AppendLine("    .source-label { margin: 4px 0 0; color: #64748b; font-size: 13px; overflow-wrap: anywhere; }");
            builder.AppendLine("    .snapshot-description { margin: 8px 0 12px; color: #475569; font-size: 13px; }");
            builder.AppendLine("    .status-pill { flex: 0 0 auto; padding: 3px 8px; border: 1px solid #99f6e4; border-radius: 999px; color: #115e59; background: #f0fdfa; font-size: 12px; font-weight: 700; }");
            builder.AppendLine("    .status-pill.warning { border-color: #fed7aa; color: #9a3412; background: #fff7ed; }");
            builder.AppendLine("    .metadata-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(132px, 1fr)); gap: 8px; margin: 0 0 12px; }");
            builder.AppendLine("    .artifact-links { display: flex; flex-wrap: wrap; gap: 10px; margin-bottom: 12px; }");
            builder.AppendLine("    .artifact-links a { display: inline-flex; align-items: center; min-height: 32px; padding: 6px 10px; border: 1px solid #0f766e; border-radius: 8px; color: #0f766e; text-decoration: none; font-weight: 700; font-size: 13px; }");
            builder.AppendLine("    .artifact-links span { display: inline-flex; align-items: center; min-height: 32px; color: #64748b; font-size: 13px; }");
            builder.AppendLine("    .preview-frame { display: block; border: 1px solid #e2e8f0; background: #ffffff; text-decoration: none; }");
            builder.AppendLine("    .preview-frame img { display: block; width: 100%; height: auto; }");
            builder.AppendLine("    .empty-state { margin: 0; padding: 14px; border: 1px solid #cbd5e1; border-radius: 8px; background: #ffffff; color: #475569; }");
            builder.AppendLine("    @media (max-width: 640px) { main { width: min(100% - 20px, 1280px); padding-top: 18px; } .artifact-guide dl { grid-template-columns: 1fr; } .snapshot-grid { grid-template-columns: 1fr; } .snapshot-card header { display: block; } .status-pill { display: inline-flex; margin-top: 8px; } }");
            builder.AppendLine("  </style>");
        }

        private static void AppendArtifactGuide(StringBuilder builder)
        {
            builder.AppendLine("    <section class=\"artifact-guide\" aria-labelledby=\"artifact-guide-title\">");
            builder.AppendLine("      <h2 id=\"artifact-guide-title\">Artifact Guide</h2>");
            builder.AppendLine("      <dl>");
            builder.AppendLine("        <dt>JSON snapshot</dt><dd>Renderer-neutral <code>DebugViewportSnapshotV1</code> source data for validation, importer checks, and optional thin debug adapters. It is the artifact to inspect when counts, distances, frame axes, or train-box placement look wrong.</dd>");
            builder.AppendLine("        <dt>SVG preview</dt><dd>Backend-only technical preview rendered from one JSON snapshot. It shows top-down X/Z, elevation/profile, raw exported samples, frame ticks, debug lines, and visual-only smoothing.</dd>");
            builder.AppendLine("        <dt>HTML gallery</dt><dd>This static local page collects generated SVG previews, pairs them with source JSON links, and summarizes snapshot metadata. It is not a frontend, renderer, editor, or contract change.</dd>");
            builder.AppendLine("      </dl>");
            builder.AppendLine("    </section>");
        }

        private static void AppendSummaryItem(StringBuilder builder, string label, string value)
        {
            builder.AppendLine("        <div><dt>" + Escape(label) + "</dt><dd>" + Escape(value) + "</dd></div>");
        }

        private static void AppendSnapshotCard(
            StringBuilder builder,
            SnapshotGalleryEntry entry,
            string linkBaseDirectory)
        {
            SnapshotGalleryMetadata metadata = entry.Metadata;
            string statusClass = metadata.HasMetadata ? "status-pill" : "status-pill warning";
            string statusText = metadata.HasMetadata ? "JSON read" : "Metadata unavailable";

            builder.AppendLine("        <article class=\"snapshot-card\">");
            builder.AppendLine("          <header>");
            builder.AppendLine("            <div>");
            builder.AppendLine("              <h3>" + Escape(entry.Label) + "</h3>");
            builder.AppendLine("              <p class=\"source-label\">Source: " + Escape(metadata.Source) + "</p>");
            builder.AppendLine("            </div>");
            builder.AppendLine("            <span class=\"" + statusClass + "\">" + Escape(statusText) + "</span>");
            builder.AppendLine("          </header>");
            builder.AppendLine("          <p class=\"snapshot-description\">" + Escape(entry.Description) + "</p>");
            builder.AppendLine("          <dl class=\"metadata-grid\">");
            AppendMetadataItem(builder, "Contract", metadata.Contract, code: metadata.HasMetadata);
            AppendMetadataItem(builder, "Version", metadata.Version);
            AppendMetadataItem(builder, "Sample count", metadata.SampleCount);
            AppendMetadataItem(builder, "Box count", metadata.BoxCount);
            AppendMetadataItem(builder, "Line count", metadata.LineCount);
            AppendMetadataItem(builder, "TrainPose", metadata.TrainPosePresence);
            AppendMetadataItem(builder, "Train cars", metadata.TrainPoseCarCount);
            AppendMetadataItem(builder, "Units", metadata.Units);
            builder.AppendLine("          </dl>");
            builder.AppendLine("          <div class=\"artifact-links\">");
            AppendArtifactLink(builder, entry.SnapshotFile, linkBaseDirectory, "Source JSON");
            AppendArtifactLink(builder, entry.PreviewFile, linkBaseDirectory, "SVG preview");
            builder.AppendLine("          </div>");

            if (entry.PreviewFile != null)
            {
                string previewHref = FormatHtmlAttributePath(entry.PreviewFile, linkBaseDirectory);
                builder.AppendLine("          <a class=\"preview-frame\" href=\"" + previewHref + "\">");
                builder.AppendLine("            <img src=\"" + previewHref + "\" alt=\"" + Escape(entry.Label) + " SVG preview\">");
                builder.AppendLine("          </a>");
            }

            builder.AppendLine("        </article>");
        }

        private static void AppendMetadataItem(StringBuilder builder, string label, string value, bool code = false)
        {
            builder.Append("            <div><dt>");
            builder.Append(Escape(label));
            builder.Append("</dt><dd>");
            if (code)
            {
                builder.Append("<code>");
            }

            builder.Append(Escape(value));
            if (code)
            {
                builder.Append("</code>");
            }

            builder.AppendLine("</dd></div>");
        }

        private static void AppendArtifactLink(
            StringBuilder builder,
            FileInfo? file,
            string linkBaseDirectory,
            string label)
        {
            if (file == null)
            {
                builder.AppendLine("            <span>" + Escape(label) + " not generated</span>");
                return;
            }

            string href = FormatHtmlAttributePath(file, linkBaseDirectory);
            builder.AppendLine(
                "            <a href=\"" +
                href +
                "\">" +
                Escape(label) +
                ": " +
                Escape(file.Name) +
                "</a>");
        }

        private static string FormatHtmlAttributePath(FileInfo file, string linkBaseDirectory)
        {
            string linkPath = ToDisplayPath(Path.GetRelativePath(linkBaseDirectory, file.FullName));
            return WebUtility.HtmlEncode(linkPath);
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

        private sealed class SnapshotGalleryEntry
        {
            private SnapshotGalleryEntry(
                FileInfo? snapshotFile,
                FileInfo? previewFile,
                string label,
                string description,
                string sortKey,
                SnapshotGalleryMetadata metadata)
            {
                SnapshotFile = snapshotFile;
                PreviewFile = previewFile;
                Label = label;
                Description = description;
                SortKey = sortKey;
                Metadata = metadata;
            }

            public FileInfo? SnapshotFile { get; }

            public FileInfo? PreviewFile { get; }

            public string Label { get; }

            public string Description { get; }

            public string SortKey { get; }

            public SnapshotGalleryMetadata Metadata { get; }

            public static SnapshotGalleryEntry Create(FileInfo? snapshotFile, FileInfo? previewFile)
            {
                string stem = GetStem(snapshotFile, previewFile);
                return new SnapshotGalleryEntry(
                    snapshotFile,
                    previewFile,
                    CreateLabel(stem),
                    CreateDescription(stem),
                    CreateSortKey(stem),
                    SnapshotGalleryMetadata.Read(snapshotFile));
            }

            private static string GetStem(FileInfo? snapshotFile, FileInfo? previewFile)
            {
                string fileName = snapshotFile?.Name ?? previewFile?.Name ?? string.Empty;
                string stem = Path.GetFileNameWithoutExtension(fileName);
                if (stem.EndsWith(".snapshot", StringComparison.OrdinalIgnoreCase))
                {
                    stem = stem.Substring(0, stem.Length - ".snapshot".Length);
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

            private static string CreateDescription(string stem)
            {
                if (string.Equals(stem, "DebugViewportSnapshotV1.sample", StringComparison.OrdinalIgnoreCase))
                {
                    return "Built-in backend sample with sampled centerline, stable frames, debug axes, simple train boxes, and nested TrainPoseExportV1.";
                }

                if (string.Equals(stem, "DebugViewportSnapshotV1.banking-profile.sample", StringComparison.OrdinalIgnoreCase))
                {
                    return "Opt-in BankingProfile train-pose snapshot with oriented train boxes and nested TrainPoseExportV1 data for pose inspection.";
                }

                if (stem.StartsWith("Milestone7.synthetic.", StringComparison.OrdinalIgnoreCase))
                {
                    string fixtureName = stem.Substring("Milestone7.synthetic.".Length).Replace('_', ' ');
                    return "Milestone 7 synthetic " + fixtureName + " CSV fixture converted for centerline and frame preview.";
                }

                return "DebugViewportSnapshotV1 snapshot output and paired backend-only SVG preview.";
            }

            private static string CreateSortKey(string stem)
            {
                if (string.Equals(stem, "DebugViewportSnapshotV1.sample", StringComparison.OrdinalIgnoreCase))
                {
                    return "0:0:" + stem;
                }

                if (string.Equals(stem, "DebugViewportSnapshotV1.banking-profile.sample", StringComparison.OrdinalIgnoreCase))
                {
                    return "0:1:" + stem;
                }

                if (stem.StartsWith("Milestone7.synthetic.", StringComparison.OrdinalIgnoreCase))
                {
                    return "1:" + stem;
                }

                return "2:" + stem;
            }
        }

        private sealed class SnapshotGalleryMetadata
        {
            private SnapshotGalleryMetadata(
                bool hasMetadata,
                string contract,
                string version,
                string source,
                string units,
                string sampleCount,
                string boxCount,
                string lineCount,
                string trainPosePresence,
                string trainPoseCarCount)
            {
                HasMetadata = hasMetadata;
                Contract = contract;
                Version = version;
                Source = source;
                Units = units;
                SampleCount = sampleCount;
                BoxCount = boxCount;
                LineCount = lineCount;
                TrainPosePresence = trainPosePresence;
                TrainPoseCarCount = trainPoseCarCount;
            }

            public bool HasMetadata { get; }

            public string Contract { get; }

            public string Version { get; }

            public string Source { get; }

            public string Units { get; }

            public string SampleCount { get; }

            public string BoxCount { get; }

            public string LineCount { get; }

            public string TrainPosePresence { get; }

            public string TrainPoseCarCount { get; }

            public static SnapshotGalleryMetadata Read(FileInfo? snapshotFile)
            {
                if (snapshotFile == null)
                {
                    return Unavailable("no source JSON");
                }

                try
                {
                    string json = File.ReadAllText(snapshotFile.FullName);
                    DebugViewportSnapshotV1Dto dto = DebugViewportSnapshotV1Json.Deserialize(json);
                    DebugViewportMetadataV1Dto? metadata = dto.Metadata;

                    return new SnapshotGalleryMetadata(
                        hasMetadata: true,
                        contract: dto.Contract,
                        version: dto.Version.ToString(CultureInfo.InvariantCulture),
                        source: string.IsNullOrWhiteSpace(metadata?.SourceFixtureName) ? snapshotFile.Name : metadata!.SourceFixtureName!,
                        units: string.IsNullOrWhiteSpace(metadata?.Units) ? "<unknown>" : metadata!.Units,
                        sampleCount: ResolveSampleCount(dto, metadata).ToString(CultureInfo.InvariantCulture),
                        boxCount: Count(dto.Boxes).ToString(CultureInfo.InvariantCulture),
                        lineCount: Count(dto.Lines).ToString(CultureInfo.InvariantCulture),
                        trainPosePresence: dto.TrainPose == null ? "No" : "Yes",
                        trainPoseCarCount: Count(dto.TrainPose?.Cars).ToString(CultureInfo.InvariantCulture));
                }
                catch (Exception ex) when (IsReadOrParseException(ex))
                {
                    return Unavailable(snapshotFile.Name);
                }
            }

            private static SnapshotGalleryMetadata Unavailable(string source)
            {
                return new SnapshotGalleryMetadata(
                    hasMetadata: false,
                    contract: "<unavailable>",
                    version: "<unavailable>",
                    source: source,
                    units: "<unavailable>",
                    sampleCount: "<unavailable>",
                    boxCount: "<unavailable>",
                    lineCount: "<unavailable>",
                    trainPosePresence: "<unavailable>",
                    trainPoseCarCount: "<unavailable>");
            }

            private static int ResolveSampleCount(
                DebugViewportSnapshotV1Dto dto,
                DebugViewportMetadataV1Dto? metadata)
            {
                if (metadata != null && metadata.SampleCount > 0)
                {
                    return metadata.SampleCount;
                }

                return Count(dto.CenterlinePoints);
            }

            private static int Count<T>(T[]? values)
            {
                return values == null ? 0 : values.Length;
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
}
