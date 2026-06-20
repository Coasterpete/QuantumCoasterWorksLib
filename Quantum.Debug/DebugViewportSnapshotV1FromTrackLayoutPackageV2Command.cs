using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Quantum.IO.DebugViewport.V1;
using Quantum.IO.TrackLayout.V2;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Debug
{
    public sealed class DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportDiagnostic
    {
        public DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportDiagnostic(
            string code,
            string path,
            string message)
        {
            Code = string.IsNullOrWhiteSpace(code) ? "Unknown" : code;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }

        public string Path { get; }

        public string Message { get; }
    }

    public sealed class DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportResult
    {
        private DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportResult(
            bool success,
            DebugViewportSnapshotV1Dto? snapshot,
            IReadOnlyList<DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportDiagnostic> diagnostics)
        {
            Success = success;
            Snapshot = snapshot;
            Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        public bool Success { get; }

        public DebugViewportSnapshotV1Dto? Snapshot { get; }

        public IReadOnlyList<DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportDiagnostic> Diagnostics { get; }

        internal static DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportResult Succeeded(
            DebugViewportSnapshotV1Dto snapshot)
        {
            return new DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportResult(
                true,
                snapshot ?? throw new ArgumentNullException(nameof(snapshot)),
                Array.Empty<DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportDiagnostic>());
        }

        internal static DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportResult Failed(
            IReadOnlyList<DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportDiagnostic> diagnostics)
        {
            return new DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportResult(
                false,
                null,
                diagnostics);
        }
    }

    public static class DebugViewportSnapshotV1FromTrackLayoutPackageV2Command
    {
        public const string CommandName = "debug-viewport-snapshot-v1-from-track-layout-package-v2";

        internal const string DefaultOutputExtension = ".debug-viewport-snapshot-v1.json";

        private const double SampleIntervalMeters = 3.0;
        private const int MaximumSampleCount = 257;
        private const double AxisLengthMeters = 4.0;
        private const int MaximumTrainCarCount = 5;
        private const double TrainCarSpacingMeters = 6.0;
        private const double TrainCarLengthMeters = 5.0;
        private const double TrainCarWidthMeters = 1.8;
        private const double TrainCarHeightMeters = 2.2;
        private const double TrainBogieSpacingMeters = 4.0;
        private const double MinimumLineLength = 1e-9;

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public static int Run(string inputJsonPath, string? outputJsonPath = null)
        {
            return Run(inputJsonPath, outputJsonPath, Console.Out);
        }

        public static int Run(string inputJsonPath, string? outputJsonPath, TextWriter output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (string.IsNullOrWhiteSpace(inputJsonPath))
            {
                output.WriteLine("inputJsonPath is required.");
                return 1;
            }

            string resolvedInputPath = Path.GetFullPath(inputJsonPath);
            string json;
            try
            {
                json = File.ReadAllText(resolvedInputPath);
            }
            catch (Exception ex) when (IsReadOrWriteException(ex))
            {
                output.WriteLine("Failed to read TrackLayoutPackageV2 JSON.");
                output.WriteLine(ex.Message);
                return 1;
            }

            DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportResult result =
                ExportJson(json, ResolveSourceFixtureName(inputJsonPath));
            if (!result.Success || result.Snapshot == null)
            {
                PrintDiagnostics(result.Diagnostics, output);
                return 1;
            }

            string snapshotJson = DebugViewportSnapshotV1Json.Serialize(result.Snapshot, indented: true);
            string resolvedOutputPath = ResolveOutputPath(resolvedInputPath, outputJsonPath);
            string? parentDirectory = Path.GetDirectoryName(resolvedOutputPath);

            if (!string.IsNullOrEmpty(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            try
            {
                File.WriteAllText(resolvedOutputPath, snapshotJson, Utf8NoBom);
            }
            catch (Exception ex) when (IsReadOrWriteException(ex))
            {
                output.WriteLine("Failed to write DebugViewportSnapshotV1 JSON.");
                output.WriteLine(ex.Message);
                return 1;
            }

            output.WriteLine(
                "Wrote TrackLayoutPackageV2 DebugViewportSnapshotV1 snapshot to '" +
                resolvedOutputPath +
                "'.");
            DebugViewportSnapshotPreviewIndex.TryWriteForGeneratedOutput(resolvedOutputPath, output);
            return 0;
        }

        public static DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportResult ExportJson(
            string json,
            string? sourceName = null)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            TrackLayoutPackageV2Dto dto;
            try
            {
                dto = TrackLayoutPackageV2Json.Deserialize(json);
            }
            catch (JsonException)
            {
                TrackLayoutPackageV2ImportResult malformedImport = TrackLayoutPackageV2Json.Import(json);
                return DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportResult.Failed(
                    MapImportDiagnostics(malformedImport.Diagnostics));
            }

            TrackLayoutPackageV2ImportResult importResult = TrackLayoutPackageV2Mapper.Import(dto);
            if (!importResult.Success || importResult.Definition == null)
            {
                return DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportResult.Failed(
                    MapImportDiagnostics(importResult.Diagnostics));
            }

            return ExportImportedPackage(dto, importResult, sourceName);
        }

        public static DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportResult Export(
            TrackLayoutPackageV2Dto dto,
            string? sourceName = null)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            TrackLayoutPackageV2ImportResult importResult = TrackLayoutPackageV2Mapper.Import(dto);
            if (!importResult.Success || importResult.Definition == null)
            {
                return DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportResult.Failed(
                    MapImportDiagnostics(importResult.Diagnostics));
            }

            return ExportImportedPackage(dto, importResult, sourceName);
        }

        private static DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportResult ExportImportedPackage(
            TrackLayoutPackageV2Dto dto,
            TrackLayoutPackageV2ImportResult importResult,
            string? sourceName)
        {
            TrackAuthoringCompilation compilation;
            try
            {
                compilation = TrackAuthoringDocumentBuilder.Compile(importResult.Definition!);
            }
            catch (Exception ex) when (IsCompilationOrEvaluationException(ex))
            {
                return DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportResult.Failed(
                    SingleDiagnostic(
                        "TrackLayoutPackageV2.CompilationFailed",
                        "track",
                        ex.Message));
            }

            try
            {
                bool hasAuthoredBanking = importResult.Definition!.Banking != null;
                var evaluator = new TrackEvaluator(compilation.Runtime);
                double totalLength = evaluator.GetBoundTrackTotalLength();
                double[] sampleDistances = BuildSampleDistances(totalLength);
                TrackFrame[] sampledFrames = hasAuthoredBanking
                    ? BankingProfileSampler.SampleFramesAtDistances(
                        evaluator,
                        compilation.BankingProfile,
                        sampleDistances)
                    : evaluator.EvaluateFramesAtDistances(sampleDistances);
                DebugLineSegment[] lines = BuildDebugLines(
                    evaluator,
                    compilation.BankingProfile,
                    sampledFrames,
                    sampleDistances,
                    importResult.HeartlineOffset,
                    hasAuthoredBanking);
                TrainPoseResult? trainPose = TryEvaluateTrainPose(
                    evaluator,
                    compilation.BankingProfile,
                    hasAuthoredBanking,
                    totalLength,
                    out DebugViewportBoxSource[] boxes);

                DebugViewportSnapshotV1Dto snapshot = DebugViewportSnapshotV1Mapper.Export(
                    new DebugViewportSnapshotV1Source
                    {
                        Units = ResolveUnits(dto),
                        SourceFixtureName = ResolveSnapshotSourceName(dto, sourceName),
                        SampledFrames = sampledFrames,
                        Lines = lines,
                        Boxes = boxes,
                        TrainPose = trainPose
                    });

                IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> validationDiagnostics =
                    DebugViewportSnapshotV1Validator.Validate(snapshot);
                if (validationDiagnostics.Count != 0)
                {
                    return DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportResult.Failed(
                        MapSnapshotDiagnostics(validationDiagnostics));
                }

                return DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportResult.Succeeded(snapshot);
            }
            catch (Exception ex) when (IsCompilationOrEvaluationException(ex))
            {
                return DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportResult.Failed(
                    SingleDiagnostic(
                        "TrackLayoutPackageV2.EvaluationFailed",
                        "track",
                        ex.Message));
            }
        }

        private static double[] BuildSampleDistances(double totalLength)
        {
            if (double.IsNaN(totalLength) || double.IsInfinity(totalLength) || totalLength <= 0.0)
            {
                throw new InvalidOperationException("Compiled track length must be finite and greater than zero.");
            }

            int intervalCount = System.Math.Max(1, (int)System.Math.Ceiling(totalLength / SampleIntervalMeters));
            int sampleCount = System.Math.Min(MaximumSampleCount, intervalCount + 1);
            var distances = new double[sampleCount];

            if (sampleCount == intervalCount + 1)
            {
                for (int i = 0; i < sampleCount - 1; i++)
                {
                    distances[i] = System.Math.Min(i * SampleIntervalMeters, totalLength);
                }
            }
            else
            {
                double interval = totalLength / (sampleCount - 1);
                for (int i = 0; i < sampleCount - 1; i++)
                {
                    distances[i] = i * interval;
                }
            }

            distances[sampleCount - 1] = totalLength;
            return distances;
        }

        private static DebugLineSegment[] BuildDebugLines(
            TrackEvaluator evaluator,
            BankingProfile bankingProfile,
            TrackFrame[] sampledFrames,
            IReadOnlyList<double> sampleDistances,
            HeartlineOffset? heartlineOffset,
            bool useBankingProfile)
        {
            var lines = new List<DebugLineSegment>();
            if (sampledFrames.Length != 0)
            {
                lines.AddRange(TrackFrameDebugGizmoBuilder.BuildAxes(
                    sampledFrames[sampledFrames.Length / 2],
                    AxisLengthMeters));
            }

            if (!heartlineOffset.HasValue)
            {
                return lines.ToArray();
            }

            HeartlineFrame[] heartlineFrames = useBankingProfile
                ? HeartlineSampler.SampleAtDistances(
                    evaluator,
                    bankingProfile,
                    heartlineOffset.Value,
                    sampleDistances)
                : HeartlineSampler.SampleAtDistances(
                    evaluator,
                    heartlineOffset.Value,
                    sampleDistances);

            for (int i = 0; i < heartlineFrames.Length; i++)
            {
                HeartlineFrame heartlineFrame = heartlineFrames[i];
                if ((heartlineFrame.Position - heartlineFrame.CenterlinePosition).Length <= MinimumLineLength)
                {
                    continue;
                }

                lines.Add(new DebugLineSegment(
                    heartlineFrame.CenterlinePosition,
                    heartlineFrame.Position,
                    TrackFrameAxisType.Diagnostic));
            }

            return lines.ToArray();
        }

        private static TrainPoseResult? TryEvaluateTrainPose(
            TrackEvaluator evaluator,
            BankingProfile bankingProfile,
            bool useBankingProfile,
            double totalLength,
            out DebugViewportBoxSource[] boxes)
        {
            boxes = Array.Empty<DebugViewportBoxSource>();
            if (totalLength < TrainBogieSpacingMeters)
            {
                return null;
            }

            double leadDistance = totalLength - (TrainBogieSpacingMeters * 0.5);
            int carCount = 1 + (int)System.Math.Floor((totalLength - TrainBogieSpacingMeters) / TrainCarSpacingMeters);
            carCount = System.Math.Max(1, System.Math.Min(MaximumTrainCarCount, carCount));

            var trainDefinition = new TrainConsistDefinition(
                carCount: carCount,
                carSpacing: TrainCarSpacingMeters,
                carLength: TrainCarLengthMeters,
                carWidth: TrainCarWidthMeters,
                carHeight: TrainCarHeightMeters,
                bogieSpacing: TrainBogieSpacingMeters,
                wheelLayout: new TrainWheelLayout(
                    wheelCountPerBogie: 2,
                    wheelRadius: 0.45,
                    wheelWidth: 0.25,
                    axleSpacing: 1.1));
            var provider = new TrainCarTransformProvider(evaluator);
            TrainPoseResult trainPose = useBankingProfile
                ? provider.EvaluateTrainPose(leadDistance, trainDefinition, bankingProfile)
                : provider.EvaluateTrainPose(leadDistance, trainDefinition);

            boxes = BuildTrainBodyBoxes(
                trainPose,
                useBankingProfile
                    ? DebugViewportSnapshotV1Vocabulary.TrainBodyBankingProfileRole
                    : DebugViewportSnapshotV1Vocabulary.TrainBodyRole);
            return trainPose;
        }

        private static DebugViewportBoxSource[] BuildTrainBodyBoxes(
            TrainPoseResult trainPose,
            string role)
        {
            IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> cars = trainPose.CarsReadOnly;
            TrainCarGeometry geometry = trainPose.Definition.CarGeometry;
            var boxes = new DebugViewportBoxSource[cars.Count];

            for (int i = 0; i < cars.Count; i++)
            {
                boxes[i] = new DebugViewportBoxSource(
                    role: role,
                    label: "v2-car-" + i.ToString(CultureInfo.InvariantCulture),
                    frame: cars[i].Body.ArticulatedFrame,
                    length: geometry.Length,
                    width: geometry.Width,
                    height: geometry.Height);
            }

            return boxes;
        }

        private static IReadOnlyList<DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportDiagnostic>
            MapImportDiagnostics(IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> diagnostics)
        {
            if (diagnostics.Count == 0)
            {
                return SingleDiagnostic(
                    "TrackLayoutPackageV2.ImportFailed",
                    "dto",
                    "TrackLayoutPackageV2 import failed without diagnostics.");
            }

            return diagnostics
                .Select(diagnostic => new DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportDiagnostic(
                    "TrackLayoutPackageV2." + diagnostic.Code,
                    diagnostic.Path,
                    diagnostic.Message))
                .ToArray();
        }

        private static IReadOnlyList<DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportDiagnostic>
            MapSnapshotDiagnostics(IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics)
        {
            return diagnostics
                .Select(diagnostic => new DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportDiagnostic(
                    "DebugViewportSnapshotV1." + diagnostic.Code,
                    diagnostic.Path,
                    diagnostic.Message))
                .ToArray();
        }

        private static IReadOnlyList<DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportDiagnostic>
            SingleDiagnostic(string code, string path, string message)
        {
            return new[]
            {
                new DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportDiagnostic(
                    code,
                    path,
                    message)
            };
        }

        private static void PrintDiagnostics(
            IReadOnlyList<DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportDiagnostic> diagnostics,
            TextWriter output)
        {
            output.WriteLine("Failed to export TrackLayoutPackageV2 as DebugViewportSnapshotV1.");

            if (diagnostics.Count == 0)
            {
                output.WriteLine("- Unknown at track: Export failed without diagnostics.");
                return;
            }

            for (int i = 0; i < diagnostics.Count; i++)
            {
                DebugViewportSnapshotV1FromTrackLayoutPackageV2ExportDiagnostic diagnostic = diagnostics[i];
                output.WriteLine(
                    "- " +
                    diagnostic.Code +
                    " at " +
                    diagnostic.Path +
                    ": " +
                    diagnostic.Message);
            }
        }

        private static string ResolveUnits(TrackLayoutPackageV2Dto dto)
        {
            string? units = dto.Metadata?.Units;
            return string.IsNullOrWhiteSpace(units) ? "meters" : units!;
        }

        private static string ResolveSnapshotSourceName(
            TrackLayoutPackageV2Dto dto,
            string? sourceName)
        {
            string? metadataSourceName = dto.Metadata?.SourceName;
            if (!string.IsNullOrWhiteSpace(metadataSourceName))
            {
                return metadataSourceName!;
            }

            string? layoutId = dto.Metadata?.LayoutId;
            if (!string.IsNullOrWhiteSpace(layoutId))
            {
                return layoutId!;
            }

            if (!string.IsNullOrWhiteSpace(sourceName))
            {
                return sourceName!;
            }

            return "TrackLayoutPackageV2";
        }

        private static string ResolveOutputPath(
            string resolvedInputPath,
            string? outputJsonPath)
        {
            if (!string.IsNullOrWhiteSpace(outputJsonPath))
            {
                return Path.GetFullPath(outputJsonPath);
            }

            string inputDirectory = Path.GetDirectoryName(resolvedInputPath) ?? Environment.CurrentDirectory;
            string inputFileName = Path.GetFileNameWithoutExtension(resolvedInputPath);

            if (string.IsNullOrWhiteSpace(inputFileName))
            {
                inputFileName = "TrackLayoutPackageV2";
            }

            return Path.GetFullPath(Path.Combine(inputDirectory, inputFileName + DefaultOutputExtension));
        }

        private static string ResolveSourceFixtureName(string inputJsonPath)
        {
            string fileName = Path.GetFileName(inputJsonPath);
            return string.IsNullOrWhiteSpace(fileName) ? inputJsonPath : fileName;
        }

        private static bool IsCompilationOrEvaluationException(Exception ex)
        {
            return ex is ArgumentException ||
                   ex is ArgumentOutOfRangeException ||
                   ex is InvalidOperationException ||
                   ex is NotSupportedException;
        }

        private static bool IsReadOrWriteException(Exception ex)
        {
            return ex is IOException ||
                   ex is UnauthorizedAccessException ||
                   ex is ArgumentException ||
                   ex is NotSupportedException;
        }
    }
}
