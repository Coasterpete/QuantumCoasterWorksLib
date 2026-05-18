using System;

namespace Quantum.IO.TrainPose.V1
{
    /// <summary>
    /// Versioned JSON DTO for exporting a complete coaster train pose snapshot.
    /// </summary>
    /// <remarks>
    /// This DTO is the public serialization boundary for train-pose handoff.
    /// Runtime spline and math internals are intentionally not part of the contract
    /// identity; fields describe coaster-domain pose data and matrices.
    /// </remarks>
    public sealed class TrainPoseExportV1Dto
    {
        /// <summary>
        /// Required contract identity for TrainPoseExportV1 JSON payloads.
        /// </summary>
        public const string ContractName = "quantum.train_pose";

        /// <summary>
        /// Required contract version for TrainPoseExportV1 JSON payloads.
        /// </summary>
        public const int ContractVersion = 1;

        public string Contract { get; set; } = ContractName;

        public int Version { get; set; } = ContractVersion;

        public double LeadDistance { get; set; }

        public TrainConsistDefinitionV1Dto Definition { get; set; } = new TrainConsistDefinitionV1Dto();

        public ArticulatedTrainCarWithWheelsV1Dto[] Cars { get; set; } = Array.Empty<ArticulatedTrainCarWithWheelsV1Dto>();
    }

    public sealed class TrainConsistDefinitionV1Dto
    {
        public int CarCount { get; set; }

        public double CarSpacing { get; set; }

        public TrainCarGeometryV1Dto CarGeometry { get; set; } = new TrainCarGeometryV1Dto();

        public TrainBogieLayoutV1Dto BogieLayout { get; set; } = new TrainBogieLayoutV1Dto();

        public TrainWheelLayoutV1Dto? WheelLayout { get; set; }
    }

    public sealed class TrainCarGeometryV1Dto
    {
        public double Length { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }
    }

    public sealed class TrainBogieLayoutV1Dto
    {
        public double BogieSpacing { get; set; }
    }

    public sealed class TrainWheelLayoutV1Dto
    {
        public int WheelCountPerBogie { get; set; }

        public double WheelRadius { get; set; }

        public double WheelWidth { get; set; }

        public double AxleSpacing { get; set; }
    }

    public sealed class ArticulatedTrainCarWithWheelsV1Dto
    {
        public ArticulatedTrainCarV1Dto Body { get; set; } = new ArticulatedTrainCarV1Dto();

        public TrainBogieWithWheelsV1Dto FrontBogie { get; set; } = new TrainBogieWithWheelsV1Dto();

        public TrainBogieWithWheelsV1Dto RearBogie { get; set; } = new TrainBogieWithWheelsV1Dto();
    }

    public sealed class ArticulatedTrainCarV1Dto
    {
        public TrainCarTransformV1Dto OriginalBody { get; set; } = new TrainCarTransformV1Dto();

        public BogieTransformV1Dto FrontBogie { get; set; } = new BogieTransformV1Dto();

        public BogieTransformV1Dto RearBogie { get; set; } = new BogieTransformV1Dto();

        public TrackFrameV1Dto ArticulatedFrame { get; set; } = new TrackFrameV1Dto();

        public Matrix4x4V1Dto ArticulatedMatrix { get; set; } = new Matrix4x4V1Dto();

        public double CenterDistance { get; set; }
    }

    public sealed class TrainCarTransformV1Dto
    {
        public int CarIndex { get; set; }

        public double Distance { get; set; }

        public TrackFrameV1Dto Frame { get; set; } = new TrackFrameV1Dto();

        public Matrix4x4V1Dto Matrix { get; set; } = new Matrix4x4V1Dto();
    }

    public sealed class TrainBogieWithWheelsV1Dto
    {
        public BogieTransformV1Dto Bogie { get; set; } = new BogieTransformV1Dto();

        public WheelTransformV1Dto[] Wheels { get; set; } = Array.Empty<WheelTransformV1Dto>();
    }

    public sealed class BogieTransformV1Dto
    {
        public int CarIndex { get; set; }

        public int BogieIndex { get; set; }

        public double Distance { get; set; }

        public TrackFrameV1Dto Frame { get; set; } = new TrackFrameV1Dto();

        public Matrix4x4V1Dto Matrix { get; set; } = new Matrix4x4V1Dto();
    }

    public sealed class WheelTransformV1Dto
    {
        public int CarIndex { get; set; }

        public int BogieIndex { get; set; }

        public int WheelIndex { get; set; }

        public double LocalOffsetX { get; set; }

        public double LocalOffsetY { get; set; }

        public double LocalOffsetZ { get; set; }

        public TrackFrameV1Dto Frame { get; set; } = new TrackFrameV1Dto();

        public Matrix4x4V1Dto Matrix { get; set; } = new Matrix4x4V1Dto();
    }

    public sealed class TrackFrameV1Dto
    {
        public double Distance { get; set; }

        public Vector3dV1Dto Position { get; set; } = new Vector3dV1Dto();

        public Vector3dV1Dto Tangent { get; set; } = new Vector3dV1Dto();

        public Vector3dV1Dto Normal { get; set; } = new Vector3dV1Dto();

        public Vector3dV1Dto Binormal { get; set; } = new Vector3dV1Dto();
    }

    public sealed class Vector3dV1Dto
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double Z { get; set; }
    }

    public sealed class Matrix4x4V1Dto
    {
        public double M11 { get; set; }
        public double M12 { get; set; }
        public double M13 { get; set; }
        public double M14 { get; set; }

        public double M21 { get; set; }
        public double M22 { get; set; }
        public double M23 { get; set; }
        public double M24 { get; set; }

        public double M31 { get; set; }
        public double M32 { get; set; }
        public double M33 { get; set; }
        public double M34 { get; set; }

        public double M41 { get; set; }
        public double M42 { get; set; }
        public double M43 { get; set; }
        public double M44 { get; set; }
    }
}
