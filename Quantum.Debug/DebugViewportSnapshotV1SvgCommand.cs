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
        private const double DefaultCanvasWidth = 1120.0;
        private const double DefaultCanvasHeight = 760.0;
        private const double PagePadding = 32.0;
        private const double HeaderHeight = 144.0;
        private const double PanelGap = 22.0;
        private const double TopDownPanelHeight = 360.0;
        private const double MinimumWorldSpan = 1.0;
        private const double MinimumDistanceSpan = 1.0;
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
            DebugViewportSnapshotPreviewIndex.TryWriteForGeneratedOutput(resolvedOutputPath, output);
            return 0;
        }

        internal static string BuildSvg(DebugViewportSnapshotV1Dto dto, string? sourceFileName = null)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            SvgRect topDownPanel = new SvgRect(
                PagePadding,
                HeaderHeight,
                DefaultCanvasWidth - PagePadding * 2.0,
                TopDownPanelHeight);
            SvgRect elevationPanel = new SvgRect(
                PagePadding,
                topDownPanel.Bottom + PanelGap,
                topDownPanel.Width,
                DefaultCanvasHeight - topDownPanel.Bottom - PanelGap - PagePadding);
            SvgRect topDownPlotArea = CreatePlotArea(topDownPanel, left: 52.0, top: 52.0, right: 36.0, bottom: 42.0);
            SvgRect elevationPlotArea = CreatePlotArea(elevationPanel, left: 64.0, top: 52.0, right: 34.0, bottom: 48.0);

            TopDownProjection topDownProjection = TopDownProjection.Create(dto, topDownPlotArea);
            ElevationProjection elevationProjection = ElevationProjection.Create(dto, elevationPlotArea);
            var builder = new StringBuilder();

            builder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            builder.AppendLine(
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"" +
                Format(DefaultCanvasWidth) +
                "\" height=\"" +
                Format(DefaultCanvasHeight) +
                "\" viewBox=\"0 0 " +
                Format(DefaultCanvasWidth) +
                " " +
                Format(DefaultCanvasHeight) +
                "\" role=\"img\" aria-labelledby=\"title desc\">");
            builder.AppendLine("  <title id=\"title\">Quantum DebugViewportSnapshotV1 Technical Preview</title>");
            builder.AppendLine("  <desc id=\"desc\">Backend-only multi-panel centerline preview generated from renderer-neutral debug data. Raw exported samples remain visible; smooth-preview paths are Catmull-Rom visual approximations only.</desc>");
            AppendStyles(builder);
            builder.AppendLine(
                "  <rect width=\"" +
                Format(DefaultCanvasWidth) +
                "\" height=\"" +
                Format(DefaultCanvasHeight) +
                "\" fill=\"#f8fafc\" />");

            AppendHeader(builder, dto, sourceFileName);
            AppendPanelFrame(
                builder,
                topDownPanel,
                "top-down X/Z centerline preview",
                "Plan view for centerline sanity checks; raw samples and preview-only smoothing are shown separately.");
            AppendPlotGrid(builder, topDownPlotArea);
            AppendTopDownAxisLabels(builder, topDownPlotArea);
            AppendTopDownCenterline(builder, dto, topDownProjection);
            AppendTopDownDebugLines(builder, dto, topDownProjection);
            AppendTopDownBoxes(builder, dto, topDownProjection);
            AppendFrameTicks(builder, dto, topDownProjection);

            AppendPanelFrame(
                builder,
                elevationPanel,
                "elevation/profile preview (distance vs Y)",
                "Distance vs Y profile; smoothing is a visual approximation over exported samples.");
            AppendPlotGrid(builder, elevationPlotArea);
            AppendElevationAxisLabels(builder, elevationPlotArea);
            AppendElevationProfile(builder, dto, elevationProjection);

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

        private static void AppendStyles(StringBuilder builder)
        {
            builder.AppendLine("  <style>");
            builder.AppendLine("    .title { font: 600 20px Segoe UI, Arial, sans-serif; fill: #111827; }");
            builder.AppendLine("    .subtitle, .metadata, .axis-label, .legend text, .panel-subtitle { font: 12px Segoe UI, Arial, sans-serif; fill: #475569; }");
            builder.AppendLine("    .panel-title { font: 600 15px Segoe UI, Arial, sans-serif; fill: #111827; }");
            builder.AppendLine("    .panel { fill: #ffffff; stroke: #cbd5e1; stroke-width: 1; }");
            builder.AppendLine("    .plot-area { fill: #ffffff; stroke: #cbd5e1; stroke-width: 1; }");
            builder.AppendLine("    .grid-line { stroke: #e2e8f0; stroke-width: 1; }");
            builder.AppendLine("    .centerline { fill: none; stroke-linecap: round; stroke-linejoin: round; }");
            builder.AppendLine("    .raw-centerline { stroke: #64748b; stroke-width: 1.4; stroke-dasharray: 5 5; opacity: 0.9; }");
            builder.AppendLine("    .smooth-preview { fill: none; stroke: #0f766e; stroke-width: 3.4; stroke-linecap: round; stroke-linejoin: round; }");
            builder.AppendLine("    .sample-point { fill: #ffffff; stroke: #0f766e; stroke-width: 1.75; }");
            builder.AppendLine("    .tangent-tick { stroke: #2563eb; stroke-width: 1.5; stroke-linecap: round; }");
            builder.AppendLine("    .binormal-tick { stroke: #7c3aed; stroke-width: 1.5; stroke-linecap: round; }");
            builder.AppendLine("    .debug-line { stroke: #475569; stroke-width: 1.8; stroke-dasharray: 5 4; stroke-linecap: round; opacity: 0.9; }");
            builder.AppendLine("    .debug-line-kind-frame-axis-tangent { stroke: #2563eb; }");
            builder.AppendLine("    .debug-line-kind-frame-axis-normal { stroke: #d97706; }");
            builder.AppendLine("    .debug-line-kind-frame-axis-binormal { stroke: #7c3aed; }");
            builder.AppendLine("    .train-box { fill: #ccfbf1; fill-opacity: 0.38; stroke: #0f766e; stroke-width: 1.8; stroke-linejoin: round; }");
            builder.AppendLine("    .train-box-role-train-body, .train-box-role-train-body-banking-profile { fill: #99f6e4; }");
            builder.AppendLine("    .train-box-forward { stroke: #0f766e; stroke-width: 1.4; stroke-linecap: round; }");
            builder.AppendLine("    .train-box-label { font: 11px Segoe UI, Arial, sans-serif; fill: #0f766e; stroke: #ffffff; stroke-width: 3; paint-order: stroke; stroke-linejoin: round; }");
            builder.AppendLine("    .baseline { stroke: #94a3b8; stroke-width: 1.25; stroke-dasharray: 5 5; }");
            builder.AppendLine("    .empty-message { font: 14px Segoe UI, Arial, sans-serif; fill: #b91c1c; }");
            builder.AppendLine("  </style>");
        }

        private static void AppendHeader(StringBuilder builder, DebugViewportSnapshotV1Dto dto, string? sourceFileName)
        {
            DebugViewportMetadataV1Dto? metadata = dto.Metadata;
            string source = string.IsNullOrWhiteSpace(metadata?.SourceFixtureName)
                ? "<unspecified source>"
                : metadata!.SourceFixtureName!;
            string units = string.IsNullOrWhiteSpace(metadata?.Units) ? "<unknown units>" : metadata!.Units;
            string file = string.IsNullOrWhiteSpace(sourceFileName) ? "<memory>" : sourceFileName!;

            builder.AppendLine("  <text class=\"title\" x=\"32\" y=\"34\">DebugViewportSnapshotV1 Technical Preview</text>");
            builder.AppendLine("  <text class=\"subtitle\" x=\"32\" y=\"58\">Backend-only SVG debug preview; not a renderer, editor, or frontend.</text>");
            AppendText(builder, "metadata", 32.0, 82.0, "source: " + Shorten(source, 72));
            AppendText(
                builder,
                "metadata",
                32.0,
                102.0,
                "file: " +
                Shorten(file, 52) +
                " | units: " +
                Shorten(units, 20) +
                " | centerline points: " +
                FormatCount(dto.CenterlinePoints) +
                " | frames: " +
                FormatCount(dto.Frames));

            AppendText(
                builder,
                "metadata",
                32.0,
                122.0,
                "boxes: " +
                FormatCount(dto.Boxes) +
                " | debug lines: " +
                FormatCount(dto.Lines) +
                " | nested TrainPoseExportV1: " +
                FormatTrainPosePresence(dto) +
                " | train cars: " +
                FormatTrainPoseCarCount(dto));

            AppendLegend(builder, DefaultCanvasWidth - PagePadding - 330.0, 18.0, dto);
        }

        private static SvgRect CreatePlotArea(
            SvgRect panel,
            double left,
            double top,
            double right,
            double bottom)
        {
            return new SvgRect(
                panel.X + left,
                panel.Y + top,
                panel.Width - left - right,
                panel.Height - top - bottom);
        }

        private static void AppendPanelFrame(
            StringBuilder builder,
            SvgRect panel,
            string title,
            string subtitle)
        {
            builder.AppendLine(
                "  <rect class=\"panel\" x=\"" +
                Format(panel.X) +
                "\" y=\"" +
                Format(panel.Y) +
                "\" width=\"" +
                Format(panel.Width) +
                "\" height=\"" +
                Format(panel.Height) +
                "\" rx=\"8\" />");
            AppendText(builder, "panel-title", panel.X + 16.0, panel.Y + 26.0, title);
            AppendText(builder, "panel-subtitle", panel.X + 16.0, panel.Y + 44.0, subtitle);
        }

        private static void AppendPlotGrid(StringBuilder builder, SvgRect plotArea)
        {
            builder.AppendLine(
                "  <rect class=\"plot-area\" x=\"" +
                Format(plotArea.X) +
                "\" y=\"" +
                Format(plotArea.Y) +
                "\" width=\"" +
                Format(plotArea.Width) +
                "\" height=\"" +
                Format(plotArea.Height) +
                "\" />");

            for (int i = 1; i < 4; i++)
            {
                double x = plotArea.X + plotArea.Width * i / 4.0;
                builder.AppendLine(
                    "  <line class=\"grid-line\" x1=\"" +
                    Format(x) +
                    "\" y1=\"" +
                    Format(plotArea.Y) +
                    "\" x2=\"" +
                    Format(x) +
                    "\" y2=\"" +
                    Format(plotArea.Bottom) +
                    "\" />");
            }

            for (int i = 1; i < 4; i++)
            {
                double y = plotArea.Y + plotArea.Height * i / 4.0;
                builder.AppendLine(
                    "  <line class=\"grid-line\" x1=\"" +
                    Format(plotArea.X) +
                    "\" y1=\"" +
                    Format(y) +
                    "\" x2=\"" +
                    Format(plotArea.Right) +
                    "\" y2=\"" +
                    Format(y) +
                    "\" />");
            }
        }

        private static void AppendTopDownAxisLabels(StringBuilder builder, SvgRect plotArea)
        {
            builder.AppendLine(
                "  <text class=\"axis-label\" x=\"" +
                Format(plotArea.Right - 2.0) +
                "\" y=\"" +
                Format(plotArea.Bottom + 24.0) +
                "\" text-anchor=\"end\">X (m)</text>");
            builder.AppendLine(
                "  <text class=\"axis-label\" x=\"" +
                Format(plotArea.X + 2.0) +
                "\" y=\"" +
                Format(plotArea.Y - 10.0) +
                "\">Z (m)</text>");
        }

        private static void AppendElevationAxisLabels(StringBuilder builder, SvgRect plotArea)
        {
            builder.AppendLine(
                "  <text class=\"axis-label\" x=\"" +
                Format(plotArea.X + plotArea.Width * 0.5) +
                "\" y=\"" +
                Format(plotArea.Bottom + 32.0) +
                "\" text-anchor=\"middle\">station distance (m)</text>");
            builder.AppendLine(
                "  <text class=\"axis-label\" transform=\"translate(" +
                Format(plotArea.X - 44.0) +
                " " +
                Format(plotArea.Y + plotArea.Height * 0.5) +
                ") rotate(-90)\" text-anchor=\"middle\">Y elevation (m)</text>");
        }

        private static void AppendTopDownCenterline(
            StringBuilder builder,
            DebugViewportSnapshotV1Dto dto,
            TopDownProjection projection)
        {
            DebugViewportCenterlinePointV1Dto[] points = dto.CenterlinePoints ?? Array.Empty<DebugViewportCenterlinePointV1Dto>();
            if (points.Length == 0)
            {
                AppendText(builder, "empty-message", projection.PlotArea.X + 20.0, projection.PlotArea.Y + 32.0, "No centerline points found.");
                return;
            }

            SvgPoint[] projectedPoints = ProjectTopDownPoints(points, projection);
            AppendPolyline(builder, "centerline top-down-centerline raw-centerline", projectedPoints);
            AppendSmoothPreviewPath(builder, "smooth-preview top-down-smooth-preview", projectedPoints);
            AppendSamplePoints(builder, "top-down-raw-samples", "top-down-sample-point", projectedPoints);
        }

        private static void AppendElevationProfile(
            StringBuilder builder,
            DebugViewportSnapshotV1Dto dto,
            ElevationProjection projection)
        {
            DebugViewportCenterlinePointV1Dto[] points = dto.CenterlinePoints ?? Array.Empty<DebugViewportCenterlinePointV1Dto>();
            if (points.Length == 0)
            {
                AppendText(builder, "empty-message", projection.PlotArea.X + 20.0, projection.PlotArea.Y + 32.0, "No centerline points found.");
                return;
            }

            if (projection.TryProjectBaseline(0.0, out SvgPoint baselineStart, out SvgPoint baselineEnd))
            {
                builder.AppendLine(
                    "  <line class=\"baseline\" x1=\"" +
                    Format(baselineStart.X) +
                    "\" y1=\"" +
                    Format(baselineStart.Y) +
                    "\" x2=\"" +
                    Format(baselineEnd.X) +
                    "\" y2=\"" +
                    Format(baselineEnd.Y) +
                    "\" />");
            }

            SvgPoint[] projectedPoints = ProjectElevationPoints(points, projection);
            AppendPolyline(builder, "centerline elevation-centerline raw-centerline", projectedPoints);
            AppendSmoothPreviewPath(builder, "smooth-preview elevation-smooth-preview", projectedPoints);
            AppendSamplePoints(builder, "elevation-raw-samples", "elevation-sample-point", projectedPoints);
        }

        private static void AppendFrameTicks(
            StringBuilder builder,
            DebugViewportSnapshotV1Dto dto,
            TopDownProjection projection)
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
                AppendAxisTick(builder, projection, frame.Position, frame.Tangent, "tangent-tick", FrameTickWorldLength);
                AppendAxisTick(builder, projection, frame.Position, frame.Binormal, "binormal-tick", FrameTickWorldLength * 0.8);
            }
        }

        private static void AppendTopDownDebugLines(
            StringBuilder builder,
            DebugViewportSnapshotV1Dto dto,
            TopDownProjection projection)
        {
            DebugViewportLineSegmentV1Dto[] lines = dto.Lines ?? Array.Empty<DebugViewportLineSegmentV1Dto>();
            if (lines.Length == 0)
            {
                return;
            }

            builder.AppendLine("  <g class=\"top-down-debug-lines\" aria-label=\"debug lines\">");
            for (int i = 0; i < lines.Length; i++)
            {
                DebugViewportLineSegmentV1Dto line = lines[i];
                SvgPoint startPoint = projection.Project(line.Start);
                SvgPoint endPoint = projection.Project(line.End);
                string kind = NormalizeCssToken(line.Kind, "unknown");
                string displayKind = string.IsNullOrWhiteSpace(line.Kind) ? "unknown" : line.Kind;

                builder.AppendLine(
                    "    <line class=\"debug-line debug-line-kind-" +
                    kind +
                    "\" data-kind=\"" +
                    Escape(displayKind) +
                    "\" x1=\"" +
                    Format(startPoint.X) +
                    "\" y1=\"" +
                    Format(startPoint.Y) +
                    "\" x2=\"" +
                    Format(endPoint.X) +
                    "\" y2=\"" +
                    Format(endPoint.Y) +
                    "\"><title>" +
                    Escape("debug line: " + displayKind) +
                    "</title></line>");
            }

            builder.AppendLine("  </g>");
        }

        private static void AppendTopDownBoxes(
            StringBuilder builder,
            DebugViewportSnapshotV1Dto dto,
            TopDownProjection projection)
        {
            DebugViewportBoxV1Dto[] boxes = dto.Boxes ?? Array.Empty<DebugViewportBoxV1Dto>();
            if (boxes.Length == 0)
            {
                return;
            }

            builder.AppendLine("  <g class=\"top-down-train-boxes\" aria-label=\"train and debug boxes\">");
            for (int i = 0; i < boxes.Length; i++)
            {
                DebugViewportBoxV1Dto box = boxes[i];
                TopDownWorldPoint[] corners = GetBoxTopDownCorners(box);
                string role = string.IsNullOrWhiteSpace(box.Role) ? "box" : box.Role;
                string label = string.IsNullOrWhiteSpace(box.Label) ? role + " " + i.ToString(CultureInfo.InvariantCulture) : box.Label!;
                string roleClass = NormalizeCssToken(role, "box");

                builder.Append("    <polygon class=\"train-box train-box-role-");
                builder.Append(roleClass);
                builder.Append("\" data-role=\"");
                builder.Append(Escape(role));
                builder.Append("\" points=\"");
                for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
                {
                    SvgPoint point = projection.Project(corners[cornerIndex]);
                    AppendPolylinePoint(builder, point, cornerIndex);
                }

                builder.Append("\"><title>");
                builder.Append(Escape(label));
                builder.AppendLine("</title></polygon>");

                AppendBoxForwardLine(builder, box, projection);

                if (!string.IsNullOrWhiteSpace(box.Label))
                {
                    SvgPoint labelPoint = projection.Project(box.Frame.Position);
                    AppendText(builder, "train-box-label", labelPoint.X + 6.0, labelPoint.Y - 6.0, box.Label!);
                }
            }

            builder.AppendLine("  </g>");
        }

        private static void AppendBoxForwardLine(
            StringBuilder builder,
            DebugViewportBoxV1Dto box,
            TopDownProjection projection)
        {
            TopDownWorldPoint center = new TopDownWorldPoint(box.Frame.Position.X, box.Frame.Position.Z);
            TopDownWorldPoint tangent = NormalizeTopDownDirection(
                box.Frame.Tangent,
                new TopDownWorldPoint(1.0, 0.0));
            double halfLength = box.Size.Length * 0.5;
            TopDownWorldPoint nose = new TopDownWorldPoint(
                center.X + tangent.X * halfLength,
                center.Z + tangent.Z * halfLength);
            SvgPoint startPoint = projection.Project(center);
            SvgPoint endPoint = projection.Project(nose);

            builder.AppendLine(
                "    <line class=\"train-box-forward\" x1=\"" +
                Format(startPoint.X) +
                "\" y1=\"" +
                Format(startPoint.Y) +
                "\" x2=\"" +
                Format(endPoint.X) +
                "\" y2=\"" +
                Format(endPoint.Y) +
                "\" />");
        }

        private static void AppendAxisTick(
            StringBuilder builder,
            TopDownProjection projection,
            DebugViewportVector3dV1Dto origin,
            DebugViewportVector3dV1Dto direction,
            string className,
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
                "  <line class=\"" +
                className +
                "\" x1=\"" +
                Format(startPoint.X) +
                "\" y1=\"" +
                Format(startPoint.Y) +
                "\" x2=\"" +
                Format(endPoint.X) +
                "\" y2=\"" +
                Format(endPoint.Y) +
                "\" />");
        }

        private static void AppendLegend(
            StringBuilder builder,
            double x,
            double y,
            DebugViewportSnapshotV1Dto dto)
        {
            builder.AppendLine(
                "  <g class=\"legend\" transform=\"translate(" +
                Format(x) +
                " " +
                Format(y) +
                ")\">");
            builder.AppendLine("    <rect x=\"0\" y=\"0\" width=\"330\" height=\"116\" rx=\"8\" fill=\"#ffffff\" stroke=\"#cbd5e1\" />");
            builder.AppendLine("    <circle cx=\"31\" cy=\"20\" r=\"3\" fill=\"#ffffff\" stroke=\"#0f766e\" stroke-width=\"1.75\" />");
            builder.AppendLine("    <text x=\"58\" y=\"24\">raw samples / exported points</text>");
            builder.AppendLine("    <line x1=\"14\" y1=\"42\" x2=\"48\" y2=\"42\" stroke=\"#64748b\" stroke-width=\"1.4\" stroke-dasharray=\"5 5\" stroke-linecap=\"round\" />");
            builder.AppendLine("    <text x=\"58\" y=\"46\">raw sampled centerline</text>");
            builder.AppendLine("    <line x1=\"14\" y1=\"62\" x2=\"48\" y2=\"62\" stroke=\"#0f766e\" stroke-width=\"3.4\" stroke-linecap=\"round\" />");
            builder.AppendLine("    <text x=\"58\" y=\"66\">smoothed visual preview only</text>");
            builder.AppendLine("    <line x1=\"14\" y1=\"82\" x2=\"33\" y2=\"82\" stroke=\"#2563eb\" stroke-width=\"1.5\" stroke-linecap=\"round\" />");
            builder.AppendLine("    <line x1=\"33\" y1=\"82\" x2=\"48\" y2=\"82\" stroke=\"#7c3aed\" stroke-width=\"1.5\" stroke-linecap=\"round\" />");
            builder.AppendLine("    <text x=\"58\" y=\"86\">frame ticks / debug lines (" + FormatCount(dto.Lines) + ")</text>");
            builder.AppendLine("    <rect x=\"17\" y=\"98\" width=\"28\" height=\"10\" fill=\"#ccfbf1\" fill-opacity=\"0.38\" stroke=\"#0f766e\" stroke-width=\"1.4\" />");
            builder.AppendLine("    <text x=\"58\" y=\"108\">train boxes (" + FormatCount(dto.Boxes) + "), TrainPose: " + FormatTrainPosePresence(dto) + "</text>");
            builder.AppendLine("  </g>");
        }

        private static SvgPoint[] ProjectTopDownPoints(
            DebugViewportCenterlinePointV1Dto[] points,
            TopDownProjection projection)
        {
            var projectedPoints = new SvgPoint[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                projectedPoints[i] = projection.Project(points[i].Position);
            }

            return projectedPoints;
        }

        private static SvgPoint[] ProjectElevationPoints(
            DebugViewportCenterlinePointV1Dto[] points,
            ElevationProjection projection)
        {
            var projectedPoints = new SvgPoint[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                projectedPoints[i] = projection.Project(points[i].Distance, points[i].Position.Y);
            }

            return projectedPoints;
        }

        private static void AppendText(
            StringBuilder builder,
            string className,
            double x,
            double y,
            string value)
        {
            builder.AppendLine(
                "  <text class=\"" +
                className +
                "\" x=\"" +
                Format(x) +
                "\" y=\"" +
                Format(y) +
                "\">" +
                Escape(value) +
                "</text>");
        }

        private static void AppendPolylinePoint(StringBuilder builder, SvgPoint point, int index)
        {
            if (index > 0)
            {
                builder.Append(' ');
            }

            builder.Append(Format(point.X));
            builder.Append(',');
            builder.Append(Format(point.Y));
        }

        private static void AppendPathPoint(StringBuilder builder, SvgPoint point)
        {
            builder.Append(Format(point.X));
            builder.Append(',');
            builder.Append(Format(point.Y));
        }

        private static void AppendPolyline(StringBuilder builder, string className, SvgPoint[] points)
        {
            builder.Append("  <polyline class=\"");
            builder.Append(className);
            builder.Append("\" points=\"");
            for (int i = 0; i < points.Length; i++)
            {
                AppendPolylinePoint(builder, points[i], i);
            }

            builder.AppendLine("\" />");
        }

        private static void AppendSmoothPreviewPath(StringBuilder builder, string className, SvgPoint[] points)
        {
            if (points.Length < 3)
            {
                return;
            }

            builder.Append("  <path class=\"");
            builder.Append(className);
            builder.Append("\" d=\"M ");
            AppendPathPoint(builder, points[0]);

            for (int i = 0; i < points.Length - 1; i++)
            {
                SvgPoint previous = i == 0 ? points[i] : points[i - 1];
                SvgPoint start = points[i];
                SvgPoint end = points[i + 1];
                SvgPoint next = i + 2 < points.Length ? points[i + 2] : end;

                // Catmull-Rom converted to cubic Beziers for SVG-only preview smoothing.
                SvgPoint control1 = new SvgPoint(
                    start.X + (end.X - previous.X) / 6.0,
                    start.Y + (end.Y - previous.Y) / 6.0);
                SvgPoint control2 = new SvgPoint(
                    end.X - (next.X - start.X) / 6.0,
                    end.Y - (next.Y - start.Y) / 6.0);

                builder.Append(" C ");
                AppendPathPoint(builder, control1);
                builder.Append(' ');
                AppendPathPoint(builder, control2);
                builder.Append(' ');
                AppendPathPoint(builder, end);
            }

            builder.AppendLine("\" />");
        }

        private static void AppendSamplePoints(
            StringBuilder builder,
            string groupClassName,
            string pointClassName,
            SvgPoint[] points)
        {
            builder.AppendLine(
                "  <g class=\"" +
                groupClassName +
                "\" aria-label=\"raw exported sample points\">");
            for (int i = 0; i < points.Length; i++)
            {
                AppendSamplePoint(builder, pointClassName, points[i]);
            }

            builder.AppendLine("  </g>");
        }

        private static void AppendSamplePoint(StringBuilder builder, string pointClassName, SvgPoint point)
        {
            builder.AppendLine(
                "    <circle class=\"raw-sample-point sample-point " +
                pointClassName +
                "\" cx=\"" +
                Format(point.X) +
                "\" cy=\"" +
                Format(point.Y) +
                "\" r=\"3\" />");
        }

        private static string Shorten(string value, int maxLength)
        {
            if (value.Length <= maxLength)
            {
                return value;
            }

            if (maxLength <= 3)
            {
                return value.Substring(0, maxLength);
            }

            int prefixLength = (maxLength - 3) / 2;
            int suffixLength = maxLength - prefixLength - 3;
            return value.Substring(0, prefixLength) + "..." + value.Substring(value.Length - suffixLength);
        }

        private static string FormatCount<T>(T[]? values)
        {
            return values == null ? "0" : values.Length.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatTrainPosePresence(DebugViewportSnapshotV1Dto dto)
        {
            return dto.TrainPose == null ? "no" : "yes";
        }

        private static string FormatTrainPoseCarCount(DebugViewportSnapshotV1Dto dto)
        {
            return dto.TrainPose?.Cars == null
                ? "0"
                : dto.TrainPose.Cars.Length.ToString(CultureInfo.InvariantCulture);
        }

        private static string NormalizeCssToken(string? value, string fallback)
        {
            string source = string.IsNullOrWhiteSpace(value) ? fallback : value!;
            var builder = new StringBuilder(source.Length);
            bool previousDash = false;

            for (int i = 0; i < source.Length; i++)
            {
                char c = char.ToLowerInvariant(source[i]);
                bool isTokenCharacter = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                if (isTokenCharacter)
                {
                    builder.Append(c);
                    previousDash = false;
                }
                else if (!previousDash)
                {
                    builder.Append('-');
                    previousDash = true;
                }
            }

            string token = builder.ToString().Trim('-');
            return token.Length == 0 ? fallback : token;
        }

        private static TopDownWorldPoint[] GetBoxTopDownCorners(DebugViewportBoxV1Dto box)
        {
            TopDownWorldPoint center = new TopDownWorldPoint(box.Frame.Position.X, box.Frame.Position.Z);
            TopDownWorldPoint tangent = NormalizeTopDownDirection(
                box.Frame.Tangent,
                new TopDownWorldPoint(1.0, 0.0));
            TopDownWorldPoint binormal = NormalizeTopDownDirection(
                box.Frame.Binormal,
                new TopDownWorldPoint(-tangent.Z, tangent.X));
            double halfLength = box.Size.Length * 0.5;
            double halfWidth = box.Size.Width * 0.5;

            return new[]
            {
                new TopDownWorldPoint(
                    center.X + tangent.X * halfLength + binormal.X * halfWidth,
                    center.Z + tangent.Z * halfLength + binormal.Z * halfWidth),
                new TopDownWorldPoint(
                    center.X + tangent.X * halfLength - binormal.X * halfWidth,
                    center.Z + tangent.Z * halfLength - binormal.Z * halfWidth),
                new TopDownWorldPoint(
                    center.X - tangent.X * halfLength - binormal.X * halfWidth,
                    center.Z - tangent.Z * halfLength - binormal.Z * halfWidth),
                new TopDownWorldPoint(
                    center.X - tangent.X * halfLength + binormal.X * halfWidth,
                    center.Z - tangent.Z * halfLength + binormal.Z * halfWidth)
            };
        }

        private static TopDownWorldPoint NormalizeTopDownDirection(
            DebugViewportVector3dV1Dto direction,
            TopDownWorldPoint fallback)
        {
            double length = System.Math.Sqrt(direction.X * direction.X + direction.Z * direction.Z);
            if (length <= 1e-9)
            {
                return fallback;
            }

            return new TopDownWorldPoint(direction.X / length, direction.Z / length);
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

        private readonly struct SvgRect
        {
            public SvgRect(double x, double y, double width, double height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }

            public double X { get; }

            public double Y { get; }

            public double Width { get; }

            public double Height { get; }

            public double Right
            {
                get { return X + Width; }
            }

            public double Bottom
            {
                get { return Y + Height; }
            }
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

        private readonly struct TopDownWorldPoint
        {
            public TopDownWorldPoint(double x, double z)
            {
                X = x;
                Z = z;
            }

            public double X { get; }

            public double Z { get; }
        }

        private sealed class TopDownProjection
        {
            private readonly double _minX;
            private readonly double _minZ;
            private readonly double _scale;
            private readonly double _offsetX;
            private readonly double _offsetY;
            private readonly double _scaledDepth;

            private TopDownProjection(
                SvgRect plotArea,
                double minX,
                double minZ,
                double scale,
                double offsetX,
                double offsetY,
                double scaledDepth)
            {
                PlotArea = plotArea;
                _minX = minX;
                _minZ = minZ;
                _scale = scale;
                _offsetX = offsetX;
                _offsetY = offsetY;
                _scaledDepth = scaledDepth;
            }

            public SvgRect PlotArea { get; }

            public static TopDownProjection Create(DebugViewportSnapshotV1Dto dto, SvgRect plotArea)
            {
                double minX = double.PositiveInfinity;
                double maxX = double.NegativeInfinity;
                double minZ = double.PositiveInfinity;
                double maxZ = double.NegativeInfinity;

                IncludeCenterline(dto.CenterlinePoints, ref minX, ref maxX, ref minZ, ref maxZ);
                IncludeFrames(dto.Frames, ref minX, ref maxX, ref minZ, ref maxZ);
                IncludeLines(dto.Lines, ref minX, ref maxX, ref minZ, ref maxZ);
                IncludeBoxes(dto.Boxes, ref minX, ref maxX, ref minZ, ref maxZ);

                if (double.IsPositiveInfinity(minX) || double.IsNegativeInfinity(maxX))
                {
                    minX = -0.5;
                    maxX = 0.5;
                    minZ = -0.5;
                    maxZ = 0.5;
                }

                ExpandDegenerateRange(ref minX, ref maxX);
                ExpandDegenerateRange(ref minZ, ref maxZ);

                double worldWidth = maxX - minX;
                double worldDepth = maxZ - minZ;
                double scale = System.Math.Min(plotArea.Width / worldWidth, plotArea.Height / worldDepth);
                double offsetX = plotArea.X + (plotArea.Width - worldWidth * scale) / 2.0;
                double offsetY = plotArea.Y + (plotArea.Height - worldDepth * scale) / 2.0;

                return new TopDownProjection(plotArea, minX, minZ, scale, offsetX, offsetY, worldDepth * scale);
            }

            public SvgPoint Project(DebugViewportVector3dV1Dto vector)
            {
                return Project(vector.X, vector.Z);
            }

            public SvgPoint Project(TopDownWorldPoint point)
            {
                return Project(point.X, point.Z);
            }

            private SvgPoint Project(double x, double z)
            {
                double projectedX = _offsetX + (x - _minX) * _scale;
                double projectedY = _offsetY + _scaledDepth - (z - _minZ) * _scale;
                return new SvgPoint(projectedX, projectedY);
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

            private static void IncludeBoxes(
                DebugViewportBoxV1Dto[]? boxes,
                ref double minX,
                ref double maxX,
                ref double minZ,
                ref double maxZ)
            {
                if (boxes == null)
                {
                    return;
                }

                for (int i = 0; i < boxes.Length; i++)
                {
                    TopDownWorldPoint[] corners = GetBoxTopDownCorners(boxes[i]);
                    for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
                    {
                        Include(corners[cornerIndex], ref minX, ref maxX, ref minZ, ref maxZ);
                    }
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

            private static void Include(
                TopDownWorldPoint point,
                ref double minX,
                ref double maxX,
                ref double minZ,
                ref double maxZ)
            {
                minX = System.Math.Min(minX, point.X);
                maxX = System.Math.Max(maxX, point.X);
                minZ = System.Math.Min(minZ, point.Z);
                maxZ = System.Math.Max(maxZ, point.Z);
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

        private sealed class ElevationProjection
        {
            private readonly double _minDistance;
            private readonly double _maxDistance;
            private readonly double _minY;
            private readonly double _maxY;
            private readonly double _scaleX;
            private readonly double _scaleY;

            private ElevationProjection(
                SvgRect plotArea,
                double minDistance,
                double maxDistance,
                double minY,
                double maxY)
            {
                PlotArea = plotArea;
                _minDistance = minDistance;
                _maxDistance = maxDistance;
                _minY = minY;
                _maxY = maxY;
                _scaleX = plotArea.Width / (_maxDistance - _minDistance);
                _scaleY = plotArea.Height / (_maxY - _minY);
            }

            public SvgRect PlotArea { get; }

            public static ElevationProjection Create(DebugViewportSnapshotV1Dto dto, SvgRect plotArea)
            {
                double minDistance = double.PositiveInfinity;
                double maxDistance = double.NegativeInfinity;
                double minY = double.PositiveInfinity;
                double maxY = double.NegativeInfinity;

                IncludeCenterline(dto.CenterlinePoints, ref minDistance, ref maxDistance, ref minY, ref maxY);
                IncludeFrames(dto.Frames, ref minDistance, ref maxDistance, ref minY, ref maxY);

                if (double.IsPositiveInfinity(minDistance) || double.IsNegativeInfinity(maxDistance))
                {
                    minDistance = 0.0;
                    maxDistance = 1.0;
                    minY = -0.5;
                    maxY = 0.5;
                }

                ExpandDegenerateDistanceRange(ref minDistance, ref maxDistance);
                ExpandDegenerateYRange(ref minY, ref maxY);

                return new ElevationProjection(plotArea, minDistance, maxDistance, minY, maxY);
            }

            public SvgPoint Project(double distance, double y)
            {
                double x = PlotArea.X + (distance - _minDistance) * _scaleX;
                double screenY = PlotArea.Bottom - (y - _minY) * _scaleY;
                return new SvgPoint(x, screenY);
            }

            public bool TryProjectBaseline(double y, out SvgPoint start, out SvgPoint end)
            {
                if (y < _minY || y > _maxY)
                {
                    start = new SvgPoint(0.0, 0.0);
                    end = new SvgPoint(0.0, 0.0);
                    return false;
                }

                start = Project(_minDistance, y);
                end = Project(_maxDistance, y);
                return true;
            }

            private static void IncludeCenterline(
                DebugViewportCenterlinePointV1Dto[]? points,
                ref double minDistance,
                ref double maxDistance,
                ref double minY,
                ref double maxY)
            {
                if (points == null)
                {
                    return;
                }

                for (int i = 0; i < points.Length; i++)
                {
                    Include(points[i].Distance, points[i].Position.Y, ref minDistance, ref maxDistance, ref minY, ref maxY);
                }
            }

            private static void IncludeFrames(
                DebugViewportFrameV1Dto[]? frames,
                ref double minDistance,
                ref double maxDistance,
                ref double minY,
                ref double maxY)
            {
                if (frames == null)
                {
                    return;
                }

                for (int i = 0; i < frames.Length; i++)
                {
                    Include(frames[i].Distance, frames[i].Position.Y, ref minDistance, ref maxDistance, ref minY, ref maxY);
                }
            }

            private static void Include(
                double distance,
                double y,
                ref double minDistance,
                ref double maxDistance,
                ref double minY,
                ref double maxY)
            {
                minDistance = System.Math.Min(minDistance, distance);
                maxDistance = System.Math.Max(maxDistance, distance);
                minY = System.Math.Min(minY, y);
                maxY = System.Math.Max(maxY, y);
            }

            private static void ExpandDegenerateDistanceRange(ref double min, ref double max)
            {
                double span = max - min;
                if (span >= MinimumDistanceSpan)
                {
                    return;
                }

                double center = (min + max) * 0.5;
                min = center - MinimumDistanceSpan * 0.5;
                max = center + MinimumDistanceSpan * 0.5;
            }

            private static void ExpandDegenerateYRange(ref double min, ref double max)
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
