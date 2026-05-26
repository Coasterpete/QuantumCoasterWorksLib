using System;
using System.Collections.Generic;
using Quantum.IO.TrainPose.V1;
using Quantum.Math;

namespace Quantum.IO.DebugViewport.V1
{
    public enum DebugViewportSnapshotV1ValidationCode
    {
        InvalidContract = 0,
        InvalidVersion = 1,
        MissingMetadata = 2,
        MissingCollection = 3,
        MissingObject = 4,
        NonFiniteNumber = 5,
        NegativeDistance = 6,
        DecreasingDistance = 7,
        SampleCountMismatch = 8,
        FrameCountMismatch = 9,
        FrameDistanceMismatch = 10,
        ZeroLengthVector = 11,
        NonPositiveBoxDimension = 12,
        InvalidNestedTrainPoseContract = 13,
        InvalidNestedTrainPoseVersion = 14,
        NestedTrainPoseValidationError = 15
    }

    public sealed class DebugViewportSnapshotV1ValidationDiagnostic
    {
        public DebugViewportSnapshotV1ValidationDiagnostic(
            DebugViewportSnapshotV1ValidationCode code,
            string path,
            string message,
            double? value = null,
            double? expected = null,
            double? tolerance = null)
        {
            Code = code;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
            Value = value;
            Expected = expected;
            Tolerance = tolerance;
        }

        public DebugViewportSnapshotV1ValidationCode Code { get; }

        public string Path { get; }

        public string Message { get; }

        public double? Value { get; }

        public double? Expected { get; }

        public double? Tolerance { get; }
    }

    public sealed class DebugViewportSnapshotV1ValidationOptions
    {
        public double ZeroVectorLengthTolerance { get; set; } = MathUtil.Epsilon;

        public double DistanceTolerance { get; set; } = 1e-9;

        public bool ValidateNestedTrainPose { get; set; } = true;
    }

    public static class DebugViewportSnapshotV1Validator
    {
        public static IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> Validate(
            DebugViewportSnapshotV1Dto dto,
            DebugViewportSnapshotV1ValidationOptions? options = null)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            DebugViewportSnapshotV1ValidationOptions effectiveOptions =
                options ?? new DebugViewportSnapshotV1ValidationOptions();
            ValidateOptions(effectiveOptions);

            var diagnostics = new List<DebugViewportSnapshotV1ValidationDiagnostic>();

            ValidateContract(dto, diagnostics);
            ValidateMetadata(dto.Metadata, diagnostics);

            DebugViewportCenterlinePointV1Dto[]? centerlinePoints = dto.CenterlinePoints;
            if (centerlinePoints == null)
            {
                AddMissingCollection("centerlinePoints", diagnostics);
            }
            else
            {
                ValidateCenterlinePoints(centerlinePoints, diagnostics);
            }

            if (dto.Metadata != null && centerlinePoints != null &&
                dto.Metadata.SampleCount != centerlinePoints.Length)
            {
                diagnostics.Add(
                    new DebugViewportSnapshotV1ValidationDiagnostic(
                        DebugViewportSnapshotV1ValidationCode.SampleCountMismatch,
                        "metadata.sampleCount",
                        "Metadata sample count must match centerline point count.",
                        dto.Metadata.SampleCount,
                        centerlinePoints.Length));
            }

            DebugViewportFrameV1Dto[]? frames = dto.Frames;
            if (frames == null)
            {
                AddMissingCollection("frames", diagnostics);
            }
            else
            {
                ValidateFrames(frames, centerlinePoints, diagnostics, effectiveOptions);
            }

            DebugViewportLineSegmentV1Dto[]? lines = dto.Lines;
            if (lines == null)
            {
                AddMissingCollection("lines", diagnostics);
            }
            else
            {
                ValidateLines(lines, diagnostics);
            }

            DebugViewportBoxV1Dto[]? boxes = dto.Boxes;
            if (boxes == null)
            {
                AddMissingCollection("boxes", diagnostics);
            }
            else
            {
                ValidateBoxes(boxes, diagnostics, effectiveOptions);
            }

            ValidateTrainPose(dto.TrainPose, diagnostics, effectiveOptions);

            return diagnostics.ToArray();
        }

        public static bool TryValidate(
            DebugViewportSnapshotV1Dto dto,
            out IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics,
            DebugViewportSnapshotV1ValidationOptions? options = null)
        {
            diagnostics = Validate(dto, options);
            return diagnostics.Count == 0;
        }

