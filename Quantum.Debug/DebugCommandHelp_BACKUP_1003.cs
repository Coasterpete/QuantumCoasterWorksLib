using System;
using System.Collections.Generic;
using System.IO;

namespace Quantum.Debug
{
    public static class DebugCommandHelp
    {
        public const string ProjectPurpose =
            "Quantum.Debug provides backend-only diagnostics and JSON fixture/export tooling for Quantum CoasterWorks.";

        public const string GeneratedArtifactsNote =
            "Generated artifacts are local by default and should not be committed unless intentionally included in a release package.";

        public const string DebugViewportPreviewIndexNote =
            "When debug viewport outputs are written under artifacts/debug-viewport, Quantum.Debug refreshes artifacts/debug-viewport/" +
            DebugViewportSnapshotPreviewIndex.FileName + ".";

        public const string GeometryInterchangeRoadmapNote =
            "Quantum.IO.GeometryInterchange defines backend-only curve interchange data and placeholder unsupported Rhino3dm diagnostics; no rhino3dm/openNURBS dependency is included.";

        private static readonly DebugCommandHelpEntry[] CommandEntries =
        {
            new DebugCommandHelpEntry(
                name: "sampling-perf",
                usage: "sampling-perf",
                summary: "Run deterministic sampling performance diagnostics.",
                arguments: new[]
                {
                    "No arguments."
                },
                examples: new[]
                {
                    "dotnet run --project Quantum.Debug -- sampling-perf"
                }),
            new DebugCommandHelpEntry(
                name: "train-pose-export-v1",
                usage: "train-pose-export-v1 [outputPath]",
                summary: "Write the deterministic TrainPoseExportV1 regression sample JSON file.",
                arguments: new[]
                {
                    "outputPath: Optional JSON output path. Defaults to " +
                    TrainPoseExportV1Command.DefaultRelativeOutputPath + ".",
                    "The generated payload matches Quantum.Tests/IO/Fixtures/TrainPoseExportV1.golden.json.",
                    "The output is backend-only JSON and does not depend on Unity or any renderer."
                },
                examples: new[]
                {
                    "dotnet run --project Quantum.Debug -- train-pose-export-v1",
                    "dotnet run --project Quantum.Debug -- train-pose-export-v1 artifacts/train-pose/TrainPoseExportV1.sample.json"
                }),
            new DebugCommandHelpEntry(
                name: MeshExportV1SampleCommand.CommandName,
                usage: "mesh-export-v1-sample [outputPath]",
                summary: "Write a deterministic MeshExportV1 sample JSON file.",
                arguments: new[]
                {
                    "outputPath: Optional JSON output path. Defaults to " +
                    MeshExportV1SampleCommand.DefaultRelativeOutputPath + ".",
                    "The sample is a tiny self-authored quad mesh using MeshExportV1 DTOs, JSON serialization, and validation.",
                    "This is a sample artifact only; it is not a real mesh exporter, Blender importer, renderer, shader, or material pipeline."
                },
                examples: new[]
                {
                    "dotnet run --project Quantum.Debug -- mesh-export-v1-sample",
                    "dotnet run --project Quantum.Debug -- mesh-export-v1-sample artifacts/mesh-export/MeshExportV1.sample.json"
                }),
            new DebugCommandHelpEntry(
                name: "debug-viewport-snapshot-v1",
                usage: "debug-viewport-snapshot-v1 [outputPath]",
                summary: "Write the built-in DebugViewportSnapshotV1 sample JSON file.",
                arguments: new[]
                {
                    "outputPath: Optional JSON output path. Defaults to " +
                    DebugViewportSnapshotV1SampleCommand.DefaultRelativeOutputPath + ".",
                    DebugViewportPreviewIndexNote
                },
                examples: new[]
                {
                    "dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1",
                    "dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1 artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json"
                }),
            new DebugCommandHelpEntry(
                name: "debug-viewport-snapshot-v1-from-csv",
                usage: "debug-viewport-snapshot-v1-from-csv <inputCsvPath> [outputJsonPath]",
                summary: "Bridge a sampled-frame CSV fixture to DebugViewportSnapshotV1 JSON.",
                arguments: new[]
                {
                    "inputCsvPath: Required sampled-frame CSV fixture path.",
                    "outputJsonPath: Optional JSON output path. Defaults next to the input CSV with .debug-viewport-snapshot-v1.json appended.",
                    DebugViewportPreviewIndexNote
                },
                examples: new[]
                {
                    "dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-from-csv Quantum.Tests/IO/Fixtures/Milestone7.synthetic.straight_line.centerline_frames.csv",
                    "dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-from-csv Quantum.Tests/IO/Fixtures/Milestone7.synthetic.straight_line.centerline_frames.csv artifacts/debug-viewport/Milestone7.synthetic.straight_line.snapshot.json"
                }),
            new DebugCommandHelpEntry(
                name: "debug-viewport-snapshot-v1-validate",
                usage: "debug-viewport-snapshot-v1-validate <snapshotJsonPath>",
                summary: "Validate and summarize a DebugViewportSnapshotV1 JSON file.",
                arguments: new[]
                {
                    "snapshotJsonPath: Required DebugViewportSnapshotV1 JSON path."
                },
                examples: new[]
                {
                    "dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-validate artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json"
                }),
            new DebugCommandHelpEntry(
                name: "debug-viewport-snapshot-v1-svg",
                usage: "debug-viewport-snapshot-v1-svg <snapshotJsonPath> [outputSvgPath]",
                summary: "Write a multi-panel backend-only SVG preview from DebugViewportSnapshotV1 JSON.",
                arguments: new[]
                {
                    "snapshotJsonPath: Required DebugViewportSnapshotV1 JSON path.",
                    "outputSvgPath: Optional SVG output path. Defaults next to the input JSON with .svg extension.",
                    "The SVG is a technical debug preview, not a renderer or editor surface.",
                    "Raw exported samples remain visible; Catmull-Rom smooth-preview geometry is visual-only and does not change the JSON contract.",
                    DebugViewportPreviewIndexNote
                },
                examples: new[]
                {
                    "dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-svg artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json",
                    "dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-svg artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json artifacts/debug-viewport/DebugViewportSnapshotV1.sample.svg"
                }),
            new DebugCommandHelpEntry(
                name: DebugViewportSnapshotGalleryCommand.CommandName,
                usage: "debug-viewport-snapshot-v1-gallery [artifactDirectory] [outputHtmlPath]",
                summary: "Write a static HTML gallery for generated DebugViewportSnapshotV1 JSON and SVG artifacts.",
                arguments: new[]
                {
                    "artifactDirectory: Optional directory to scan. Defaults to " +
                    DebugViewportSnapshotGalleryCommand.DefaultRelativeArtifactDirectory + ".",
                    "outputHtmlPath: Optional HTML output path. Defaults to artifacts/debug-viewport/" +
                    DebugViewportSnapshotPreviewIndex.GalleryFileName + ".",
                    "The gallery reads DebugViewportSnapshotV1 JSON metadata, links source JSON/SVG files, and remains a static local debug artifact."
                },
                examples: new[]
                {
                    "dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-gallery",
                    "dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-gallery artifacts/debug-viewport artifacts/debug-viewport/index.html"
                }),
            new DebugCommandHelpEntry(
                name: DebugViewportSnapshotBrowserCommand.CommandName,
                usage: "debug-viewport-snapshot-v1-browser [artifactDirectory] [outputHtmlPath]",
                summary: "Write a self-contained browser inspector for DebugViewportSnapshotV1 JSON artifacts.",
                arguments: new[]
                {
                    "artifactDirectory: Optional directory to scan. Defaults to " +
                    DebugViewportSnapshotBrowserCommand.DefaultRelativeArtifactDirectory + ".",
                    "outputHtmlPath: Optional HTML output path. Defaults to artifacts/debug-viewport/" +
                    DebugViewportSnapshotBrowserCommand.DefaultFileName + ".",
                    "The viewer embeds local DebugViewportSnapshotV1 JSON, draws centerline samples, distance labels/ticks, curvature/radius diagnostics, frame axes, debug lines, train boxes, bogie markers, wheel markers, metadata, and a sample measurement readout with inline style/script only.",
                    "Curvature/radius diagnostics use optional per-sample curvature or radius fields when present and otherwise derive deterministic approximate curvature from neighboring centerline samples.",
                    "This is a backend-only debug aid, not a final editor, frontend, renderer, or JSON contract change."
                },
                examples: new[]
                {
                    "dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-browser",
                    "dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-browser artifacts/debug-viewport artifacts/debug-viewport/browser.html"
                }),
            new DebugCommandHelpEntry(
                name: DebugViewportSnapshotV1BankingProfileSampleCommand.CommandName,
                usage: "debug-viewport-snapshot-v1-banking-profile [outputPath]",
                summary: "Write a DebugViewportSnapshotV1 sample from the opt-in BankingProfile train-pose path.",
                arguments: new[]
                {
                    "outputPath: Optional JSON output path. Defaults to " +
                    DebugViewportSnapshotV1BankingProfileSampleCommand.DefaultRelativeOutputPath + ".",
                    "The sample uses a self-authored backend-only track, BankingProfile, and train consist fixture.",
                    "The nested trainPose is produced by EvaluateTrainPose(..., BankingProfile) and TrainPoseExportV1Mapper.Export.",
                    "This command leaves default TrackEvaluator, default EvaluateTrainPose(...), TrackDocument, and renderer behavior unchanged.",
                    DebugViewportPreviewIndexNote
                },
                examples: new[]
                {
                    "dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-banking-profile",
                    "dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-banking-profile artifacts/debug-viewport/DebugViewportSnapshotV1.banking-profile.sample.json"
                }),
            new DebugCommandHelpEntry(
                name: "longitudinal-force-preview",
                usage: "longitudinal-force-preview [preset] [outputPath]",
                summary: "Write longitudinal force preview diagnostics.",
                arguments: new[]
                {
                    "preset: Optional preset name: soft, balanced, or punchy. Defaults to balanced.",
                    "outputPath: Optional JSON output path. Defaults to " +
                    LongitudinalForcePreviewCommand.DefaultRelativeOutputPath + ".",
                    "With one argument, the value is treated as a preset when it matches one; otherwise it is treated as outputPath."
                },
                examples: new[]
                {
                    "dotnet run --project Quantum.Debug -- longitudinal-force-preview",
                    "dotnet run --project Quantum.Debug -- longitudinal-force-preview punchy artifacts/force-target/punchy.sample.json"
                }),
            new DebugCommandHelpEntry(
                name: "longitudinal-speed-preview",
                usage: "longitudinal-speed-preview [preset] [outputPath] [initialSpeedMps]",
                summary: "Write longitudinal speed preview diagnostics.",
                arguments: new[]
                {
                    "preset: Optional preset name: soft, balanced, or punchy. Defaults to balanced.",
                    "outputPath: Optional JSON output path. Defaults to " +
                    LongitudinalSpeedPreviewCommand.DefaultRelativeOutputPath + ".",
                    "initialSpeedMps: Optional finite value greater than or equal to 0. Defaults to 0."
                },
                examples: new[]
                {
                    "dotnet run --project Quantum.Debug -- longitudinal-speed-preview",
                    "dotnet run --project Quantum.Debug -- longitudinal-speed-preview balanced artifacts/speed-preview/balanced.sample.json 12.5"
                }),
            new DebugCommandHelpEntry(
                name: "centerline-frame-continuity",
                usage: "centerline-frame-continuity [outputPath]",
                summary: "Write deterministic centerline frame continuity diagnostics.",
                arguments: new[]
                {
                    "outputPath: Optional JSON output path. Defaults to " +
                    CenterlineFrameContinuityCommand.DefaultRelativeOutputPath + ".",
                    "The command samples a self-authored deterministic centerline and reports tangent, normal, binormal, roll, and matrix orientation continuity.",
                    "The output is backend-only JSON and does not depend on Unity or any renderer."
                },
                examples: new[]
                {
                    "dotnet run --project Quantum.Debug -- centerline-frame-continuity",
                    "dotnet run --project Quantum.Debug -- centerline-frame-continuity artifacts/frame-continuity/centerline-frame-continuity.sample.json"
                }),
            new DebugCommandHelpEntry(
                name: "transported-frame-comparison",
                usage: "transported-frame-comparison [outputPath]",
                summary: "Write deterministic transported frame comparison diagnostics for the diagnostic fixture set.",
                arguments: new[]
                {
                    "outputPath: Optional JSON output path. Defaults to " +
                    TransportedFrameComparisonCommand.DefaultRelativeOutputPath + ".",
                    "The command compares stateless TrackEvaluator frames with TransportedTrackFrameSampler frames over the self-authored diagnostic track fixtures.",
                    "The output includes per-sample deltas, summary metrics, smoothness metrics, and continuity metrics for both frame sets.",
                    "The output is backend-only JSON and does not depend on Unity or any renderer."
                },
                examples: new[]
                {
                    "dotnet run --project Quantum.Debug -- transported-frame-comparison",
                    "dotnet run --project Quantum.Debug -- transported-frame-comparison artifacts/frame-comparison/transported-frame-comparison.sample.json"
                }),
            new DebugCommandHelpEntry(
                name: BankingProfileDiagnosticsCommand.CommandName,
                usage: "banking-profile-diagnostics [outputPath]",
                summary: "Write deterministic BankingProfile roll sampling diagnostics.",
                arguments: new[]
                {
                    "outputPath: Optional JSON output path. Defaults to " +
                    BankingProfileDiagnosticsCommand.DefaultRelativeOutputPath + ".",
                    "The command samples a self-authored BankingProfile over station distances and reports roll radians, roll degrees, interpolation source intervals, approximate roll slope, and summary metrics.",
                    "The output is backend-only JSON and does not change TrackEvaluator, TrackFrame, DebugViewportSnapshotV1, or TrainPoseExportV1 behavior."
                },
                examples: new[]
                {
                    "dotnet run --project Quantum.Debug -- banking-profile-diagnostics",
                    "dotnet run --project Quantum.Debug -- banking-profile-diagnostics artifacts/banking-profile/banking-profile-diagnostics.sample.json"
                }),
            new DebugCommandHelpEntry(
                name: ContinuousRollDiagnosticsSampleCommand.CommandName,
                usage: "continuous-roll-diagnostics-sample [outputPath]",
                summary: "Write a deterministic continuous roll diagnostics text report.",
                arguments: new[]
                {
                    "outputPath: Optional text output path. Defaults to " +
                    ContinuousRollDiagnosticsSampleCommand.DefaultRelativeOutputPath + ".",
                    "The sample feeds explicit station-distance roll values into ContinuousRollDiagnostics, reports adjacent roll deltas, maximum and average roll rate, wrap-around handling, and roll continuity warnings.",
                    "Full-turn wrap handling treats 359 degrees to 1 degree as a small continuous transition while still warning on a deliberately discontinuous roll jump.",
                    "This is a backend-only diagnostic report and does not change DebugViewportSnapshotV1, TrainPoseExportV1, MeshExportV1, TrackFrame, or TrackEvaluator behavior."
                },
                examples: new[]
                {
                    "dotnet run --project Quantum.Debug -- continuous-roll-diagnostics-sample",
                    "dotnet run --project Quantum.Debug -- continuous-roll-diagnostics-sample artifacts/banking-profile/continuous-roll-diagnostics.sample.txt"
                }),
            new DebugCommandHelpEntry(
                name: ContinuousRollDiagnosticsJsonCommand.CommandName,
                usage: "continuous-roll-diagnostics-json [outputPath]",
                summary: "Write a deterministic continuous roll diagnostics JSON artifact.",
                arguments: new[]
                {
                    "outputPath: Optional JSON output path. Defaults to " +
                    ContinuousRollDiagnosticsJsonCommand.DefaultRelativeOutputPath + ".",
                    "The artifact uses contract quantum.continuous_roll_diagnostics version 1 and System.Text.Json camelCase serialization.",

                    "The generated payload matches Quantum.Tests/IO/Fixtures/ContinuousRollDiagnosticsExportV1.golden.json.",


                    "The JSON is mapped from ContinuousRollDiagnostics so it shares the same roll delta, roll rate, wrap handling, and warning calculations as the text report.",
                    "This is a backend inspection artifact only; it does not change DebugViewportSnapshotV1, TrainPoseExportV1, MeshExportV1, TrackFrame, or default TrackEvaluator behavior."
                },
                examples: new[]
                {
                    "dotnet run --project Quantum.Debug -- continuous-roll-diagnostics-json",
                    "dotnet run --project Quantum.Debug -- continuous-roll-diagnostics-json artifacts/banking-profile/continuous-roll-diagnostics.sample.json"
                }),
            new DebugCommandHelpEntry(
                name: BankingProfileBrowserCommand.CommandName,
                usage: "banking-profile-browser [diagnosticsJsonPath] [outputHtmlPath]",
                summary: "Write a self-contained browser viewer for BankingProfile diagnostics JSON.",
                arguments: new[]
                {
                    "diagnosticsJsonPath: Optional JSON input path. Defaults to " +
                    BankingProfileDiagnosticsCommand.DefaultRelativeOutputPath + ".",
                    "outputHtmlPath: Optional HTML output path. Defaults next to the input JSON as " +
                    BankingProfileBrowserCommand.DefaultFileName + ".",
                    "The viewer embeds BankingProfileDiagnosticsExportV1 JSON, shows profile metadata, sample count, min/max roll, maximum roll slope, roll radians/degrees, and interpolation modes.",
                    "The viewer renders SVG graphs for roll angle and roll slope versus station distance, with source key markers, interpolation transition markers, and simple roll slope severity indicators.",
                    "This is a local-file-friendly HTML/SVG/vanilla JavaScript debug artifact and does not change TrackFrame, TrackEvaluator, DebugViewportSnapshotV1, TrainPoseExportV1, or runtime banking behavior."
                },
                examples: new[]
                {
                    "dotnet run --project Quantum.Debug -- banking-profile-browser",
                    "dotnet run --project Quantum.Debug -- banking-profile-browser artifacts/banking-profile/banking-profile-diagnostics.sample.json artifacts/banking-profile/banking-profile.browser.html"
                }),
            new DebugCommandHelpEntry(
                name: TransportedFrameComparisonBrowserCommand.CommandName,
                usage: "transported-frame-comparison-browser [comparisonJsonPath] [outputHtmlPath]",
                summary: "Write a self-contained browser viewer for transported frame comparison JSON.",
                arguments: new[]
                {
                    "comparisonJsonPath: Optional JSON input path. Defaults to " +
                    TransportedFrameComparisonCommand.DefaultRelativeOutputPath + ".",
                    "outputHtmlPath: Optional HTML output path. Defaults next to the input JSON as " +
                    TransportedFrameComparisonBrowserCommand.DefaultFileName + ".",
                    "The viewer embeds TransportedFrameComparisonDiagnosticsExportV1 JSON, shows summary metrics, renders a per-sample delta table, and marks normal/binormal/frame/matrix delta severity.",
                    "This is a local-file-friendly HTML/SVG/vanilla JavaScript debug artifact and does not change DebugViewportSnapshotV1, TrainPoseExportV1, TrackFrame, or runtime banking behavior."
                },
                examples: new[]
                {
                    "dotnet run --project Quantum.Debug -- transported-frame-comparison-browser",
                    "dotnet run --project Quantum.Debug -- transported-frame-comparison-browser artifacts/frame-comparison/transported-frame-comparison.sample.json artifacts/frame-comparison/transported-frame-comparison.browser.html"
                })
        };

