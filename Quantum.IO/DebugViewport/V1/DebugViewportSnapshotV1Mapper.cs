using System;
using System.Collections.Generic;
using Quantum.Math;
using Quantum.Track;
using Quantum.IO.TrainPose.V1;

namespace Quantum.IO.DebugViewport.V1
{
    /// <summary>
    /// Source data for building a renderer-agnostic debug viewport snapshot.
    /// </summary>
    public sealed class DebugViewportSnapshotV1Source
    {
        public string Units { get; set; } = "meters";

        public string? SourceFixtureName { get; set; }

        public IReadOnlyList<TrackFrame>? SampledFrames { get; set; }

        public IReadOnlyList<DebugLineSegment>? Lines { get; set; }

        public IReadOnlyList<DebugViewportBoxSource>? Boxes { get; set; }

        public TrainPoseResult? TrainPose { get; set; }
    }

    /// <summary>
    /// Renderer-neutral oriented box source, expressed in coaster track-frame axes.
    /// </summary>
    public sealed class DebugViewportBoxSource
    {
        public DebugViewportBoxSource(
            string role,
            string? label,
            TrackFrame frame,
            double length,
            double width,
            double height)
        {
            if (role == null)
            {
                throw new ArgumentNullException(nameof(role));
            }

            ValidatePositiveFinite(length, nameof(length));
            ValidatePositiveFinite(width, nameof(width));
            ValidatePositiveFinite(height, nameof(height));

            Role = role;
            Label = label;
            Frame = frame;
            Length = length;
            Width = width;
            Height = height;
        }

        public string Role { get; }

        public string? Label { get; }

        public TrackFrame Frame { get; }

        public double Length { get; }

        public double Width { get; }

        public double Height { get; }

        private static void ValidatePositiveFinite(double value, string paramName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    value,
                    "Box dimensions must be finite and greater than zero.");
            }
        }
    }

    /// <summary>
    /// Maps existing backend track/debug/train data into DebugViewportSnapshotV1 DTOs.
    /// </summary>
    public static class DebugViewportSnapshotV1Mapper
    {
        public static DebugViewportSnapshotV1Dto Export(DebugViewportSnapshotV1Source source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            IReadOnlyList<TrackFrame>? frames = source.SampledFrames;
            int frameCount = frames == null ? 0 : frames.Count;

            return new DebugViewportSnapshotV1Dto
            {
                Contract = DebugViewportSnapshotV1Dto.ContractName,
                Version = DebugViewportSnapshotV1Dto.ContractVersion,
                Metadata = new DebugViewportMetadataV1Dto
                {
                    Units = string.IsNullOrWhiteSpace(source.Units) ? "meters" : source.Units,
                    SourceFixtureName = source.SourceFixtureName,
                    SampleCount = frameCount
                },
                CenterlinePoints = MapCenterlinePoints(frames),
                Frames = MapFrames(frames),
                Lines = MapLines(source.Lines),
                Boxes = MapBoxes(source.Boxes),
                TrainPose = source.TrainPose == null ? null : TrainPoseExportV1Mapper.Export(source.TrainPose)
            };
        }

        private static DebugViewportCenterlinePointV1Dto[] MapCenterlinePoints(IReadOnlyList<TrackFrame>? frames)
        {
            if (frames == null || frames.Count == 0)
            {
                return Array.Empty<DebugViewportCenterlinePointV1Dto>();
            }

            var points = new DebugViewportCenterlinePointV1Dto[frames.Count];
            for (int i = 0; i < frames.Count; i++)
            {
                TrackFrame frame = frames[i];
                points[i] = new DebugViewportCenterlinePointV1Dto
                {
                    Distance = frame.Distance,
                    Position = MapVector(frame.Position)
                };
            }

            return points;
        }

        private static DebugViewportFrameV1Dto[] MapFrames(IReadOnlyList<TrackFrame>? frames)
        {
            if (frames == null || frames.Count == 0)
            {
                return Array.Empty<DebugViewportFrameV1Dto>();
            }

            var mappedFrames = new DebugViewportFrameV1Dto[frames.Count];
            for (int i = 0; i < frames.Count; i++)
            {
                mappedFrames[i] = MapFrame(frames[i]);
            }

            return mappedFrames;
        }

        private static DebugViewportLineSegmentV1Dto[] MapLines(IReadOnlyList<DebugLineSegment>? lines)
        {
            if (lines == null || lines.Count == 0)
            {
                return Array.Empty<DebugViewportLineSegmentV1Dto>();
            }

            var mappedLines = new DebugViewportLineSegmentV1Dto[lines.Count];
            for (int i = 0; i < lines.Count; i++)
            {
                DebugLineSegment line = lines[i];
                mappedLines[i] = new DebugViewportLineSegmentV1Dto
                {
                    Kind = MapAxisType(line.AxisType),
                    Start = MapVector(line.Start),
                    End = MapVector(line.End)
                };
            }

            return mappedLines;
        }

        private static DebugViewportBoxV1Dto[] MapBoxes(IReadOnlyList<DebugViewportBoxSource>? boxes)
        {
            if (boxes == null || boxes.Count == 0)
            {
                return Array.Empty<DebugViewportBoxV1Dto>();
            }

            var mappedBoxes = new DebugViewportBoxV1Dto[boxes.Count];
            for (int i = 0; i < boxes.Count; i++)
            {
                DebugViewportBoxSource box = boxes[i] ?? throw new ArgumentException(
                    "Box source collection cannot contain null entries.",
                    nameof(boxes));

                mappedBoxes[i] = new DebugViewportBoxV1Dto
                {
                    Role = box.Role,
                    Label = box.Label,
                    Frame = MapFrame(box.Frame),
                    Size = new DebugViewportBoxSizeV1Dto
                    {
                        Length = box.Length,
                        Width = box.Width,
                        Height = box.Height
                    }
                };
            }

            return mappedBoxes;
        }

        private static DebugViewportFrameV1Dto MapFrame(TrackFrame frame)
        {
            return new DebugViewportFrameV1Dto
            {
                Distance = frame.Distance,
                Position = MapVector(frame.Position),
                Tangent = MapVector(frame.Tangent),
                Normal = MapVector(frame.Normal),
                Binormal = MapVector(frame.Binormal)
            };
        }

        private static DebugViewportVector3dV1Dto MapVector(Vector3d vector)
        {
            return new DebugViewportVector3dV1Dto
            {
                X = vector.X,
                Y = vector.Y,
                Z = vector.Z
            };
        }

        private static string MapAxisType(TrackFrameAxisType axisType)
        {
            switch (axisType)
            {
                case TrackFrameAxisType.Tangent:
                    return DebugViewportSnapshotV1Vocabulary.FrameAxisTangentKind;
                case TrackFrameAxisType.Normal:
                    return DebugViewportSnapshotV1Vocabulary.FrameAxisNormalKind;
                case TrackFrameAxisType.Binormal:
                    return DebugViewportSnapshotV1Vocabulary.FrameAxisBinormalKind;
                case TrackFrameAxisType.Diagnostic:
                    return DebugViewportSnapshotV1Vocabulary.DiagnosticLineKind;
                default:
                    return DebugViewportSnapshotV1Vocabulary.DiagnosticLineKind;
            }
        }
    }
}
