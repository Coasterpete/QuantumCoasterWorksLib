using System;
using Quantum.IO.TrainPose.V1;

namespace Quantum.IO.DebugViewport.V1
{
    /// <summary>
    /// Versioned backend-only payload for renderer-agnostic debug viewport data.
    /// </summary>
    public sealed class DebugViewportSnapshotV1Dto
    {
        public const string ContractName = "quantum.debug_viewport_snapshot";

        public const int ContractVersion = 1;

        public string Contract { get; set; } = ContractName;

        public int Version { get; set; } = ContractVersion;

        public DebugViewportMetadataV1Dto Metadata { get; set; } = new DebugViewportMetadataV1Dto();

        public DebugViewportCenterlinePointV1Dto[] CenterlinePoints { get; set; } =
            Array.Empty<DebugViewportCenterlinePointV1Dto>();

        public DebugViewportFrameV1Dto[] Frames { get; set; } =
            Array.Empty<DebugViewportFrameV1Dto>();

        public DebugViewportLineSegmentV1Dto[] Lines { get; set; } =
            Array.Empty<DebugViewportLineSegmentV1Dto>();

        public DebugViewportBoxV1Dto[] Boxes { get; set; } =
            Array.Empty<DebugViewportBoxV1Dto>();

        public TrainPoseExportV1Dto? TrainPose { get; set; }
    }

    public sealed class DebugViewportMetadataV1Dto
    {
        public string Units { get; set; } = "meters";

        public string? SourceFixtureName { get; set; }

        public int SampleCount { get; set; }
    }

    public sealed class DebugViewportCenterlinePointV1Dto
    {
        public double Distance { get; set; }

        public DebugViewportVector3dV1Dto Position { get; set; } = new DebugViewportVector3dV1Dto();
    }

    public sealed class DebugViewportFrameV1Dto
    {
        public double Distance { get; set; }

        public DebugViewportVector3dV1Dto Position { get; set; } = new DebugViewportVector3dV1Dto();

        public DebugViewportVector3dV1Dto Tangent { get; set; } = new DebugViewportVector3dV1Dto();

        public DebugViewportVector3dV1Dto Normal { get; set; } = new DebugViewportVector3dV1Dto();

        public DebugViewportVector3dV1Dto Binormal { get; set; } = new DebugViewportVector3dV1Dto();
    }

    public sealed class DebugViewportLineSegmentV1Dto
    {
        public string Kind { get; set; } = string.Empty;

        public DebugViewportVector3dV1Dto Start { get; set; } = new DebugViewportVector3dV1Dto();

        public DebugViewportVector3dV1Dto End { get; set; } = new DebugViewportVector3dV1Dto();
    }

    public sealed class DebugViewportBoxV1Dto
    {
        public string Role { get; set; } = string.Empty;

        public string? Label { get; set; }

        public DebugViewportFrameV1Dto Frame { get; set; } = new DebugViewportFrameV1Dto();

        public DebugViewportBoxSizeV1Dto Size { get; set; } = new DebugViewportBoxSizeV1Dto();
    }

    public sealed class DebugViewportBoxSizeV1Dto
    {
        public double Length { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }
    }

    public sealed class DebugViewportVector3dV1Dto
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double Z { get; set; }
    }
}