        public static bool TryWriteRequestedHelp(
            IReadOnlyList<string> args,
            TextWriter output,
            out int exitCode)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            exitCode = 0;

            if (args.Count == 0)
            {
                return false;
            }

            if (IsHelpToken(args[0]))
            {
                if (args.Count > 2)
                {
                    output.WriteLine("Usage: help [command]");
                    exitCode = 1;
                    return true;
                }

                if (args.Count == 2)
                {
                    exitCode = TryWriteCommandHelp(args[1], output) ? 0 : 1;
                    return true;
                }

                WriteGeneralHelp(output);
                return true;
            }

            if (args.Count == 2 && IsHelpOption(args[1]))
            {
                exitCode = TryWriteCommandHelp(args[0], output) ? 0 : 1;
                return true;
            }

            return false;
        }

        public static bool IsHelpToken(string value)
        {
            return string.Equals(value, "help", StringComparison.OrdinalIgnoreCase) ||
                   IsHelpOption(value);
        }

        public static void WriteGeneralHelp(TextWriter output)
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            output.WriteLine(ProjectPurpose);
            output.WriteLine();
            output.WriteLine("Usage:");
            output.WriteLine("  dotnet run --project Quantum.Debug -- <command> [arguments]");
            output.WriteLine("  dotnet run --project Quantum.Debug -- help [command]");
            output.WriteLine("  dotnet run --project Quantum.Debug -- <command> --help");
            output.WriteLine();
            output.WriteLine("Commands:");
            output.WriteLine("  (no command) - Run the default backend validation smoke checks.");
            output.WriteLine("  help [command] - Show general help or command-specific help.");

