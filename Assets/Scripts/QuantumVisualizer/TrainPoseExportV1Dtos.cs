using System;

namespace QuantumVisualizer
{
    [Serializable]
    public sealed class TrainPoseExportV1Dto
    {
        public string contract;
        public int version;
        public float leadDistance;
        public TrainConsistDefinitionV1Dto definition;
        public ArticulatedTrainCarWithWheelsV1Dto[] cars;

        // Optional extra sample channels if present in JSON.
        public TrackFrameV1Dto[] trackSamples;
        public TrackFrameV1Dto[] sampledTrackFrames;
        public TrackFrameV1Dto[] samples;
    }

    [Serializable]
    public sealed class TrainConsistDefinitionV1Dto
    {
        public int carCount;
        public float carSpacing;
        public TrainCarGeometryV1Dto carGeometry;
        public TrainBogieLayoutV1Dto bogieLayout;
        public TrainWheelLayoutV1Dto wheelLayout;
    }

    [Serializable]
    public sealed class TrainCarGeometryV1Dto
    {
        public float length;
        public float width;
        public float height;
    }

    [Serializable]
    public sealed class TrainBogieLayoutV1Dto
    {
        public float bogieSpacing;
    }

    [Serializable]
    public sealed class TrainWheelLayoutV1Dto
    {
        public int wheelCountPerBogie;
        public float wheelRadius;
        public float wheelWidth;
        public float axleSpacing;
    }

    [Serializable]
    public sealed class ArticulatedTrainCarWithWheelsV1Dto
    {
        public ArticulatedTrainCarV1Dto body;
        public TrainBogieWithWheelsV1Dto frontBogie;
        public TrainBogieWithWheelsV1Dto rearBogie;
    }

    [Serializable]
    public sealed class ArticulatedTrainCarV1Dto
    {
        public TrainCarTransformV1Dto originalBody;
        public BogieTransformV1Dto frontBogie;
        public BogieTransformV1Dto rearBogie;
        public TrackFrameV1Dto articulatedFrame;
        public Matrix4x4V1Dto articulatedMatrix;
        public float centerDistance;
    }

    [Serializable]
    public sealed class TrainCarTransformV1Dto
    {
        public int carIndex;
        public float distance;
        public TrackFrameV1Dto frame;
        public Matrix4x4V1Dto matrix;
    }

    [Serializable]
    public sealed class TrainBogieWithWheelsV1Dto
    {
        public BogieTransformV1Dto bogie;
        public WheelTransformV1Dto[] wheels;
    }

    [Serializable]
    public sealed class BogieTransformV1Dto
    {
        public int carIndex;
        public int bogieIndex;
        public float distance;
        public TrackFrameV1Dto frame;
        public Matrix4x4V1Dto matrix;
    }

    [Serializable]
    public sealed class WheelTransformV1Dto
    {
        public int carIndex;
        public int bogieIndex;
        public int wheelIndex;
        public float localOffsetX;
        public float localOffsetY;
        public float localOffsetZ;
        public TrackFrameV1Dto frame;
        public Matrix4x4V1Dto matrix;
    }

    [Serializable]
    public sealed class TrackFrameV1Dto
    {
        public float distance;
        public Vector3V1Dto position;
        public Vector3V1Dto tangent;
        public Vector3V1Dto normal;
        public Vector3V1Dto binormal;
    }

    [Serializable]
    public sealed class Vector3V1Dto
    {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public sealed class Matrix4x4V1Dto
    {
        public float m11;
        public float m12;
        public float m13;
        public float m14;

        public float m21;
        public float m22;
        public float m23;
        public float m24;

        public float m31;
        public float m32;
        public float m33;
        public float m34;

        public float m41;
        public float m42;
        public float m43;
        public float m44;
    }
}
