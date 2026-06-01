using System;

namespace QuantumVisualizer
{
    public static class DebugViewportSnapshotV1Vocabulary
    {
        public const string FrameAxisTangentKind = "frame.axis.tangent";
        public const string FrameAxisNormalKind = "frame.axis.normal";
        public const string FrameAxisBinormalKind = "frame.axis.binormal";
        public const string DiagnosticLineKind = "diagnostic.line";

        public const string TrainBodyRole = "train.body";
        public const string TrainBodyBankingProfileRole = "train.body.banking-profile";
        public const string TrainBogieRole = "train.bogie";
        public const string TrainWheelRole = "train.wheel";
    }

    [Serializable]
    public sealed class DebugViewportSnapshotV1Dto
    {
        public string contract;
        public int version;
        public DebugViewportMetadataV1Dto metadata;
        public DebugViewportCenterlinePointV1Dto[] centerlinePoints;
        public DebugViewportFrameV1Dto[] frames;
        public DebugViewportLineSegmentV1Dto[] lines;
        public DebugViewportBoxV1Dto[] boxes;
        public TrainPoseExportV1Dto trainPose;
    }

    [Serializable]
    public sealed class DebugViewportMetadataV1Dto
    {
        public string units;
        public string sourceFixtureName;
        public int sampleCount;
    }

    [Serializable]
    public sealed class DebugViewportCenterlinePointV1Dto
    {
        public float distance;
        public DebugViewportVector3V1Dto position;
    }

    [Serializable]
    public sealed class DebugViewportFrameV1Dto
    {
        public float distance;
        public DebugViewportVector3V1Dto position;
        public DebugViewportVector3V1Dto tangent;
        public DebugViewportVector3V1Dto normal;
        public DebugViewportVector3V1Dto binormal;
    }

    [Serializable]
    public sealed class DebugViewportLineSegmentV1Dto
    {
        public string kind;
        public DebugViewportVector3V1Dto start;
        public DebugViewportVector3V1Dto end;
    }

    [Serializable]
    public sealed class DebugViewportBoxV1Dto
    {
        public string role;
        public string label;
        public DebugViewportFrameV1Dto frame;
        public DebugViewportBoxSizeV1Dto size;
    }

    [Serializable]
    public sealed class DebugViewportBoxSizeV1Dto
    {
        public float length;
        public float width;
        public float height;
    }

    [Serializable]
    public sealed class DebugViewportVector3V1Dto
    {
        public float x;
        public float y;
        public float z;
    }
}