        private static void ValidateOptions(DebugViewportSnapshotV1ValidationOptions options)
        {
            if (!IsFinite(options.ZeroVectorLengthTolerance) || options.ZeroVectorLengthTolerance < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    options.ZeroVectorLengthTolerance,
                    "Zero vector length tolerance must be finite and non-negative.");
            }

            if (!IsFinite(options.DistanceTolerance) || options.DistanceTolerance < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    options.DistanceTolerance,
                    "Distance tolerance must be finite and non-negative.");
            }
        }

        private static void ValidateContract(
            DebugViewportSnapshotV1Dto dto,
            List<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics)
        {
            if (!string.Equals(dto.Contract, DebugViewportSnapshotV1Dto.ContractName, StringComparison.Ordinal))
            {
                diagnostics.Add(
                    new DebugViewportSnapshotV1ValidationDiagnostic(
                        DebugViewportSnapshotV1ValidationCode.InvalidContract,
                        "contract",
                        "Contract name must match DebugViewportSnapshotV1."));
            }

            if (dto.Version != DebugViewportSnapshotV1Dto.ContractVersion)
            {
                diagnostics.Add(
                    new DebugViewportSnapshotV1ValidationDiagnostic(
                        DebugViewportSnapshotV1ValidationCode.InvalidVersion,
                        "version",
                        "Version must match DebugViewportSnapshotV1.",
                        dto.Version,
                        DebugViewportSnapshotV1Dto.ContractVersion));
            }
        }

        private static void ValidateMetadata(
            DebugViewportMetadataV1Dto? metadata,
            List<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics)
        {
            if (metadata == null)
            {
                diagnostics.Add(
                    new DebugViewportSnapshotV1ValidationDiagnostic(
                        DebugViewportSnapshotV1ValidationCode.MissingMetadata,
                        "metadata",
                        "Metadata object is required."));
            }
        }

        private static void ValidateCenterlinePoints(
            DebugViewportCenterlinePointV1Dto[] centerlinePoints,
            List<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics)
        {
            double previousDistance = 0.0;
            bool hasPreviousDistance = false;

            for (int i = 0; i < centerlinePoints.Length; i++)
            {
                string pointPath = "centerlinePoints[" + i + "]";
                DebugViewportCenterlinePointV1Dto? point = centerlinePoints[i];
                if (point == null)
                {
                    AddMissingObject(pointPath, diagnostics);
                    continue;
                }

                bool distanceFinite = ValidateFinite(point.Distance, pointPath + ".distance", diagnostics);
                if (distanceFinite)
                {
                    ValidateNonNegativeDistance(point.Distance, pointPath + ".distance", diagnostics);
                    ValidateMonotonicDistance(
                        point.Distance,
                        previousDistance,
                        hasPreviousDistance,
                        pointPath + ".distance",
                        diagnostics);

                    previousDistance = point.Distance;
                    hasPreviousDistance = true;
                }

                ValidateFiniteVector(point.Position, pointPath + ".position", diagnostics);
            }
        }

        private static void ValidateFrames(
            DebugViewportFrameV1Dto[] frames,
            DebugViewportCenterlinePointV1Dto[]? centerlinePoints,
            List<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics,
            DebugViewportSnapshotV1ValidationOptions options)
        {
            if (frames.Length > 0 && centerlinePoints != null && frames.Length != centerlinePoints.Length)
            {
                diagnostics.Add(
                    new DebugViewportSnapshotV1ValidationDiagnostic(
                        DebugViewportSnapshotV1ValidationCode.FrameCountMismatch,
                        "frames",
                        "Frame count must match centerline point count when frames are present.",
                        frames.Length,
                        centerlinePoints.Length));
            }

            double previousDistance = 0.0;
            bool hasPreviousDistance = false;

            for (int i = 0; i < frames.Length; i++)
            {
                string framePath = "frames[" + i + "]";
                DebugViewportFrameV1Dto? frame = frames[i];
                if (frame == null)
                {
                    AddMissingObject(framePath, diagnostics);
                    continue;
                }

                bool distanceFinite = ValidateFrame(frame, framePath, diagnostics, options);
                if (distanceFinite)
                {
                    ValidateNonNegativeDistance(frame.Distance, framePath + ".distance", diagnostics);
                    ValidateMonotonicDistance(
                        frame.Distance,
                        previousDistance,
                        hasPreviousDistance,
                        framePath + ".distance",
                        diagnostics);

                    previousDistance = frame.Distance;
                    hasPreviousDistance = true;
                }

                if (centerlinePoints != null &&
                    i < centerlinePoints.Length &&
                    centerlinePoints[i] != null &&
                    distanceFinite &&
                    IsFinite(centerlinePoints[i].Distance))
                {
                    double expectedDistance = centerlinePoints[i].Distance;
                    double delta = System.Math.Abs(frame.Distance - expectedDistance);
                    if (delta > options.DistanceTolerance)
                    {
                        diagnostics.Add(
                            new DebugViewportSnapshotV1ValidationDiagnostic(
                                DebugViewportSnapshotV1ValidationCode.FrameDistanceMismatch,
                                framePath + ".distance",
                                "Frame distance must match the centerline point at the same index.",
                                frame.Distance,
                                expectedDistance,
                                options.DistanceTolerance));
                    }
                }
            }
        }

        private static void ValidateLines(
            DebugViewportLineSegmentV1Dto[] lines,
            List<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                string linePath = "lines[" + i + "]";
                DebugViewportLineSegmentV1Dto? line = lines[i];
                if (line == null)
                {
                    AddMissingObject(linePath, diagnostics);
                    continue;
                }

                ValidateFiniteVector(line.Start, linePath + ".start", diagnostics);
                ValidateFiniteVector(line.End, linePath + ".end", diagnostics);
            }
        }

        private static void ValidateBoxes(
            DebugViewportBoxV1Dto[] boxes,
            List<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics,
            DebugViewportSnapshotV1ValidationOptions options)
        {
            for (int i = 0; i < boxes.Length; i++)
            {
                string boxPath = "boxes[" + i + "]";
                DebugViewportBoxV1Dto? box = boxes[i];
                if (box == null)
                {
                    AddMissingObject(boxPath, diagnostics);
                    continue;
                }

                ValidateFrame(box.Frame, boxPath + ".frame", diagnostics, options);

                if (box.Size == null)
                {
                    AddMissingObject(boxPath + ".size", diagnostics);
                    continue;
                }

                ValidatePositiveBoxDimension(box.Size.Length, boxPath + ".size.length", diagnostics);
                ValidatePositiveBoxDimension(box.Size.Width, boxPath + ".size.width", diagnostics);
                ValidatePositiveBoxDimension(box.Size.Height, boxPath + ".size.height", diagnostics);
            }
        }

        private static void ValidateTrainPose(
            TrainPoseExportV1Dto? trainPose,
            List<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics,
            DebugViewportSnapshotV1ValidationOptions options)
        {
            if (trainPose == null)
            {
                return;
            }

            if (!string.Equals(trainPose.Contract, TrainPoseExportV1Dto.ContractName, StringComparison.Ordinal))
            {
                diagnostics.Add(
                    new DebugViewportSnapshotV1ValidationDiagnostic(
                        DebugViewportSnapshotV1ValidationCode.InvalidNestedTrainPoseContract,
                        "trainPose.contract",
                        "Nested train pose contract name must match TrainPoseExportV1."));
            }

            if (trainPose.Version != TrainPoseExportV1Dto.ContractVersion)
            {
                diagnostics.Add(
                    new DebugViewportSnapshotV1ValidationDiagnostic(
                        DebugViewportSnapshotV1ValidationCode.InvalidNestedTrainPoseVersion,
                        "trainPose.version",
                        "Nested train pose version must match TrainPoseExportV1.",
                        trainPose.Version,
                        TrainPoseExportV1Dto.ContractVersion));
            }

            if (!options.ValidateNestedTrainPose)
            {
                return;
            }

            IReadOnlyList<TrainPoseExportV1ValidationDiagnostic> nestedDiagnostics =
                TrainPoseExportV1Validator.Validate(trainPose);
            foreach (TrainPoseExportV1ValidationDiagnostic nestedDiagnostic in nestedDiagnostics)
            {
                diagnostics.Add(
                    new DebugViewportSnapshotV1ValidationDiagnostic(
                        DebugViewportSnapshotV1ValidationCode.NestedTrainPoseValidationError,
                        "trainPose." + nestedDiagnostic.Path,
                        "Nested train pose validation failed: " + nestedDiagnostic.Message,
                        nestedDiagnostic.Value,
                        nestedDiagnostic.Expected,
                        nestedDiagnostic.Tolerance));
            }
        }

        private static bool ValidateFrame(
            DebugViewportFrameV1Dto? frame,
            string path,
            List<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics,
            DebugViewportSnapshotV1ValidationOptions options)
        {
            if (frame == null)
            {
                AddMissingObject(path, diagnostics);
                return false;
            }

            bool distanceFinite = ValidateFinite(frame.Distance, path + ".distance", diagnostics);
            ValidateFiniteVector(frame.Position, path + ".position", diagnostics);
            ValidateNonZeroVector(frame.Tangent, path + ".tangent", diagnostics, options);
            ValidateNonZeroVector(frame.Normal, path + ".normal", diagnostics, options);
            ValidateNonZeroVector(frame.Binormal, path + ".binormal", diagnostics, options);

            return distanceFinite;
        }

        private static void ValidateNonNegativeDistance(
            double distance,
            string path,
            List<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics)
        {
            if (distance < 0.0)
            {
                diagnostics.Add(
                    new DebugViewportSnapshotV1ValidationDiagnostic(
                        DebugViewportSnapshotV1ValidationCode.NegativeDistance,
                        path,
                        "Distance must be non-negative.",
                        distance,
                        0.0));
            }
        }

        private static void ValidateMonotonicDistance(
            double distance,
            double previousDistance,
            bool hasPreviousDistance,
            string path,
            List<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics)
        {
            if (hasPreviousDistance && distance < previousDistance)
            {
                diagnostics.Add(
                    new DebugViewportSnapshotV1ValidationDiagnostic(
                        DebugViewportSnapshotV1ValidationCode.DecreasingDistance,
                        path,
                        "Distances must be monotonically increasing.",
                        distance,
                        previousDistance));
            }
        }

        private static void ValidatePositiveBoxDimension(
            double value,
            string path,
            List<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics)
        {
            bool finite = ValidateFinite(value, path, diagnostics);
            if (finite && value <= 0.0)
            {
                diagnostics.Add(
                    new DebugViewportSnapshotV1ValidationDiagnostic(
                        DebugViewportSnapshotV1ValidationCode.NonPositiveBoxDimension,
                        path,
                        "Box dimensions must be finite and greater than zero.",
                        value,
                        0.0));
            }
        }

        private static bool ValidateNonZeroVector(
            DebugViewportVector3dV1Dto? vector,
            string path,
            List<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics,
            DebugViewportSnapshotV1ValidationOptions options)
        {
            if (!ValidateFiniteVector(vector, path, diagnostics))
            {
                return false;
            }

            double length = Length(vector!);
            if (length <= options.ZeroVectorLengthTolerance)
            {
                diagnostics.Add(
                    new DebugViewportSnapshotV1ValidationDiagnostic(
                        DebugViewportSnapshotV1ValidationCode.ZeroLengthVector,
                        path,
                        "Frame vector length must be greater than tolerance.",
                        length,
                        options.ZeroVectorLengthTolerance,
                        options.ZeroVectorLengthTolerance));
                return false;
            }

            return true;
        }

        private static bool ValidateFiniteVector(
            DebugViewportVector3dV1Dto? vector,
            string path,
            List<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics)
        {
            if (vector == null)
            {
                AddMissingObject(path, diagnostics);
                return false;
            }

            bool finite = true;
            finite &= ValidateFinite(vector.X, path + ".x", diagnostics);
            finite &= ValidateFinite(vector.Y, path + ".y", diagnostics);
            finite &= ValidateFinite(vector.Z, path + ".z", diagnostics);
            return finite;
        }

        private static bool ValidateFinite(
            double value,
            string path,
            List<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics)
        {
            if (IsFinite(value))
            {
                return true;
            }

            diagnostics.Add(
                new DebugViewportSnapshotV1ValidationDiagnostic(
                    DebugViewportSnapshotV1ValidationCode.NonFiniteNumber,
                    path,
                    "Numeric value must be finite.",
                    value));
            return false;
        }

        private static void AddMissingCollection(
            string path,
            List<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics)
        {
            diagnostics.Add(
                new DebugViewportSnapshotV1ValidationDiagnostic(
                    DebugViewportSnapshotV1ValidationCode.MissingCollection,
                    path,
                    "Required collection is missing."));
        }

        private static void AddMissingObject(
            string path,
            List<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics)
        {
            diagnostics.Add(
                new DebugViewportSnapshotV1ValidationDiagnostic(
                    DebugViewportSnapshotV1ValidationCode.MissingObject,
                    path,
                    "Required object is missing."));
        }

        private static bool IsFinite(double value)
        {
            return !(double.IsNaN(value) || double.IsInfinity(value));
        }

        private static double Length(DebugViewportVector3dV1Dto vector)
        {
            return System.Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z);
        }
    }
}