            for (int i = 0; i < CommandEntries.Length; i++)
            {
                DebugCommandHelpEntry entry = CommandEntries[i];
                output.WriteLine("  " + entry.Usage + " - " + entry.Summary);
            }

            output.WriteLine();
            output.WriteLine("Examples:");
            output.WriteLine("  dotnet run --project Quantum.Debug -- help");
            output.WriteLine("  dotnet run --project Quantum.Debug -- help debug-viewport-snapshot-v1");
            output.WriteLine("  dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1 artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json");
            output.WriteLine();
            output.WriteLine("Artifact note:");
            output.WriteLine("  " + GeneratedArtifactsNote);
            output.WriteLine();
            output.WriteLine("Geometry Interchange Roadmap:");
            output.WriteLine("  " + GeometryInterchangeRoadmapNote);
        }

        public static bool TryWriteCommandHelp(string commandName, TextWriter output)
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            DebugCommandHelpEntry? entry = FindCommand(commandName);
            if (entry is null)
            {
                output.WriteLine("Unknown command.");
                output.WriteLine("Supported commands:");
                WriteSupportedCommandLines(output);
                return false;
            }

            output.WriteLine(ProjectPurpose);
            output.WriteLine();
            output.WriteLine("Command:");
            output.WriteLine("  " + entry.Name);
            output.WriteLine();
            output.WriteLine("Purpose:");
            output.WriteLine("  " + entry.Summary);
            output.WriteLine();
            output.WriteLine("Usage:");
            output.WriteLine("  dotnet run --project Quantum.Debug -- " + entry.Usage);
            output.WriteLine();
            output.WriteLine("Arguments:");

            for (int i = 0; i < entry.Arguments.Length; i++)
            {
                output.WriteLine("  " + entry.Arguments[i]);
            }

            output.WriteLine();
            output.WriteLine("Examples:");

            for (int i = 0; i < entry.Examples.Length; i++)
            {
                output.WriteLine("  " + entry.Examples[i]);
            }

            output.WriteLine();
            output.WriteLine("Artifact note:");
            output.WriteLine("  " + GeneratedArtifactsNote);
            return true;
        }

        public static void WriteUnknownCommand(TextWriter output)
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            output.WriteLine("Unknown command.");
            output.WriteLine("Supported commands:");
            WriteSupportedCommandLines(output);
        }

        private static bool IsHelpOption(string value)
        {
            return string.Equals(value, "--help", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "-h", StringComparison.OrdinalIgnoreCase);
        }

        private static void WriteSupportedCommandLines(TextWriter output)
        {
            output.WriteLine("  help [command]");

            for (int i = 0; i < CommandEntries.Length; i++)
            {
                output.WriteLine("  " + CommandEntries[i].Usage);
            }

            output.WriteLine("  longitudinal-force-preview presets: soft | balanced | punchy");
            output.WriteLine("  longitudinal-speed-preview presets: soft | balanced | punchy");
        }

        private static DebugCommandHelpEntry? FindCommand(string commandName)
        {
            for (int i = 0; i < CommandEntries.Length; i++)
            {
                DebugCommandHelpEntry entry = CommandEntries[i];
                if (string.Equals(commandName, entry.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        private sealed class DebugCommandHelpEntry
        {
            public DebugCommandHelpEntry(
                string name,
                string usage,
                string summary,
                string[] arguments,
                string[] examples)
            {
                Name = name;
                Usage = usage;
                Summary = summary;
                Arguments = arguments;
                Examples = examples;
            }

            public string Name { get; }

            public string Usage { get; }

            public string Summary { get; }

            public string[] Arguments { get; }

            public string[] Examples { get; }
        }
    }
}
